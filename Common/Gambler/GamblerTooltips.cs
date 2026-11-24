using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Gambler
{
	/// <summary>
	/// Gambler weapons never crit, so their tooltip shows the odds instead of a critical strike line:
	/// jackpot and bust chance for the local player with this weapon.
	/// </summary>
	public class GamblerTooltips : GlobalItem
	{
		public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.damage > 0;

		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			if (!item.CountsAsClass<GamblerDamageClass>())
				return;
			int index = tooltips.FindIndex(l => l.Mod == "Terraria" && l.Name == "CritChance");
			Player player = Main.LocalPlayer;
			GamblerPlayer g = player.GetModPlayer<GamblerPlayer>();
			float jackpot = g.JackpotChance + Content.Prefixes.GamblerPrefix.JackpotOf(item) + item.crit * 0.005f;
			string text = Language.GetTextValue("Mods.CasinoWorld.Gambler.TooltipOdds", (int)System.Math.Round(jackpot * 100), (int)System.Math.Round(g.BustChance * 100));
			var line = new TooltipLine(Mod, "GamblerOdds", text) { OverrideColor = new Color(255, 215, 90) };
			if (index >= 0)
				tooltips[index] = line;
			else {
				int dmg = tooltips.FindIndex(l => l.Mod == "Terraria" && l.Name == "Damage");
				tooltips.Insert(dmg >= 0 ? dmg + 1 : tooltips.Count, line);
			}
			string key = GamblerPot.KeyName;
			tooltips.Add(new TooltipLine(Mod, "GamblerAllIn", key == null ? Language.GetTextValue("Mods.CasinoWorld.Gambler.AllInUnbound") : Language.GetTextValue("Mods.CasinoWorld.Gambler.TooltipAllIn", key)) {
				OverrideColor = key == null ? new Color(255, 110, 100) : new Color(200, 180, 140)
			});
		}
	}
}
