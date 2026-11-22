using System.Collections.Generic;
using CasinoWorld.Common.Games;
using CasinoWorld.Content.Items.Weapons;
using CasinoWorld.Content.NPCs;
using CasinoWorld.Content.NPCs.People;
using CasinoWorld.Content.Tiles;
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
	/// Hardmode casino for high rollers, two storeys deep. Ground floor: a long row of slot machines,
	/// roulette, blackjack, an amethyst-lit fountain under an open atrium, the upgrade machine and the
	/// prize counter. Upper floor (VIP): bar, VIP roulette and blackjack, and a jazz stage with a
	/// dancer. A staircase along the right wall and an elevator connect the floors.
	/// Higher limits and hardmode jackpots.
	/// </summary>
	public class UndergroundCasino : CasinoSubworld
	{
		public const int HallLeft = 40;
		public const int HallRight = 180;
		public const int HallTop = 30;     // ceiling row
		public const int UpperFloor = 54;  // floor row of the VIP storey
		public const int HallFloor = 78;   // floor row of the ground storey

		// Furniture positions, as tile offsets from HallLeft.
		public const int Elevator = 14;
		public const int SlotsStart = 20, SlotCount = 8, SlotStep = 4;
		public const int Roulette = 52, BlackjackA = 61;
		public const int AtriumLeft = 72, AtriumRight = 88, Fountain = 76;
		public const int UpgradeStation = 91, PrizeCounter = 99;
		public const int StairBottom = 112;
		// Upper floor
		public const int BarStart = 20, BarEnd = 32, VipRoulette = 44, BlackjackB = 56, StageLeft = 92, StageRight = 106;
		private const int GuestCount = 9;

		public override int Width => 220;
		public override int Height => 130;

		// Hardmode stakes: 1 gold to 1 platinum chips, 5 platinum per round.
		public override long[] ChipValues => new[] { Coins.Gold, 10 * Coins.Gold, 50 * Coins.Gold, Coins.Platinum };
		public override long MaxBet => 5 * Coins.Platinum;
		public override long MinJackpotStake => Coins.Gold;
		public override long FullJackpotStake => 10 * Coins.Gold;

		/// <summary>Jackpots drop the highest unlocked tier: hardmode, post-Plantera or post-Moon Lord.</summary>
		public override int[] JackpotItems => NPC.downedMoonlord
			? new[] { ModContent.ItemType<StardustDeck>(), ModContent.ItemType<MeteorDice>(), ModContent.ItemType<InfinitySlot>() }
			: NPC.downedPlantBoss
				? new[] { ModContent.ItemType<AceOfSpades>(), ModContent.ItemType<DoublingDice>(), ModContent.ItemType<RouletteRevolver>() }
				: new[] { ModContent.ItemType<SnakeEyes>(), ModContent.ItemType<RoyalFlush>(), ModContent.ItemType<JackpotCannon>() };

		public override Point[] GuestSpots => Spots;

		private static readonly Point[] Spots = {
			new(HallLeft + SlotsStart + 1, HallFloor), new(HallLeft + SlotsStart + 9, HallFloor), new(HallLeft + SlotsStart + 17, HallFloor),
			new(HallLeft + SlotsStart + 25, HallFloor), new(HallLeft + Roulette + 2, HallFloor), new(HallLeft + BlackjackA + 3, HallFloor),
			new(HallLeft + Fountain - 2, HallFloor), new(HallLeft + Fountain + 10, HallFloor), new(HallLeft + PrizeCounter - 3, HallFloor),
			new(HallLeft + BarStart + 2, UpperFloor), new(HallLeft + BarStart + 8, UpperFloor), new(HallLeft + VipRoulette + 2, UpperFloor),
			new(HallLeft + BlackjackB + 3, UpperFloor), new(HallLeft + StageLeft - 2, UpperFloor),
		};

		public override List<GenPass> Tasks => new() { new UndergroundCasinoPass() };

		public override Vector3 Ambient => new(0.24f, 0.14f, 0.30f);

		protected override void SpawnPeople()
		{
			SpawnAt<RouletteCroupier>(HallLeft + Roulette + 7, HallFloor, -1);
			SpawnAt<BlackjackDealer>(HallLeft + BlackjackA + 7, HallFloor, -1);
			SpawnAt<PrizeCashier>(HallLeft + PrizeCounter + 1, HallFloor, -1);
			SpawnAt<RouletteCroupier>(HallLeft + VipRoulette + 7, UpperFloor, -1);
			SpawnAt<BlackjackDealer>(HallLeft + BlackjackB + 7, UpperFloor, -1);
			SpawnAt<Bartender>(HallLeft + BarEnd + 3, UpperFloor, -1);
			SpawnAt<Showgirl>(HallLeft + (StageLeft + StageRight) / 2 + 3, UpperFloor - 2, 1);

			// Two guests in three downstairs among the slots, the rest in the VIP lounge.
			Point[] ground = System.Array.FindAll(Spots, p => p.Y == HallFloor);
			Point[] upper = System.Array.FindAll(Spots, p => p.Y == UpperFloor);
			for (int k = 0; k < GuestCount; k++) {
				Point[] pool = k % 3 == 2 ? upper : ground;
				Point spot = pool[Main.rand.Next(pool.Length)];
				SpawnAt<CasinoGuest>(spot.X, spot.Y);
			}
		}
	}

	/// <summary>
	/// Dark, glittering two-storey hall: obsidian brick with amethyst gemspark trim, gold columns,
	/// a ruby neon VIP sign over the atrium and lit marble foundations instead of black voids.
	/// </summary>
	public class UndergroundCasinoPass : GenPass
	{
		private const int Left = UndergroundCasino.HallLeft;
		private const int Right = UndergroundCasino.HallRight;
		private const int Top = UndergroundCasino.HallTop;
		private const int Upper = UndergroundCasino.UpperFloor;
		private const int Floor = UndergroundCasino.HallFloor;

		private static readonly int[] Columns = { Left + 9, Left + 50, Left + 69, Left + 89, Left + 108, Left + 130 };

		private static readonly string[][] Letters = {
			new[] { "1001", "1001", "1001", "0110", "0110" }, // V
			new[] { "111", "010", "010", "010", "111" },     // I
			new[] { "1110", "1001", "1110", "1000", "1000" }, // P
		};

		public UndergroundCasinoPass() : base("HighRollerHall", 1) { }

		private static bool InAtrium(int x) => x >= Left + UndergroundCasino.AtriumLeft && x <= Left + UndergroundCasino.AtriumRight;

		private static bool InStairwell(int x) => x >= Left + UndergroundCasino.StairBottom && x < Right;

		protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
		{
			progress.Message = Language.GetTextValue("Mods.CasinoWorld.Subworlds.Generating");
			Main.worldSurface = 20;
			Main.rockLayer = 25;
			Main.spawnTileX = Left + 6;
			Main.spawnTileY = Floor - 1;
			ushort foundation = (ushort)ModContent.TileType<CasinoFoundation>();

			for (int x = 0; x < Main.maxTilesX; x++) {
				for (int y = 0; y < Main.maxTilesY; y++) {
					Tile tile = Main.tile[x, y];
					bool inside = x > Left && x < Right && y > Top && y < Floor;
					bool upperSlab = y == Upper && x > Left && x < Right && !InAtrium(x);
					if (!inside || upperSlab) {
						tile.HasTile = true;
						if (upperSlab && InStairwell(x))
							tile.TileType = TileID.Platforms;
						else if (upperSlab || y == Floor)
							tile.TileType = (x - Left) % 4 == 0 ? TileID.AmethystGemspark : TileID.ObsidianBrick;
						else if (y > Floor && y <= Floor + 5 || y < Top && y >= Top - 4)
							tile.TileType = foundation;
						else if (y == Top && x > Left && x < Right)
							tile.TileType = (x - Left) % 3 == 0 ? TileID.AmethystGemspark : TileID.GoldBrick;
						else
							tile.TileType = TileID.ObsidianBrick;
					}

					if (x >= Left && x <= Right && y >= Top && y <= Floor) {
						int fromFloor = y > Upper ? Floor - y : Upper - y;
						tile.WallType = fromFloor == 5 || y == Top + 1 || y == Upper + 1 ? WallID.GoldBrick
							: fromFloor > 5 && (fromFloor - 5) % 7 == 0 ? WallID.AmethystGemspark
							: WallID.ObsidianBrickUnsafe;
					}
				}
			}

			foreach (int cx in Columns) {
				for (int x = cx; x < cx + 2; x++) {
					for (int y = Top + 1; y < Floor; y++) {
						if (y != Upper)
							Main.tile[x, y].WallType = WallID.GoldBrick;
					}
				}
			}

			BuildStaircase();
			NeonSign();
			WorldGen.RangeFrame(0, 0, Main.maxTilesX - 1, Main.maxTilesY - 1);
			FurnishGround();
			FurnishUpper();
		}

		/// <summary>Obsidian staircase along the right wall, rising to the right, every third tread lit amethyst.</summary>
		private static void BuildStaircase()
		{
			int bottomX = Left + UndergroundCasino.StairBottom;
			for (int k = 0; k < Floor - Upper; k++) {
				int x = bottomX + k;
				int stepY = Floor - 1 - k;
				for (int y = stepY; y < Floor; y++) {
					Tile tile = Main.tile[x, y];
					tile.HasTile = true;
					tile.TileType = y == stepY ? (k % 3 == 0 ? TileID.AmethystGemspark : TileID.ObsidianBrick)
						: y == stepY + 1 ? TileID.GoldBrick : TileID.ObsidianBrick;
				}
			}
		}

		private static void NeonSign()
		{
			int width = -1;
			foreach (string[] rows in Letters)
				width += rows[0].Length + 1;

			int x = Left + (UndergroundCasino.AtriumLeft + UndergroundCasino.AtriumRight) / 2 - width / 2;
			int y = Top + 6;
			for (int bx = x - 2; bx < x + width + 2; bx++) {
				for (int by = y - 2; by < y + 7; by++) {
					bool frame = bx == x - 2 || bx == x + width + 1 || by == y - 2 || by == y + 6;
					Main.tile[bx, by].WallType = frame ? WallID.GoldBrick : WallID.ObsidianBrickUnsafe;
				}
			}
			foreach (string[] rows in Letters) {
				for (int r = 0; r < rows.Length; r++) {
					for (int c = 0; c < rows[r].Length; c++) {
						if (rows[r][c] == '1')
							Main.tile[x + c, y + r].WallType = WallID.RubyGemspark;
					}
				}
				x += rows[0].Length + 1;
			}
		}

		private static void Chandeliers(int ceilingY)
		{
			for (int x = Left + 6; x < Right - 4; x += 12) {
				if (Main.tile[x, ceilingY].HasTile && !(ceilingY == Upper && (InStairwell(x) || InAtrium(x))))
					WorldGen.PlaceTile(x, ceilingY + 1, TileID.Chandeliers, mute: true, forced: true, style: 2);
			}
		}

		private static void FurnishGround()
		{
			Chandeliers(Upper);
			foreach (int cx in Columns) {
				WorldGen.PlaceTile(cx - 1, Floor - 7, TileID.Torches, mute: true, forced: true, style: 8);
				WorldGen.PlaceTile(cx + 2, Floor - 7, TileID.Torches, mute: true, forced: true, style: 8);
			}

			WorldGen.PlaceObject(Left + 3, Floor - 1, ModContent.TileType<CasinoDoor>(), mute: true);
			WorldGen.PlaceObject(Left + UndergroundCasino.Elevator, Floor - 1, ModContent.TileType<CasinoElevator>(), mute: true);

			for (int k = 0; k < UndergroundCasino.SlotCount; k++)
				WorldGen.PlaceObject(Left + UndergroundCasino.SlotsStart + k * UndergroundCasino.SlotStep, Floor - 1, ModContent.TileType<SlotMachine>(), mute: true);

			WorldGen.PlaceObject(Left + UndergroundCasino.Roulette, Floor - 1, ModContent.TileType<RouletteTable>(), mute: true);
			WorldGen.PlaceObject(Left + UndergroundCasino.BlackjackA, Floor - 1, ModContent.TileType<BlackjackTable>(), mute: true);
			WorldGen.PlaceObject(Left + UndergroundCasino.Fountain, Floor - 1, ModContent.TileType<CasinoFountain>(), mute: true);
			WorldGen.PlaceObject(Left + UndergroundCasino.UpgradeStation, Floor - 1, ModContent.TileType<UpgradeMachine>(), mute: true);

			WorldGen.PlaceObject(Left + UndergroundCasino.PrizeCounter - 1, Floor - 1, TileID.Tables, mute: true, style: 0);
			WorldGen.PlaceTile(Left + UndergroundCasino.PrizeCounter - 1, Floor - 3, TileID.PlatinumCoinPile, mute: true, forced: true);

			WorldGen.PlaceObject(Left + 8, Floor - 1, TileID.PottedPlants1, mute: true, style: 3);
			WorldGen.PlaceObject(Left + 106, Floor - 1, TileID.PottedPlants1, mute: true, style: 4);
		}

		private static void FurnishUpper()
		{
			Chandeliers(Top);
			WorldGen.PlaceObject(Left + UndergroundCasino.Elevator, Upper - 1, ModContent.TileType<CasinoElevator>(), mute: true);

			// VIP bar: counter with drinks and candles, kegs behind the bartender.
			for (int x = Left + UndergroundCasino.BarStart; x <= Left + UndergroundCasino.BarEnd; x += 4) {
				WorldGen.PlaceObject(x, Upper - 1, TileID.Tables, mute: true, style: 0);
				WorldGen.PlaceTile(x, Upper - 3, (x / 4) % 2 == 0 ? TileID.Bottles : TileID.Candles, mute: true, forced: true, style: 4);
			}
			WorldGen.PlaceObject(Left + UndergroundCasino.BarEnd + 5, Upper - 1, TileID.Kegs, mute: true);
			WorldGen.PlaceObject(Left + UndergroundCasino.BarEnd + 7, Upper - 1, TileID.Kegs, mute: true);

			WorldGen.PlaceObject(Left + UndergroundCasino.VipRoulette, Upper - 1, ModContent.TileType<RouletteTable>(), mute: true);
			WorldGen.PlaceObject(Left + UndergroundCasino.BlackjackB, Upper - 1, ModContent.TileType<BlackjackTable>(), mute: true);

			// Jazz stage: raised obsidian floor with a gold edge, piano, curtains and purple lights.
			for (int x = Left + UndergroundCasino.StageLeft; x <= Left + UndergroundCasino.StageRight; x++) {
				for (int y = Upper - 2; y < Upper; y++) {
					Tile tile = Main.tile[x, y];
					tile.HasTile = true;
					tile.TileType = y == Upper - 2 ? TileID.GoldBrick : TileID.ObsidianBrick;
				}
			}
			WorldGen.RangeFrame(Left + UndergroundCasino.StageLeft - 1, Upper - 3, Left + UndergroundCasino.StageRight + 1, Upper + 1);
			for (int y = Top + 2; y < Upper - 2; y += 3) {
				WorldGen.PlaceTile(Left + UndergroundCasino.StageLeft, y, TileID.Banners, mute: true, forced: true, style: 3);
				WorldGen.PlaceTile(Left + UndergroundCasino.StageRight, y, TileID.Banners, mute: true, forced: true, style: 3);
			}
			WorldGen.PlaceObject(Left + UndergroundCasino.StageLeft + 2, Upper - 3, TileID.Pianos, mute: true, style: 0);
			WorldGen.PlaceTile(Left + UndergroundCasino.StageLeft + 6, Upper - 3, TileID.Torches, mute: true, forced: true, style: 8);
			WorldGen.PlaceTile(Left + UndergroundCasino.StageRight - 2, Upper - 3, TileID.Torches, mute: true, forced: true, style: 8);
			WorldGen.PlaceObject(Left + 108, Upper - 1, TileID.PottedPlants1, mute: true, style: 4);
		}
	}
}
