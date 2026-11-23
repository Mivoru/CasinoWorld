using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Effects
{
	/// <summary>
	/// Client-side boss presentation: a title card ("THE WILD CARD / Jester of Fortune") that slides in when
	/// a boss appears, and a short coloured screen flash for phase changes.
	/// </summary>
	public class BossTitle : ModSystem
	{
		private const int TitleTicks = 240, FlashTicks = 24;

		private static string title, subtitle;
		private static Color titleColor, flashColor;
		private static int titleTimer, flashTimer;

		public static void Show(string titleKey, string subtitleKey, Color color)
		{
			title = Language.GetTextValue(titleKey);
			subtitle = Language.GetTextValue(subtitleKey);
			titleColor = color;
			titleTimer = TitleTicks;
		}

		public static void Flash(Color color)
		{
			if (Main.dedServ)
				return;
			flashColor = color;
			flashTimer = FlashTicks;
		}

		public override void PostUpdateEverything()
		{
			if (titleTimer > 0)
				titleTimer--;
			if (flashTimer > 0)
				flashTimer--;
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Inventory");
			if (index < 0)
				index = 0;
			layers.Insert(index, new LegacyGameInterfaceLayer("CasinoWorld: Boss Title", () => {
				Draw(Main.spriteBatch);
				return true;
			}, InterfaceScaleType.UI));
		}

		private static void Draw(SpriteBatch sb)
		{
			if (flashTimer > 0) {
				Color c = flashColor * (flashTimer / (float)FlashTicks * 0.35f);
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), c);
			}
			if (titleTimer <= 0 || title == null)
				return;
			int age = TitleTicks - titleTimer;
			float alpha = Math.Min(1f, Math.Min(age / 30f, titleTimer / 40f));
			float slide = (1f - Math.Min(1f, age / 30f)) * 60f;
			Vector2 center = new(Main.screenWidth / 2f, Main.screenHeight * 0.28f);
			// Thin gold bars that grow out from the middle.
			float bar = Math.Min(1f, age / 40f) * 260f;
			Vector2 size = Terraria.GameContent.FontAssets.DeathText.Value.MeasureString(title);
			float top = center.Y - size.Y / 2f - 10f, bottom = center.Y + size.Y / 2f + 38f;
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)(center.X - bar * 0.7f), (int)top, (int)(bar * 1.4f), 1), titleColor * (alpha * 0.6f));
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)(center.X - bar), (int)bottom, (int)(bar * 2), 2), titleColor * alpha);
			Utils.DrawBorderStringBig(sb, title, center + new Vector2(-slide, 0), titleColor * alpha, 1f, 0.5f, 0.5f);
			Utils.DrawBorderString(sb, subtitle, new Vector2(center.X + slide, center.Y + size.Y / 2f + 16f), Color.White * alpha, 1.1f, 0.5f, 0.5f);
		}
	}
}
