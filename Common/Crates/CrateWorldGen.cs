using System.Collections.Generic;
using CasinoWorld.Content.Items.Crates;
using CasinoWorld.Content.Tiles;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CasinoWorld.Common.Crates
{
	/// <summary>
	/// Puts crates into the world: Lucky Crates lie on cave floors and sometimes sit in chests.
	/// High Roller Crates are scattered through the caverns when the underground casino opens.
	/// Counts scale with world width (small world 4200 tiles: 18 crates, large 8400: 36).
	/// </summary>
	public class CrateWorldGen : ModSystem
	{
		private const float LuckyCratesPer1000Tiles = 4.3f;
		private const float HighRollerCratesPer1000Tiles = 3.0f;

		public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
		{
			int index = tasks.FindIndex(pass => pass.Name == "Pots");
			if (index < 0)
				index = tasks.Count - 1;
			tasks.Insert(index + 1, new PassLegacy("Casino World: Lucky Crates", (progress, _) => {
				progress.Message = Language.GetTextValue("Mods.CasinoWorld.WorldGen.Crates");
				Scatter(ModContent.TileType<LuckyCrateTile>(), (int)(Main.maxTilesX / 1000f * LuckyCratesPer1000Tiles),
					(int)Main.worldSurface + 20, Main.maxTilesY - 220);
			}));
		}

		/// <summary>
		/// Chance (in percent) that a generated chest holds a Lucky Crate, by chest type:
		/// plain wooden chests 15 %, gold chests 35 %, locked dungeon chests 30 %, other biome
		/// chests (ivy, ice, water, sky, shadow and so on) 25 %. Gold chests may hold two.
		/// </summary>
		private static int ChestCrateChance(Chest chest, out int count)
		{
			count = 1;
			Tile tile = Main.tile[chest.x, chest.y];
			if (tile.TileType != TileID.Containers && tile.TileType != TileID.Containers2)
				return 0;
			int style = tile.TileFrameX / 36;
			if (tile.TileType == TileID.Containers2)
				return 25;
			switch (style) {
				case 0:   // wooden
					return 15;
				case 1:   // gold
					count = WorldGen.genRand.NextBool(4) ? 2 : 1;
					return 35;
				case 2:   // locked gold (dungeon)
					return 30;
				default:
					return 25;
			}
		}

		public override void PostWorldGen()
		{
			int crate = ModContent.ItemType<LuckyCrate>();
			foreach (Chest chest in Main.chest) {
				if (chest == null)
					continue;
				int chance = ChestCrateChance(chest, out int count);
				if (chance <= 0 || WorldGen.genRand.Next(100) >= chance)
					continue;
				for (int k = 0; k < Chest.maxItems; k++) {
					if (chest.item[k].IsAir) {
						chest.item[k].SetDefaults(crate);
						chest.item[k].stack = count;
						break;
					}
				}
			}
		}

		/// <summary>Scatters High Roller Crates; called once when the underground casino opens.</summary>
		public static void ScatterHighRollerCrates()
		{
			Scatter(ModContent.TileType<HighRollerCrateTile>(), (int)(Main.maxTilesX / 1000f * HighRollerCratesPer1000Tiles),
				(int)Main.rockLayer + 20, Main.maxTilesY - 220);
		}

		/// <summary>Places up to <paramref name="count"/> 2x2 crates on solid floors with free space above.</summary>
		public static int Scatter(int tileType, int count, int minY, int maxY)
		{
			int placed = 0;
			for (int attempt = 0; attempt < count * 400 && placed < count; attempt++) {
				int x = WorldGen.genRand.Next(60, Main.maxTilesX - 60);
				int y = WorldGen.genRand.Next(minY, maxY);
				if (!IsFloor(x, y))
					continue;
				if (WorldGen.PlaceObject(x, y - 1, tileType, mute: true)) {
					placed++;
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendTileSquare(-1, x, y - 2, 3, 3);
				}
			}
			return placed;
		}

		private static bool IsFloor(int x, int y)
		{
			for (int dx = 0; dx < 2; dx++) {
				Tile ground = Main.tile[x + dx, y];
				if (!ground.HasTile || !Main.tileSolid[ground.TileType] || Main.tileSolidTop[ground.TileType] || ground.IsHalfBlock || ground.Slope != SlopeType.Solid)
					return false;
				for (int dy = 1; dy <= 2; dy++) {
					Tile above = Main.tile[x + dx, y - dy];
					if (above.HasTile || above.LiquidAmount > 0)
						return false;
				}
			}
			return true;
		}
	}
}
