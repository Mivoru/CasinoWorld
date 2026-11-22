using System;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Systems
{
	/// <summary>
	/// Finds a site for the surface casino and builds it: flattens the ground,
	/// lays a foundation and places the facade tile.
	/// </summary>
	public static class SurfaceCasinoStructure
	{
		private const int Width = CasinoFacade.Width;
		private const int Height = CasinoFacade.Height;

		// Free space kept on each side of the facade.
		private const int Margin = 4;

		// Distance from the world spawn, in tiles.
		private const int MinDistance = 150;
		private const int MaxDistance = 300;

		// Largest height difference of the ground under the site that still counts as buildable.
		private const int MaxRoughness = 8;

		// Ocean width kept clear at both world edges.
		private const int OceanClearance = 380;

		/// <summary>Tries to find a site near spawn. Returns the left x and the ground row y.</summary>
		public static bool TryFindSite(out int left, out int groundY)
		{
			int[] directions = WorldGen.genRand.NextBool() ? new[] { 1, -1 } : new[] { -1, 1 };

			// First pass prefers the target distance, second pass accepts a wider range.
			foreach ((int min, int max) in new[] { (MinDistance, MaxDistance), (80, 450) }) {
				foreach (int dir in directions) {
					for (int dist = min; dist <= max; dist += 3) {
						int candidate = Main.spawnTileX + dir * dist - (dir < 0 ? Width : 0);
						if (IsGoodSite(candidate, out groundY)) {
							left = candidate;
							return true;
						}
					}
				}
			}

			left = groundY = -1;
			return false;
		}

		private static bool IsGoodSite(int left, out int groundY)
		{
			groundY = -1;
			int x0 = left - Margin;
			int x1 = left + Width + Margin;
			if (x0 < OceanClearance || x1 > Main.maxTilesX - OceanClearance)
				return false;

			int minY = int.MaxValue, maxY = int.MinValue;
			int[] heights = new int[x1 - x0];
			for (int x = x0; x < x1; x++) {
				int y = FindSurface(x);
				if (y < 0)
					return false;

				Tile ground = Main.tile[x, y];
				if (IsForbiddenGround(ground.TileType))
					return false;

				heights[x - x0] = y;
				minY = Math.Min(minY, y);
				maxY = Math.Max(maxY, y);
			}

			if (maxY - minY > MaxRoughness)
				return false;

			Array.Sort(heights);
			groundY = heights[heights.Length / 2];

			// No liquid and no structures (dungeon, living trees, other mods' tiles) in the building area.
			for (int x = x0; x < x1; x++) {
				for (int y = groundY - Height - 4; y <= groundY + 2; y++) {
					Tile tile = Main.tile[x, y];
					if (tile.LiquidAmount > 0)
						return false;
					if (tile.HasTile && IsProtectedStructure(tile.TileType))
						return false;
				}
			}

			return true;
		}

		/// <summary>Top solid tile in the column, or -1 when none is found above the cavern layer.</summary>
		private static int FindSurface(int x)
		{
			for (int y = 40; y < Main.worldSurface + 50 && y < Main.maxTilesY - 1; y++) {
				Tile tile = Main.tile[x, y];
				if (tile.LiquidAmount > 0)
					return -1;
				if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
					return y;
			}
			return -1;
		}

		private static bool IsForbiddenGround(ushort type) => type switch {
			TileID.Ebonstone or TileID.CorruptGrass or TileID.Ebonsand or TileID.CorruptIce
				or TileID.Crimstone or TileID.CrimsonGrass or TileID.Crimsand or TileID.FleshIce
				or TileID.Sand or TileID.HardenedSand or TileID.Sandstone
				or TileID.Cloud or TileID.RainCloud => true,
			_ => false,
		};

		private static bool IsProtectedStructure(ushort type)
		{
			if (type >= TileID.Count)
				return true;
			if (Main.tileDungeon[type])
				return true;
			return type is TileID.LivingWood or TileID.LeafBlock or TileID.Containers or TileID.Containers2
				or TileID.Heart or TileID.ShadowOrbs or TileID.DemonAltar or TileID.TeleportationPylon;
		}

		/// <summary>Builds the casino with its facade bottom row resting on groundY - 1.</summary>
		public static void Build(int left, int groundY)
		{
			int x0 = left - Margin;
			int x1 = left + Width + Margin;
			int top = groundY - Height - 6;

			// Clear the space above the ground, including background walls, trees and grass.
			for (int x = x0; x < x1; x++) {
				for (int y = top; y < groundY; y++) {
					Tile tile = Main.tile[x, y];
					tile.ClearEverything();
				}
			}

			// Fill gaps under the site so the building does not float.
			for (int x = x0; x < x1; x++) {
				for (int y = groundY; y < groundY + 30; y++) {
					Tile tile = Main.tile[x, y];
					if (tile.HasTile && Main.tileSolid[tile.TileType])
						break;
					tile.ClearTile();
					tile.HasTile = true;
					tile.TileType = TileID.Dirt;
					tile.LiquidAmount = 0;
				}
			}

			// Grass on the margins, stone foundation under the facade.
			for (int x = x0; x < x1; x++) {
				Tile tile = Main.tile[x, groundY];
				bool underFacade = x >= left - 1 && x <= left + Width;
				tile.HasTile = true;
				tile.TileType = underFacade ? TileID.GrayBrick : TileID.Grass;
				tile.Slope = SlopeType.Solid;
				tile.IsHalfBlock = false;
			}

			PlaceFacade(left, groundY - Height);

			WorldGen.RangeFrame(x0 - 2, top - 2, x1 + 2, groundY + 32);
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendTileSquare(-1, x0 - 2, top - 2, x1 - x0 + 4, groundY + 34 - top);

			CasinoWorldSystem.SurfaceCasinoPosition = new Point(left, groundY - Height);
		}

		private static void PlaceFacade(int left, int top)
		{
			ushort type = (ushort)ModContent.TileType<CasinoFacade>();
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
