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

		private int timer;

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
			SetMessage("", Color.White);
			SoundEngine.PlaySound(CasinoSounds.LeverPull);
			SoundEngine.PlaySound(CasinoSounds.ReelSpin);
			return true;
		}

		public void Update(Player player)
		{
			if (WinFlash > 0)
				WinFlash--;
			if (Phase != SlotPhase.Spinning)
				return;

			timer++;
			for (int k = 0; k < SlotRules.Reels; k++) {
				if (Stopped[k])
					continue;

				Offset[k] += SpinSpeed;
				if (timer >= StopTicks[k]) {
					Stopped[k] = true;
					Offset[k] = (int)Result[k];
					SoundEngine.PlaySound(CasinoSounds.ReelStop);
				}
			}

			if (Stopped[SlotRules.Reels - 1])
				Finish(player);
		}

		/// <summary>Called when the window closes mid-spin: stops all reels and pays out at once.</summary>
		public void FinishImmediately(Player player)
		{
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
