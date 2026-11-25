using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Games;
using CasinoWorld.Common.Net;
using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.NPCs.Enemies;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasinoWorld.Content.NPCs.Boss;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace CasinoWorld.Common.Club
{
	/// <summary>
	/// One step of a contract. Stat: a statistic of the Ledger has to grow by Count. Marker: go to a place in the world
	/// and beat the ambush there. Zone: the same in any place that fits. Kill: defeat Count enemies of the list.
	/// </summary>
	public record ContractStep(StepKind Kind, int Count, Func<CasinoStats, long> Stat = null, Func<Player, bool> Zone = null, Func<int[]> Enemies = null, bool NoDeath = false, bool Money = false);

	public enum StepKind { Stat, Marker, Zone, Kill }

	/// <summary>A job for the house: steps in order, then tokens and one item of your choice.</summary>
	public record Contract(string Key, int Tokens, Func<bool> Open, ContractStep[] Steps, Func<(int type, int stack)[]> Rewards)
	{
		public string Name => Language.GetTextValue($"Mods.CasinoWorld.Contracts.{Key}.Name");

		public string Text => Language.GetTextValue($"Mods.CasinoWorld.Contracts.{Key}.Text");

		public string Locked => Language.GetTextValue($"Mods.CasinoWorld.Contracts.{Key}.Locked");

		public string StepText(int k) => Language.GetTextValue($"Mods.CasinoWorld.Contracts.{Key}.Step{k}", Steps[k].Money ? Coins.Format(Steps[k].Count * Coins.Gold) : Steps[k].Count);

		public long Goal(int k) => Steps[k].Money ? Steps[k].Count * Coins.Gold : Steps[k].Count;
	}

	public static class ContractBook
	{
		private static int N<T>() where T : ModNPC => ModContent.NPCType<T>();

		private static int I<T>() where T : ModItem => ModContent.ItemType<T>();

		private static Func<(int, int)[]> One(params Func<int>[] types) => () => types.Select(t => (t(), 1)).ToArray();

		private static ContractStep Stat(int count, Func<CasinoStats, long> stat, bool noDeath = false, bool money = false) => new(StepKind.Stat, count, stat, NoDeath: noDeath, Money: money);

		private static ContractStep Ambush(int count, Func<Player, bool> zone, Func<int[]> enemies) => new(zone == null ? StepKind.Marker : StepKind.Zone, count, Zone: zone, Enemies: enemies);

		private static Func<CasinoStats, long> Wins(CasinoGame g) => s => s.GameWins[(int)g];

		private static int[] Pests() => new[] { N<CardSoldier>(), N<ChipSlime>(), N<LuckyBat>() };

		private static int[] Guards() => new[] { N<RoyalGuard>(), N<RouletteWraith>(), N<CardSoldier>() };

		private static bool Always() => true;

		public static readonly Contract[] All = {
			new("LostShipment", 60, Always, new[] { Ambush(6, null, Pests) },
				One(I<Content.Items.Weapons.LoadedDice>, I<Content.Items.Weapons.CardDeck>, I<Content.Items.Weapons.HighRoller>)),
			new("HouseRules", 50, Always, new[] { Stat(3, Wins(CasinoGame.Roulette)), Stat(3, Wins(CasinoGame.Blackjack)), Stat(5, Wins(CasinoGame.Arcade)) },
				One(I<Content.Items.Armor.GamblerFedora>, I<Content.Items.Armor.GamblerVest>, I<Content.Items.Armor.GamblerShoes>)),
			new("Debts", 40, Always, new[] { new ContractStep(StepKind.Kill, 10, Enemies: () => new[] { N<BrokeGambler>(), N<LoanShark>() }) },
				One(I<Content.Items.Accessories.FourLeafClover>, I<Content.Items.Accessories.AceUpTheSleeve>)),
			new("ColdDeck", 60, Always, new[] { Ambush(6, p => p.ZoneSnow, Pests), Ambush(6, p => p.ZoneDesert || p.ZoneUndergroundDesert, Pests) },
				() => new[] { (I<Content.Items.Tools.ChipChainHook>(), 1), (I<Content.Items.Accessories.GolexWatch>(), 1), (I<Content.Items.Crates.LuckyCrate>(), 3) }),
			new("JokersWild", 80, () => NPC.downedBoss3, new[] { Stat(1, s => s.WildCardKills, true) },
				One(I<Content.Items.Weapons.JackpotLever>, I<Content.Items.Weapons.DealersHand>, I<Content.Items.Weapons.WheelOfFortune>)),
			new("BigScore", 100, () => Main.hardMode, new[] { Stat(150, s => s.JackpotHits), Stat(100, s => s.WonTotal, money: true) },
				One(I<Content.Items.Accessories.MartingaleMedallion>, I<Content.Items.Accessories.HighRollersRing>, I<Content.Items.Accessories.DealersVisor>)),
			new("HellLedger", 100, () => Main.hardMode, new[] { Ambush(8, p => p.ZoneUnderworldHeight, Guards) },
				One(I<Content.Items.Weapons.SnakeEyes>, I<Content.Items.Weapons.RoyalFlush>, I<Content.Items.Weapons.JackpotCannon>)),
			new("Raid", 120, () => NPC.downedMechBossAny, new[] { Stat(1, s => s.RaidWins) },
				() => new[] { (I<Content.Items.PitBoss.VaultBullion>(), 12), (I<Content.Items.PitBoss.VaultKey>(), 2), (I<Content.Items.Crates.HighRollerCrate>(), 4) }),
			new("VaultAudit", 150, () => NPC.downedMechBossAny, new[] { Stat(5, s => s.AllIns), Stat(1, s => s.PitBossKills, true) },
				One(I<Content.Items.PitBoss.CroupiersRake>, I<Content.Items.PitBoss.BankBreaker>, I<Content.Items.PitBoss.VaultBuster>)),
			new("JungleMarker", 150, () => NPC.downedPlantBoss, new[] { Ambush(10, p => p.ZoneJungle && p.ZoneRockLayerHeight, Guards), Stat(3, s => s.Crates) },
				One(I<Content.Items.Weapons.AceOfSpades>, I<Content.Items.Weapons.DoublingDice>, I<Content.Items.Weapons.RouletteRevolver>)),
			new("StarDebt", 250, () => NPC.downedMoonlord, new[] { Ambush(12, p => p.ZoneSkyHeight, () => new[] { N<RouletteWraith>() }), Stat(300, s => s.JackpotHits) },
				One(I<Content.Items.Weapons.StardustDeck>, I<Content.Items.Weapons.MeteorDice>, I<Content.Items.Weapons.InfinitySlot>)),
			new("FortunesFavor", 300, () => CasinoDowned.DownedFortuna, new[] { Stat(1, s => s.FortunaKills, true) },
				() => new[] { (I<Content.Items.Fortuna.FateShard>(), 18) }),
		};

		public static Contract Find(string key) => Array.Find(All, c => c.Key == key);
	}

	/// <summary>The contract a character works on (one at a time) and those already done.</summary>
	public class ContractPlayer : ModPlayer
	{
		private const int MarkerRange = 400, AmbushSize = 4;

		public string Active = "";
		public int Step;
		public long Progress;
		public readonly HashSet<string> Done = new();

		private long baseline;              // the statistic when the step began
		private bool arrived;               // the ambush of this step has started
		private Vector2 marker;             // Marker step: the place...
		private int markerWorld;            // ...and the world it belongs to
		private int ambushTimer;
		private bool diedInFight;
		private int quietTicks;

		public Contract Current => ContractBook.Find(Active);

		public bool Complete => Current is { } c && Step >= c.Steps.Length;

		public bool Arrived => arrived;

		/// <summary>Where to go, or null when the step has no marker here.</summary>
		public Vector2? Marker => Current is { } c && Step < c.Steps.Length && c.Steps[Step].Kind == StepKind.Marker && markerWorld == Main.worldID ? marker : null;

		private static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.Contracts." + key, args);

		public bool Accept(Contract c)
		{
			if (Active != "" || !c.Open())
				return false;
			Active = c.Key;
			Step = 0;
			BeginStep();
			SoundEngine.PlaySound(CasinoSounds.CardDeal);
			Main.NewText(Text("Accepted", c.Name), CasinoDraw.Gold);
			return true;
		}

		public void Abandon()
		{
			Active = "";
			SoundEngine.PlaySound(CasinoSounds.PokerFold);
		}

		/// <summary>Pays a finished contract: the first time tokens and the chosen item, after that half the tokens.</summary>
		public void Claim(int choice)
		{
			if (!Complete)
				return;
			Contract c = Current;
			bool first = Done.Add(c.Key);
			int tokens = first ? c.Tokens : c.Tokens / 2;
			Coins.GiveItem(Player, new Item(ModContent.ItemType<LoyaltyToken>(), tokens));
			CasinoStats stats = Player.GetModPlayer<CasinoStats>();
			stats.TokensEarned += tokens;
			stats.Contracts++;
			if (first) {
				(int type, int stack)[] rewards = c.Rewards();
				(int type, int stack) = rewards[Math.Clamp(choice, 0, rewards.Length - 1)];
				Coins.GiveItem(Player, new Item(type, stack));
			}
			Active = "";
			SoundEngine.PlaySound(CasinoSounds.Jackpot);
			UiFx.Banner(Text("Paid", c.Name), CasinoDraw.Gold, 0, 1);
			UiFx.Coins_(UiFx.ScreenCenter, 40, 12f);
		}

		private void BeginStep()
		{
			Progress = 0;
			arrived = false;
			ambushTimer = 0;
			if (Complete)
				return;
			ContractStep s = Current.Steps[Step];
			if (s.Kind == StepKind.Stat)
				baseline = s.Stat(Player.GetModPlayer<CasinoStats>());
			markerWorld = 0;                // rolled in the main world, see PostUpdate
		}

		private void RollMarker()
		{
			// Between a fifth and a half of the way to the edge, in the upper caverns.
			float way = Main.rand.NextFloat(0.2f, 0.5f) * (Main.rand.NextBool() ? 1 : -1);
			int x = (int)(Main.maxTilesX * (0.5f + way * 0.8f));
			int y = (int)MathHelper.Lerp((float)Main.rockLayer, Main.maxTilesY - 300, Main.rand.NextFloat(0.05f, 0.4f));
			marker = new Vector2(x * 16, y * 16);
			markerWorld = Main.worldID;
		}

		public override void PostUpdate()
		{
			if (Player.whoAmI != Main.myPlayer)
				return;
			bool boss = false;
			foreach (NPC npc in Main.ActiveNPCs)
				boss |= npc.boss;
			quietTicks = boss ? 0 : quietTicks + 1;
			if (quietTicks > 120)
				diedInFight = false;

			if (Active == "" || Complete)
				return;
			if (Current == null) {
				Active = "";                // a contract of an older version
				return;
			}
			ContractStep s = Current.Steps[Step];
			switch (s.Kind) {
				case StepKind.Stat: {
					long now = s.Stat(Player.GetModPlayer<CasinoStats>());
					if (s.NoDeath && diedInFight && now > baseline + Progress) {
						baseline = now - Progress;
						Main.NewText(Text("Died"), new Color(255, 140, 120));
					}
					Progress = Math.Max(0, now - baseline);
					break;
				}
				case StepKind.Marker or StepKind.Zone when !SubworldLibrary.SubworldSystem.AnyActive():
					if (s.Kind == StepKind.Marker && markerWorld != Main.worldID) {
						RollMarker();
						arrived = false;
					}
					bool here = s.Kind == StepKind.Marker ? Player.Distance(marker) < MarkerRange : s.Zone(Player);
					if (!arrived && here && !Player.dead) {
						arrived = true;
						ambushTimer = 60;
						Main.NewText(Text("Ambush"), new Color(255, 110, 100));
						SoundEngine.PlaySound(CasinoSounds.HuntHorn);
					}
					if (arrived && here && !Player.dead)
						Reinforce(s);
					break;
			}
			if (Progress < Current.Goal(Step))
				return;
			Step++;
			SoundEngine.PlaySound(CasinoSounds.SmallWin);
			Main.NewText(Complete ? Text("Complete", Current.Name) : Text("StepDone", Current.StepText(Step)), CasinoDraw.Gold);
			BeginStep();
		}

		/// <summary>Sends the next few enemies of the ambush once the last ones are gone.</summary>
		private void Reinforce(ContractStep s)
		{
			int[] types = s.Enemies();
			foreach (NPC npc in Main.ActiveNPCs)
				if (Array.IndexOf(types, npc.type) >= 0 && npc.Distance(Player.Center) < 1600f)
					ambushTimer = Math.Max(ambushTimer, 90);
			if (--ambushTimer > 0)
				return;
			ambushTimer = 240;
			int count = (int)Math.Min(AmbushSize, s.Count - Progress);
			if (Main.netMode == NetmodeID.SinglePlayer)
				Ambush.Spawn(Player, Active, Step, count);
			else
				Ambush.Send(Mod, Active, Step, count);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Player.whoAmI != Main.myPlayer || Active == "" || Complete || target.life > 0 || target.SpawnedFromStatue || Current == null)
				return;
			ContractStep s = Current.Steps[Step];
			if (s.Kind != StepKind.Stat && (s.Kind == StepKind.Kill || arrived) && Array.IndexOf(s.Enemies(), target.type) >= 0 && Progress < s.Count) {
				Progress++;
				CombatText.NewText(Player.Hitbox, CasinoDraw.Gold, $"{Progress} / {s.Count}");
			}
		}

		public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
		{
			if (Player.whoAmI == Main.myPlayer && quietTicks == 0)
				diedInFight = true;
		}

		public override void SaveData(TagCompound tag)
		{
			tag["done"] = Done.ToList();
			if (Active == "")
				return;
			tag["active"] = Active;
			tag["step"] = Step;
			tag["progress"] = Progress;
			tag["baseline"] = baseline;
			tag["arrived"] = arrived;
			tag["marker"] = marker;
			tag["markerWorld"] = markerWorld;
		}

		public override void LoadData(TagCompound tag)
		{
			Done.Clear();
			foreach (string key in tag.GetList<string>("done"))
				Done.Add(key);
			Active = tag.GetString("active");
			Step = tag.GetInt("step");
			Progress = tag.GetLong("progress");
			baseline = tag.GetLong("baseline");
			arrived = tag.GetBool("arrived");
			marker = tag.Get<Vector2>("marker");
			markerWorld = tag.GetInt("markerWorld");
			if (Current == null)
				Active = "";
		}
	}

	/// <summary>Contract ambushes. The server spawns them; the client only names its contract step.</summary>
	public static class Ambush
	{
		private static readonly uint[] lastCall = new uint[Main.maxPlayers];

		public static void Send(Mod mod, string key, int step, int count)
		{
			ModPacket packet = mod.GetPacket();
			packet.Write((byte)CasinoMessage.ContractAmbush);
			packet.Write(key);
			packet.Write((byte)step);
			packet.Write((byte)count);
			packet.Send();
		}

		public static void Handle(BinaryReader reader, int sender)
		{
			string key = reader.ReadString();
			int step = reader.ReadByte(), count = reader.ReadByte();
			// A client cannot ask more often than the game itself does.
			if (lastCall[sender] != 0 && Main.GameUpdateCount - lastCall[sender] < 120)
				return;
			lastCall[sender] = Main.GameUpdateCount;
			Spawn(Main.player[sender], key, step, Math.Min(count, 4));
		}

		public static void Spawn(Player player, string key, int step, int count)
		{
			if (ContractBook.Find(key) is not { } c || step < 0 || step >= c.Steps.Length || c.Steps[step].Enemies == null || player.dead)
				return;
			int[] types = c.Steps[step].Enemies();
			for (int k = 0; k < count; k++) {
				if (FindSpot(player) is not { } at)
					continue;
				int n = NPC.NewNPC(player.GetSource_Misc("Contract"), at.X, at.Y, Main.rand.Next(types), Target: player.whoAmI);
				if (n >= Main.maxNPCs)
					continue;
				Main.npc[n].timeLeft = NPC.activeTime * 2;
				if (Main.netMode == NetmodeID.Server)
					NetMessage.SendData(MessageID.SyncNPC, number: n);
			}
		}

		/// <summary>An open place 18 to 40 tiles away, on the ground when there is some.</summary>
		private static Point? FindSpot(Player player)
		{
			Point p = player.Center.ToTileCoordinates();
			Point? air = null;
			for (int tries = 0; tries < 200; tries++) {
				int x = p.X + Main.rand.Next(18, 40) * (Main.rand.NextBool() ? 1 : -1), y = p.Y + Main.rand.Next(-18, 14);
				if (!WorldGen.InWorld(x, y, 10) || Collision.SolidTiles(x - 1, x + 1, y - 3, y) || Main.tile[x, y].LiquidAmount > 100)
					continue;
				var spot = new Point(x * 16 + 8, y * 16 + 16);
				air ??= spot;
				if (WorldGen.SolidTile(x, y + 1))
					return spot;
			}
			return air;
		}
	}

	/// <summary>The way to a contract's marker: an arrow at the screen edge, a beam of light at the place itself.</summary>
	public class ContractMarker : ModSystem
	{
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Entity Health Bars");
			if (index < 0)
				return;
			layers.Insert(index, new LegacyGameInterfaceLayer("CasinoWorld: Contract Marker", () => {
				if (!Main.LocalPlayer.dead && !SubworldLibrary.SubworldSystem.AnyActive() && Main.LocalPlayer.GetModPlayer<ContractPlayer>() is { Marker: Vector2 at } c)
					Draw(Main.spriteBatch, at, c.Arrived);
				return true;
			}, InterfaceScaleType.Game));
		}

		private static void Draw(SpriteBatch sb, Vector2 world, bool arrived)
		{
			float t = Main.GlobalTimeWrappedHourly, pulse = 0.75f + 0.25f * (float)Math.Sin(t * 4f);
			Vector2 at = world - Main.screenPosition;
			var view = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
			Color gold = arrived ? new Color(255, 110, 90) : new Color(255, 215, 90);
			if (view.Contains(at.ToPoint())) {
				PixelFx.Glow(sb, at, 72f, gold * (0.5f * pulse));
				PixelFx.Burst(sb, at, gold * 0.5f, t * 0.5f, 2);
				LedgerPanel.DrawItem(sb, ModContent.ItemType<Content.Items.Crates.LuckyCrate>(), at + new Vector2(0, (float)Math.Sin(t * 2f) * 4f), 40f, Color.White);
				return;
			}
			Vector2 center = view.Center.ToVector2(), dir = (at - center).SafeNormalize(Vector2.UnitY);
			Vector2 tip = center + dir * Math.Min(view.Width, view.Height) * 0.36f, side = dir.RotatedBy(MathHelper.PiOver2);
			for (int k = 0; k < 2; k++) {
				Vector2 point = tip + dir * (k * 14f + pulse * 6f);
				PixelFx.Line(sb, point, point - dir * 16f + side * 14f, gold * pulse, 4f);
				PixelFx.Line(sb, point, point - dir * 16f - side * 14f, gold * pulse, 4f);
			}
			int feet = (int)(Main.LocalPlayer.Distance(world) / 8f);
			Utils.DrawBorderString(sb, Language.GetTextValue("Mods.CasinoWorld.Contracts.Distance", feet), tip - dir * 34f, gold, 0.8f, 0.5f, 0.5f);
		}
	}
}
