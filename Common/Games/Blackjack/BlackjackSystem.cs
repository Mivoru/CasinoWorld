using CasinoWorld.Content.Tiles;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Games.Blackjack
{
	public class BlackjackSystem : CasinoTableGameSystem
	{
		public BlackjackRound Round { get; } = new();

		public override long Owed => Round.Owed;

		protected override int TableTileType => ModContent.TileType<BlackjackTable>();

		protected override UIState CreateState() => new BlackjackUIState();

		protected override void UpdateGame(Player player) => Round.Update();

		protected override void OnClose(Player player) => Round.FinishImmediately();
	}
}
