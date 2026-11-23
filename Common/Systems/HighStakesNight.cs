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
	public class HighStakesNight : ModSystem
	{
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
		}

		public override void OnWorldLoad() => wasDay = Main.dayTime;

		public override void SaveWorldData(Terraria.ModLoader.IO.TagCompound tag)
		{
			if (!Active)
				return;
			tag["hsnActive"] = true;
			tag["hsnKills"] = Kills;
			tag["hsnCaptain"] = CaptainSpawned;
		}

		public override void LoadWorldData(Terraria.ModLoader.IO.TagCompound tag)
		{
			Active = tag.ContainsKey("hsnActive");
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
			if (Main.netMode == NetmodeID.MultiplayerClient || CasinoTravel.InCasino)
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
			Announce("Mods.CasinoWorld.Event.Start", new Color(255, 80, 90));
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
			writer.Write(Kills);
			writer.Write(CaptainSpawned);
		}

		public override void NetReceive(BinaryReader reader)
		{
			Active = reader.ReadBoolean();
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
			sb.Draw(px, new Rectangle(r.X, r.Y, (int)(r.Width * p), r.Height), Color.Lerp(new Color(200, 30, 50), new Color(255, 120, 60), 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4)));
			string text = CaptainSpawned ? Language.GetTextValue("Mods.CasinoWorld.Event.BarCaptain") : Language.GetTextValue("Mods.CasinoWorld.Event.Bar", (int)(p * 100));
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
			pool[ModContent.NPCType<SlotMimic>()] = 0.08f;
		}

		public override void OnKill(NPC npc) => HighStakesNight.CountKill(npc);

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
		}
	}
}

namespace CasinoWorld.Content.NPCs.Enemies
{
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
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<HighRollerCrate>(), 1, 2, 3));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 1, 60, 90));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.Weapons.RoyalFlush>(), 3));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.PitBoss.VaultKey>(), 2));
		}
	}
}
