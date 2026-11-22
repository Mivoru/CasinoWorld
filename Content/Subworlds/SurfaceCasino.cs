using System.Collections.Generic;
using CasinoWorld.Content.Items.Accessories;
using CasinoWorld.Content.Items.Armor;
using CasinoWorld.Content.Items.Weapons;
using CasinoWorld.Content.NPCs;
using CasinoWorld.Content.NPCs.People;
using CasinoWorld.Content.Tiles;
using CasinoWorld.Content.Walls;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CasinoWorld.Content.Subworlds
{
	/// <summary>
	/// Interior of the pre-hardmode surface casino: a two-storey hall.
	/// Ground floor: entrance, roulette and blackjack tables, a fountain under an open atrium,
	/// the prize counter and slot machines. Upper floor: piano lounge, bar, a row of slot machines
	/// and a cabaret stage with a dancer. A grand carpeted staircase along the right wall and an elevator
	/// connect the floors. Above the staircase the upper floor is made of platforms, which players pass
	/// through from below, so the stairs have headroom and lead straight onto the upper floor.
	/// </summary>
	public class SurfaceCasino : CasinoSubworld
	{
		public const int HallLeft = 40;
		public const int HallRight = 180;
		public const int HallTop = 30;     // ceiling row
		public const int UpperFloor = 54;  // floor row of the upper storey
		public const int HallFloor = 78;   // floor row of the ground storey

		public override int Width => 220;
		public override int Height => 130;

		// Pre-hardmode players have little money: 1 silver to 5 gold chips, 20 gold per round.
		public override long[] ChipValues => new long[] { 100, 1_000, 10_000, 50_000 };
		public override long MaxBet => 200_000;

		public override int[] JackpotItems => new[] {
			ModContent.ItemType<LoadedDice>(), ModContent.ItemType<CardDeck>(), ModContent.ItemType<HighRoller>(),
			ModContent.ItemType<GamblerFedora>(), ModContent.ItemType<GamblerVest>(), ModContent.ItemType<GamblerShoes>(),
			ModContent.ItemType<FourLeafClover>(), ModContent.ItemType<AceUpTheSleeve>(),
		};

		// Layout, as tile offsets from HallLeft.
		public const int Elevator = 14;
		public const int RouletteA = 22, RouletteB = 34, BlackjackA = 48, BlackjackB = 82;
		public const int AtriumLeft = 62, AtriumRight = 78, Fountain = 66;
		public const int PrizeCounter = 92, SlotA = 96;
		public const int StairBottom = 112, StairTop = 135;   // stairs rise to the right, along the right wall
		public const int Piano = 5, BarStart = 20, BarEnd = 32, UpperSlots = 40, StageLeft = 82, StageRight = 98;
		private const int GuestCount = 8;

		public override Point[] GuestSpots => Spots;

		private static readonly Point[] Spots = {
			new(HallLeft + RouletteA + 2, HallFloor), new(HallLeft + RouletteB + 3, HallFloor), new(HallLeft + BlackjackA + 2, HallFloor),
			new(HallLeft + Fountain - 2, HallFloor), new(HallLeft + Fountain + 10, HallFloor), new(HallLeft + BlackjackB + 3, HallFloor),
			new(HallLeft + PrizeCounter - 3, HallFloor), new(HallLeft + SlotA + 1, HallFloor),
			new(HallLeft + BarStart + 2, UpperFloor), new(HallLeft + BarStart + 8, UpperFloor), new(HallLeft + UpperSlots + 4, UpperFloor),
			new(HallLeft + UpperSlots + 12, UpperFloor), new(HallLeft + StageLeft + 3, UpperFloor), new(HallLeft + Piano + 6, UpperFloor),
		};

		public override List<GenPass> Tasks => new() { new SurfaceCasinoPass() };

		protected override void SpawnPeople()
		{
			SpawnAt<RouletteCroupier>(HallLeft + RouletteA + 7, HallFloor, -1);
			SpawnAt<RouletteCroupier>(HallLeft + RouletteB + 7, HallFloor, -1);
			SpawnAt<BlackjackDealer>(HallLeft + BlackjackA + 7, HallFloor, -1);
			SpawnAt<BlackjackDealer>(HallLeft + BlackjackB + 7, HallFloor, -1);
			SpawnAt<PrizeCashier>(HallLeft + PrizeCounter + 1, HallFloor, -1);
			SpawnAt<Bartender>(HallLeft + BarEnd + 3, UpperFloor, -1);
			SpawnAt<Showgirl>(HallLeft + (StageLeft + StageRight) / 2, UpperFloor - 2, 1);

			// Spread guests over both floors: even ones downstairs, odd ones upstairs.
			Point[] ground = System.Array.FindAll(Spots, p => p.Y == HallFloor);
			Point[] upper = System.Array.FindAll(Spots, p => p.Y == UpperFloor);
			for (int k = 0; k < GuestCount; k++) {
				Point[] pool = k % 2 == 0 ? ground : upper;
				Point spot = pool[Main.rand.Next(pool.Length)];
				SpawnAt<CasinoGuest>(spot.X, spot.Y);
			}
		}
	}

	public class SurfaceCasinoPass : GenPass
	{
		private const int Left = SurfaceCasino.HallLeft;
		private const int Right = SurfaceCasino.HallRight;
		private const int Top = SurfaceCasino.HallTop;
		private const int Upper = SurfaceCasino.UpperFloor;
		private const int Floor = SurfaceCasino.HallFloor;

		private static readonly int[] Columns = { Left + 11, Left + 44, Left + 60, Left + 80, Left + 100, Left + 130 };

		public SurfaceCasinoPass() : base("CasinoHall", 1) { }

		private static bool InAtrium(int x) => x >= Left + SurfaceCasino.AtriumLeft && x <= Left + SurfaceCasino.AtriumRight;

		private static bool InStairwell(int x) => x >= Left + SurfaceCasino.StairBottom && x < Right;

		protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
		{
			progress.Message = Language.GetTextValue("Mods.CasinoWorld.Subworlds.Generating");
			Main.worldSurface = 20;
			Main.rockLayer = 25;
			Main.spawnTileX = Left + 8;
			Main.spawnTileY = Floor - 1;

			ushort carpet = (ushort)ModContent.TileType<CasinoCarpet>();
			ushort foundation = (ushort)ModContent.TileType<CasinoFoundation>();
			ushort wallpaper = (ushort)ModContent.WallType<CasinoWallpaper>();
			ushort panel = (ushort)ModContent.WallType<CasinoPanelWall>();

			for (int x = 0; x < Main.maxTilesX; x++) {
				for (int y = 0; y < Main.maxTilesY; y++) {
					Tile tile = Main.tile[x, y];
					bool inside = x > Left && x < Right && y > Top && y < Floor;
					bool upperSlab = y == Upper && x > Left && x < Right && !InAtrium(x);
					if (!inside || upperSlab) {
						tile.HasTile = true;
						if (upperSlab)
							tile.TileType = InStairwell(x) ? TileID.Platforms : carpet;
						else if (y == Floor)
							tile.TileType = carpet;
						else if (y > Floor && y <= Floor + 5 || y < Top && y >= Top - 4)
							tile.TileType = foundation;
						else if (y == Top && x > Left && x < Right)
							tile.TileType = (x - Left) % 3 == 0 ? TileID.AmberGemspark : TileID.GoldBrick;
						else
							tile.TileType = TileID.ObsidianBrick;
					}

					if (x >= Left && x <= Right && y >= Top && y <= Floor) {
						int fromFloor = y > Upper ? Floor - y : Upper - y;
						tile.WallType = fromFloor <= 4 && fromFloor > 0 ? panel
							: fromFloor == 5 || y == Top + 1 || y == Upper + 1 ? WallID.GoldBrick
							: wallpaper;
					}
				}
			}

			foreach (int cx in Columns) {
				for (int x = cx; x < cx + 2; x++) {
					for (int y = Top + 1; y < Floor; y++) {
						if (y == Upper)
							continue;
						bool cap = y <= Top + 2 || y == Upper - 1 || y == Upper + 1 || y == Floor - 1 || y == Upper - 5 || y == Floor - 5;
						Main.tile[x, y].WallType = cap ? WallID.GoldBrick : WallID.MarbleBlock;
					}
				}
			}

			BuildStaircase(carpet);
			WorldGen.RangeFrame(0, 0, Main.maxTilesX - 1, Main.maxTilesY - 1);
			CasinoSignTile.Place(Left + (SurfaceCasino.AtriumLeft + SurfaceCasino.AtriumRight) / 2 - CasinoSignTile.Width / 2, Top + 4);
			FurnishGround();
			FurnishUpper();
		}

		/// <summary>
		/// Grand staircase along the right wall: one-tile steps rising to the right (players walk up single
		/// steps), carpeted, on a solid marble body with a gold edge. The top step is level with the upper
		/// floor, whose platforms above the stairs lead back left over the stairwell.
		/// </summary>
		private static void BuildStaircase(ushort carpet)
		{
			int bottomX = Left + SurfaceCasino.StairBottom;
			for (int k = 0; k < Floor - Upper; k++) {
				int x = bottomX + k;
				int stepY = Floor - 1 - k;
				for (int y = stepY; y < Floor; y++) {
					Tile tile = Main.tile[x, y];
					tile.HasTile = true;
					tile.TileType = y == stepY ? carpet : (y == stepY + 1 ? TileID.GoldBrick : TileID.MarbleBlock);
				}
			}
		}

		private static void Chandeliers(int ceilingY)
		{
			for (int x = Left + 6; x < Right - 4; x += 12) {
				if (!Main.tile[x, ceilingY].HasTile || InStairwell(x) && ceilingY == Upper)
					continue;
				WorldGen.PlaceTile(x, ceilingY + 1, TileID.Chandeliers, mute: true, forced: true, style: 2);
			}
		}

		private static void FurnishGround()
		{
			Chandeliers(Upper);
			foreach (int cx in Columns) {
				WorldGen.PlaceTile(cx - 1, Floor - 7, TileID.Torches, mute: true, forced: true);
				WorldGen.PlaceTile(cx + 2, Floor - 7, TileID.Torches, mute: true, forced: true);
			}

			WorldGen.PlaceObject(Left + 3, Floor - 1, ModContent.TileType<CasinoDoor>(), mute: true);
			WorldGen.PlaceObject(Left + 9, Floor - 1, TileID.PottedPlants1, mute: true, style: 0);
			WorldGen.PlaceObject(Left + SurfaceCasino.Elevator, Floor - 1, ModContent.TileType<CasinoElevator>(), mute: true);

			WorldGen.PlaceObject(Left + SurfaceCasino.RouletteA, Floor - 1, ModContent.TileType<RouletteTable>(), mute: true);
			WorldGen.PlaceObject(Left + SurfaceCasino.RouletteB, Floor - 1, ModContent.TileType<RouletteTable>(), mute: true);
			WorldGen.PlaceObject(Left + SurfaceCasino.BlackjackA, Floor - 1, ModContent.TileType<BlackjackTable>(), mute: true);
			WorldGen.PlaceObject(Left + SurfaceCasino.BlackjackB, Floor - 1, ModContent.TileType<BlackjackTable>(), mute: true);

			WorldGen.PlaceObject(Left + SurfaceCasino.Fountain, Floor - 1, ModContent.TileType<CasinoFountain>(), mute: true);
			WorldGen.PlaceObject(Left + SurfaceCasino.Fountain - 3, Floor - 1, TileID.PottedPlants1, mute: true, style: 1);
			WorldGen.PlaceObject(Left + SurfaceCasino.Fountain + 10, Floor - 1, TileID.PottedPlants1, mute: true, style: 1);

			WorldGen.PlaceObject(Left + SurfaceCasino.PrizeCounter - 1, Floor - 1, TileID.Tables, mute: true, style: 0);
			WorldGen.PlaceTile(Left + SurfaceCasino.PrizeCounter - 1, Floor - 3, TileID.GoldCoinPile, mute: true, forced: true);
			WorldGen.PlaceObject(Left + SurfaceCasino.SlotA, Floor - 1, ModContent.TileType<SlotMachine>(), mute: true);
			WorldGen.PlaceObject(Left + SurfaceCasino.SlotA + 3, Floor - 1, ModContent.TileType<SlotMachine>(), mute: true);
			WorldGen.PlaceObject(Left + 108, Floor - 1, TileID.PottedPlants1, mute: true, style: 2);
		}

		private static void FurnishUpper()
		{
			Chandeliers(Top);
			WorldGen.PlaceObject(Left + SurfaceCasino.Elevator, Upper - 1, ModContent.TileType<CasinoElevator>(), mute: true);

			// Piano lounge
			WorldGen.PlaceObject(Left + SurfaceCasino.Piano, Upper - 1, TileID.Pianos, mute: true, style: 0);
			WorldGen.PlaceObject(Left + 10, Upper - 1, TileID.PottedPlants1, mute: true, style: 3);

			// Bar: counter with drinks and candles, kegs behind the bartender
			for (int x = Left + SurfaceCasino.BarStart; x <= Left + SurfaceCasino.BarEnd; x += 4) {
				WorldGen.PlaceObject(x, Upper - 1, TileID.Tables, mute: true, style: 0);
				WorldGen.PlaceTile(x, Upper - 3, (x / 4) % 2 == 0 ? TileID.Bottles : TileID.Candles, mute: true, forced: true, style: 4);
			}
			WorldGen.PlaceObject(Left + SurfaceCasino.BarEnd + 5, Upper - 1, TileID.Kegs, mute: true);

			// Row of slot machines
			for (int k = 0; k < 6; k++)
				WorldGen.PlaceObject(Left + SurfaceCasino.UpperSlots + k * 3, Upper - 1, ModContent.TileType<SlotMachine>(), mute: true);

			// Cabaret stage: raised gold floor with red curtains on both sides
			for (int x = Left + SurfaceCasino.StageLeft; x <= Left + SurfaceCasino.StageRight; x++) {
				for (int y = Upper - 2; y < Upper; y++) {
					Tile tile = Main.tile[x, y];
					tile.HasTile = true;
					tile.TileType = y == Upper - 2 ? TileID.GoldBrick : TileID.RichMahogany;
				}
			}
			WorldGen.RangeFrame(Left + SurfaceCasino.StageLeft - 1, Upper - 3, Left + SurfaceCasino.StageRight + 1, Upper + 1);
			for (int y = Top + 2; y < Upper - 2; y += 3) {
				WorldGen.PlaceTile(Left + SurfaceCasino.StageLeft, y, TileID.Banners, mute: true, forced: true, style: 0);
				WorldGen.PlaceTile(Left + SurfaceCasino.StageRight, y, TileID.Banners, mute: true, forced: true, style: 0);
			}
			WorldGen.PlaceTile(Left + SurfaceCasino.StageLeft + 2, Upper - 3, TileID.Torches, mute: true, forced: true, style: 9);
			WorldGen.PlaceTile(Left + SurfaceCasino.StageRight - 2, Upper - 3, TileID.Torches, mute: true, forced: true, style: 9);

			WorldGen.PlaceObject(Left + 106, Upper - 1, TileID.PottedPlants1, mute: true, style: 4);
		}
	}
}
