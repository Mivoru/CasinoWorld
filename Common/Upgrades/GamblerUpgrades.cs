using System.Collections.Generic;
using System.IO;
using CasinoWorld.Common.Gambler;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CasinoWorld.Common.Upgrades
{
	/// <summary>Marks Gambler weapons and armour that the upgrade machine accepts.</summary>
	public interface IGamblerUpgradeable { }

	public readonly record struct UpgradeTier(int[] Materials, int Count, float Chance);

	/// <summary>
	/// Upgrade rules. A failed attempt only costs the materials and the fee; the item never loses a level.
	/// Weapons gain 6 % damage per level (+60 % at +10). Armour gains 1 defence per level and
	/// 1 Luck per 2 levels. Boost doubles the materials for +15 % chance.
	/// Expected materials to reach +10 without boost: sum of Count / Chance over the tiers.
	/// </summary>
	public static class UpgradeRules
	{
		public const int MaxLevel = 10;
		public const float DamagePerLevel = 0.06f;
		public const float BoostChance = 0.15f;

		private static UpgradeTier[] tiers;

		public static void Unload() => tiers = null;

		/// <summary>Tier used to go from <paramref name="level"/> to level + 1.</summary>
		public static UpgradeTier TierFor(int level) => (tiers ??= Build())[level];

		private static UpgradeTier[] Build()
		{
			int[] t1 = { ItemID.CobaltBar, ItemID.PalladiumBar };
			int[] t2 = { ItemID.MythrilBar, ItemID.OrichalcumBar };
			int[] t3 = { ItemID.AdamantiteBar, ItemID.TitaniumBar };
			int[] t4 = { ItemID.HallowedBar };
			int[] t5 = { ItemID.ChlorophyteBar };
			int[] t6 = { ItemID.LunarBar };
			return new[] {
				new UpgradeTier(t1, 5, 0.90f), new UpgradeTier(t1, 5, 0.80f),
				new UpgradeTier(t2, 5, 0.70f), new UpgradeTier(t2, 5, 0.60f),
				new UpgradeTier(t3, 5, 0.50f), new UpgradeTier(t3, 5, 0.45f),
				new UpgradeTier(t4, 5, 0.40f), new UpgradeTier(t4, 5, 0.35f),
				new UpgradeTier(t5, 8, 0.30f), new UpgradeTier(t6, 5, 0.25f),
			};
		}

		/// <summary>Coin fee per attempt, in copper: (level + 1) * 2 gold.</summary>
		public static long Fee(int level) => (level + 1) * 2 * Games.Coins.Gold;
	}

	/// <summary>Stores the upgrade level on each Gambler item and applies its bonuses.</summary>
	public class UpgradeGlobalItem : GlobalItem
	{
		public int Level;

		public override bool InstancePerEntity => true;

		public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.ModItem is IGamblerUpgradeable;

		public static int LevelOf(Item item) => item.TryGetGlobalItem(out UpgradeGlobalItem g) ? g.Level : 0;

		public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
		{
			if (Level > 0)
				damage += UpgradeRules.DamagePerLevel * Level;
		}

		public override void UpdateEquip(Item item, Player player)
		{
			if (Level <= 0 || item.damage > 0)
				return;
			player.statDefense += Level;
			player.GetModPlayer<GamblerPlayer>().Luck += Level / 2;
		}

		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			if (Level <= 0)
				return;

			TooltipLine name = tooltips.Find(line => line.Mod == "Terraria" && line.Name == "ItemName");
			if (name != null) {
				name.Text += " +" + Level;
				name.OverrideColor = Color.Lerp(new Color(120, 220, 255), new Color(255, 215, 60), Level / (float)UpgradeRules.MaxLevel);
			}

			string text = item.damage > 0
				? Language.GetTextValue("Mods.CasinoWorld.Upgrade.WeaponBonus", Level, (int)(UpgradeRules.DamagePerLevel * Level * 100))
				: Language.GetTextValue("Mods.CasinoWorld.Upgrade.ArmorBonus", Level, Level, Level / 2);
			tooltips.Add(new TooltipLine(Mod, "CasinoUpgrade", text) { OverrideColor = new Color(120, 220, 255) });
		}

		public override void SaveData(Item item, TagCompound tag)
		{
			if (Level > 0)
				tag["upgradeLevel"] = Level;
		}

		public override void LoadData(Item item, TagCompound tag)
		{
			Level = tag.ContainsKey("upgradeLevel") ? tag.GetInt("upgradeLevel") : 0;
		}

		public override void NetSend(Item item, BinaryWriter writer) => writer.Write((byte)Level);

		public override void NetReceive(Item item, BinaryReader reader) => Level = reader.ReadByte();
	}
}
