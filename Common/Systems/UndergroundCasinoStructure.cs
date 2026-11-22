using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Systems
{
	/// <summary>
	/// Finds a spot in the cavern layer for the hardmode casino and carves a lit hall around
	/// its vault entrance: obsidian brick floor, walls and ceiling, with open space in front.
	/// </summary>
	public static class UndergroundCasinoStructure
	{
		private const int Width = UndergroundCasinoFacade.Width;
		private const int Height = UndergroundCasinoFacade.Height;

		// Hall carved around the entrance, in tiles beyond the entrance on each side.
		private const int MarginX = 8;
		private const int MarginTop = 3;

		private const int Attempts = 2000;
		private const int MinDistanceFromSpawn = 250;

		public static bool TryFindSite(out int left, out int top)
		{
			int minY = (int)Main.rockLayer + 40;
			int maxY = Main.maxTilesY - 260;   // stay above the underworld
			for (int k = 0; k < Attempts && minY < maxY; k++) {
				int x = WorldGen.genRand.Next(300, Main.maxTilesX - 300 - Width);
				int y = WorldGen.genRand.Next(minY, maxY);
				if (System.Math.Abs(x + Width / 2 - Main.spawnTileX) < MinDistanceFromSpawn)
					continue;
				if (IsGoodSite(x, y)) {
					left = x;
					top = y;
					return true;
				}
			}

			left = top = -1;
			return false;
		}

		private static bool IsGoodSite(int left, int top)
		{
			for (int x = left - MarginX - 1; x <= left + Width + MarginX; x++) {
				for (int y = top - MarginTop - 1; y <= top + Height + 1; y++) {
					if (!WorldGen.InWorld(x, y, 10))
						return false;
					Tile tile = Main.tile[x, y];
					if (!tile.HasTile)
						continue;
					ushort type = tile.TileType;
					if (type >= TileID.Count || Main.tileDungeon[type] || type == TileID.LihzahrdBrick
						|| type == TileID.Containers || type == TileID.Containers2 || type == TileID.DemonAltar
						|| type == TileID.Hive || type == TileID.LihzahrdAltar)
						return false;
					if (tile.WallType == WallID.LihzahrdBrickUnsafe)
						return false;
				}
			}
			return true;
		}

		/// <summary>Carves the hall and places the entrance with its top-left tile at (left, top).</summary>
		public static void Build(int left, int top)
		{
			int x0 = left - MarginX, x1 = left + Width + MarginX;
			int y0 = top - MarginTop, floor = top + Height;

			for (int x = x0 - 1; x <= x1; x++) {
				for (int y = y0 - 1; y <= floor + 1; y++) {
					Tile tile = Main.tile[x, y];
					bool shell = x == x0 - 1 || x == x1 || y == y0 - 1 || y >= floor;
					tile.LiquidAmount = 0;
					if (shell) {
						tile.ClearTile();
						tile.HasTile = true;
						tile.TileType = y == y0 - 1 && (x - x0) % 3 == 0 ? TileID.AmethystGemspark : TileID.ObsidianBrick;
					}
					else {
						tile.ClearTile();
					}
					tile.WallType = WallID.ObsidianBrickUnsafe;
				}
			}

			ushort type = (ushort)ModContent.TileType<UndergroundCasinoFacade>();
			for (int dx = 0; dx < Width; dx++) {
				for (int dy = 0; dy < Height; dy++) {
					Tile tile = Main.tile[left + dx, top + dy];
					tile.HasTile = true;
					tile.TileType = type;
					tile.TileFrameX = (short)(dx * 18);
					tile.TileFrameY = (short)(dy * 18);
				}
			}

			WorldGen.RangeFrame(x0 - 2, y0 - 2, x1 + 2, floor + 3);

			// Torches on the side walls so the hall is visible from a distance.
			WorldGen.PlaceTile(x0 + 2, floor - 3, TileID.Torches, mute: true, forced: true, style: 8);
			WorldGen.PlaceTile(x1 - 3, floor - 3, TileID.Torches, mute: true, forced: true, style: 8);

			CasinoWorldSystem.UndergroundCasinoPosition = new Point(left, top);
			if (Main.netMode == NetmodeID.Server) {
				NetMessage.SendTileSquare(-1, x0 - 2, y0 - 2, x1 - x0 + 4, floor - y0 + 5);
				NetMessage.SendData(MessageID.WorldData);
			}
		}
	}
}
