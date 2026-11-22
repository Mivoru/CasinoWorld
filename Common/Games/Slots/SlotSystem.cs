using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Games.Slots
{
	public class SlotSystem : CasinoTableGameSystem
	{
		public SlotRound Round { get; } = new();

		protected override int TableTileType => ModContent.TileType<SlotMachine>();

		protected override Point TableSize => new(SlotMachine.Width, SlotMachine.Height);

		protected override UIState CreateState() => new SlotUIState();

		protected override void UpdateGame(Player player) => Round.Update(player);

		protected override void OnClose(Player player) => Round.FinishImmediately(player);
	}
}
