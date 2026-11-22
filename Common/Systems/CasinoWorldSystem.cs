using System.Collections.Generic;
using System.IO;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using SubworldLibrary;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CasinoWorld.Common.Systems
{
	/// <summary>
	/// Remembers where the casino entrances are, and opens the underground casino
	/// the first time the world is in hardmode.
	/// </summary>
	public class CasinoWorldSystem : ModSystem
	{
		// Ticks between attempts when no underground site was found (the world may change).
		private const int RetryTicks = 60 * 60 * 5;

		/// <summary>Top-left tile of the surface casino facade, or (-1, -1) if the world has none.</summary>
		public static Point SurfaceCasinoPosition = new(-1, -1);

		/// <summary>Top-left tile of the underground casino entrance, or (-1, -1) if it has not opened yet.</summary>
		public static Point UndergroundCasinoPosition = new(-1, -1);

		public static bool HasSurfaceCasino => SurfaceCasinoPosition.X >= 0;

		public static bool HasUndergroundCasino => UndergroundCasinoPosition.X >= 0;

		private int retryTimer;

		/// <summary>Every entrance in the world: its tile and its top-left position.</summary>
		public static IEnumerable<(CasinoEntrance entrance, Point topLeft)> Entrances()
		{
			if (HasSurfaceCasino)
				yield return (ModContent.GetInstance<CasinoFacade>(), SurfaceCasinoPosition);
			if (HasUndergroundCasino)
				yield return (ModContent.GetInstance<UndergroundCasinoFacade>(), UndergroundCasinoPosition);
		}

		public override void ClearWorld()
		{
			SurfaceCasinoPosition = new Point(-1, -1);
			UndergroundCasinoPosition = new Point(-1, -1);
			retryTimer = 0;
		}

		public override void PostUpdateWorld()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || !Main.hardMode || HasUndergroundCasino || SubworldSystem.AnyActive())
				return;
			if (retryTimer-- > 0)
				return;

			if (UndergroundCasinoStructure.TryFindSite(out int left, out int top)) {
				UndergroundCasinoStructure.Build(left, top);
				Crates.CrateWorldGen.ScatterHighRollerCrates();
				AnnounceUndergroundCasino();
			}
			else {
				Mod.Logger.Warn("No site found for the underground casino; trying again later.");
				retryTimer = RetryTicks;
			}
		}

		private static void AnnounceUndergroundCasino()
		{
			Point pos = UndergroundCasinoPosition;
			int dx = pos.X - Main.spawnTileX;
			int depthFeet = (int)((pos.Y - Main.worldSurface) * 2);
			string side = dx >= 0 ? "Mods.CasinoWorld.WorldGen.East" : "Mods.CasinoWorld.WorldGen.West";
			var text = NetworkText.FromKey("Mods.CasinoWorld.WorldGen.UndergroundOpened",
				System.Math.Abs(dx) * 2, NetworkText.FromKey(side), depthFeet);
			ChatHelper.BroadcastChatMessage(text, new Color(255, 120, 220));
		}

		public override void SaveWorldData(TagCompound tag)
		{
			if (HasSurfaceCasino) {
				tag["surfaceCasinoX"] = SurfaceCasinoPosition.X;
				tag["surfaceCasinoY"] = SurfaceCasinoPosition.Y;
			}
			if (HasUndergroundCasino) {
				tag["undergroundCasinoX"] = UndergroundCasinoPosition.X;
				tag["undergroundCasinoY"] = UndergroundCasinoPosition.Y;
			}
		}

		public override void LoadWorldData(TagCompound tag)
		{
			if (tag.ContainsKey("surfaceCasinoX"))
				SurfaceCasinoPosition = new Point(tag.GetInt("surfaceCasinoX"), tag.GetInt("surfaceCasinoY"));
			if (tag.ContainsKey("undergroundCasinoX"))
				UndergroundCasinoPosition = new Point(tag.GetInt("undergroundCasinoX"), tag.GetInt("undergroundCasinoY"));
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(SurfaceCasinoPosition.X);
			writer.Write(SurfaceCasinoPosition.Y);
			writer.Write(UndergroundCasinoPosition.X);
			writer.Write(UndergroundCasinoPosition.Y);
		}

		public override void NetReceive(BinaryReader reader)
		{
			SurfaceCasinoPosition = new Point(reader.ReadInt32(), reader.ReadInt32());
			UndergroundCasinoPosition = new Point(reader.ReadInt32(), reader.ReadInt32());
		}
	}
}
