using System;
using System.Collections.Generic;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Games;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Club
{
	/// <summary>Plate that slides in at the bottom right when an achievement is unlocked; several wait in line.</summary>
	public class AchievementToast : ModSystem
	{
		private const int Length = 270, Slide = 24, Width = 340, Height = 74;
		private static readonly Queue<Achievement> queue = new();
		private static Achievement current;
		private static int age;

		public static void Show(Achievement achievement)
		{
			queue.Enqueue(achievement);
			Main.NewText(Language.GetTextValue("Mods.CasinoWorld.Achievements.Unlocked", achievement.Name, achievement.Tokens), new Color(255, 215, 90));
		}

		public override void OnWorldUnload()
		{
			queue.Clear();
			current = null;
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (current == null) {
				if (!queue.TryDequeue(out current))
					return;
				age = 0;
				SoundEngine.PlaySound(CasinoSounds.Achievement);
			}
			if (++age > Length)
				current = null;
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			if (index < 0 || current == null)
				return;
			layers.Insert(index, new LegacyGameInterfaceLayer("CasinoWorld: Achievement", () => {
				Draw(Main.spriteBatch);
				return true;
			}, InterfaceScaleType.UI));
		}

		private static void Draw(SpriteBatch sb)
		{
			float p = MathHelper.Clamp(Math.Min(age, Length - age) / (float)Slide, 0f, 1f);
			p = 1f - (1f - p) * (1f - p);
			var r = new Rectangle(Main.screenWidth - 24 - Width, (int)(Main.screenHeight - (Height + 24) * p), Width, Height);
			CasinoDraw.Fill(sb, new Rectangle(r.X + 4, r.Y + 5, r.Width, r.Height), Color.Black * 0.35f);
			CasinoDraw.Fill(sb, r, new Color(146, 88, 26));
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(r, 2), CasinoDraw.Gold);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(r, 4), new Color(44, 18, 34));
			// A light runs over the plate.
			int x = r.X + 4 + age * 7 % (Width * 2);
			if (x < r.Right - 24)
				CasinoDraw.Fill(sb, new Rectangle(x, r.Y + 4, 20, r.Height - 8), new Color(255, 230, 150, 0) * 0.12f);
			var icon = new Vector2(r.X + 40, r.Center.Y);
			PixelFx.Glow(sb, icon, 32f, CasinoDraw.Gold * 0.6f);
			LedgerPanel.DrawItem(sb, current.Icon(), icon, 44f, Color.White);
			CasinoDraw.Fit(sb, Language.GetTextValue("Mods.CasinoWorld.Achievements.Title"), new Vector2(r.X + 78, r.Y + 10), CasinoDraw.Gold, 0.7f, Width - 100);
			CasinoDraw.Fit(sb, current.Name, new Vector2(r.X + 78, r.Y + 27), Color.White, 0.95f, Width - 100);
			CasinoDraw.Fit(sb, Language.GetTextValue("Mods.CasinoWorld.Achievements.Reward", current.Tokens), new Vector2(r.X + 78, r.Y + 50), Color.LightGreen, 0.7f, Width - 100);
		}
	}
}
