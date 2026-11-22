using CasinoWorld.Common.Games.Slots;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CasinoWorld.Content.Tiles
{
	/// <summary>Slot machine cabinet, 3x4 tiles. Right-click opens the slot window. The top light and reels glow.</summary>
	public class SlotMachine : ModTile
	{
		public const int Width = 3;
		public const int Height = 4;

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
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16, 16 };
			TileObjectData.newTile.Origin = new Point16(0, Height - 1);
			TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, Width, 0);
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.addTile(Type);

			AddMapEntry(new Color(110, 50, 160), CreateMapEntryName());
			DustType = DustID.PurpleTorch;
		}

		public override void Unload()
		{
			glow = null;
			symbols = null;
		}

		public static Point16 TopLeft(int i, int j)
		{
			Tile tile = Main.tile[i, j];
			return new Point16(i - tile.TileFrameX / 18 % Width, j - tile.TileFrameY / 18 % Height);
		}

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

		public override bool RightClick(int i, int j)
		{
			ModContent.GetInstance<SlotSystem>().Open(TopLeft(i, j));
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = -1;
			player.cursorItemIconText = Language.GetTextValue("Mods.CasinoWorld.UI.Slots.Hover");
		}

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			if (Main.tile[i, j].TileFrameY / 18 % Height == 0) {
				r = 0.6f;
				g = 0.2f;
				b = 0.5f;
			}
		}

		private static Asset<Texture2D> symbols;

		// Reel window centres inside the 48x64 sprite (from the Blender model: x 0.92/1.42/1.92, z 2.4 tiles).
		private static readonly Vector2[] ReelCentres = { new(14.7f, 25.6f), new(22.7f, 25.6f), new(30.7f, 25.6f) };

		public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref Terraria.DataStructures.TileDrawInfo drawData)
		{
			if (drawData.tileFrameX % 54 == 0 && drawData.tileFrameY % 72 == 0)
				Main.instance.TilesRenderer.AddSpecialLegacyPoint(i, j);
		}

		/// <summary>
		/// Idle attract mode: the reels in the window flick through symbols every so often, each machine on
		/// its own rhythm. Drawn after all tiles so the glow mask does not cover it.
		/// </summary>
		public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
		{
			symbols ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/SlotSymbols");
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			Vector2 origin = new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen;
			float time = Main.GlobalTimeWrappedHourly;
			int seed = i * 7 + j * 13;
			bool spinning = (time * 0.6f + seed * 0.37f) % 4f < 0.8f;
			for (int k = 0; k < 3; k++) {
				int symbol = spinning
					? (int)(time * 18f + k * 2 + seed) % 7
					: (int)(time * 0.6f + seed * 0.37f + k * 1.3f) % 7;
				spriteBatch.Draw(symbols.Value, origin + ReelCentres[k], new Rectangle(symbol * 64, 0, 64, 64), Color.White, 0f, new Vector2(32), 0.1f, SpriteEffects.None, 0f);
			}
		}

		public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Tile tile = Main.tile[i, j];
			glow ??= ModContent.Request<Texture2D>(Texture + "_Glow");
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			Vector2 position = new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen;
			spriteBatch.Draw(glow.Value, position, new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16), Color.White);
		}
	}
}
