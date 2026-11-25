using CasinoWorld.Common.Games;
using CasinoWorld.Common.Games.Poker;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Tiles
{
	/// <summary>Texas Hold'em table.</summary>
	public class PokerTableTile : CasinoTable
	{
		protected override string HoverKey => "Mods.CasinoWorld.UI.Poker.Hover";

		protected override Color MapColor => new(128, 26, 52);

		protected override void Use(Point16 topLeft) => ModContent.GetInstance<PokerSystem>().Enter(topLeft);
	}

	/// <summary>Coin Duel table.</summary>
	public class CoinDuelTable : CasinoTable
	{
		protected override string HoverKey => "Mods.CasinoWorld.UI.Duel.Hover";

		protected override Color MapColor => new(44, 78, 180);

		protected override void Use(Point16 topLeft) => ModContent.GetInstance<CoinDuelSystem>().Enter(topLeft);
	}
}
