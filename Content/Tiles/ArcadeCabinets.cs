using System;
using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Games.Arcade;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CasinoWorld.Content.Tiles
{
	/// <summary>Arcade game cabinet (3x4) with an animated screen and blinking marquee. Right-click opens its game.</summary>
	public abstract class ArcadeCabinet : ModTile
	{
		public const int Width = 3, Height = 4;

		protected abstract ArcadeGame Game { get; }

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
			AddMapEntry(Game.Accent, CreateMapEntryName());
			DustType = DustID.PurpleTorch;
		}

		private static Point16 TopLeft(int i, int j)
		{
			Tile tile = Main.tile[i, j];
			return new Point16(i - tile.TileFrameX / 18 % Width, j - tile.TileFrameY / 18 % Height);
		}

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

		public override bool RightClick(int i, int j)
		{
			Game.Open(TopLeft(i, j));
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = -1;
			player.cursorItemIconText = Language.GetTextValue($"Mods.CasinoWorld.UI.{Game.Key}.Hover");
		}

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			if (Main.tile[i, j].TileFrameY / 18 % Height > 1)
				return;
			Vector3 c = Game.Accent.ToVector3() * 0.6f;
			(r, g, b) = (c.X, c.Y, c.Z);
		}

		public override void NearbyEffects(int i, int j, bool closer)
		{
			if (!closer || Main.tile[i, j].TileFrameX != 0 || Main.tile[i, j].TileFrameY != 0)
				return;
			if (Main.rand.NextBool(60))
				CasinoFx.Spark(new Vector2(i * 16 + Main.rand.Next(48), j * 16 + Main.rand.Next(16)), new Vector2(0, -0.4f), Game.Accent, 0.7f);
			if (Main.rand.NextBool(2400))
				SoundEngine.PlaySound(CasinoSounds.ReelStop with { Volume = 0.2f, Pitch = 0.5f }, new Vector2(i * 16 + 24, j * 16 + 24));
		}

		public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
		{
			if (drawData.tileFrameX == 0 && drawData.tileFrameY == 0)
				Main.instance.TilesRenderer.AddSpecialLegacyPoint(i, j);
		}

		public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			Vector2 origin = new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen;
			float t = Main.GlobalTimeWrappedHourly + (i * 7 + j * 3) % 10;
			for (int k = 0; k < 7; k++)   // marquee bulbs
				Px(spriteBatch, origin + new Vector2(6 + k * 5, 3), 3, 3, (k + (int)(t * 6f)) % 3 == 0 ? Color.White : Game.Accent);
			DrawScreen(spriteBatch, origin + new Vector2(8, 16), t);
		}

		protected static void Px(SpriteBatch sb, Vector2 at, int w, int h, Color c) => sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)at.X, (int)at.Y, w, h), c);

		/// <summary>Attract mode on the 32x22 screen.</summary>
		protected abstract void DrawScreen(SpriteBatch sb, Vector2 at, float t);
	}

	public class PlinkoCabinet : ArcadeCabinet
	{
		protected override ArcadeGame Game => ModContent.GetInstance<Plinko>();

		protected override void DrawScreen(SpriteBatch sb, Vector2 at, float t)
		{
			for (int row = 0; row < 5; row++)
				for (int c = 0; c <= row; c++)
					Px(sb, at + new Vector2(15 + (c - row / 2f) * 6f, 2 + row * 4), 2, 2, new Color(170, 200, 255));
			int seed = (int)(t * 0.5f);
			float f = t * 0.5f % 1f * 5f;
			int r = (int)f;
			float x = 0;
			for (int k = 0; k < r; k++)
				x += (seed * 31 + k * 17) % 2 == 0 ? 3 : -3;
			float nextX = x + ((seed * 31 + r * 17) % 2 == 0 ? 3 : -3);
			Px(sb, at + new Vector2(14 + MathHelper.Lerp(x, nextX, f - r), f * 4), 4, 4, new Color(255, 210, 80));
		}
	}

	public class DerbyCabinet : ArcadeCabinet
	{
		protected override ArcadeGame Game => ModContent.GetInstance<SlimeDerby>();

		private static readonly Color[] colors = { new(110, 230, 110), new(90, 150, 255), new(255, 90, 90) };

		protected override void DrawScreen(SpriteBatch sb, Vector2 at, float t)
		{
			Px(sb, at, 32, 22, new Color(34, 84, 44));
			for (int k = 0; k < 3; k++) {
				float x = (t * 0.25f + (float)Math.Sin(t * 1.3f + k * 2f) * 0.08f) % 1f * 26f;
				float hop = Math.Abs((float)Math.Sin(t * 6f + k)) * 2f;
				Px(sb, at + new Vector2(Math.Max(0, x), 2 + k * 7 - hop + 1), 6, 4, colors[k]);
			}
			for (int q = 0; q < 8; q++)
				Px(sb, at + new Vector2(29, q * 3 - 1 + 1), 2, 2, q % 2 == 0 ? Color.White : Color.Black);
		}
	}

	public class HiLoCabinet : ArcadeCabinet
	{
		protected override ArcadeGame Game => ModContent.GetInstance<HigherLower>();

		protected override void DrawScreen(SpriteBatch sb, Vector2 at, float t)
		{
			float flip = Math.Abs((float)Math.Cos(t * 2f));
			int w = Math.Max(1, (int)(12 * flip));
			Px(sb, at + new Vector2(10 - w / 2, 3), w, 16, new Color(250, 246, 235));
			if (w > 6)
				Px(sb, at + new Vector2(8, 8), 4, 5, (int)(t / 1.57f) % 2 == 0 ? new Color(210, 40, 50) : new Color(30, 30, 36));
			int bob = (int)(Math.Sin(t * 5f) * 2);
			for (int k = 0; k < 4; k++) {
				Px(sb, at + new Vector2(24 - k, 4 + k + bob), k * 2 + 1, 1, new Color(90, 200, 255));
				Px(sb, at + new Vector2(24 - k, 18 - k - bob), k * 2 + 1, 1, new Color(255, 100, 100));
			}
		}
	}
}
