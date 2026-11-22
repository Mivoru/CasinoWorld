using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CasinoWorld.Content.Tiles
{
	/// <summary>Red casino carpet with a gold edge, used as the hall floor.</summary>
	public class CasinoCarpet : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileSolid[Type] = true;
			Main.tileBlockLight[Type] = true;
			AddMapEntry(new Color(150, 24, 36));
			DustType = DustID.RedTorch;
		}
	}

	/// <summary>
	/// Big lit marquee sign (16x5) hung on the casino wall. Rendered in Blender with a pixel-font CASINO;
	/// bulbs and letters glow and light up the hall.
	/// </summary>
	public partial class CasinoSignTile : ModTile
	{
		public const int Width = 16;
		public const int Height = 5;

		private static Asset<Texture2D> glow;

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = false;
			TileObjectData.newTile.Width = Width;
			TileObjectData.newTile.Height = Height;
			TileObjectData.newTile.CoordinateWidth = 16;
			TileObjectData.newTile.CoordinatePadding = 2;
			TileObjectData.newTile.CoordinateHeights = Enumerable.Repeat(16, Height).ToArray();
			TileObjectData.newTile.Origin = new Point16(0, 0);
			TileObjectData.newTile.AnchorBottom = AnchorData.Empty;
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(222, 172, 48));
		}

		public override void Unload()
		{
			glow = null;
		}

		/// <summary>Places the sign with its top-left tile at (left, top), ignoring anchors (world generation).</summary>
		public static void Place(int left, int top)
		{
			ushort type = (ushort)ModContent.TileType<CasinoSignTile>();
			for (int dx = 0; dx < Width; dx++) {
				for (int dy = 0; dy < Height; dy++) {
					Tile tile = Main.tile[left + dx, top + dy];
					tile.HasTile = true;
					tile.TileType = type;
					tile.TileFrameX = (short)(dx * 18);
					tile.TileFrameY = (short)(dy * 18);
				}
			}
		}

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			Tile tile = Main.tile[i, j];
			int x = tile.TileFrameX / 18, y = tile.TileFrameY / 18;
			if (x >= Width || y >= Height)
				return;
			float s = LightMap[y, x] / 255f;
			r = s * 1.05f;
			g = s * 0.8f;
			b = s * 0.45f;
		}

		public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Tile tile = Main.tile[i, j];
			glow ??= ModContent.Request<Texture2D>(Texture + "_Glow");
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			Vector2 position = new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen;
			// Chasing bulbs: brightness runs along the sign.
			float chase = 0.8f + 0.2f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 6f - i * 0.9f);
			spriteBatch.Draw(glow.Value, position, new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16), Color.White * chase);
		}
	}
}
