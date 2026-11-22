using System;
using System.Collections.Generic;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Games.VideoSlots
{
	/// <summary>How each machine looks in the game: vanilla item icons as symbols, theme colours.</summary>
	public static class SlotThemes
	{
		public static int[] Icons(string key) => key switch {
			"LunarGods" => new int[] { ItemID.FragmentSolar, ItemID.FragmentVortex, ItemID.FragmentNebula, ItemID.FragmentStardust, ItemID.LunarOre,
				ItemID.LunarBar, ItemID.Terrarian, ItemID.Meowmere, ItemID.CelestialSigil, ItemID.MoonLordTrophy, ItemID.SoulofLight },
			"Angler" => new int[] { ItemID.Bass, ItemID.Trout, ItemID.Salmon, ItemID.AtlanticCod, ItemID.Tuna,
				ItemID.RedSnapper, ItemID.Swordfish, ItemID.GoldenCarp, ItemID.AnglerHat, ItemID.GoldenCrate },
			"Boreal" => new int[] { ItemID.Shiverthorn, ItemID.IceBlock, ItemID.SnowballCannon, ItemID.IceSkates, ItemID.FrostCore,
				ItemID.IceFeather, ItemID.Frostbrand, ItemID.NorthPole, ItemID.IceQueenTrophy, ItemID.FrozenCrate },
			_ => new int[] { ItemID.Vertebrae, ItemID.ChumBucket, ItemID.SharkToothNecklace, ItemID.BloodRainBow, ItemID.VampireFrogStaff,
				ItemID.BloodyMachete, ItemID.SanguineStaff, ItemID.MoneyTrough, ItemID.BloodMoonMonolith, ItemID.BloodMoonStarter },
		};

		public static Color Accent(string key) => key switch {
			"LunarGods" => new Color(110, 220, 255),
			"Angler" => new Color(80, 190, 255),
			"Boreal" => new Color(190, 235, 255),
			_ => new Color(255, 60, 70),
		};

		public static Color Body(string key) => key switch {
			"LunarGods" => new Color(30, 40, 80),
			"Angler" => new Color(20, 70, 90),
			"Boreal" => new Color(40, 70, 100),
			_ => new Color(70, 10, 20),
		};
	}

	public enum VideoPhase { Idle, Spinning, Tumbling, Result }

	/// <summary>
	/// One player's session at a video slot: bets, the engine (with free-spin state), the spin animation
	/// and payouts. Free spins play automatically one after another; closing the window plays out the rest.
	/// </summary>
	public class VideoSlotRound
	{
		public const int ReelStopStart = 30, ReelStopStep = 14, StepTicks = 34, ResultTicks = 70;

		public SlotDefinition Definition { get; private set; }
		public VideoSlotEngine Engine { get; private set; }
		public VideoPhase Phase { get; private set; } = VideoPhase.Idle;
		public SpinOutcome Outcome { get; private set; }
		public int Step { get; private set; }
		public int Timer { get; private set; }
		public int BetIndex;
		public long LastBet { get; private set; }
		public long BonusTotal { get; private set; }
		public long LastWinCoins { get; private set; }
		public string Message = "";
		public Color MessageColor = Color.White;

		public bool InBonus => Engine?.InBonus == true || bonusPending;
		private bool bonusPending;
		public bool CanSpin => Phase is VideoPhase.Idle or VideoPhase.Result && !InBonus;

		public void Use(SlotDefinition definition)
		{
			if (Definition == definition && Engine != null)
				return;
			Definition = definition;
			Engine = new VideoSlotEngine(definition, n => Main.rand.Next(n));
			Phase = VideoPhase.Idle;
			Outcome = null;
			Message = Language.GetTextValue("Mods.CasinoWorld.UI.VideoSlots.Hint");
			MessageColor = Color.White;
		}

		public long Coins(long lineMultiples) => lineMultiples * LastBet / VideoSlotEngine.LineCount;

		public void TrySpin(Player player, long bet)
		{
			if (!CanSpin)
				return;
			if (!CasinoRewards.TryWager(player, bet)) {
				Message = Language.GetTextValue("Mods.CasinoWorld.UI.Slots.NotEnough");
				MessageColor = Color.OrangeRed;
				return;
			}
			LastBet = bet;
			BonusTotal = 0;
			StartSpin();
			SoundEngine.PlaySound(CasinoSounds.LeverPull);
		}

		private void StartSpin()
		{
			Outcome = Engine.Spin();
			Phase = VideoPhase.Spinning;
			Step = 0;
			Timer = 0;
			Message = "";
			SoundEngine.PlaySound(CasinoSounds.ReelSpin);
		}

		public void Update(Player player)
		{
			if (Phase == VideoPhase.Idle || Outcome == null)
				return;
			Timer++;
			switch (Phase) {
				case VideoPhase.Spinning:
					if (Timer >= ReelStopStart && (Timer - ReelStopStart) % ReelStopStep == 0 && (Timer - ReelStopStart) / ReelStopStep < VideoSlotEngine.Reels)
						SoundEngine.PlaySound(CasinoSounds.ReelStop);
					if (Timer >= ReelStopStart + ReelStopStep * VideoSlotEngine.Reels) {
						Timer = 0;
						Phase = Outcome.Steps.Count > 1 ? VideoPhase.Tumbling : VideoPhase.Result;
						if (Phase == VideoPhase.Result)
							Finish(player);
					}
					break;
				case VideoPhase.Tumbling:
					if (Timer >= StepTicks) {
						Timer = 0;
						Step++;
						SoundEngine.PlaySound(CasinoSounds.ChipStack with { Pitch = Step * 0.1f });
						if (Step >= Outcome.Steps.Count - 1) {
							Phase = VideoPhase.Result;
							Finish(player);
						}
					}
					break;
				case VideoPhase.Result:
					if (Timer >= ResultTicks && (Engine.InBonus || bonusPending)) {
						bonusPending = false;
						if (Engine.InBonus)
							StartSpin();
						else
							EndBonus(player);
					}
					break;
			}
		}

		private void Finish(Player player)
		{
			long coins = Coins(Outcome.Win);
			LastWinCoins = coins;
			if (coins > 0) {
				Games.Coins.Give(player, coins);
				CombatText.NewText(player.Hitbox, Color.Gold, "+" + Games.Coins.Format(coins), coins >= LastBet * 20);
			}
			bool freeSpin = inBonusRun;
			if (freeSpin)
				BonusTotal += coins;
			else
				CasinoRecords.Report(player, CasinoGame.Slots, coins - LastBet);

			if (Outcome.FreeSpinsAwarded > 0) {
				if (!inBonusRun) {
					inBonusRun = true;
					BonusTotal = 0;
					Message = Language.GetTextValue("Mods.CasinoWorld.UI.VideoSlots.Bonus", Outcome.FreeSpinsAwarded);
				}
				else
					Message = Language.GetTextValue("Mods.CasinoWorld.UI.VideoSlots.Retrigger", Outcome.FreeSpinsAwarded);
				MessageColor = SlotThemes.Accent(Definition.Key);
				SoundEngine.PlaySound(CasinoSounds.CrowdCheer);
				SoundEngine.PlaySound(CasinoSounds.Jackpot with { Volume = 0.6f });
			}
			else if (coins > 0) {
				Message = Language.GetTextValue("Mods.CasinoWorld.UI.Slots.Win", Outcome.Multiplier > 1 ? Outcome.Multiplier : 1, Games.Coins.Format(coins));
				MessageColor = Color.LightGreen;
				SoundEngine.PlaySound(coins >= LastBet * 20 ? CasinoSounds.Jackpot : CasinoSounds.SmallWin);
				if (coins >= LastBet * 20)
					CasinoRewards.TryJackpot(player, LastBet, 0.5f);
			}
			else if (!inBonusRun) {
				Message = Language.GetTextValue("Mods.CasinoWorld.UI.VideoSlots.Nothing");
				MessageColor = Color.LightGray;
			}

			if (inBonusRun && !Engine.InBonus)
				bonusPending = true;   // show the last free spin, then the bonus summary
		}

		private bool inBonusRun;

		private void EndBonus(Player player)
		{
			inBonusRun = false;
			Message = Language.GetTextValue("Mods.CasinoWorld.UI.VideoSlots.BonusTotal", Games.Coins.Format(BonusTotal));
			MessageColor = Color.Gold;
			CasinoRecords.Report(player, CasinoGame.Slots, BonusTotal);
			if (BonusTotal >= LastBet * 50)
				SoundEngine.PlaySound(CasinoSounds.Jackpot);
			BonusTotal = 0;
			Phase = VideoPhase.Idle;
		}

		/// <summary>Window closed: finish the animation and play out all remaining free spins at once.</summary>
		public void FinishImmediately(Player player)
		{
			if (Outcome == null || Phase == VideoPhase.Idle)
				return;
			if (Phase != VideoPhase.Result)
				Finish(player);
			int guard = 0;
			while (Engine.InBonus && guard++ < 500) {
				Outcome = Engine.Spin();
				Finish(player);
			}
			if (inBonusRun)
				EndBonus(player);
			bonusPending = false;
			Phase = VideoPhase.Idle;
		}
	}

	/// <summary>Window host for the video slots; remembers which machine (tile type and theme) is open.</summary>
	public class VideoSlotSystem : CasinoTableGameSystem
	{
		public VideoSlotRound Round { get; } = new();
		private int machineType = -1;

		protected override int TableTileType => machineType;

		protected override Point TableSize => new(VideoSlotMachine.Width, VideoSlotMachine.Height);

		protected override UIState CreateState() => new VideoSlotUIState();

		protected override void UpdateGame(Player player) => Round.Update(player);

		protected override void OnClose(Player player) => Round.FinishImmediately(player);

		public void OpenMachine(Point16 topLeft, int tileType, SlotDefinition definition)
		{
			if (IsOpen && Round.Definition != definition)
				Close();
			machineType = tileType;
			Round.Use(definition);
			Open(topLeft);
		}
	}
}
