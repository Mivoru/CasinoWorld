using System.Collections.Generic;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.PitBoss;
using CasinoWorld.Content.Items.Vanity;
using CasinoWorld.Content.NPCs.Boss;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Systems
{
	/// <summary>Optional integrations with other mods (Boss Checklist).</summary>
	public class CrossMod : ModSystem
	{
		public override void PostSetupContent()
		{
			if (!ModLoader.TryGetMod("BossChecklist", out Mod checklist))
				return;

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
