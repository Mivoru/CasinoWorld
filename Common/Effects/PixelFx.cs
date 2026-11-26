using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Effects
{
	/// <summary>
	/// Effects in Terraria's pixel style: hand-dithered sprites (Assets/Fx, tools/pixel_fx_art.py) on the 2x2 pixel
	/// grid, animated by frames instead of being rotated or stretched. Positions are screen coordinates.
	/// </summary>
	public class PixelFx : ModSystem
	{
		private static readonly Dictionary<string, Asset<Texture2D>> cache = new();
		private static readonly int[] GlowSizes = { 12, 20, 32, 48, 72, 110 };

		public override void Unload() => cache.Clear();

		public static Texture2D Tex(string name)
		{
			if (!cache.TryGetValue(name, out Asset<Texture2D> asset))
				cache[name] = asset = ModContent.Request<Texture2D>("CasinoWorld/Assets/Fx/" + name, AssetRequestMode.ImmediateLoad);
			return asset.Value;
		}

		/// <summary>Snaps to the 2x2 pixel grid.</summary>
		public static Vector2 Snap(Vector2 v) => new((int)Math.Floor(v.X / 2f) * 2, (int)Math.Floor(v.Y / 2f) * 2);

		private static Color Additive(Color c)
		{
			c.A = 0;
			return c;
		}

		/// <summary>Dithered round glow; takes the sprite whose size is closest, never a scaled one.</summary>
		public static void Glow(SpriteBatch sb, Vector2 at, float radius, Color color, bool additive = true)
		{
			int best = GlowSizes[0];
			foreach (int s in GlowSizes)   // art diameter = radius in screen pixels
				if (Math.Abs(s - radius) < Math.Abs(best - radius))
					best = s;
			Texture2D tex = Tex("Glow" + best);
			sb.Draw(tex, Snap(at - tex.Size() / 2f), additive ? Additive(color) : color);
		}

		/// <summary>
		/// Turning sunburst. Size 1 = 48 art pixels wide, 2 = 96, 3 and more = 144. Bigger sizes are separate drawings
		/// at the same 2x2 pixel size, never a scaled-up sprite (which would turn into big blocks).
		/// </summary>
		public static void Burst(SpriteBatch sb, Vector2 at, Color color, float time, int scale = 1)
		{
			Texture2D tex = Tex(scale <= 1 ? "Burst" : scale == 2 ? "BurstBig" : "BurstHuge");
			int h = tex.Height / 8;
			var frame = new Rectangle(0, (int)(time * 10f) % 8 * h, tex.Width, h);
			sb.Draw(tex, Snap(at - new Vector2(tex.Width, h) / 2f), frame, Additive(color), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
		}

		/// <summary>Four-pointed twinkle; life runs 0..1.</summary>
		public static void Twinkle(SpriteBatch sb, Vector2 at, Color color, float life)
		{
			Texture2D tex = Tex("Twinkle");
			int h = tex.Height / 5;
			var frame = new Rectangle(0, Math.Clamp((int)(life * 5f), 0, 4) * h, tex.Width, h);
			sb.Draw(tex, Snap(at - new Vector2(tex.Width, h) / 2f), frame, Additive(color), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
		}

		/// <summary>Flame tongue standing on a point; seed shifts the frame.</summary>
		public static void Flame(SpriteBatch sb, Vector2 foot, float time, int seed, float alpha = 1f)
		{
			Texture2D tex = Tex("Flame");
			int h = tex.Height / 6;
			var frame = new Rectangle(0, ((int)(time * 12f) + seed) % 6 * h, tex.Width, h);
			sb.Draw(tex, Snap(foot - new Vector2(tex.Width / 2f, h)), frame, Color.White * alpha, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
		}

		/// <summary>Line of square pixels along a path: no rotated rectangles, hard steps like a sprite.</summary>
		public static void Line(SpriteBatch sb, Vector2 a, Vector2 b, Color color, float width)
		{
			Vector2 d = b - a;
			float length = d.Length();
			if (length < 1f)
				return;
			int size = Math.Max(2, (int)Math.Round(width / 2f) * 2);
			// Translucent lines must not overlap themselves, solid ones may and look smoother.
			bool solid = color.A > 240;
			float major = Math.Max(Math.Abs(d.X), Math.Abs(d.Y));
			float step = solid ? Math.Max(2f, size / 2f) : size;
			int count = Math.Max(1, (int)(major / step));
			Texture2D px = TextureAssets.MagicPixel.Value;
			for (int k = 0; k <= count; k++) {
				Vector2 at = Snap(a + d * (k / (float)count) - new Vector2(size / 2f));
				sb.Draw(px, new Rectangle((int)at.X, (int)at.Y, size, size), color);
			}
		}

		/// <summary>Fills a rectangle with a repeating texture, clipped, optionally scrolling by whole pixels.</summary>
		public static void Tile(SpriteBatch sb, Texture2D tex, Rectangle source, Rectangle area, Color color, int scrollX = 0, int scrollY = 0)
		{
			int w = source.Width, h = source.Height;
			int sx = ((scrollX / 2 * 2) % w + w) % w, sy = ((scrollY / 2 * 2) % h + h) % h;
			for (int y = area.Y - sy; y < area.Bottom; y += h)
				for (int x = area.X - sx; x < area.Right; x += w) {
					Rectangle dst = Rectangle.Intersect(new Rectangle(x, y, w, h), area);
					if (dst.Width > 0 && dst.Height > 0)
						sb.Draw(tex, dst, new Rectangle(source.X + dst.X - x, source.Y + dst.Y - y, dst.Width, dst.Height), color);
				}
		}

		public static void Tile(SpriteBatch sb, string name, Rectangle area, Color color, int scrollX = 0, int scrollY = 0)
		{
			Texture2D tex = Tex(name);
			Tile(sb, tex, tex.Bounds, area, color, scrollX, scrollY);
		}

		/// <summary>Dashed vertical warning that runs downwards, with a bracket on the ground.</summary>
		public static void DropWarning(SpriteBatch sb, float x, float top, float ground, Color color, float time)
		{
			Texture2D dash = Tex("Dash");
			Tile(sb, "Dash", new Rectangle((int)(x - dash.Width / 2f) / 2 * 2, (int)top, dash.Width, (int)Math.Max(0, ground - top)), color, 0, -(int)(time * 90f));
			Texture2D mark = Tex("Mark");
			sb.Draw(mark, Snap(new Vector2(x - mark.Width / 2f, ground - mark.Height)), color);
		}

		/// <summary>
		/// Charge lane: gold casino rails over a dithered runway, marching chevrons and a striped barrier where the
		/// charge ends. Progress 0 = still following the player (only the rails blink), 1 = locked.
		/// </summary>
		public static void ChargeLane(SpriteBatch sb, int left, int right, int centerY, int direction, bool locked, float alpha, float time)
		{
			Texture2D lane = Tex("Lane"), chevron = Tex("Chevron"), end = Tex("LaneEnd");
			int top = centerY / 2 * 2 - lane.Height / 2;
			left = left / 2 * 2;
			right = right / 2 * 2;
			bool blink = !locked && (int)(time * 8f) % 2 == 0;
			Color tint = Color.White * (alpha * (locked ? 1f : blink ? 0.35f : 0.55f));
			PixelFx.Tile(sb, "Lane", new Rectangle(left, top, right - left, lane.Height), tint);
			if (!locked)
				return;
			const int Gap = 120;
			int march = (int)(time * 260f) % Gap / 2 * 2;
			bool bright = (int)(time * 10f) % 2 == 0;
			for (int d = march; d < right - left - chevron.Width; d += Gap) {
				int x = direction > 0 ? left + d : right - d - chevron.Width;
				sb.Draw(chevron, new Vector2(x, centerY / 2 * 2 - chevron.Height / 2), null, (bright ? Color.White : new Color(225, 200, 170)) * alpha, 0f, Vector2.Zero, 1f, direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
			}
			sb.Draw(end, new Vector2(direction > 0 ? right - end.Width : left, top), Color.White * alpha);
		}
	}
}
