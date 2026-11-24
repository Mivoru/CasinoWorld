using System.Collections.Generic;
using CasinoWorld.Common.Effects;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.Armor;
using CasinoWorld.Content.Items.PitBoss;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Upgrades
{
	/// <summary>What an armour piece turns into at the upgrade machine. Bars: any of the listed types.</summary>
	public readonly record struct Evolution(int Result, int[] Bars, int BarCount, int Extra, int ExtraCount, int Tokens);

	/// <summary>
	/// Armour tiers at the upgrade machine: a piece turns into the same piece of the next set for about two
	/// thirds of the crafting cost and keeps half of its upgrade levels. It never fails.
	/// </summary>
	public static class ArmorEvolution
	{
		private static Dictionary<int, Evolution> table;

		public static void Unload() => table = null;

		public static bool TryGet(Item item, out Evolution evolution)
		{
			evolution = default;
			return !item.IsAir && (table ??= Build()).TryGetValue(item.type, out evolution);
		}

		private static Dictionary<int, Evolution> Build()
		{
			int[] adamantite = { ItemID.AdamantiteBar, ItemID.TitaniumBar }, hallowed = { ItemID.HallowedBar }, spectre = { ItemID.SpectreBar }, luminite = { ItemID.LunarBar };
			int[][] sets = {
				new[] { ModContent.ItemType<GamblerFedora>(), ModContent.ItemType<GamblerVest>(), ModContent.ItemType<GamblerShoes>() },
				new[] { ModContent.ItemType<HighRollerTopHat>(), ModContent.ItemType<HighRollerTailcoat>(), ModContent.ItemType<HighRollerTrousers>() },
				new[] { ModContent.ItemType<PitBossFedora>(), ModContent.ItemType<PitBossSuit>(), ModContent.ItemType<PitBossShoes>() },
				new[] { ModContent.ItemType<SpectralFedora>(), ModContent.ItemType<SpectralCoat>(), ModContent.ItemType<SpectralLoafers>() },
				new[] { ModContent.ItemType<CelestialFedora>(), ModContent.ItemType<CelestialCoat>(), ModContent.ItemType<CelestialLoafers>() },
			};
			int[][] bars = { adamantite, hallowed, spectre, luminite };
			int[,] counts = { { 8, 13, 10 }, { 7, 11, 8 }, { 8, 12, 9 }, { 6, 11, 8 } };
			int[,] bullion = { { 0, 0, 0 }, { 4, 6, 4 }, { 0, 0, 0 }, { 0, 0, 0 } };
			int[,] tokens = { { 40, 60, 40 }, { 50, 80, 50 }, { 70, 100, 70 }, { 130, 200, 130 } };
			var result = new Dictionary<int, Evolution>();
			for (int set = 0; set < 4; set++)
				for (int piece = 0; piece < 3; piece++)
					result[sets[set][piece]] = new Evolution(sets[set + 1][piece], bars[set], counts[set, piece], ModContent.ItemType<VaultBullion>(), bullion[set, piece], tokens[set, piece]);
			return result;
		}

		public static bool CanAfford(Player player, Evolution e)
			=> UpgradeSystem.CountMaterials(player, e.Bars) >= e.BarCount
				&& UpgradeSystem.CountMaterials(player, new[] { e.Extra }) >= e.ExtraCount
				&& UpgradeSystem.CountMaterials(player, new[] { ModContent.ItemType<LoyaltyToken>() }) >= e.Tokens;

		public static void Pay(Player player, Evolution e)
		{
			Take(player, e.Bars, e.BarCount);
			Take(player, new[] { e.Extra }, e.ExtraCount);
			Take(player, new[] { ModContent.ItemType<LoyaltyToken>() }, e.Tokens);
		}

		public static void Take(Player player, int[] types, int left)
		{
			for (int k = 0; k < 58 && left > 0; k++) {
				Item item = player.inventory[k];
				if (item.IsAir || System.Array.IndexOf(types, item.type) < 0)
					continue;
				int take = System.Math.Min(left, item.stack);
				item.stack -= take;
				left -= take;
				if (item.stack <= 0)
					item.TurnToAir();
			}
		}
	}

	/// <summary>Worn gear shows its upgrades: sparks from +5 on all three pieces, a golden aura at +10.</summary>
	public class UpgradeGlowPlayer : ModPlayer
	{
		public override void PostUpdate()
		{
			if (Main.dedServ || Player.dead || Player.invis)
				return;
			int level = UpgradeRules.MaxLevel;
			for (int k = 0; k < 3; k++)
				level = System.Math.Min(level, UpgradeGlobalItem.LevelOf(Player.armor[k]));
			if (level < 5)
				return;
			bool max = level >= UpgradeRules.MaxLevel;
			Color color = max ? new Color(255, 215, 80) : new Color(120, 220, 255);
			Lighting.AddLight(Player.Center, color.ToVector3() * (max ? 0.5f : 0.25f));
			if (Main.rand.NextBool(max ? 4 : 10))
				CasinoFx.Spark(Player.position + new Vector2(Main.rand.Next(Player.width), Main.rand.Next(Player.height)), new Vector2(0, -0.8f), color, max ? 0.9f : 0.7f);
		}
	}
}
