using System;
using System.Collections.Generic;
using CasinoWorld.Common.Effects;
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

namespace CasinoWorld.Common.Games
{
	/// <summary>
	/// Celebration effects over the game windows, in UI coordinates: coin fountains, sparkles, confetti, and
	/// win banners with a rolling counter. Local player only. Banners need ScreenEffects for their light show.
	/// </summary>
	public class UiFx : ModSystem
	{
		private enum Kind { Coin, Spark, Confetti, Chip }

		private struct Particle
		{
			public Kind Kind;
			public Vector2 Position, Velocity;
			public Color Color;
			public float Life, MaxLife, Rotation, Spin, Scale;
		}

		private const int MaxParticles = 500;
		private static readonly List<Particle> particles = new();
		private static Asset<Texture2D> coinTex;

		private static string banner;
		private static Color bannerColor;
		private static long bannerAmount;
		private static int bannerAge = -1, bannerLength, bannerTier, countTicks;
		private static Vector2 bannerAt;

		public static Vector2 ScreenCenter => new(Main.screenWidth / 2f, Main.screenHeight / 2f);

		public override void Unload()
		{
			coinTex = null;
			particles.Clear();
		}

		public override void OnWorldUnload()
		{
			particles.Clear();
			bannerAge = -1;
		}

		private static void Add(Particle p)
		{
			if (particles.Count < MaxParticles)
				particles.Add(p);
		}

		/// <summary>A fountain of spinning coins.</summary>
		public static void Coins_(Vector2 at, int count, float power = 9f)
		{
			for (int k = 0; k < count; k++)
				Add(new Particle {
					Kind = Kind.Coin, Position = at, Velocity = new Vector2(Main.rand.NextFloat(-0.7f, 0.7f) * power, -Main.rand.NextFloat(0.4f, 1.2f) * power),
					Color = Main.rand.NextBool(5) ? new Color(235, 235, 245) : new Color(255, 215, 90), MaxLife = Main.rand.NextFloat(60, 110), Scale = Main.rand.NextFloat(0.9f, 1.5f), Rotation = Main.rand.Next(20),
				});
		}

		public static void Sparkle(Vector2 at, Color color, int count, float speed = 3f)
		{
			for (int k = 0; k < count; k++)
				Add(new Particle {
					Kind = Kind.Spark, Position = at, Velocity = Main.rand.NextVector2Circular(speed, speed), Color = color, MaxLife = Main.rand.NextFloat(18, 40), Scale = Main.rand.NextFloat(2f, 5f),
				});
		}

		/// <summary>One sparkle somewhere inside a rectangle; call it while something should shimmer.</summary>
		public static void Shimmer(Rectangle r, Color color, int oneIn = 4)
		{
			if (Main.rand.NextBool(oneIn))
				Sparkle(new Vector2(r.X + Main.rand.Next(Math.Max(1, r.Width)), r.Y + Main.rand.Next(Math.Max(1, r.Height))), color, 1, 0.6f);
		}

		public static void Confetti(int count)
		{
			for (int k = 0; k < count; k++)
				Add(new Particle {
					Kind = Kind.Confetti, Position = new Vector2(Main.rand.Next(Main.screenWidth), Main.rand.NextFloat(-120, -5)), Velocity = new Vector2(Main.rand.NextFloat(-1, 1), Main.rand.NextFloat(2, 5)),
					Color = Main.hslToRgb(Main.rand.NextFloat(), 0.8f, 0.6f), MaxLife = 220, Scale = Main.rand.NextFloat(4, 8), Spin = Main.rand.NextFloat(-0.3f, 0.3f),
				});
		}

		/// <summary>Celebrates a settled round: total returned, stake, and where the win is on screen.</summary>
		public static void Win(long payout, long stake, Vector2? at = null)
		{
			if (Main.dedServ || payout <= stake || stake <= 0)
				return;
			float ratio = payout / (float)stake;
			Vector2 p = at ?? ScreenCenter;
			Coins_(p, (int)Math.Min(70, 8 + ratio * 4f), 8f + Math.Min(6f, ratio));
			Sparkle(p, new Color(255, 230, 140), 14, 4f);
			if (ratio < 5f)
				return;
			int tier = ratio >= 50f ? 3 : ratio >= 15f ? 2 : 1;
			Banner(Language.GetTextValue("Mods.CasinoWorld.UI.Win.Tier" + tier), tier == 3 ? new Color(255, 90, 80) : tier == 2 ? new Color(225, 130, 255) : new Color(255, 215, 80), payout, tier, p);
		}

		/// <summary>A big line of text with an optional rolling amount. Tier 0-3 scales the show.</summary>
		public static void Banner(string text, Color color, long amount, int tier, Vector2? at = null)
		{
			if (Main.dedServ)
				return;
			banner = text;
			bannerColor = color;
			bannerAmount = amount;
			bannerTier = tier;
			bannerAge = 0;
			countTicks = amount > 0 ? 40 + tier * 25 : 0;
			bannerLength = countTicks + 70 + tier * 20;
			bannerAt = at ?? ScreenCenter;
			if (tier >= 1)
				Confetti(40 * tier);
			if (tier >= 2) {
				SoundEngine.PlaySound(CasinoSounds.MegaWin);
				SoundEngine.PlaySound(CasinoSounds.CrowdCheer);
				CasinoFx.Ring(Main.LocalPlayer.Center, color, 24, 6f, 1.3f);
			}
			if (tier >= 3) {
				SoundEngine.PlaySound(CasinoSounds.CoinRain);
				BossTitle.Flash(color);
			}
		}

		public override void UpdateUI(GameTime gameTime)
		{
			for (int k = particles.Count - 1; k >= 0; k--) {
				Particle p = particles[k];
				p.Life++;
				p.Position += p.Velocity;
				p.Rotation += p.Spin;
				switch (p.Kind) {
					case Kind.Coin:
						p.Velocity.Y += 0.35f;
						break;
					case Kind.Spark:
						p.Velocity *= 0.94f;
						break;
					case Kind.Confetti:
						p.Velocity.X += (float)Math.Sin(p.Life * 0.1f + k) * 0.06f;
						break;
				}
				if (p.Life >= p.MaxLife || p.Position.Y > Main.screenHeight + 40)
					particles.RemoveAt(k);
				else
					particles[k] = p;
			}
			if (bannerAge < 0)
				return;
			bannerAge++;
			if (bannerAge < countTicks) {
				if (bannerAge % 5 == 0)
					SoundEngine.PlaySound(CasinoSounds.CrateTick with { Volume = 0.35f, Pitch = -0.2f + 0.8f * bannerAge / countTicks });
				if (bannerTier >= 1 && bannerAge % 6 == 0)
					Coins_(bannerAt, 2 + bannerTier * 2, 10f);
			}
			else if (bannerAge == countTicks && bannerAmount > 0) {
				SoundEngine.PlaySound(CasinoSounds.CashRegister);
				Coins_(bannerAt, 10 + bannerTier * 15, 12f);
			}
			if (bannerAge > bannerLength)
				bannerAge = -1;
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			if (index < 0)
				return;
			layers.Insert(index + 1, new LegacyGameInterfaceLayer("CasinoWorld: Win effects", () => {
				Draw(Main.spriteBatch);
				return true;
			}, InterfaceScaleType.UI));
		}

		private static void Draw(SpriteBatch sb)
		{
			Texture2D px = TextureAssets.MagicPixel.Value;
			if (bannerAge >= 0)
				DrawRays(sb);
			coinTex ??= ModContent.Request<Texture2D>("CasinoWorld/Content/Projectiles/PennyCoin", AssetRequestMode.ImmediateLoad);
			int fh = coinTex.Height() / 4;
			foreach (Particle p in particles) {
				float left = 1f - p.Life / p.MaxLife;
				float alpha = Math.Min(1f, left * 4f);
				switch (p.Kind) {
					case Kind.Coin:
						int frame = (int)(p.Life / 3 + p.Rotation) % 4;
						sb.Draw(coinTex.Value, p.Position, new Rectangle(0, frame * fh, coinTex.Width(), fh), p.Color * alpha, 0f, new Vector2(coinTex.Width() / 2f, fh / 2f), p.Scale, SpriteEffects.None, 0f);
						break;
					case Kind.Spark:
						Color c = p.Color * left;
						c.A = 0;
						if (p.Scale >= 3.2f)
							PixelFx.Twinkle(sb, p.Position, c, p.Life / p.MaxLife);
						else {
							Vector2 q = PixelFx.Snap(p.Position);
							sb.Draw(px, new Rectangle((int)q.X, (int)q.Y, left > 0.5f ? 4 : 2, left > 0.5f ? 4 : 2), c);
						}
						break;
					case Kind.Confetti:
						// Paper strip that flips: four hard shapes instead of a rotated rectangle.
						int flip = (int)(p.Life * 0.2f + p.Scale) % 4;
						Vector2 cq = PixelFx.Snap(p.Position);
						sb.Draw(px, new Rectangle((int)cq.X, (int)cq.Y, flip == 1 ? 2 : flip == 3 ? 8 : 6, flip == 0 ? 6 : flip == 1 ? 8 : flip == 2 ? 4 : 2), (flip == 2 ? Color.Lerp(p.Color, Color.Black, 0.3f) : p.Color) * alpha);
						break;
				}
			}
			if (bannerAge >= 0)
				DrawBanner(sb);
		}

		private static float BannerAlpha => Math.Min(1f, Math.Min(bannerAge / 8f, (bannerLength - bannerAge) / 20f));

		private static void DrawRays(SpriteBatch sb)
		{
			if (bannerTier < 1 || !CasinoClientConfig.Instance.ScreenEffects)
				return;
			float t = Main.GlobalTimeWrappedHourly;
			PixelFx.Burst(sb, bannerAt, bannerColor * (0.5f * BannerAlpha), t * 0.6f, 4 + bannerTier * 2);
			PixelFx.Burst(sb, bannerAt, Color.White * (0.3f * BannerAlpha), t * 0.6f + 0.4f, 2 + bannerTier);
			PixelFx.Glow(sb, bannerAt, 110f, bannerColor * (0.6f * BannerAlpha));
		}

		private static void DrawBanner(SpriteBatch sb)
		{
			float alpha = BannerAlpha;
			float pop = bannerAge < 12 ? 1.7f - 0.7f * bannerAge / 12f : 1f + 0.03f * (float)Math.Sin(bannerAge * 0.3f);
			var at = new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.2f);
			Color text = Color.Lerp(bannerColor, Color.White, 0.35f + 0.35f * (float)Math.Sin(bannerAge * 0.35f));
			Utils.DrawBorderStringBig(sb, banner, at, text * alpha, pop * (0.8f + bannerTier * 0.12f), 0.5f, 0.5f);
			if (bannerAmount <= 0)
				return;
			float f = Math.Min(1f, bannerAge / (float)Math.Max(1, countTicks));
			long shown = (long)(bannerAmount * (double)(1f - (1f - f) * (1f - f)));
			Utils.DrawBorderStringBig(sb, Games.Coins.Format(Math.Max(1, shown)), at + new Vector2(0, 52), new Color(255, 240, 190) * alpha, 0.55f, 0.5f, 0.5f);
		}
	}
}
