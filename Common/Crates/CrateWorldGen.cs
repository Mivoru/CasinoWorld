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
		private const int ChestChanceOneIn = 4;

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

		public override void PostWorldGen()
		{
			int crate = ModContent.ItemType<LuckyCrate>();
			foreach (Chest chest in Main.chest) {
				if (chest == null || chest.y < Main.worldSurface || WorldGen.genRand.Next(ChestChanceOneIn) != 0)
					continue;
				for (int k = 0; k < Chest.maxItems; k++) {
					if (chest.item[k].IsAir) {
						chest.item[k].SetDefaults(crate);
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
