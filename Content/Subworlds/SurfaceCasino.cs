using System.Collections.Generic;
using CasinoWorld.Content.Items.Weapons;
using CasinoWorld.Content.NPCs;
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

		public override int[] JackpotItems => new[] {
			ModContent.ItemType<LoadedDice>(), ModContent.ItemType<CardDeck>(), ModContent.ItemType<HighRoller>(),
		};

		// Furniture positions, as tile offsets from HallLeft.
		public const int RouletteA = 16, RouletteB = 28, BlackjackA = 46, BlackjackB = 58;
		public const int BarStart = 84, BarEnd = 100, PrizeCounter = 74;
		private const int GuestCount = 6;

		/// <summary>Tile x positions guests walk to: beside the tables and at the bar.</summary>
		public static readonly int[] GuestSpots = {
			HallLeft + RouletteA + 2, HallLeft + RouletteA + 4, HallLeft + RouletteB + 2, HallLeft + RouletteB + 4,
			HallLeft + BlackjackA + 2, HallLeft + BlackjackA + 4, HallLeft + BlackjackB + 2, HallLeft + BlackjackB + 4,
			HallLeft + BarStart + 1, HallLeft + BarStart + 6, HallLeft + BarStart + 11, HallLeft + PrizeCounter - 4,
		};

		public override List<GenPass> Tasks => new() { new SurfaceCasinoPass() };

		protected override void SpawnPeople()
		{
			// Staff stand at the right end of each table, facing it.
			SpawnAt<RouletteCroupier>(HallLeft + RouletteA + 7, HallFloor, -1);
			SpawnAt<RouletteCroupier>(HallLeft + RouletteB + 7, HallFloor, -1);
			SpawnAt<BlackjackDealer>(HallLeft + BlackjackA + 7, HallFloor, -1);
			SpawnAt<BlackjackDealer>(HallLeft + BlackjackB + 7, HallFloor, -1);
			SpawnAt<Bartender>(HallLeft + BarEnd + 3, HallFloor, -1);
			SpawnAt<PrizeCashier>(HallLeft + PrizeCounter + 1, HallFloor, -1);

			for (int k = 0; k < GuestCount; k++)
				SpawnAt<CasinoGuest>(GuestSpots[Main.rand.Next(GuestSpots.Length)], HallFloor);
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
			WorldGen.PlaceObject(left + SurfaceCasino.RouletteA, floor - 1, ModContent.TileType<RouletteTable>(), mute: true);
			WorldGen.PlaceObject(left + SurfaceCasino.RouletteB, floor - 1, ModContent.TileType<RouletteTable>(), mute: true);

			// Blackjack area.
			WorldGen.PlaceObject(left + SurfaceCasino.BlackjackA, floor - 1, ModContent.TileType<BlackjackTable>(), mute: true);
			WorldGen.PlaceObject(left + SurfaceCasino.BlackjackB, floor - 1, ModContent.TileType<BlackjackTable>(), mute: true);

			// Bar corner: counter of tables with drinks, kegs behind it.
			for (int x = left + SurfaceCasino.BarStart; x <= left + SurfaceCasino.BarEnd; x += 4) {
				WorldGen.PlaceObject(x, floor - 1, TileID.Tables, mute: true, style: 0);
				WorldGen.PlaceTile(x, floor - 3, TileID.Bottles, mute: true, forced: true, style: 4);
			}
			// Prize counter: a display table with the cashier behind it.
			WorldGen.PlaceObject(left + SurfaceCasino.PrizeCounter - 1, floor - 1, TileID.Tables, mute: true, style: 0);
			WorldGen.PlaceTile(left + SurfaceCasino.PrizeCounter - 1, floor - 3, TileID.GoldCoinPile, mute: true, forced: true);

			WorldGen.PlaceObject(left + SurfaceCasino.BarEnd + 6, floor - 1, TileID.Kegs, mute: true);
			WorldGen.PlaceObject(left + SurfaceCasino.BarEnd + 10, floor - 1, TileID.Kegs, mute: true);

			// Pots of plants at the ends of the hall.
			WorldGen.PlaceObject(left + 12, floor - 1, TileID.PottedPlants1, mute: true, style: 0);
			WorldGen.PlaceObject(left + 114, floor - 1, TileID.PottedPlants1, mute: true, style: 1);
		}
	}
}
