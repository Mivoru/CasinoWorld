using Microsoft.Xna.Framework;
using CasinoWorld.Common;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;

namespace CasinoWorld.Common.Games.Slots
{
	public enum SlotPhase { Idle, Spinning, Result }

	/// <summary>
	/// Local player's slot machine. The reels are rolled on the client when the spin starts
	/// (coins live in the client's inventory, and nobody else is affected); the animation only
	/// reveals them. Reels stop one after another.
	/// </summary>
	public class SlotRound
	{
		public static readonly int[] StopTicks = { 55, 80, 105 };
		private const float SpinSpeed = 0.42f; // symbols per tick
		private const int FlashTicks = 90;

		public SlotPhase Phase { get; private set; } = SlotPhase.Idle;
		public readonly SlotSymbol[] Result = { SlotSymbol.Seven, SlotSymbol.Seven, SlotSymbol.Seven };

		/// <summary>Scroll position of each reel, in symbols. Whole numbers show one symbol centred.</summary>
		public readonly float[] Offset = new float[SlotRules.Reels];
		public readonly bool[] Stopped = { true, true, true };

		public int BetIndex;
		public long LastBet { get; private set; }
		public long LastPayout { get; private set; }
		public int WinFlash { get; private set; }
		public string Message { get; private set; } = "";
		public Color MessageColor { get; private set; } = Color.White;

		/// <summary>Landing bounce per reel, counts down.</summary>
		public readonly int[] Bounce = new int[SlotRules.Reels];

		/// <summary>Two equal symbols are in and the third reel is still spinning.</summary>
		public bool Tension => Phase == SlotPhase.Spinning && Stopped[1] && !Stopped[2] && Result[0] == Result[1];
		private const int TensionTicks = 80;

		/// <summary>Ticks since the lever was pulled.</summary>
		public int SpinAge { get; private set; } = 999;

		/// <summary>Spins in a row that paid more than the bet.</summary>
		public int Streak { get; private set; }

		/// <summary>Reels that are part of the win (for the glow).</summary>
		public int WinningReels { get; private set; }

		private int timer;

		/// <summary>Auto spin: spins left, the chosen number, and the pause between spins.</summary>
		public int AutoLeft;
		public int AutoChoice = 1;
		private int autoWait;

		public void ToggleAuto()
		{
			AutoLeft = AutoLeft > 0 ? 0 : AutoSpin.Counts[AutoChoice];
			autoWait = 0;
		}

		public bool CanSpin => Phase != SlotPhase.Spinning;

		public bool TrySpin(Player player, long bet)
		{
			if (!CanSpin)
				return false;
			if (!CasinoRewards.TryWager(player, bet)) {
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.UI.Slots.NotEnough"), Color.OrangeRed);
				return false;
			}

			LastBet = bet;
			for (int k = 0; k < SlotRules.Reels; k++) {
				Result[k] = SlotRules.Roll(Main.rand);
				Stopped[k] = false;
			}
			Phase = SlotPhase.Spinning;
			timer = 0;
			WinFlash = 0;
			WinningReels = 0;
			SpinAge = 0;
			SetMessage("", Color.White);
			SoundEngine.PlaySound(CasinoSounds.LeverPull);
			SoundEngine.PlaySound(CasinoSounds.ReelSpin);
			return true;
		}

		public void Update(Player player)
		{
			SpinAge++;
			if (WinFlash > 0)
				WinFlash--;
			for (int k = 0; k < Bounce.Length; k++)
				if (Bounce[k] > 0)
					Bounce[k]--;
			if (CanSpin && AutoLeft > 0 && ++autoWait > (WinFlash > 0 ? 80 : 30)) {
				autoWait = 0;
				long[] chips = CasinoTableGameSystem.Casino?.ChipValues;
				bool started = chips != null && TrySpin(player, chips[System.Math.Clamp(BetIndex, 0, chips.Length - 1)]);
				AutoLeft = started ? AutoLeft - 1 : 0;   // out of coins: stop
			}
			if (Phase != SlotPhase.Spinning)
				return;

			timer++;
			for (int k = 0; k < SlotRules.Reels; k++) {
				if (Stopped[k])
					continue;

				bool slow = k == 2 && Tension;
				int before = (int)Offset[k];
				float left = StopTicks[2] + TensionTicks - timer;
				Offset[k] += slow ? MathHelper.Lerp(0.08f, SpinSpeed, MathHelper.Clamp(left / TensionTicks, 0f, 1f)) : SpinSpeed;
				if (slow && (int)Offset[k] != before)
					SoundEngine.PlaySound(CasinoSounds.CrateTick with { Volume = 0.8f, Pitch = 0.4f - left / TensionTicks * 0.4f });
				if (timer >= StopTicks[k] + (slow ? TensionTicks : 0)) {
					Bounce[k] = 14;
					Stopped[k] = true;
					Offset[k] = (int)Result[k];
					SoundEngine.PlaySound(CasinoSounds.ReelStop with { Pitch = k * 0.12f });
					if (k == 1 && Result[0] == Result[1])
						SoundEngine.PlaySound(CasinoSounds.SlotTease);
				}
			}

			if (Stopped[SlotRules.Reels - 1])
				Finish(player);
		}

		/// <summary>Called when the window closes mid-spin: stops all reels and pays out at once.</summary>
		public void FinishImmediately(Player player)
		{
			AutoLeft = 0;
			if (Phase != SlotPhase.Spinning)
				return;
			for (int k = 0; k < SlotRules.Reels; k++) {
				Stopped[k] = true;
				Offset[k] = (int)Result[k];
			}
			Finish(player);
		}

		private void Finish(Player player)
		{
			Phase = SlotPhase.Result;
			int multiplier = SlotRules.Payout(Result[0], Result[1], Result[2]);
			LastPayout = LastBet * multiplier;
			Coins.Give(player, LastPayout);
			CasinoRecords.Report(player, CasinoGame.Slots, LastPayout - LastBet);
			WinningReels = multiplier <= 0 ? 0 : Result[0] == Result[1] && Result[1] == Result[2] ? 3 : Result[0] == Result[1] ? 2 : 1;
			Streak = multiplier > 1 ? Streak + 1 : 0;
			UiFx.Win(LastPayout, LastBet);
			if (WinningReels == 3 && Result[0] == SlotSymbol.Diamond)
				SoundEngine.PlaySound(CasinoSounds.DiamondBeam);
			if (Streak >= 3)
				SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.6f });
			if (WinningReels != 3 && Result[0] == Result[1])
				SoundEngine.PlaySound(CasinoSounds.NearMiss);

			if (multiplier > 1) {
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.UI.Slots.Win", multiplier, Coins.Format(LastPayout)), Color.LightGreen);
				SoundEngine.PlaySound(multiplier >= 30 ? CasinoSounds.Jackpot : CasinoSounds.SmallWin);
				SoundEngine.PlaySound(CasinoSounds.CoinsPayout);
				WinFlash = FlashTicks;
				if (multiplier >= 30) {
					CombatText.NewText(player.Hitbox, new Color(255, 215, 60), "+" + Coins.Format(LastPayout), dramatic: true);
				}
			}
			else if (multiplier == 1) {
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.UI.Slots.BetBack"), Color.LightGray);
			}
			else {
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.UI.Slots.Lose"), Color.LightGray);
			}

			CasinoRewards.TryJackpot(player, LastBet, SlotRules.JackpotChance(Result[0], Result[1], Result[2]));
		}

		private void SetMessage(string text, Color color)
		{
			Message = text;
			MessageColor = color;
		}
	}
}
