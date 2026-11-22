using CasinoWorld.Common.Games.Roulette;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Tiles
{
	public class RouletteTable : CasinoTable
	{
		protected override string HoverKey => "Mods.CasinoWorld.UI.Roulette.Hover";

		protected override Color MapColor => new(24, 120, 60);

		protected override void Use(Point16 topLeft) => ModContent.GetInstance<RouletteSystem>().Open(topLeft);
	}
}
