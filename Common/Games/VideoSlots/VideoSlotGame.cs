using System;
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
	/// <summary>
	/// How each machine looks: symbol icons (positive = item, negative = NPC portrait, 0 = drawn by the UI),
	/// theme colours.
	/// </summary>
	public static class SlotThemes
	{
		public static int[] Icons(string key) => key switch {
			"LunarGods" => new int[] { ItemID.FragmentSolar, ItemID.FragmentVortex, ItemID.FragmentNebula, ItemID.FragmentStardust, ItemID.LunarOre,
				ItemID.LunarBar, ItemID.Terrarian, ItemID.Meowmere, ItemID.BossMaskMoonlord, ItemID.CelestialSigil, 0 },
			"Angler" => new int[] { ItemID.ApprenticeBait, ItemID.JourneymanBait, ItemID.MasterBait, ItemID.FishingPotion,
				ItemID.TackleBox, ItemID.FishermansGuide, ItemID.GoldenFishingRod, ItemID.Bass, ItemID.AnglerHat, ItemID.GoldenCrate },
			"Boreal" => new int[] { ItemID.Shiverthorn, ItemID.IceTorch, ItemID.SnowballCannon, ItemID.IceSkates,
				-NPCID.Penguin, -NPCID.SnowFlinx, -NPCID.Wolf, -NPCID.IceGolem, ItemID.IceMirror, ItemID.FrozenCrate },
			_ => new int[] { ItemID.Vertebrae, ItemID.ChumBucket, ItemID.SharkToothNecklace, ItemID.BloodRainBow,
				ItemID.VampireFrogStaff, ItemID.BloodyMachete, ItemID.SanguineStaff, ItemID.MoneyTrough, ItemID.Boomstick, ItemID.BloodMoonStarter },
		};

		/// <summary>Angler fish: the icon grows with the cash it carries.</summary>
		public static int FishIcon(int units) => units switch {
			>= 1000 => ItemID.GoldenCarp,
			>= 250 => ItemID.Swordfish,
			>= 100 => ItemID.Tuna,
			>= 50 => ItemID.RedSnapper,
			>= 20 => ItemID.Salmon,
			_ => ItemID.Bass,
		};

		public static int ScatterIndex => 9;

		public static int SymbolCount(string key) => key == "LunarGods" ? 11 : 10;

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

	public enum VideoPhase { Idle, Spinning, Steps, Intro, Pause, Outro }

	/// <summary>
	/// One player's session at a video slot: the machine engine, the spin and step animation timeline and
	/// payouts. Free spins play automatically; closing the window plays out the rest instantly.
	/// </summary>
	public class VideoSlotRound
	{
		public const int ReelStopStart = 26, ReelStopStep = 10, TeaseStep = 42, PauseTicks = 46, IntroTicks = 170, LotteryTicks = 250, OutroTicks = 170;

		public string Key { get; private set; }
		public SlotMachine Machine { get; private set; }
		public VideoPhase Phase { get; private set; } = VideoPhase.Idle;
		public SpinResult Result { get; private set; }
		public int Step { get; private set; }
		public int Timer { get; private set; }
		public int BetIndex;
		public long LastBet { get; private set; }
		public long BonusTotal { get; private set; }
		/// <summary>Win of the current spin shown so far (counts up with the steps).</summary>
		public long ShownUnits { get; private set; }
		public int BigWinTimer { get; private set; }
		public long BigWinUnits { get; private set; }
		public string Message = "";
		public Color MessageColor = Color.White;

		private bool inBonusRun;
		private bool paid;
		private ReLogic.Utilities.SlotId teaseSound = ReLogic.Utilities.SlotId.Invalid;

		/// <summary>Auto spin: spins left, the chosen number, and the pause between spins.</summary>
		public int AutoLeft;
		public int AutoChoice = 1;
		private int autoWait;

		public void ToggleAuto()
		{
			AutoLeft = AutoLeft > 0 ? 0 : AutoSpin.Counts[AutoChoice];
			autoWait = 0;
		}

		public bool InBonusRun => inBonusRun;
		public bool CanSpin => Phase == VideoPhase.Idle && !inBonusRun;

		public void Use(string key)
		{
			if (Key == key && Machine != null)
				return;
			AutoLeft = 0;
			Key = key;
			Machine = SlotGames.Create(key, n => Main.rand.Next(n));
			Machine.PayScale *= Systems.CasinoConfig.Instance.VideoSlotPayout;
			Phase = VideoPhase.Idle;
			Result = null;
			inBonusRun = false;
			Message = Language.GetTextValue("Mods.CasinoWorld.UI.VideoSlots.Hint" + key);
			MessageColor = Color.White;
		}

		public long Coins(long units) => units * LastBet / SlotMachine.Unit;

		/// <summary>Tick at which a reel stops; teased reels (one scatter short) spin noticeably longer.</summary>
		public int StopTick(int reel)
		{
			int t = ReelStopStart + ReelStopStep * reel;
			if (Result != null && Result.TeaseFrom >= 0 && reel >= Result.TeaseFrom)
				t += TeaseStep * (reel - Result.TeaseFrom + 1);
			return t;
		}

		/// <summary>Remaining reels glow once the reels before the tease have stopped.</summary>
		public bool Teasing(int reel) => Phase == VideoPhase.Spinning && Result != null && Result.TeaseFrom > 0 && reel >= Result.TeaseFrom
			&& Timer < StopTick(reel) && Timer >= StopTick(Result.TeaseFrom - 1);

		/// <summary>The reel that is about to stop during a tease (spins slowest, glows brightest).</summary>
		public bool TeaseNext(int reel) => Teasing(reel) && (reel == Result.TeaseFrom || Timer >= StopTick(reel - 1));

		public SlotStep CurrentStep => Result == null ? null : Result.Steps[Math.Min(Step, Result.Steps.Count - 1)];

		public int StepDuration(SlotStep s)
		{
			int fx = s.Fx.Count;
			int d = s.Kind switch {
				StepKind.Land => s.Wins.Count > 0 ? 44 : 8,
				StepKind.Tumble => 40,
				StepKind.Multiply => 80,
				StepKind.Collect => 34 + 16 * fx,
				StepKind.Shots => 22 + 12 * fx,
				StepKind.Expand => 56,
				StepKind.Upgrade => 80,
				_ => 30,
			};
			if (Key == "LunarGods" && fx > 0 && s.Kind is StepKind.Land or StepKind.Tumble)
				d += 26 + 10 * fx;   // Moon Lord's beams reveal the orbs
			return d;
		}

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

		/// <summary>Click on the reels: jump to the end of the running animation. The result is already rolled.</summary>
		public void Skip()
		{
			if (Result == null)
				return;
			switch (Phase) {
				case VideoPhase.Spinning:
					Timer = Math.Max(Timer, StopTick(Machine.Reels - 1) + 3);
					break;
				case VideoPhase.Steps:
					Timer = Math.Max(Timer, StepDuration(CurrentStep));
					break;
				case VideoPhase.Intro when Timer > 60:
					Timer = Math.Max(Timer, Key == "Boreal" ? LotteryTicks : IntroTicks);
					break;
				case VideoPhase.Pause:
					Timer = PauseTicks;
					break;
				case VideoPhase.Outro when Timer > 90:
					Timer = OutroTicks;
					break;
			}
		}

		private void StartSpin()
		{
			Result = Machine.Spin();
			Phase = VideoPhase.Spinning;
			Step = 0;
			Timer = 0;
			ShownUnits = 0;
			paid = false;
			Message = "";
			SoundEngine.PlaySound(CasinoSounds.ReelSpin);
		}

		public void Update(Player player)
		{
			if (BigWinTimer > 0)
				BigWinTimer--;
			if (CanSpin && AutoLeft > 0 && ++autoWait > 24) {
				autoWait = 0;
				long[] chips = CasinoTableGameSystem.Casino?.ChipValues;
				if (chips != null)
					TrySpin(player, chips[Math.Clamp(BetIndex, 0, chips.Length - 1)]);
				AutoLeft = Phase == VideoPhase.Spinning ? AutoLeft - 1 : 0;   // out of coins: stop
			}
			if (Phase == VideoPhase.Idle || Result == null)
				return;
			Timer++;
			switch (Phase) {
				case VideoPhase.Spinning:
					for (int r = 0; r < Machine.Reels; r++)
						if (Timer == StopTick(r))
							SoundEngine.PlaySound(CasinoSounds.ReelStop with { Pitch = Result.TeaseFrom >= 0 && r >= Result.TeaseFrom ? 0.3f : 0f });
					if (Result.TeaseFrom > 0 && Timer == StopTick(Result.TeaseFrom - 1) + 1)
						teaseSound = SoundEngine.PlaySound(CasinoSounds.SlotTease);
					if (Result.TeaseFrom > 0 && Timer > StopTick(Result.TeaseFrom - 1) && Timer % 10 == 0 && Timer < StopTick(Machine.Reels - 1))
						SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.7f, Pitch = Math.Min(1f, -0.4f + Timer / 200f) });
					if (Timer >= StopTick(Machine.Reels - 1) + 4) {
						if (SoundEngine.TryGetActiveSound(teaseSound, out var tease))
							tease.Stop();
						Timer = 0;
						Phase = VideoPhase.Steps;
						BeginStep(player);
					}
					break;
				case VideoPhase.Steps:
					if (Timer >= StepDuration(CurrentStep)) {
						Timer = 0;
						if (Step >= Result.Steps.Count - 1)
							Finish(player);
						else {
							Step++;
							BeginStep(player);
						}
					}
					break;
				case VideoPhase.Intro:
					if (Timer >= (Key == "Boreal" ? LotteryTicks : IntroTicks)) {
						Timer = 0;
						StartSpin();
					}
					break;
				case VideoPhase.Pause:
					if (Timer >= PauseTicks) {
						Timer = 0;
						if (Machine.InBonus)
							StartSpin();
						else if (inBonusRun) {
							Phase = VideoPhase.Outro;
							SoundEngine.PlaySound(BonusTotal >= LastBet * 30 ? CasinoSounds.Jackpot : CasinoSounds.CoinsPayout);
						}
						else
							Phase = VideoPhase.Idle;
					}
					break;
				case VideoPhase.Outro:
					if (Timer >= OutroTicks)
						EndBonus(player);
					break;
			}
		}

		private void BeginStep(Player player)
		{
			SlotStep s = CurrentStep;
			ShownUnits = 0;
			for (int k = 0; k <= Step && k < Result.Steps.Count; k++) {
				SlotStep prev = Result.Steps[k];
				ShownUnits = prev.Kind == StepKind.Multiply ? prev.Win : ShownUnits + prev.Win;
			}
			switch (s.Kind) {
				case StepKind.Tumble:
					SoundEngine.PlaySound(CasinoSounds.ChipStack with { Pitch = Math.Min(0.8f, Step * 0.12f) });
					break;
				case StepKind.Multiply:
					SoundEngine.PlaySound(SoundID.Item4);
					break;
				case StepKind.Collect:
					SoundEngine.PlaySound(SoundID.Splash);
					break;
				case StepKind.Shots:
					SoundEngine.PlaySound(Key == "BloodMoon" ? SoundID.Item36 : Key == "Boreal" ? SoundID.Item51 : SoundID.Splash);
					break;
				case StepKind.Expand:
					SoundEngine.PlaySound(SoundID.Item30);
					break;
				case StepKind.Upgrade:
					SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.6f });
					break;
			}
			if (Key == "LunarGods" && s.Fx.Count > 0 && s.Kind is StepKind.Land or StepKind.Tumble)
				SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.45f });
			if (s.Wins.Count > 0)
				SoundEngine.PlaySound(CasinoSounds.SmallWin with { Volume = 0.6f, Pitch = Math.Min(0.6f, Step * 0.1f) });
		}

		private void Finish(Player player)
		{
			if (paid)
				return;
			paid = true;
			long units = Result.Win;
			ShownUnits = units;
			long coins = Coins(units);
			if (coins > 0) {
				Games.Coins.Give(player, coins);
				CombatText.NewText(player.Hitbox, Color.Gold, "+" + Games.Coins.Format(coins), units >= SlotMachine.Unit * 20);
			}
			if (Result.FreeSpin)
				BonusTotal += coins;
			else
				CasinoRecords.Report(player, CasinoGame.Slots, coins - LastBet);

			if (units >= SlotMachine.Unit * 15) {
				BigWinTimer = 150;
				BigWinUnits = units;
				int tier = units >= SlotMachine.Unit * 100 ? 3 : units >= SlotMachine.Unit * 50 ? 2 : 1;
				UiFx.Banner(Language.GetTextValue("Mods.CasinoWorld.UI.VideoSlots." + (tier == 3 ? "EpicWin" : tier == 2 ? "MegaWin" : "BigWin")), SlotThemes.Accent(Key), coins, tier);
				UiFx.Coins_(UiFx.ScreenCenter, 60, 13f);
				SoundEngine.PlaySound(CasinoSounds.BigWin);
				CasinoRewards.TryJackpot(player, LastBet, units >= SlotMachine.Unit * 50 ? 1f : 0.5f);
			}

			if (Result.BonusStarted) {
				DailyChallenges.Count(player, Challenge.VideoBonus);
				inBonusRun = true;
				BonusTotal = 0;
				Message = Language.GetTextValue("Mods.CasinoWorld.UI.VideoSlots.Bonus", Result.FreeSpinsAwarded);
				MessageColor = SlotThemes.Accent(Key);
				SoundEngine.PlaySound(CasinoSounds.BonusFanfare);
				SoundEngine.PlaySound(CasinoSounds.CrowdCheer with { Volume = 0.6f });
				Phase = VideoPhase.Intro;
				Timer = 0;
				return;
			}
			if (Result.FreeSpinsAwarded > 0) {
				Message = Language.GetTextValue("Mods.CasinoWorld.UI.VideoSlots.Retrigger", Result.FreeSpinsAwarded);
				MessageColor = SlotThemes.Accent(Key);
				SoundEngine.PlaySound(CasinoSounds.CrowdCheer with { Volume = 0.6f });
			}
			else if (coins > 0) {
				Message = Language.GetTextValue("Mods.CasinoWorld.UI.VideoSlots.Win", Games.Coins.Format(coins));
				MessageColor = Color.LightGreen;
			}
			else if (!inBonusRun) {
				Message = Language.GetTextValue("Mods.CasinoWorld.UI.VideoSlots.Nothing");
				MessageColor = Color.LightGray;
			}
			Phase = inBonusRun || BigWinTimer > 0 ? VideoPhase.Pause : VideoPhase.Idle;
			Timer = 0;
		}

		private void EndBonus(Player player)
		{
			inBonusRun = false;
			Message = Language.GetTextValue("Mods.CasinoWorld.UI.VideoSlots.BonusTotal", Games.Coins.Format(BonusTotal));
			MessageColor = Color.Gold;
			CasinoRecords.Report(player, CasinoGame.Slots, BonusTotal);
			Phase = VideoPhase.Idle;
			Timer = 0;
		}

		/// <summary>Window closed: pay this spin and play out all remaining free spins at once.</summary>
		public void FinishImmediately(Player player)
		{
			AutoLeft = 0;
			if (Result == null || Phase == VideoPhase.Idle)
				return;
			Finish(player);
			int guard = 0;
			while (Machine.InBonus && guard++ < 1000) {
				Result = Machine.Spin();
				paid = false;
				Finish(player);
			}
			if (inBonusRun)
				EndBonus(player);
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

		public void OpenMachine(Point16 topLeft, int tileType, string key)
		{
			if (IsOpen && Round.Key != key)
				Close();
			machineType = tileType;
			Round.Use(key);
			Open(topLeft);
		}
	}
}
