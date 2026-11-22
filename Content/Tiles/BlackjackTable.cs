using CasinoWorld.Common.Games.Blackjack;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Tiles
{
	public class BlackjackTable : CasinoTable
	{
		protected override string HoverKey => "Mods.CasinoWorld.UI.Blackjack.Hover";

		protected override Color MapColor => new(14, 80, 40);

		protected override void Use(Point16 topLeft) => ModContent.GetInstance<BlackjackSystem>().Open(topLeft);
	}
}
