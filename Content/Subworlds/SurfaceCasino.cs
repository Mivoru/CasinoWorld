using System.Collections.Generic;
using CasinoWorld.Content.Items.Accessories;
using CasinoWorld.Content.Items.Armor;
using CasinoWorld.Content.Items.Weapons;
using CasinoWorld.Content.NPCs;
using CasinoWorld.Content.Tiles;
using CasinoWorld.Content.Walls;
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
			ModContent.ItemType<GamblerFedora>(), ModContent.ItemType<GamblerVest>(), ModContent.ItemType<GamblerShoes>(),
			ModContent.ItemType<FourLeafClover>(), ModContent.ItemType<AceUpTheSleeve>(),
		};

		// Furniture positions, as tile offsets from HallLeft.
		public const int RouletteA = 16, RouletteB = 28, BlackjackA = 46, BlackjackB = 58;
		public const int BarStart = 84, BarEnd = 100, PrizeCounter = 74, SlotA = 66;
		private const int GuestCount = 6;

		/// <summary>Tile x positions guests walk to: beside the tables and at the bar.</summary>
		public override int[] GuestSpots => Spots;

		private static readonly int[] Spots = {
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
				SpawnAt<CasinoGuest>(Spots[Main.rand.Next(Spots.Length)], HallFloor);
		}
	}

	/// <summary>
	/// Builds the hall from vanilla blocks and furniture, so it matches the look of the rest of
	/// Terraria: marble floor, glowing amber ceiling, wood panelling, marble columns, red banners,
	/// paintings, a big lit CASINO marquee sign, and the game tables.
	/// </summary>
	public class SurfaceCasinoPass : GenPass
	{
		private const int Left = SurfaceCasino.HallLeft;
		private const int Right = SurfaceCasino.HallRight;
		private const int Top = SurfaceCasino.HallTop;
		private const int Floor = SurfaceCasino.HallFloor;

		// Columns (2 tiles wide, background walls) split the hall into areas.
		private static readonly int[] Columns = { Left + 11, Left + 38, Left + 70, Left + 81, Left + 104 };

		public SurfaceCasinoPass() : base("CasinoHall", 1) { }

		protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
		{
			progress.Message = Language.GetTextValue("Mods.CasinoWorld.Subworlds.Generating");

			Main.worldSurface = 20;
			Main.rockLayer = 25;
			Main.spawnTileX = Left + 8;
			Main.spawnTileY = Floor - 1;

			BuildShell();
			PaintWalls();
			WorldGen.RangeFrame(0, 0, Main.maxTilesX - 1, Main.maxTilesY - 1);
			CasinoSignTile.Place((Left + Right) / 2 - CasinoSignTile.Width / 2, Top + 3);
			Furnish();
		}

		private static void BuildShell()
		{
			for (int x = 0; x < Main.maxTilesX; x++) {
				for (int y = 0; y < Main.maxTilesY; y++) {
					Tile tile = Main.tile[x, y];
					bool inHall = x > Left && x < Right && y > Top && y < Floor;
					if (inHall)
						continue;

					tile.HasTile = true;
					if (y == Floor)
						tile.TileType = (ushort)ModContent.TileType<CasinoCarpet>();
					else if (y == Floor + 1)
						tile.TileType = TileID.GoldBrick;
					else if (y == Top && x > Left && x < Right)
						tile.TileType = (x - Left) % 3 == 0 ? TileID.AmberGemspark : TileID.GoldBrick;
					else if (y == Top - 1)
						tile.TileType = TileID.GoldBrick;
					else
						tile.TileType = TileID.ObsidianBrick;
				}
			}
		}

		private static void PaintWalls()
		{
			for (int x = Left; x <= Right; x++) {
				for (int y = Top; y <= Floor; y++) {
					Tile tile = Main.tile[x, y];
					if (y >= Floor - 4)
						tile.WallType = (ushort)ModContent.WallType<CasinoPanelWall>();
					else if (y == Floor - 5 || y == Top + 1)
						tile.WallType = WallID.GoldBrick;        // trim
					else
						tile.WallType = (ushort)ModContent.WallType<CasinoWallpaper>();
				}
			}

			foreach (int cx in Columns) {
				for (int x = cx; x < cx + 2; x++) {
					for (int y = Top + 1; y < Floor; y++) {
						bool cap = y <= Top + 2 || y == Floor - 5 || y == Floor - 1;
						Main.tile[x, y].WallType = cap ? WallID.GoldBrick : WallID.MarbleBlock;
					}
				}
			}
		}

		private static void Furnish()
		{
			// Gold chandeliers along the ceiling, skipping the sign.
			int signLeft = (Left + Right) / 2 - CasinoSignTile.Width / 2 - 2, signRight = (Left + Right) / 2 + CasinoSignTile.Width / 2 + 2;
			for (int x = Left + 6; x < Right - 4; x += 12) {
				if (x + 2 < signLeft || x > signRight)
					WorldGen.PlaceTile(x, Top + 1, TileID.Chandeliers, mute: true, forced: true, style: 2);
			}

			// Red banners on the columns (not in front of the neon sign).
			foreach (int cx in Columns) {
				if (cx + 1 >= signLeft && cx <= signRight)
					continue;
				WorldGen.PlaceTile(cx, Top + 3, TileID.Banners, mute: true, forced: true, style: 0);
				WorldGen.PlaceTile(cx + 1, Top + 3, TileID.Banners, mute: true, forced: true, style: 0);
			}

			// Paintings between the columns, above the panelling.
			int[] paintingStyles = { 0, 4, 9, 13, 17, 21 };
			int painting = 0;
			for (int k = 0; k < Columns.Length - 1; k++) {
				int mid = (Columns[k] + Columns[k + 1]) / 2;
				if (Columns[k + 1] - Columns[k] < 8 || (mid > signLeft && mid < signRight))
					continue;
				WorldGen.PlaceObject(mid, Floor - 9, TileID.Painting3X3, mute: true, style: paintingStyles[painting++ % paintingStyles.Length]);
			}

			// Wall torches on the columns' lower trim.
			foreach (int cx in Columns) {
				WorldGen.PlaceTile(cx - 1, Floor - 7, TileID.Torches, mute: true, forced: true);
				WorldGen.PlaceTile(cx + 2, Floor - 7, TileID.Torches, mute: true, forced: true);
			}

			// Exit door next to the spawn point.
			WorldGen.PlaceObject(Left + 3, Floor - 1, ModContent.TileType<CasinoDoor>(), mute: true);

			// Game tables.
			WorldGen.PlaceObject(Left + SurfaceCasino.RouletteA, Floor - 1, ModContent.TileType<RouletteTable>(), mute: true);
			WorldGen.PlaceObject(Left + SurfaceCasino.RouletteB, Floor - 1, ModContent.TileType<RouletteTable>(), mute: true);
			WorldGen.PlaceObject(Left + SurfaceCasino.BlackjackA, Floor - 1, ModContent.TileType<BlackjackTable>(), mute: true);
			WorldGen.PlaceObject(Left + SurfaceCasino.BlackjackB, Floor - 1, ModContent.TileType<BlackjackTable>(), mute: true);

			// Candelabras on the bar counter and drinks between them.
			for (int x = Left + SurfaceCasino.BarStart; x <= Left + SurfaceCasino.BarEnd; x += 4) {
				WorldGen.PlaceObject(x, Floor - 1, TileID.Tables, mute: true, style: 0);
				WorldGen.PlaceTile(x, Floor - 3, (x / 4) % 2 == 0 ? TileID.Bottles : TileID.Candles, mute: true, forced: true, style: 4);
			}
			WorldGen.PlaceObject(Left + SurfaceCasino.BarEnd + 5, Floor - 1, TileID.Kegs, mute: true);
			WorldGen.PlaceObject(Left + SurfaceCasino.BarEnd + 7, Floor - 1, TileID.Kegs, mute: true);

			// Two slot machines next to the prize counter.
			WorldGen.PlaceObject(Left + SurfaceCasino.SlotA, Floor - 1, ModContent.TileType<SlotMachine>(), mute: true);
			WorldGen.PlaceObject(Left + SurfaceCasino.SlotA + 3, Floor - 1, ModContent.TileType<SlotMachine>(), mute: true);

			// Prize counter with a pile of gold on display.
			WorldGen.PlaceObject(Left + SurfaceCasino.PrizeCounter - 1, Floor - 1, TileID.Tables, mute: true, style: 0);
			WorldGen.PlaceTile(Left + SurfaceCasino.PrizeCounter - 1, Floor - 3, TileID.GoldCoinPile, mute: true, forced: true);

			// Piano corner at the far end.
			WorldGen.PlaceObject(Left + 112, Floor - 1, TileID.Pianos, mute: true, style: 0);

			// Potted plants at both ends and beside the entrance.
			WorldGen.PlaceObject(Left + 8, Floor - 1, TileID.PottedPlants1, mute: true, style: 0);
			WorldGen.PlaceObject(Left + 117, Floor - 1, TileID.PottedPlants1, mute: true, style: 1);
			WorldGen.PlaceObject(Left + 108, Floor - 1, TileID.PottedPlants1, mute: true, style: 2);
		}
	}
}
