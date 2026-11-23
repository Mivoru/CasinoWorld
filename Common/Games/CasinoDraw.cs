using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Games
{
	/// <summary>Colours and drawing helpers shared by the casino game windows.</summary>
	public static class CasinoDraw
	{
		public static readonly Color Felt = new(18, 96, 50);
		public static readonly Color FeltDark = new(10, 60, 30);
		public static readonly Color Gold = new(222, 172, 48);
		public static readonly Color Red = new(176, 26, 36);
		public static readonly Color Black = new(28, 28, 32);
		public static readonly Color Green = new(20, 130, 60);
		public static readonly Color ButtonGold = new(190, 140, 30);

		private static Asset<Texture2D> chip, suits, cardBack;

		public static Texture2D Chip => (chip ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/Chip")).Value;
		public static Texture2D Suits => (suits ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/Suits")).Value;
		public static Texture2D CardBack => (cardBack ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/CardBack")).Value;

		public static Color ChipColor(int index) => index switch {
			0 => new Color(230, 230, 230),
			1 => new Color(220, 60, 60),
			2 => new Color(60, 120, 230),
			3 => new Color(40, 40, 40),
			4 => new Color(160, 60, 200),
			_ => new Color(240, 190, 40),
		};

		public static void Fill(SpriteBatch sb, Rectangle rect, Color color) => sb.Draw(TextureAssets.MagicPixel.Value, rect, color);

		public static Rectangle Shrink(Rectangle r, int by) => new(r.X + by, r.Y + by, r.Width - by * 2, r.Height - by * 2);

		private static Asset<Texture2D> felt;

		public static Texture2D FeltTexture => (felt ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/Felt")).Value;

		/// <summary>Game window background: drop shadow, dark wood frame with a gold bead, textured felt with a
		/// darker vignette towards the edges and small gold corner studs.</summary>
		public static void Panel(SpriteBatch sb, Rectangle area)
		{
			Fill(sb, new Rectangle(area.X + 6, area.Y + 8, area.Width, area.Height), Color.Black * 0.35f);
			Fill(sb, area, new Color(40, 22, 14));
			Fill(sb, Shrink(area, 2), new Color(96, 56, 30));
			Fill(sb, new Rectangle(area.X + 2, area.Y + 2, area.Width - 4, 2), new Color(150, 96, 52));   // top light on the frame
			Fill(sb, Shrink(area, 7), Gold);
			Fill(sb, new Rectangle(area.X + 7, area.Bottom - 9, area.Width - 14, 2), new Color(150, 100, 30));
			Rectangle inner = Shrink(area, 9);
			Fill(sb, inner, Felt);
			Texture2D tex = FeltTexture;
			for (int y = inner.Y; y < inner.Bottom; y += tex.Height) {
				for (int x = inner.X; x < inner.Right; x += tex.Width) {
					int w = System.Math.Min(tex.Width, inner.Right - x), h = System.Math.Min(tex.Height, inner.Bottom - y);
					sb.Draw(tex, new Rectangle(x, y, w, h), new Rectangle(0, 0, w, h), Felt);
				}
			}
			for (int k = 0; k < 6; k++) {
				Color shade = Color.Black * (0.07f * (6 - k) / 6f);
				Fill(sb, new Rectangle(inner.X + k * 3, inner.Y + k * 3, inner.Width - k * 6, 3), shade);
				Fill(sb, new Rectangle(inner.X + k * 3, inner.Bottom - k * 3 - 3, inner.Width - k * 6, 3), shade);
				Fill(sb, new Rectangle(inner.X + k * 3, inner.Y + k * 3 + 3, 3, inner.Height - k * 6 - 6), shade);
				Fill(sb, new Rectangle(inner.Right - k * 3 - 3, inner.Y + k * 3 + 3, 3, inner.Height - k * 6 - 6), shade);
			}
			foreach (Point c in new[] { new Point(area.X + 4, area.Y + 4), new Point(area.Right - 4, area.Y + 4), new Point(area.X + 4, area.Bottom - 4), new Point(area.Right - 4, area.Bottom - 4) }) {
				Fill(sb, new Rectangle(c.X - 3, c.Y - 3, 6, 6), new Color(150, 100, 30));
				Fill(sb, new Rectangle(c.X - 2, c.Y - 2, 4, 4), Gold);
				Fill(sb, new Rectangle(c.X - 2, c.Y - 2, 2, 2), new Color(255, 236, 160));
			}
		}

		/// <summary>Slot machine cabinet: drop shadow, accent rim, lacquered body with a glossy left band and a
		/// shaded right band, chrome strip along the top.</summary>
		public static void Cabinet(SpriteBatch sb, Rectangle a, Color rim, Color body)
		{
			Fill(sb, new Rectangle(a.X + 6, a.Y + 8, a.Width, a.Height), Color.Black * 0.35f);
			Fill(sb, a, Color.Lerp(rim, Color.Black, 0.45f));
			Fill(sb, Shrink(a, 2), rim);
			Fill(sb, new Rectangle(a.X + 2, a.Y + 2, a.Width - 4, 2), Color.Lerp(rim, Color.White, 0.45f));
			Rectangle inner = Shrink(a, 6);
			Fill(sb, inner, body);
			int bands = 8;
			for (int k = 0; k < bands; k++) {
				float f = k / (float)(bands - 1);
				Fill(sb, new Rectangle(inner.X + k * 3, inner.Y, 3, inner.Height), Color.White * (0.10f * (1f - f)));
				Fill(sb, new Rectangle(inner.Right - (k + 1) * 3, inner.Y, 3, inner.Height), Color.Black * (0.22f * (1f - f)));
			}
			Fill(sb, new Rectangle(inner.X, inner.Y, inner.Width, 3), Color.Lerp(body, Color.White, 0.3f));
			Fill(sb, new Rectangle(inner.X, inner.Bottom - 4, inner.Width, 4), Color.Black * 0.3f);
		}

		/// <summary>Bevelled button: gold rim, light top edge, dark bottom edge, pressed look on hover.</summary>
		public static void Button(SpriteBatch sb, Rectangle r, string label, Color color, bool hover, bool enabled, float scale = 0.85f)
		{
			Fill(sb, new Rectangle(r.X + 2, r.Y + 3, r.Width, r.Height), Color.Black * 0.3f);
			Fill(sb, r, enabled ? Gold : Color.DimGray);
			Color inner = enabled ? color : Color.Gray;
			if (hover && enabled)
				inner = Color.Lerp(inner, Color.White, 0.2f);
			Rectangle face = Shrink(r, 2);
			Fill(sb, face, inner);
			Fill(sb, new Rectangle(face.X, face.Y, face.Width, 2), Color.Lerp(inner, Color.White, 0.35f));
			Fill(sb, new Rectangle(face.X, face.Bottom - 2, face.Width, 2), Color.Lerp(inner, Color.Black, 0.35f));
			Utils.DrawBorderString(sb, label, r.Center.ToVector2() + new Vector2(0, hover && enabled ? 4 : 3), enabled ? Color.White : Color.LightGray, scale, 0.5f, 0.5f);
		}

		public static void ChipButton(SpriteBatch sb, Rectangle r, string label, int index, bool hover, bool selected)
		{
			if (selected)
				Fill(sb, r, Gold);
			Fill(sb, Shrink(r, 2), hover ? FeltDark * 0.7f : FeltDark);
			Texture2D tex = Chip;
			sb.Draw(tex, new Vector2(r.Center.X, r.Y + 14), null, ChipColor(index), 0f, tex.Size() / 2f, 1.1f, SpriteEffects.None, 0f);
			Utils.DrawBorderString(sb, label, new Vector2(r.Center.X, r.Bottom - 2), Color.White, 0.7f, 0.5f, 1f);
		}
	}
}
