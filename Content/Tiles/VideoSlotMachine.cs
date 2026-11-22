using CasinoWorld.Common.Games.VideoSlots;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.ObjectInteractions;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CasinoWorld.Content.Tiles
{
	/// <summary>
	/// Themed video slot cabinet (3x4). Right-click opens the 5x3 video slot window. The marquee shows the
	/// theme's signature item and the reel window cycles the machine's symbols.
	/// </summary>
	public abstract class VideoSlotMachine : ModTile
	{
		public const int Width = 3, Height = 4;

		public abstract SlotDefinition Definition { get; }

		private Asset<Texture2D> glow;

		public override string Texture => "CasinoWorld/Content/Tiles/VideoSlot" + Definition.Key;

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
			AddMapEntry(SlotThemes.Accent(Definition.Key), Language.GetText("Mods.CasinoWorld.UI.VideoSlots.Title" + Definition.Key));
		}

		public static Point16 TopLeft(int i, int j)
		{
			Tile tile = Main.tile[i, j];
			return new Point16(i - tile.TileFrameX / 18 % Width, j - tile.TileFrameY / 18 % Height);
		}

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

		public override bool RightClick(int i, int j)
		{
			ModContent.GetInstance<VideoSlotSystem>().OpenMachine(TopLeft(i, j), Type, Definition);
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = -1;
			player.cursorItemIconText = Language.GetTextValue("Mods.CasinoWorld.UI.VideoSlots.Title" + Definition.Key);
		}

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			if (Main.tile[i, j].TileFrameY / 18 % Height == 0) {
				Color c = SlotThemes.Accent(Definition.Key);
				r = c.R / 400f;
				g = c.G / 400f;
				b = c.B / 400f;
			}
		}

		public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Tile tile = Main.tile[i, j];
			glow ??= ModContent.Request<Texture2D>(Texture + "_Glow");
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			spriteBatch.Draw(glow.Value, new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen, new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16), Color.White);
		}

		public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
		{
			if (drawData.tileFrameX % 54 == 0 && drawData.tileFrameY % 72 == 0)
				Main.instance.TilesRenderer.AddSpecialLegacyPoint(i, j);
		}

		public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			Vector2 origin = new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen;
			int[] icons = SlotThemes.Icons(Definition.Key);
			float t = Main.GlobalTimeWrappedHourly;
			// Signature item on the marquee, gently bobbing.
			DrawIcon(spriteBatch, icons[Definition.Scatter], origin + new Vector2(24, 8 + (float)System.Math.Sin(t * 2 + i) * 1.5f), 14f);
			// Reel window: three symbols cycling now and then.
			int seed = i * 7 + j * 13;
			bool spinning = (t * 0.6f + seed * 0.37f) % 4f < 0.8f;
			for (int k = 0; k < 3; k++) {
				int s = spinning ? (int)(t * 18f + k * 2 + seed) % Definition.SymbolCount : (int)(t * 0.6f + seed * 0.37f + k * 1.3f) % Definition.SymbolCount;
				DrawIcon(spriteBatch, icons[s], origin + new Vector2(14.7f + k * 8f, 25.6f), 8f);
			}
		}

		private static void DrawIcon(SpriteBatch sb, int type, Vector2 center, float size)
		{
			Main.instance.LoadItem(type);
			Texture2D tex = TextureAssets.Item[type].Value;
			Rectangle frame = Main.itemAnimations[type] != null ? Main.itemAnimations[type].GetFrame(tex) : tex.Frame();
			float scale = size / System.Math.Max(frame.Width, frame.Height);
			sb.Draw(tex, center, frame, Color.White, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
		}
	}

	public class VideoSlotLunarGods : VideoSlotMachine { public override SlotDefinition Definition => SlotDefinitions.LunarGods; }

	public class VideoSlotAngler : VideoSlotMachine { public override SlotDefinition Definition => SlotDefinitions.Angler; }

	public class VideoSlotBoreal : VideoSlotMachine { public override SlotDefinition Definition => SlotDefinitions.Boreal; }

	public class VideoSlotBloodMoon : VideoSlotMachine { public override SlotDefinition Definition => SlotDefinitions.BloodMoon; }
}
