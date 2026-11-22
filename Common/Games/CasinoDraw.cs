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

		/// <summary>Gold-framed felt background of a game window.</summary>
		public static void Panel(SpriteBatch sb, Rectangle area)
		{
			Fill(sb, area, Gold);
			Fill(sb, Shrink(area, 3), FeltDark);
			Fill(sb, Shrink(area, 6), Felt);
		}

		public static void Button(SpriteBatch sb, Rectangle r, string label, Color color, bool hover, bool enabled, float scale = 0.85f)
		{
			Fill(sb, r, enabled ? Gold : Color.DimGray);
			Color inner = enabled ? color : Color.Gray;
			if (hover && enabled)
				inner = Color.Lerp(inner, Color.White, 0.25f);
			Fill(sb, Shrink(r, 2), inner);
			Utils.DrawBorderString(sb, label, r.Center.ToVector2() + new Vector2(0, 3), enabled ? Color.White : Color.LightGray, scale, 0.5f, 0.5f);
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
