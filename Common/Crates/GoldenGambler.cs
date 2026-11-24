using System.Collections.Generic;
using System.IO;
using CasinoWorld.Common.Upgrades;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CasinoWorld.Common.Crates
{
	/// <summary>
	/// Golden variant of a Gambler weapon, only from the Gold rarity of crates: +10 % damage,
	/// a gold tint and a gold "Golden" name. Stacks with prefixes and with upgrade levels.
	/// </summary>
	public class GoldenGambler : GlobalItem
	{
		public const float DamageBonus = 0.10f;
		private static readonly Color Gold = new(255, 205, 40);

		public bool Golden;

		public override bool InstancePerEntity => true;

		public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.ModItem is IGamblerUpgradeable && entity.damage > 0;

		public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
		{
			if (Golden)
				damage += DamageBonus;
		}

		public override Color? GetAlpha(Item item, Color lightColor)
		{
			if (!Golden)
				return null;
			float shine = 0.55f + 0.15f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 3f);
			Color tinted = Color.Lerp(lightColor, Gold, shine);
			tinted.A = lightColor.A;
			return tinted;
		}

		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			if (!Golden)
				return;
			TooltipLine name = tooltips.Find(line => line.Mod == "Terraria" && line.Name == "ItemName");
			if (name != null) {
				name.Text = Language.GetTextValue("Mods.CasinoWorld.Crates.GoldenPrefix") + " " + name.Text;
				name.OverrideColor = Gold;
			}
			tooltips.Add(new TooltipLine(Mod, "CasinoGolden", Language.GetTextValue("Mods.CasinoWorld.Crates.GoldenBonus", (int)(DamageBonus * 100))) { OverrideColor = Gold });
		}

		public override void SaveData(Item item, TagCompound tag)
		{
			if (Golden)
				tag["golden"] = true;
		}

		public override void LoadData(Item item, TagCompound tag) => Golden = tag.ContainsKey("golden");

		public override void NetSend(Item item, BinaryWriter writer) => writer.Write(Golden);

		public override void NetReceive(Item item, BinaryReader reader) => Golden = reader.ReadBoolean();
	}

	/// <summary>Per-player pity counters for crates (saved with the character).</summary>
	public class CratePity : ModPlayer
	{
		public int SinceRare;
		public int SinceRed;
		public int SinceGold;

		public override void SaveData(TagCompound tag)
		{
			tag["sinceRare"] = SinceRare;
			tag["sinceRed"] = SinceRed;
			tag["sinceGold"] = SinceGold;
		}

		public override void LoadData(TagCompound tag)
		{
			SinceRare = tag.ContainsKey("sinceRare") ? tag.GetInt("sinceRare") : 0;
			SinceRed = tag.ContainsKey("sinceRed") ? tag.GetInt("sinceRed") : 0;
			SinceGold = tag.ContainsKey("sinceGold") ? tag.GetInt("sinceGold") : 0;
		}
	}
}
