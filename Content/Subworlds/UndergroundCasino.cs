using System.Collections.Generic;
using CasinoWorld.Common.Games;
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
	/// Hardmode casino for high rollers: a long row of slot machines, a roulette table,
	/// two blackjack tables, the prize counter and a bar. Higher limits and hardmode jackpots.
	/// </summary>
	public class UndergroundCasino : CasinoSubworld
	{
		public const int HallLeft = 40;
		public const int HallRight = 180;
		public const int HallTop = 46;
		public const int HallFloor = 72;

		// Furniture positions, as tile offsets from HallLeft.
		public const int SlotsStart = 12, SlotCount = 8, SlotStep = 4;
		public const int Roulette = 50, BlackjackA = 66, BlackjackB = 80;
		public const int PrizeCounter = 96, BarStart = 106, BarEnd = 122, UpgradeStation = 88;
		private const int GuestCount = 8;

		public override int Width => 220;
		public override int Height => 120;

		// Hardmode stakes: 1 gold to 1 platinum chips, 5 platinum per round.
		public override long[] ChipValues => new[] { Coins.Gold, 10 * Coins.Gold, 50 * Coins.Gold, Coins.Platinum };
		public override long MaxBet => 5 * Coins.Platinum;
		public override long MinJackpotStake => Coins.Gold;
		public override long FullJackpotStake => 10 * Coins.Gold;

		public override int[] JackpotItems => new[] {
			ModContent.ItemType<SnakeEyes>(), ModContent.ItemType<RoyalFlush>(), ModContent.ItemType<JackpotCannon>(),
		};

		public override int[] GuestSpots => Spots;

		private static readonly int[] Spots = {
			HallLeft + SlotsStart + 1, HallLeft + SlotsStart + 9, HallLeft + SlotsStart + 17, HallLeft + SlotsStart + 25,
			HallLeft + Roulette + 2, HallLeft + Roulette + 4, HallLeft + BlackjackA + 2, HallLeft + BlackjackB + 3,
			HallLeft + BarStart + 2, HallLeft + BarStart + 8, HallLeft + BarStart + 14, HallLeft + PrizeCounter - 4,
		};

		public override List<GenPass> Tasks => new() { new UndergroundCasinoPass() };

		public override Microsoft.Xna.Framework.Vector3 Ambient => new(0.24f, 0.14f, 0.30f);

		protected override void SpawnPeople()
		{
			SpawnAt<RouletteCroupier>(HallLeft + Roulette + 7, HallFloor, -1);
			SpawnAt<BlackjackDealer>(HallLeft + BlackjackA + 7, HallFloor, -1);
			SpawnAt<BlackjackDealer>(HallLeft + BlackjackB + 7, HallFloor, -1);
			SpawnAt<PrizeCashier>(HallLeft + PrizeCounter + 1, HallFloor, -1);
			SpawnAt<Bartender>(HallLeft + BarEnd + 3, HallFloor, -1);

			for (int k = 0; k < GuestCount; k++)
				SpawnAt<CasinoGuest>(Spots[Main.rand.Next(Spots.Length)], HallFloor);
		}
	}

	/// <summary>
	/// Dark, glittering hall: obsidian brick with amethyst gemspark trim, gold columns,
	/// a pink neon VIP sign and a row of slot machines.
	/// </summary>
	public class UndergroundCasinoPass : GenPass
	{
		private const int Left = UndergroundCasino.HallLeft;
		private const int Right = UndergroundCasino.HallRight;
		private const int Top = UndergroundCasino.HallTop;
		private const int Floor = UndergroundCasino.HallFloor;

		private static readonly int[] Columns = { Left + 9, Left + 46, Left + 62, Left + 92, Left + 103, Left + 128 };

		private static readonly string[][] Letters = {
			new[] { "1001", "1001", "1001", "0110", "0110" }, // V
			new[] { "111", "010", "010", "010", "111" },     // I
			new[] { "1110", "1001", "1110", "1000", "1000" }, // P
		};

		public UndergroundCasinoPass() : base("HighRollerHall", 1) { }

		protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
		{
			progress.Message = Language.GetTextValue("Mods.CasinoWorld.Subworlds.Generating");

			Main.worldSurface = 20;
			Main.rockLayer = 25;
			Main.spawnTileX = Left + 6;
			Main.spawnTileY = Floor - 1;

			for (int x = 0; x < Main.maxTilesX; x++) {
				for (int y = 0; y < Main.maxTilesY; y++) {
					Tile tile = Main.tile[x, y];
					bool inHall = x > Left && x < Right && y > Top && y < Floor;
					if (!inHall) {
						tile.HasTile = true;
						if (y == Floor)
							tile.TileType = (x - Left) % 4 == 0 ? TileID.AmethystGemspark : TileID.ObsidianBrick;
						else if (y == Top && x > Left && x < Right)
							tile.TileType = (x - Left) % 3 == 0 ? TileID.AmethystGemspark : TileID.GoldBrick;
						else
							tile.TileType = TileID.ObsidianBrick;
					}

					if (x >= Left && x <= Right && y >= Top && y <= Floor) {
						int fromFloor = Floor - y;
						tile.WallType = fromFloor == 5 || y == Top + 1 ? WallID.GoldBrick
							: fromFloor > 5 && (fromFloor - 5) % 7 == 0 ? WallID.AmethystGemspark
							: WallID.ObsidianBrickUnsafe;
					}
				}
			}

			foreach (int cx in Columns) {
				for (int x = cx; x < cx + 2; x++) {
					for (int y = Top + 1; y < Floor; y++)
						Main.tile[x, y].WallType = WallID.GoldBrick;
				}
			}

			NeonSign();
			WorldGen.RangeFrame(0, 0, Main.maxTilesX - 1, Main.maxTilesY - 1);
			Furnish();
		}

		private static void NeonSign()
		{
			int width = -1;
			foreach (string[] rows in Letters)
				width += rows[0].Length + 1;

			int x = Left + UndergroundCasino.SlotsStart + 14 - width / 2;
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

		private static void Furnish()
		{
			for (int x = Left + 6; x < Right - 4; x += 12)
				WorldGen.PlaceTile(x, Top + 1, TileID.Chandeliers, mute: true, forced: true, style: 2);

			foreach (int cx in Columns) {
				WorldGen.PlaceTile(cx - 1, Floor - 7, TileID.Torches, mute: true, forced: true, style: 8);
				WorldGen.PlaceTile(cx + 2, Floor - 7, TileID.Torches, mute: true, forced: true, style: 8);
			}

			WorldGen.PlaceObject(Left + 3, Floor - 1, ModContent.TileType<CasinoDoor>(), mute: true);

			for (int k = 0; k < UndergroundCasino.SlotCount; k++)
				WorldGen.PlaceObject(Left + UndergroundCasino.SlotsStart + k * UndergroundCasino.SlotStep, Floor - 1, ModContent.TileType<SlotMachine>(), mute: true);

			WorldGen.PlaceObject(Left + UndergroundCasino.Roulette, Floor - 1, ModContent.TileType<RouletteTable>(), mute: true);
			WorldGen.PlaceObject(Left + UndergroundCasino.BlackjackA, Floor - 1, ModContent.TileType<BlackjackTable>(), mute: true);
			WorldGen.PlaceObject(Left + UndergroundCasino.BlackjackB, Floor - 1, ModContent.TileType<BlackjackTable>(), mute: true);

			WorldGen.PlaceObject(Left + UndergroundCasino.UpgradeStation, Floor - 1, ModContent.TileType<UpgradeMachine>(), mute: true);

			WorldGen.PlaceObject(Left + UndergroundCasino.PrizeCounter - 1, Floor - 1, TileID.Tables, mute: true, style: 0);
			WorldGen.PlaceTile(Left + UndergroundCasino.PrizeCounter - 1, Floor - 3, TileID.PlatinumCoinPile, mute: true, forced: true);

			for (int x = Left + UndergroundCasino.BarStart; x <= Left + UndergroundCasino.BarEnd; x += 4) {
				WorldGen.PlaceObject(x, Floor - 1, TileID.Tables, mute: true, style: 0);
				WorldGen.PlaceTile(x, Floor - 3, (x / 4) % 2 == 0 ? TileID.Bottles : TileID.Candles, mute: true, forced: true, style: 4);
			}
			WorldGen.PlaceObject(Left + UndergroundCasino.BarEnd + 5, Floor - 1, TileID.Kegs, mute: true);
			WorldGen.PlaceObject(Left + UndergroundCasino.BarEnd + 7, Floor - 1, TileID.Kegs, mute: true);

			WorldGen.PlaceObject(Left + 132, Floor - 1, TileID.Pianos, mute: true, style: 0);
			WorldGen.PlaceObject(Left + 8, Floor - 1, TileID.PottedPlants1, mute: true, style: 3);
			WorldGen.PlaceObject(Left + 137, Floor - 1, TileID.PottedPlants1, mute: true, style: 4);
		}
	}
}
