using System.Collections.Generic;
using CasinoWorld.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CasinoWorld.Content.Subworlds
{
	/// <summary>
	/// Interior of the pre-hardmode surface casino: one long hall with
	/// roulette tables, blackjack tables and a bar corner.
	/// </summary>
	public class SurfaceCasino : CasinoSubworld
	{
		// Hall bounds in tile coordinates (exclusive inner area).
		public const int HallLeft = 40;
		public const int HallRight = 160;
		public const int HallTop = 46;
		public const int HallFloor = 70;

		public override int Width => 200;
		public override int Height => 120;

		// Pre-hardmode players have little money: 1 silver to 5 gold chips, 20 gold per round.
		public override long[] ChipValues => new long[] { 100, 1_000, 10_000, 50_000 };
		public override long MaxBet => 200_000;

		public override List<GenPass> Tasks => new() { new SurfaceCasinoPass() };

		public override void OnLoad()
		{
			Main.dayTime = true;
			Main.time = 27000;
		}
	}

	public class SurfaceCasinoPass : GenPass
	{
		public SurfaceCasinoPass() : base("CasinoHall", 1) { }

		protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
		{
			progress.Message = Language.GetTextValue("Mods.CasinoWorld.Subworlds.Generating");

			const int left = SurfaceCasino.HallLeft;
			const int right = SurfaceCasino.HallRight;
			const int top = SurfaceCasino.HallTop;
			const int floor = SurfaceCasino.HallFloor;

			Main.worldSurface = 20;
			Main.rockLayer = 25;
			Main.spawnTileX = left + 8;
			Main.spawnTileY = floor - 1;

			for (int x = 0; x < Main.maxTilesX; x++) {
				for (int y = 0; y < Main.maxTilesY; y++) {
					Tile tile = Main.tile[x, y];
					bool inHall = x > left && x < right && y > top && y < floor;

					if (!inHall) {
						tile.HasTile = true;
						tile.TileType = y == floor ? TileID.GoldBrick
							: y == top ? TileID.GoldBrick
							: TileID.ObsidianBrick;
					}

					if (x >= left && x <= right && y >= top && y <= floor) {
						// Wood panelling at the bottom, red wallpaper above, gold trim between them.
						tile.WallType = y >= floor - 4 ? WallID.Wood
							: y == floor - 5 ? WallID.GoldBrick
							: WallID.RedBrick;
					}
				}
			}

			WorldGen.RangeFrame(0, 0, Main.maxTilesX - 1, Main.maxTilesY - 1);

			// Gold chandeliers along the ceiling.
			for (int x = left + 10; x < right - 4; x += 12)
				WorldGen.PlaceTile(x, top + 1, TileID.Chandeliers, mute: true, forced: true, style: 2);

			// Wall torches between the chandeliers.
			for (int x = left + 4; x < right - 2; x += 6)
				WorldGen.PlaceTile(x, floor - 8, TileID.Torches, mute: true, forced: true);

			// Exit door next to the spawn point.
			WorldGen.PlaceObject(left + 3, floor - 1, ModContent.TileType<CasinoDoor>(), mute: true);

			// Roulette area.
			WorldGen.PlaceObject(left + 16, floor - 1, ModContent.TileType<RouletteTable>(), mute: true);
			WorldGen.PlaceObject(left + 28, floor - 1, ModContent.TileType<RouletteTable>(), mute: true);

			// Blackjack area.
			WorldGen.PlaceObject(left + 46, floor - 1, ModContent.TileType<BlackjackTable>(), mute: true);
			WorldGen.PlaceObject(left + 58, floor - 1, ModContent.TileType<BlackjackTable>(), mute: true);

			// Bar corner: counter of tables with drinks, kegs behind it.
			for (int x = left + 84; x <= left + 100; x += 4) {
				WorldGen.PlaceObject(x, floor - 1, TileID.Tables, mute: true, style: 0);
				WorldGen.PlaceTile(x, floor - 3, TileID.Bottles, mute: true, forced: true, style: 4);
			}
			WorldGen.PlaceObject(left + 106, floor - 1, TileID.Kegs, mute: true);
			WorldGen.PlaceObject(left + 110, floor - 1, TileID.Kegs, mute: true);

			// Pots of plants at the ends of the hall.
			WorldGen.PlaceObject(left + 12, floor - 1, TileID.PottedPlants1, mute: true, style: 0);
			WorldGen.PlaceObject(left + 114, floor - 1, TileID.PottedPlants1, mute: true, style: 1);
		}
	}
}
