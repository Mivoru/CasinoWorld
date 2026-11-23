using System.Collections.Generic;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.Vanity;
using CasinoWorld.Content.NPCs.Boss;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Systems
{
	/// <summary>Optional integrations with other mods (Boss Checklist).</summary>
	public partial class CrossMod : ModSystem
	{
		public override void PostSetupContent()
		{
			if (!ModLoader.TryGetMod("BossChecklist", out Mod checklist) || checklist.Version < new System.Version(1, 6))
				return;

			// The Wild Card: after Skeletron, before the Wall of Flesh.
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
			PitBossChecklist(checklist);
		}

		/// <summary>Registers the hardmode boss (filled in by the Pit Boss file).</summary>
		partial void PitBossChecklistImpl(Mod checklist);

		private void PitBossChecklist(Mod checklist) => PitBossChecklistImpl(checklist);
	}
}
