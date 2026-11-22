using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;

namespace CasinoWorld.Common.Systems
{
	/// <summary>Generates the casinos during world creation and remembers where they are.</summary>
	public class CasinoWorldSystem : ModSystem
	{
		/// <summary>Top-left tile of the surface casino facade, or (-1, -1) if the world has none.</summary>
		public static Point SurfaceCasinoPosition = new(-1, -1);

		public static bool HasSurfaceCasino => SurfaceCasinoPosition.X >= 0;

		// The surface casino is placed by the player with the Casino Blueprint, so world creation
		// does not generate it. GenerateSurfaceCasino stays for a future "generate in world" option;
		// the hardmode underground casino will use the same site search.
		private void GenerateSurfaceCasino(GenerationProgress progress, GameConfiguration configuration)
		{
			progress.Message = Language.GetTextValue("Mods.CasinoWorld.WorldGen.SurfaceCasino");
			if (SurfaceCasinoStructure.TryFindSite(out int left, out int groundY))
				SurfaceCasinoStructure.Build(left, groundY);
			else
				Mod.Logger.Warn("No site found for the surface casino.");
		}

		public override void ClearWorld()
		{
			SurfaceCasinoPosition = new Point(-1, -1);
		}

		public override void SaveWorldData(TagCompound tag)
		{
			if (HasSurfaceCasino) {
				tag["surfaceCasinoX"] = SurfaceCasinoPosition.X;
				tag["surfaceCasinoY"] = SurfaceCasinoPosition.Y;
			}
		}

		public override void LoadWorldData(TagCompound tag)
		{
			if (tag.ContainsKey("surfaceCasinoX"))
				SurfaceCasinoPosition = new Point(tag.GetInt("surfaceCasinoX"), tag.GetInt("surfaceCasinoY"));
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(SurfaceCasinoPosition.X);
			writer.Write(SurfaceCasinoPosition.Y);
		}

		public override void NetReceive(BinaryReader reader)
		{
			SurfaceCasinoPosition = new Point(reader.ReadInt32(), reader.ReadInt32());
		}
	}
}
