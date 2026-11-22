using System.Collections.Generic;
using CasinoWorld.Content.NPCs;
using CasinoWorld.Content.NPCs.People;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Systems
{
	/// <summary>
	/// Map icons inside the casinos: staff (what they run) and elevators (up or down), so players can
	/// find the tables, the bar and the lifts on the full-screen map and the minimap.
	/// </summary>
	public class CasinoMapLayer : ModMapLayer
	{
		private const int IconSize = 22;
		private static Asset<Texture2D> icons;

		private static readonly Dictionary<int, int> StaffIcon = new();

		public override void Load()
		{
			icons = ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/MapIcons");
		}

		public override void Unload() => icons = null;

		private static int IconFor(NPC npc) => npc.ModNPC switch {
			RouletteCroupier => 0,
			BlackjackDealer => 1,
			PrizeCashier => 2,
			Bartender => 3,
			Showgirl => 4,
			PoleDancer => 5,
			Togi => 6,
			_ => -1,
		};

		public override void Draw(ref MapOverlayDrawContext context, ref string text)
		{
			if (!CasinoTravel.InCasino)
				return;
			Texture2D tex = icons.Value;

			foreach (NPC npc in Main.ActiveNPCs) {
				int icon = IconFor(npc);
				if (icon < 0)
					continue;
				var frame = new SpriteFrame(9, 1, (byte)icon, 0);
				if (context.Draw(tex, npc.Center / 16f, Color.White, frame, 1f, 1.4f, Alignment.Center).IsMouseOver)
					text = npc.GivenOrTypeName;
			}

			// Elevators: scan the casino once per draw (the hall is small and the world is tiny).
			int up = ModContent.TileType<CasinoElevatorUp>(), down = ModContent.TileType<CasinoElevatorDown>();
			for (int x = 0; x < Main.maxTilesX; x++) {
				for (int y = 0; y < Main.maxTilesY; y++) {
					Tile tile = Main.tile[x, y];
					if (!tile.HasTile || tile.TileFrameX != 0 || tile.TileFrameY != 0 || (tile.TileType != up && tile.TileType != down))
						continue;
					bool isUp = tile.TileType == up;
					var frame = new SpriteFrame(9, 1, (byte)(isUp ? 7 : 8), 0);
					if (context.Draw(tex, new Vector2(x + 1.5f, y + 2f), Color.White, frame, 0.9f, 1.3f, Alignment.Center).IsMouseOver)
						text = Language.GetTextValue(isUp ? "Mods.CasinoWorld.UI.ElevatorUp" : "Mods.CasinoWorld.UI.ElevatorDown");
				}
			}
		}
	}
}
