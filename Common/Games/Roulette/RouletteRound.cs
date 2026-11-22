using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using CasinoWorld.Common;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;

namespace CasinoWorld.Common.Games.Roulette
{
	public enum RoulettePhase
	{
		Betting,
		WaitingForResult, // coins taken, waiting for the server to roll
		Spinning,
		ShowingResult,
	}

	/// <summary>
	/// Client-side state of the local player's roulette round: bets, the spin animation
	/// and the payout. Lives outside the UI so closing the window never loses a round.
	/// </summary>
	public class RouletteRound
	{
		public const int SpinTicks = 300;
		private const int ResultTicks = 150;
		private const int ResultTimeoutTicks = 300;
		private const float WheelTurns = 2.5f;
		private const int BallTurns = 7;
		private const int HistoryLength = 12;

		private static readonly float PocketAngle = MathHelper.TwoPi / RouletteRules.Pockets;

		public readonly Dictionary<RouletteBet, long> Bets = new();
		private Dictionary<RouletteBet, long> lastBets = new();
		public readonly List<int> History = new();

		public RoulettePhase Phase { get; private set; } = RoulettePhase.Betting;
		public int Result { get; private set; } = -1;
		public long LastPayout { get; private set; }
		public long LastStake { get; private set; }
		public string Message { get; private set; } = "";
		public Color MessageColor { get; private set; } = Color.White;

		// Animation state, angles in radians, clockwise on screen.
		public float WheelAngle { get; private set; }
		public float BallAngle { get; private set; }
		/// <summary>0 = ball on the outer track, 1 = ball resting in a pocket.</summary>
		public float BallDrop { get; private set; }

		private int timer;
		private float wheelStart, ballStart, ballTravel;
		private int lastTickPocket = -1;
		private int ticksSinceSound;

		public long TotalBet => Bets.Values.Sum();

		public bool CanEditBets => Phase is RoulettePhase.Betting or RoulettePhase.ShowingResult;

		public void AddChip(RouletteBet bet, long chip, long maxBet)
		{
			if (!CanEditBets)
				return;
			if (Phase == RoulettePhase.ShowingResult)
				Phase = RoulettePhase.Betting;

			if (TotalBet + chip > maxBet) {
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.UI.Roulette.OverMax", Coins.Format(maxBet)), Color.OrangeRed);
				return;
			}

			Bets[bet] = Bets.GetValueOrDefault(bet) + chip;
			SoundEngine.PlaySound(CasinoSounds.ChipPlace);
		}

		public void RemoveChip(RouletteBet bet, long chip)
		{
			if (!CanEditBets || !Bets.TryGetValue(bet, out long stake))
				return;

			stake -= chip;
			if (stake <= 0)
				Bets.Remove(bet);
			else
				Bets[bet] = stake;
			SoundEngine.PlaySound(CasinoSounds.ChipPlace with { Pitch = -0.2f });
		}

		public void Clear()
		{
			if (CanEditBets)
				Bets.Clear();
		}

		public void Repeat(long maxBet)
		{
			if (!CanEditBets || lastBets.Count == 0)
				return;

			Bets.Clear();
			foreach ((RouletteBet bet, long stake) in lastBets)
				Bets[bet] = stake;
			if (TotalBet > maxBet)
				Bets.Clear();
			Phase = RoulettePhase.Betting;
		}

		/// <summary>Takes the coins and asks for a result. Returns false if the spin could not start.</summary>
		public bool TrySpin(Player player, long maxBet)
		{
			if (!CanEditBets)
				return false;

			long total = TotalBet;
			if (total <= 0) {
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.UI.Roulette.NoBet"), Color.OrangeRed);
				return false;
			}
			if (total > maxBet) {
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.UI.Roulette.OverMax", Coins.Format(maxBet)), Color.OrangeRed);
				return false;
			}
			if (!CasinoRewards.TryWager(player, total)) {
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.UI.Roulette.NotEnough"), Color.OrangeRed);
				return false;
			}

			LastStake = total;
			lastBets = new Dictionary<RouletteBet, long>(Bets);
			Phase = RoulettePhase.WaitingForResult;
			timer = 0;
			SetMessage(Language.GetTextValue("Mods.CasinoWorld.UI.Roulette.NoMoreBets"), Color.Gold);
			SoundEngine.PlaySound(CasinoSounds.ChipStack);
			return true;
		}

		/// <summary>Starts the spin animation towards the rolled number.</summary>
		public void ReceiveResult(int number)
		{
			if (Phase != RoulettePhase.WaitingForResult)
				return;

			Result = number;
			Phase = RoulettePhase.Spinning;
			timer = 0;
			BallDrop = 0f;

			wheelStart = MathHelper.WrapAngle(WheelAngle);
			ballStart = Main.rand.NextFloat(MathHelper.TwoPi);

			// The ball runs counter-clockwise and must stop on the pocket of the result.
			float wheelEnd = wheelStart + WheelTurns * MathHelper.TwoPi;
			float target = wheelEnd + RouletteRules.WheelIndexOf(number) * PocketAngle;
			float delta = Modulo(ballStart - target, MathHelper.TwoPi);
			ballTravel = delta + BallTurns * MathHelper.TwoPi;
			SoundEngine.PlaySound(CasinoSounds.RouletteSpin);
		}

		/// <summary>Advances the round by one tick. Call every frame on the client.</summary>
		public void Update(Player player)
		{
			timer++;
			switch (Phase) {
				case RoulettePhase.Betting:
					WheelAngle += 0.004f; // idle wheel drift
					break;

				case RoulettePhase.WaitingForResult:
					WheelAngle += 0.01f;
					if (timer > ResultTimeoutTicks)
						Refund(player);
					break;

				case RoulettePhase.Spinning:
					AnimateSpin();
					if (timer >= SpinTicks)
						Finish(player);
					break;

				case RoulettePhase.ShowingResult:
					// The ball sits in the winning pocket and turns with the wheel.
					WheelAngle += 0.004f;
					BallAngle += 0.004f;
					if (timer > ResultTicks && Bets.Count == 0)
						Phase = RoulettePhase.Betting;
					break;
			}
		}

		/// <summary>Called when the window closes: a running spin resolves at once so nothing is lost.</summary>
		public void FinishImmediately(Player player)
		{
			if (Phase == RoulettePhase.Spinning)
				Finish(player);
		}

		private void AnimateSpin()
		{
			float p = Math.Min(1f, timer / (float)SpinTicks);
			float wheelEase = 1f - (1f - p) * (1f - p);
			float ballEase = 1f - (float)Math.Pow(1f - p, 3);

			WheelAngle = wheelStart + WheelTurns * MathHelper.TwoPi * wheelEase;
			BallAngle = ballStart - ballTravel * ballEase;
			BallDrop = MathHelper.Clamp((p - 0.7f) / 0.25f, 0f, 1f);

			// Clicks as the ball passes pockets; they slow down with the ball.
			int pocket = (int)Math.Floor(Modulo(BallAngle - WheelAngle, MathHelper.TwoPi) / PocketAngle);
			ticksSinceSound++;
			if (pocket != lastTickPocket && ticksSinceSound >= 3 && p < 0.97f) {
				if (BallDrop > 0f)
					SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.25f + 0.35f * BallDrop, Pitch = 0.3f });
				ticksSinceSound = 0;
			}
			lastTickPocket = pocket;
		}

		private void Finish(Player player)
		{
			timer = SpinTicks;
			AnimateSpin();

			SoundEngine.PlaySound(CasinoSounds.RouletteBallDrop);
			LastPayout = RouletteRules.Resolve(lastBets, Result);
			Coins.Give(player, LastPayout);

			// A hit on a single number can drop a jackpot item.
			if (lastBets.TryGetValue(new RouletteBet(RouletteBetType.Straight, Result), out long straightStake))
				CasinoRewards.TryJackpot(player, straightStake, CasinoRewards.RouletteStraightJackpot);

			History.Insert(0, Result);
			if (History.Count > HistoryLength)
				History.RemoveAt(History.Count - 1);

			string number = NumberName(Result);
			if (LastPayout > 0) {
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.UI.Roulette.Win", number, Coins.Format(LastPayout)), Color.LightGreen);
				SoundEngine.PlaySound(LastPayout >= LastStake * 10 ? CasinoSounds.Jackpot : CasinoSounds.SmallWin);
				SoundEngine.PlaySound(CasinoSounds.CoinsPayout);
				CombatText.NewText(player.Hitbox, Color.Gold, "+" + Coins.Format(LastPayout), dramatic: LastPayout >= LastStake * 10);
			}
			else {
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.UI.Roulette.Lose", number), Color.LightGray);
			}

			Bets.Clear();
			Phase = RoulettePhase.ShowingResult;
			timer = 0;
		}

		private void Refund(Player player)
		{
			Coins.Give(player, LastStake);
			Bets.Clear();
			foreach ((RouletteBet bet, long stake) in lastBets)
				Bets[bet] = stake;
			Phase = RoulettePhase.Betting;
			SetMessage(Language.GetTextValue("Mods.CasinoWorld.UI.Roulette.NoResponse"), Color.OrangeRed);
		}

		public static string NumberName(int n)
		{
			string colour = n == 0 ? "Green" : RouletteRules.IsRed(n) ? "Red" : "Black";
			return Language.GetTextValue("Mods.CasinoWorld.UI.Roulette.Number." + colour, n);
		}

		private void SetMessage(string text, Color color)
		{
			Message = text;
			MessageColor = color;
		}

		private static float Modulo(float a, float m) => (a % m + m) % m;
	}
}
