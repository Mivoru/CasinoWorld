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
	/// Puts crates into the world: Lucky Crates lie on cave floors and sit in every chest.
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
		/// How many Lucky Crates a generated chest holds: every vanilla chest gets one (wooden, gold,
		/// dungeon, every biome chest). Gold and locked dungeon chests hold two one time in four.
		/// Returns 0 for anything that is not a vanilla chest.
		/// </summary>
		private static int ChestCrateCount(Chest chest)
		{
			Tile tile = Main.tile[chest.x, chest.y];
			if (tile.TileType != TileID.Containers && tile.TileType != TileID.Containers2)
				return 0;
			int style = tile.TileFrameX / 36;
			bool rich = tile.TileType == TileID.Containers && (style == 1 || style == 2);
			return rich && WorldGen.genRand.NextBool(4) ? 2 : 1;
		}

		private static bool HasCrate(Chest chest, int crate)
		{
			foreach (Item item in chest.item) {
				if (item != null && item.type == crate)
					return true;
			}
			return false;
		}

		/// <summary>Puts Lucky Crates into every chest (skips chests that already have one or are full).</summary>
		private static int FillChests()
		{
			int crate = ModContent.ItemType<LuckyCrate>();
			int filled = 0;
			foreach (Chest chest in Main.chest) {
				if (chest == null || HasCrate(chest, crate))
					continue;
				int count = ChestCrateCount(chest);
				if (count <= 0)
					continue;
				for (int k = 0; k < Chest.maxItems; k++) {
					if (chest.item[k].IsAir) {
						chest.item[k].SetDefaults(crate);
						chest.item[k].stack = count;
						filled++;
						break;
					}
				}
			}
			return filled;
		}

		public override void PostWorldGen()
		{
			FillChests();
			chestsFilled = true;
		}

		// Worlds made before this rule get their chests filled once, on the first load.
		private bool chestsFilled;

		public override void ClearWorld() => chestsFilled = false;

		public override void LoadWorldData(Terraria.ModLoader.IO.TagCompound tag) => chestsFilled = tag.ContainsKey("chestCrates");

		public override void SaveWorldData(Terraria.ModLoader.IO.TagCompound tag)
		{
			if (chestsFilled)
				tag["chestCrates"] = true;
		}

		public override void PostUpdateWorld()
		{
			if (chestsFilled || Main.netMode == NetmodeID.MultiplayerClient || Systems.CasinoTravel.InCasino)
				return;
			chestsFilled = true;
			FillChests();
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
