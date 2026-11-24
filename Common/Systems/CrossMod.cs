using System.Collections.Generic;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.PitBoss;
using CasinoWorld.Content.Items.Vanity;
using CasinoWorld.Content.Items.Accessories;
using CasinoWorld.Content.NPCs.Boss;
using CasinoWorld.Content.NPCs.People;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Systems
{
	/// <summary>Optional integrations with other mods (Boss Checklist, Census).</summary>
	public class CrossMod : ModSystem
	{
		public override void PostSetupContent()
		{
			JoinEmblemShimmer();

			if (ModLoader.TryGetMod("Census", out Mod census)) {
				foreach (int type in new[] { ModContent.NPCType<TownGambler>(), ModContent.NPCType<Bookie>(), ModContent.NPCType<Magician>(), ModContent.NPCType<Bouncer>() })
					census.Call("TownNPCCondition", type, Mod.GetLocalization("Census." + ModContent.GetModNPC(type).Name));
			}

			if (ModLoader.TryGetMod("BossChecklist", out Mod checklist))
				LogBosses(checklist);
		}

		/// <summary>
		/// Joins the emblem shimmer cycle after Summoner. Runs after every mod's SetStaticDefaults and keeps
		/// the previous target, so emblems of other mods in the cycle stay reachable.
		/// </summary>
		private static void JoinEmblemShimmer()
		{
			int emblem = ModContent.ItemType<GamblerEmblem>();
			int next = ItemID.Sets.ShimmerTransformToItem[ItemID.SummonerEmblem];
			ItemID.Sets.ShimmerTransformToItem[emblem] = next > 0 ? next : ItemID.WarriorEmblem;
			ItemID.Sets.ShimmerTransformToItem[ItemID.SummonerEmblem] = emblem;
		}

		private void LogBosses(Mod checklist)
		{
			// The Wild Card: after Skeletron (5.0), before the Wall of Flesh (6.0).
			checklist.Call("LogBoss", Mod, nameof(TheWildCard), 5.5f, () => CasinoDowned.DownedWildCard, ModContent.NPCType<TheWildCard>(),
				new Dictionary<string, object> {
					["spawnItems"] = ModContent.ItemType<GoldenChip>(),
					["collectibles"] = new List<int> {
						ModContent.ItemType<WildCardRelicItem>(), ModContent.ItemType<MiniSlotItem>(),
						ModContent.ItemType<WildCardTrophy>(), ModContent.ItemType<WildCardMask>(),
					},
					["spawnInfo"] = Language.GetText("Mods.CasinoWorld.BossChecklist.WildCardSpawn"),
					["despawnMessage"] = Language.GetText("Mods.CasinoWorld.Boss.WildCard.Leave"),
				});

			// The Pit Boss: after the mechanical bosses (6-8), before Plantera (12).
			checklist.Call("LogBoss", Mod, nameof(ThePitBoss), 9.5f, () => CasinoDowned.DownedPitBoss, ModContent.NPCType<ThePitBoss>(),
				new Dictionary<string, object> {
					["spawnItems"] = ModContent.ItemType<VaultKey>(),
					["collectibles"] = new List<int> {
						ModContent.ItemType<PitBossRelicItem>(), ModContent.ItemType<PitBossTrophy>(), ModContent.ItemType<PitBossMask>(),
					},
					["spawnInfo"] = Language.GetText("Mods.CasinoWorld.BossChecklist.PitBossSpawn"),
					["despawnMessage"] = Language.GetText("Mods.CasinoWorld.Boss.PitBoss.Leave"),
				});
		}
	}
}
