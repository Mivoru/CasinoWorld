using System;
using System.Collections.Generic;
using System.IO;
using CasinoWorld.Common.Effects;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.Crates;
using CasinoWorld.Content.NPCs.Enemies;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Systems
{
	/// <summary>
	/// High Stakes Night: in hardmode after a mechanical boss, 1 night in 10 the card army raids the
	/// surface. Defeat 100 of them (more with more players) and the Guard Captain comes for you; beat
	/// him to end the night with his loot. Dawn ends the raid early.
	/// </summary>
	public enum RaidKind : byte { Standard, DoubleOrNothing, Golden }

	public class HighStakesNight : ModSystem
	{
		/// <summary>Double or Nothing: tougher army, double pay. Golden Night: gold-plated army that drops tokens and more coins.</summary>
		public static RaidKind Kind;

		public static bool CanStart => !Active && Main.hardMode && NPC.downedMechBossAny && !Main.dayTime && CasinoConfig.Instance.HighStakesNight && !SubworldLibrary.SubworldSystem.AnyActive();

		public static bool Active;
		public static int Kills;
		public static bool CaptainSpawned;
		private static bool wasDay = true;

		public static int Goal => 100 + 25 * Math.Max(0, ActivePlayers() - 1);

		private static int ActivePlayers()
		{
			int n = 0;
			foreach (Player p in Main.ActivePlayers)
				n++;
			return Math.Max(1, n);
		}

		public static bool IsArmy(int type) => type == ModContent.NPCType<CardSoldier>() || type == ModContent.NPCType<RoyalGuard>()
			|| type == ModContent.NPCType<RouletteWraith>() || type == ModContent.NPCType<ChipSlime>() || type == ModContent.NPCType<SlotMimic>();

		public override void ClearWorld()
		{
			Active = CaptainSpawned = false;
			Kills = 0;
			Kind = RaidKind.Standard;
		}

		public override void OnWorldLoad() => wasDay = Main.dayTime;

		public override void SaveWorldData(Terraria.ModLoader.IO.TagCompound tag)
		{
			if (!Active)
				return;
			tag["hsnActive"] = true;
			tag["hsnKind"] = (byte)Kind;
			tag["hsnKills"] = Kills;
			tag["hsnCaptain"] = CaptainSpawned;
		}

		public override void LoadWorldData(Terraria.ModLoader.IO.TagCompound tag)
		{
			Active = tag.ContainsKey("hsnActive");
			Kind = (RaidKind)tag.GetByte("hsnKind");
			Kills = tag.ContainsKey("hsnKills") ? tag.GetInt("hsnKills") : 0;
			CaptainSpawned = tag.ContainsKey("hsnCaptain") && tag.GetBool("hsnCaptain");
		}

		private static void Announce(string key, Color color)
		{
			if (Main.netMode == NetmodeID.Server)
				ChatHelper.BroadcastChatMessage(NetworkText.FromKey(key), color);
			else if (Main.netMode == NetmodeID.SinglePlayer)
				Main.NewText(Language.GetTextValue(key), color);
		}

		public override void PostUpdateWorld()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || SubworldLibrary.SubworldSystem.AnyActive())
				return;
			// Nightfall: maybe start a raid.
			if (wasDay && !Main.dayTime) {
				if (!Active && Main.hardMode && NPC.downedMechBossAny && CasinoConfig.Instance.HighStakesNight && Main.rand.NextBool(10))
					Start();
			}
			if (!wasDay && Main.dayTime && Active)
				End(false);
			wasDay = Main.dayTime;
		}

		public static void Start()
		{
			Active = true;
			Kills = 0;
			CaptainSpawned = false;
			Kind = Main.rand.Next(4) switch { 0 => RaidKind.DoubleOrNothing, 1 => RaidKind.Golden, _ => RaidKind.Standard };
			Announce("Mods.CasinoWorld.Event.Start" + (Kind == RaidKind.Standard ? "" : Kind), Kind == RaidKind.Golden ? new Color(255, 215, 90) : new Color(255, 80, 90));
			Sync();
		}

		public static void End(bool won)
		{
			Active = false;
			Announce(won ? "Mods.CasinoWorld.Event.Won" : "Mods.CasinoWorld.Event.Dawn", won ? new Color(255, 215, 90) : new Color(200, 200, 220));
			Sync();
		}

		private static void Sync()
		{
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		/// <summary>Counts an army kill (server); at the goal the Guard Captain arrives.</summary>
		public static void CountKill(NPC npc)
		{
			if (!Active || !IsArmy(npc.type) || npc.SpawnedFromStatue)
				return;
			Kills++;
			if (Kills >= Goal && !CaptainSpawned) {
				CaptainSpawned = true;
				Player target = Main.player[npc.lastInteraction >= 0 && npc.lastInteraction < Main.maxPlayers ? npc.lastInteraction : Player.FindClosest(npc.position, 1, 1)];
				NPC.SpawnOnPlayer(target.whoAmI, ModContent.NPCType<GuardCaptain>());
				Announce("Mods.CasinoWorld.Event.Captain", new Color(255, 120, 90));
			}
			if (Kills % 5 == 0 || CaptainSpawned)
				Sync();
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(Active);
			writer.Write((byte)Kind);
			writer.Write(Kills);
			writer.Write(CaptainSpawned);
		}

		public override void NetReceive(BinaryReader reader)
		{
			Active = reader.ReadBoolean();
			Kind = (RaidKind)reader.ReadByte();
			Kills = reader.ReadInt32();
			CaptainSpawned = reader.ReadBoolean();
		}

		// Progress bar under the vanilla invasion bar position.
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Invasion Progress Bars");
			if (index < 0)
				return;
			layers.Insert(index + 1, new LegacyGameInterfaceLayer("CasinoWorld: High Stakes Night", () => {
				if (Active && !CasinoTravel.InCasino && Main.LocalPlayer.ZoneOverworldHeight)
					DrawBar(Main.spriteBatch);
				return true;
			}, InterfaceScaleType.UI));
		}

		private static void DrawBar(SpriteBatch sb)
		{
			Texture2D px = TextureAssets.MagicPixel.Value;
			int w = 260, h = 18;
			var r = new Rectangle(Main.screenWidth - w - 30, Main.screenHeight - 90, w, h);
			sb.Draw(px, CasinoDrawShrink(r, -3), new Color(40, 22, 14));
			sb.Draw(px, CasinoDrawShrink(r, -1), new Color(226, 172, 62));
			sb.Draw(px, r, new Color(30, 10, 16));
			float p = CaptainSpawned ? 1f : Math.Min(1f, Kills / (float)Goal);
			sb.Draw(px, new Rectangle(r.X, r.Y, (int)(r.Width * p), r.Height), Color.Lerp(Kind == RaidKind.Golden ? new Color(190, 130, 20) : Kind == RaidKind.DoubleOrNothing ? new Color(90, 20, 130) : new Color(200, 30, 50), Kind == RaidKind.Golden ? new Color(255, 225, 110) : Kind == RaidKind.DoubleOrNothing ? new Color(230, 40, 90) : new Color(255, 120, 60), 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4)));
			string name = Language.GetTextValue("Mods.CasinoWorld.Event.Name" + Kind);
			string text = CaptainSpawned ? Language.GetTextValue("Mods.CasinoWorld.Event.BarCaptain", name) : Language.GetTextValue("Mods.CasinoWorld.Event.Bar", name, (int)(p * 100));
			Utils.DrawBorderString(sb, text, new Vector2(r.Center.X, r.Y - 14), Color.White, 0.85f, 0.5f, 0.5f);
		}

		private static Rectangle CasinoDrawShrink(Rectangle r, int by) => new(r.X + by, r.Y + by, r.Width - by * 2, r.Height - by * 2);
	}

	/// <summary>Raid spawns: lots of card army enemies at night on the surface, kills count to the goal.</summary>
	public class HighStakesSpawns : GlobalNPC
	{
		public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
		{
			if (HighStakesNight.Active && player.ZoneOverworldHeight && !CasinoTravel.InCasino) {
				spawnRate = Math.Min(spawnRate, 40);
				maxSpawns = Math.Max(maxSpawns, 12);
			}
		}

		public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
		{
			if (!HighStakesNight.Active || !spawnInfo.Player.ZoneOverworldHeight || CasinoTravel.InCasino || spawnInfo.Invasion)
				return;
			pool.Clear();
			pool[ModContent.NPCType<CardSoldier>()] = 1.4f;
			pool[ModContent.NPCType<ChipSlime>()] = 0.6f;
			pool[ModContent.NPCType<RoyalGuard>()] = 0.8f;
			pool[ModContent.NPCType<RouletteWraith>()] = 0.6f;
			pool[ModContent.NPCType<SlotMimic>()] = HighStakesNight.Kind == RaidKind.Golden ? 0.3f : 0.08f;
		}

		public override void OnKill(NPC npc)
		{
			if (HighStakesNight.Active && HighStakesNight.Kind == RaidKind.Golden && HighStakesNight.IsArmy(npc.type) && !npc.SpawnedFromStatue && Main.rand.NextBool(4))
				Item.NewItem(npc.GetSource_Loot(), npc.Hitbox, ModContent.ItemType<LoyaltyToken>());
			HighStakesNight.CountKill(npc);
		}

		/// <summary>Plated in gold, or burning red, for everyone who sees them.</summary>
		public override void DrawEffects(NPC npc, ref Color drawColor)
		{
			if (!HighStakesNight.Active || HighStakesNight.Kind == RaidKind.Standard || !HighStakesNight.IsArmy(npc.type))
				return;
			bool gold = HighStakesNight.Kind == RaidKind.Golden;
			drawColor = Color.Lerp(drawColor, gold ? new Color(255, 205, 60) : new Color(255, 60, 90), 0.5f);
			if (Main.rand.NextBool(14))
				CasinoFx.Spark(npc.Center + Main.rand.NextVector2Circular(npc.width / 2f, npc.height / 2f), new Vector2(0, -1f), gold ? new Color(255, 225, 110) : new Color(255, 70, 100), 0.7f);
		}

		/// <summary>Raid enemies are tougher than their everyday cousins (the soldiers and slimes are early-game).</summary>
		public override void OnSpawn(NPC npc, Terraria.DataStructures.IEntitySource source)
		{
			if (!HighStakesNight.Active || Main.netMode == NetmodeID.MultiplayerClient)
				return;
			if (npc.type == ModContent.NPCType<CardSoldier>() || npc.type == ModContent.NPCType<ChipSlime>()) {
				npc.lifeMax *= 4;
				npc.life = npc.lifeMax;
				npc.damage = (int)(npc.damage * 2.5f);
				npc.defense += 14;
			}
			if (!HighStakesNight.IsArmy(npc.type) && npc.type != ModContent.NPCType<GuardCaptain>() || npc.SpawnedFromStatue)
				return;
			if (HighStakesNight.Kind == RaidKind.DoubleOrNothing) {
				npc.lifeMax = npc.life = npc.lifeMax * 3 / 2;
				npc.damage = npc.damage * 23 / 20;
			}
			else if (HighStakesNight.Kind == RaidKind.Golden) {
				npc.lifeMax = npc.life = npc.lifeMax * 6 / 5;
				npc.value *= 4f;
			}
		}
	}
}

namespace CasinoWorld.Content.Items
{
	/// <summary>Starts High Stakes Night at once; works at night in hardmode after a mechanical boss.</summary>
	public class MarkedInvitation : ModItem
	{
		public override void SetStaticDefaults() => Item.ResearchUnlockCount = 3;

		public override void SetDefaults()
		{
			Item.width = 28;
			Item.height = 32;
			Item.maxStack = 20;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = Item.useAnimation = 30;
			Item.consumable = true;
			Item.rare = ItemRarityID.Pink;
			Item.value = Item.sellPrice(gold: 1);
		}

		public override bool CanUseItem(Player player) => Common.Systems.HighStakesNight.CanStart;

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer)
				return true;
			SoundEngine.PlaySound(Common.CasinoSounds.HuntHorn, player.position);
			if (Main.netMode == NetmodeID.SinglePlayer)
				Common.Systems.HighStakesNight.Start();
			else {
				ModPacket packet = Mod.GetPacket();
				packet.Write((byte)Common.Net.CasinoMessage.StartRaid);
				packet.Send();
			}
			return true;
		}

		public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.SoulofNight, 5).AddIngredient(ItemID.Silk, 5).AddIngredient(ModContent.ItemType<LoyaltyToken>(), 30).AddTile(TileID.MythrilAnvil).Register();
	}
}

namespace CasinoWorld.Content.NPCs.Enemies
{
	public class DoubleRaidCondition : IItemDropRuleCondition
	{
		public bool CanDrop(DropAttemptInfo info) => Common.Systems.HighStakesNight.Kind == Common.Systems.RaidKind.DoubleOrNothing && !info.IsInSimulation;

		public bool CanShowItemDropInUI() => false;

		public string GetConditionDescription() => null;
	}

	/// <summary>Captain of the card guard: a giant Royal Guard that ends High Stakes Night.</summary>
	public class GuardCaptain : RoyalGuard
	{
		public override string Texture => "CasinoWorld/Content/NPCs/Enemies/RoyalGuard";

		public override void SetDefaults()
		{
			base.SetDefaults();
			NPC.lifeMax = 4500;
			NPC.damage = 85;
			NPC.defense = 38;
			NPC.scale = 1.5f;
			NPC.width = 44;
			NPC.height = 76;
			NPC.knockBackResist = 0f;
			NPC.value = Item.buyPrice(gold: 5);
			NPC.npcSlots = 5f;
			Banner = ModContent.NPCType<RoyalGuard>();
		}

		public override float SpawnChance(NPCSpawnInfo spawnInfo) => 0f;

		public override void SetBestiary(Terraria.GameContent.Bestiary.BestiaryDatabase database, Terraria.GameContent.Bestiary.BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.Add(new Terraria.GameContent.Bestiary.FlavorTextBestiaryInfoElement("Mods.CasinoWorld.Bestiary.GuardCaptain"));
		}

		public override void AI()
		{
			Lighting.AddLight(NPC.Center, 0.5f, 0.4f, 0.2f);
			if (Main.rand.NextBool(4))
				CasinoFx.Spark(NPC.Center + Main.rand.NextVector2Circular(24, 38), new Vector2(0, -1), new Color(255, 210, 90), 0.8f);
		}

		public override void OnKill()
		{
			if (Common.Systems.HighStakesNight.Active)
				Common.Systems.HighStakesNight.End(true);
		}

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			// Double or Nothing pays the crates and tokens twice.
			var twice = new LeadingConditionRule(new DoubleRaidCondition());
			twice.OnSuccess(ItemDropRule.Common(ModContent.ItemType<HighRollerCrate>(), 1, 2, 3));
			twice.OnSuccess(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 1, 60, 90));
			npcLoot.Add(twice);
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<HighRollerCrate>(), 1, 2, 3));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 1, 60, 90));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.Weapons.RoyalFlush>(), 3));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.PitBoss.VaultKey>(), 2));
		}
	}
}
