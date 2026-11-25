using System;
using System.Collections.Generic;
using CasinoWorld.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Effects
{
	/// <summary>
	/// The big moment: three gambler jackpots in a row. A marquee of bulbs runs around the screen, coins rain
	/// down and the camera leans in. Local player only, visual only; at most every 20 seconds.
	/// </summary>
	public class JackpotMoment : ModSystem
	{
		private const int Length = 170, Cooldown = 1200, Coins = 70;

		private static int timer, cooldown;
		private static readonly float[] coinX = new float[Coins], coinDelay = new float[Coins], coinSpeed = new float[Coins];
		private static Asset<Texture2D> coinTex;

		public static void Trigger()
		{
			if (Main.dedServ || cooldown > 0)
				return;
			timer = Length;
			cooldown = Cooldown;
			for (int k = 0; k < Coins; k++) {
				coinX[k] = Main.rand.NextFloat();
				coinDelay[k] = Main.rand.NextFloat(70f);
				coinSpeed[k] = Main.rand.NextFloat(9f, 16f);
			}
			SoundEngine.PlaySound(CasinoSounds.BigWin);
			SoundEngine.PlaySound(CasinoSounds.CoinRain);
			BossTitle.Flash(new Color(255, 215, 90));
		}

		public override void Unload() => coinTex = null;

		public override void OnWorldUnload() => timer = cooldown = 0;

		public override void PostUpdateEverything()
		{
			if (timer > 0)
				timer--;
			if (cooldown > 0)
				cooldown--;
		}

		public override void ModifyTransformMatrix(ref Terraria.Graphics.SpriteViewMatrix Transform)
		{
			if (timer > 0 && CasinoClientConfig.Zooming)
				Transform.Zoom *= 1f + 0.06f * (float)Math.Sin(Math.PI * timer / Length);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = Math.Max(0, layers.FindIndex(l => l.Name == "Vanilla: Inventory"));
			layers.Insert(index, new LegacyGameInterfaceLayer("CasinoWorld: Jackpot Moment", () => {
				if (timer > 0)
					Draw(Main.spriteBatch);
				return true;
			}, InterfaceScaleType.UI));
		}

		private static void Draw(SpriteBatch sb)
		{
			int age = Length - timer;
			float alpha = Math.Min(1f, Math.Min(age / 10f, timer / 30f));
			int w = Main.screenWidth, h = Main.screenHeight;
			if (CasinoClientConfig.Flashing) {
				Texture2D px = TextureAssets.MagicPixel.Value;
				const int Step = 44;
				int n = 0;
				void Bulb(int x, int y)
				{
					bool on = (n++ + age / 4) % 3 != 0;
					Color c = (on ? n % 2 == 0 ? new Color(255, 80, 70) : new Color(255, 225, 120) : new Color(80, 50, 30)) * alpha;
					if (on) {
						Color halo = c * 0.35f;
						halo.A = 0;
						sb.Draw(px, new Rectangle(x - 8, y - 8, 16, 16), halo);
					}
					sb.Draw(px, new Rectangle(x - 4, y - 4, 8, 8), c);
				}
				for (int x = 12; x < w; x += Step)
					Bulb(x, 12);
				for (int y = 12 + Step; y < h; y += Step)
					Bulb(w - 12, y);
				for (int x = w - 12; x > 0; x -= Step)
					Bulb(x, h - 12);
				for (int y = h - 12 - Step; y > 12; y -= Step)
					Bulb(12, y);

				coinTex ??= ModContent.Request<Texture2D>("CasinoWorld/Content/Projectiles/PennyCoin", AssetRequestMode.ImmediateLoad);
				int fh = coinTex.Height() / 4;
				for (int k = 0; k < Coins; k++) {
					float t = age - coinDelay[k];
					float y = t * coinSpeed[k] - 20f;
					if (t < 0 || y > h)
						continue;
					int frame = (int)(t / 3f + k) % 4;
					sb.Draw(coinTex.Value, new Vector2(coinX[k] * w, y), new Rectangle(0, frame * fh, coinTex.Width(), fh - 2), new Color(255, 225, 120) * alpha, 0f, new Vector2(coinTex.Width() / 2f, fh / 2f), 1.6f, SpriteEffects.None, 0f);
				}
			}
			float pop = age < 12 ? 1.6f - 0.6f * age / 12f : 1f + 0.04f * (float)Math.Sin(age * 0.3f);
			Utils.DrawBorderStringBig(sb, Language.GetTextValue("Mods.CasinoWorld.Gambler.TripleJackpot"), new Vector2(w / 2f, h * 0.24f),
				Color.Lerp(new Color(255, 215, 80), Color.White, 0.5f + 0.5f * (float)Math.Sin(age * 0.4f)) * alpha, pop, 0.5f, 0.5f);
		}
	}
}
