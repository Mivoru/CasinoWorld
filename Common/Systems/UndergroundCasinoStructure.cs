using System;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Systems
{
	/// <summary>
	/// Finds a spot in the cavern layer for the hardmode casino and shapes a natural-looking cave
	/// chamber around its entrance: an irregular rounded hollow (the surrounding rock and its background
	/// walls stay as they were), a smooth stone landing with minecart tracks running off into the cave,
	/// crystal shards, amethyst in the walls and hanging lanterns. The entrance sprite has a ragged,
	/// transparent rock edge, so it blends into the cave instead of sitting in a box.
	/// </summary>
	public static class UndergroundCasinoStructure
	{
		private const int Width = UndergroundCasinoFacade.Width;
		private const int Height = UndergroundCasinoFacade.Height;

		// Chamber radii beyond the entrance, in tiles.
		private const int ExtraX = 9;
		private const int ExtraY = 4;

		private const int Attempts = 2000;
		private const int MinDistanceFromSpawn = 250;

		public static bool TryFindSite(out int left, out int top)
		{
			int minY = (int)Main.rockLayer + 40;
			int maxY = Main.maxTilesY - 270;
			for (int k = 0; k < Attempts && minY < maxY; k++) {
				int x = WorldGen.genRand.Next(320, Main.maxTilesX - 320 - Width);
				int y = WorldGen.genRand.Next(minY, maxY);
				if (Math.Abs(x + Width / 2 - Main.spawnTileX) < MinDistanceFromSpawn)
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
			for (int x = left - ExtraX - 2; x <= left + Width + ExtraX + 2; x++) {
				for (int y = top - ExtraY - 2; y <= top + Height + 4; y++) {
					if (!WorldGen.InWorld(x, y, 10))
						return false;
					Tile tile = Main.tile[x, y];
					if (tile.WallType == WallID.LihzahrdBrickUnsafe)
						return false;
					if (!tile.HasTile)
						continue;
					ushort type = tile.TileType;
					if (type >= TileID.Count || Main.tileDungeon[type] || type == TileID.LihzahrdBrick
						|| type == TileID.Containers || type == TileID.Containers2 || type == TileID.DemonAltar
						|| type == TileID.Hive || type == TileID.LihzahrdAltar)
						return false;
				}
			}
			return true;
		}

		/// <summary>Is (x, y) inside the irregular chamber? A wobbly ellipse around the entrance.</summary>
		private static bool InChamber(int x, int y, Vector2 center, float rx, float ry, float seed)
		{
			float dx = (x - center.X) / rx, dy = (y - center.Y) / ry;
			float angle = (float)Math.Atan2(dy, dx);
			float wobble = 1f + 0.10f * (float)Math.Sin(angle * 3 + seed) + 0.06f * (float)Math.Sin(angle * 7 + seed * 2.3f);
			return dx * dx + dy * dy < wobble * wobble;
		}

		/// <summary>Shapes the chamber and places the entrance with its top-left tile at (left, top).</summary>
		public static void Build(int left, int top)
		{
			int floor = top + Height;                       // first row below the entrance
			var center = new Vector2(left + Width / 2f, top + Height / 2f + 1);
			float rx = Width / 2f + ExtraX, ry = Height / 2f + ExtraY;
			float seed = WorldGen.genRand.NextFloat(0, 100);
			int x0 = (int)(center.X - rx * 1.2f), x1 = (int)(center.X + rx * 1.2f);
			int y0 = (int)(center.Y - ry * 1.2f), y1 = floor + 4;

			// Hollow out the chamber above the floor; keep background walls (add stone walls where bare).
			for (int x = x0; x <= x1; x++) {
				for (int y = y0; y < floor; y++) {
					if (!InChamber(x, y, center, rx, ry, seed))
						continue;
					Tile tile = Main.tile[x, y];
					tile.ClearTile();
					tile.LiquidAmount = 0;
					if (tile.WallType == WallID.None)
						tile.WallType = WallID.Stone;
				}
			}

			// Smooth stone landing under the entrance, rough stone below it, across the chamber width.
			for (int x = x0; x <= x1; x++) {
				bool landing = x >= left - 2 && x <= left + Width + 1;
				for (int y = floor; y <= y1; y++) {
					Tile tile = Main.tile[x, y];
					if (y > floor && tile.HasTile && Main.tileSolid[tile.TileType])
						continue;
					if (y == floor && !landing && !InChamber(x, floor - 1, center, rx, ry, seed))
						continue;
					tile.ClearTile();
					tile.HasTile = true;
					tile.TileType = y == floor && landing ? TileID.StoneSlab : TileID.Stone;
					tile.LiquidAmount = 0;
				}
			}

			WorldGen.RangeFrame(x0 - 2, y0 - 2, x1 + 2, y1 + 2);
			Decorate(left, top, floor, x0, x1, y0, center, rx, ry, seed);
			PlaceEntrance(left, top);
			WorldGen.RangeFrame(x0 - 2, y0 - 2, x1 + 2, y1 + 2);

			CasinoWorldSystem.UndergroundCasinoPosition = new Point(left, top);
			if (Main.netMode == NetmodeID.Server) {
				NetMessage.SendTileSquare(-1, x0 - 2, y0 - 2, x1 - x0 + 5, y1 - y0 + 5);
				NetMessage.SendData(MessageID.WorldData);
			}
		}

		private static void Decorate(int left, int top, int floor, int x0, int x1, int y0, Vector2 center, float rx, float ry, float seed)
		{
			// Minecart tracks run from the landing out to both sides of the chamber.
			for (int x = x0; x <= x1; x++) {
				if (x >= left + 2 && x <= left + Width - 3)
					continue;
				Tile ground = Main.tile[x, floor];
				if (ground.HasTile && Main.tileSolid[ground.TileType] && !Main.tile[x, floor - 1].HasTile)
					WorldGen.PlaceTile(x, floor - 1, TileID.MinecartTrack, mute: true, forced: true);
			}

			// Amethyst veins in the rock around the chamber edge.
			for (int k = 0; k < 40; k++) {
				double a = WorldGen.genRand.NextDouble() * Math.PI * 2;
				int x = (int)(center.X + Math.Cos(a) * rx * 1.08f), y = (int)(center.Y + Math.Sin(a) * ry * 1.08f);
				if (y < floor && WorldGen.InWorld(x, y) && Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Stone)
					Main.tile[x, y].TileType = TileID.Amethyst;
			}

			// Crystal shards and hanging lanterns along the chamber ceiling.
			for (int x = x0; x <= x1; x++) {
				int ceiling = -1;
				for (int y = (int)center.Y; y > y0 - 6; y--) {
					if (Main.tile[x, y].HasTile && Main.tileSolid[Main.tile[x, y].TileType]) {
						ceiling = y;
						break;
					}
				}
				if (ceiling < 0 || x >= left && x < left + Width && ceiling >= top - 1)
					continue;
				if (WorldGen.genRand.NextBool(5))
					WorldGen.PlaceTile(x, ceiling + 1, TileID.Crystals, mute: true, forced: true, style: WorldGen.genRand.Next(3));
				else if (x % 9 == 0 && !(x >= left - 1 && x <= left + Width))
					WorldGen.PlaceObject(x, ceiling + 1, TileID.HangingLanterns, mute: true, style: 7);
			}

			// Crystal shards on the floor at the chamber sides.
			for (int x = x0; x <= x1; x++) {
				if (x >= left - 1 && x <= left + Width)
					continue;
				if (Main.tile[x, floor].HasTile && !Main.tile[x, floor - 1].HasTile && WorldGen.genRand.NextBool(4))
					WorldGen.PlaceTile(x, floor - 1, TileID.Crystals, mute: true, forced: true, style: WorldGen.genRand.Next(3));
			}
		}

		private static void PlaceEntrance(int left, int top)
		{
			ushort type = (ushort)ModContent.TileType<UndergroundCasinoFacade>();
			for (int dx = 0; dx < Width; dx++) {
				for (int dy = 0; dy < Height; dy++) {
					Tile tile = Main.tile[left + dx, top + dy];
					tile.ClearTile();
					tile.HasTile = true;
					tile.TileType = type;
					tile.TileFrameX = (short)(dx * 18);
					tile.TileFrameY = (short)(dy * 18);
				}
			}
		}
	}
}
