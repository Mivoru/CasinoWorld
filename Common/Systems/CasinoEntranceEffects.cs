using System;
using System.Collections.Generic;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Systems
{
	/// <summary>
	/// Extra lighting drawn over the casino entrances with additive blending (no shader needed):
	///  - a soft halo over every glowing tile (windows, sign, bulbs, lanterns), like bloom,
	///  - a warm pool of light on the ground in front of the door,
	///  - small star-shaped glints that twinkle on the bright parts.
	/// All of it is stronger at night and underground, and fades out in daylight.
	/// </summary>
	public class CasinoEntranceEffects : ModSystem
	{
		private const int HaloSize = 128;
		private const int GlintSize = 15;
		private const int GlintLife = 36;
		private const int MaxGlints = 24;

		private const float ForegroundParallax = 0.08f;

		private static Texture2D halo, glint, foreground;

		private readonly List<(Vector2 pos, int age, Color color)> glints = new();

		public override void Unload()
		{
			Main.QueueMainThreadAction(() => {
				halo?.Dispose();
				glint?.Dispose();
				halo = glint = null;
				foreground = null;
			});
		}

		public override void PostDrawTiles()
		{
			if (Main.gameMenu || Main.dedServ)
				return;

			EnsureTextures();
			Rectangle screen = new((int)Main.screenPosition.X - 200, (int)Main.screenPosition.Y - 200, Main.screenWidth + 400, Main.screenHeight + 400);

			bool begun = false;
			foreach ((CasinoEntrance entrance, Point topLeft) in CasinoWorldSystem.Entrances()) {
				var area = new Rectangle(topLeft.X * 16, topLeft.Y * 16, entrance.EntranceWidth * 16, entrance.EntranceHeight * 16);
				if (!area.Intersects(screen))
					continue;

				if (!begun) {
					Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None,
						Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
					begun = true;
				}
				DrawEntrance(entrance, topLeft, area);
			}

			UpdateAndDrawGlints(begun);
			if (begun)
				Main.spriteBatch.End();

			if (CasinoTravel.InCasino)
				DrawInteriorBloom();

			DrawForegroundLayers(screen);
		}

		/// <summary>
		/// 2.5D depth for the underground entrance: a dark layer of stalactites and crystals drawn in front
		/// of everything and shifted with the camera a little more than the world (parallax), so it reads
		/// as closer to the viewer.
		/// </summary>
		private static void DrawForegroundLayers(Rectangle screen)
		{
			if (!CasinoWorldSystem.HasUndergroundCasino)
				return;

			Point topLeft = CasinoWorldSystem.UndergroundCasinoPosition;
			var entrance = ModContent.GetInstance<UndergroundCasinoFacade>();
			var area = new Rectangle(topLeft.X * 16, topLeft.Y * 16, entrance.EntranceWidth * 16, entrance.EntranceHeight * 16);
			if (!area.Intersects(screen))
				return;

			foreground ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/Effects/UndergroundForeground", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
			Vector2 screenCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
			Vector2 parallax = (area.Center.ToVector2() - screenCenter) * ForegroundParallax;
			Vector2 pos = area.TopLeft() - Main.screenPosition + parallax;
			Color light = Lighting.GetColor(area.Center.X / 16, area.Center.Y / 16);
			Color tint = Color.Lerp(new Color(70, 60, 90), light, 0.35f);

			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
				Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
			Main.spriteBatch.Draw(foreground, pos, tint);
			Main.spriteBatch.End();
		}

		public override void PostUpdateEverything()
		{
			if (Main.dedServ || Main.gameMenu || !CasinoWorldSystem.HasUndergroundCasino)
				return;

			// Glowing motes drifting up around the underground entrance.
			Point topLeft = CasinoWorldSystem.UndergroundCasinoPosition;
			var entrance = ModContent.GetInstance<UndergroundCasinoFacade>();
			var area = new Rectangle(topLeft.X * 16, topLeft.Y * 16, entrance.EntranceWidth * 16, entrance.EntranceHeight * 16);
			if (Main.LocalPlayer.Distance(area.Center.ToVector2()) > 1400 || !Main.rand.NextBool(3))
				return;

			var spot = new Vector2(area.X + Main.rand.Next(area.Width), area.Bottom - Main.rand.Next(area.Height / 2));
			Dust dust = Dust.NewDustPerfect(spot, Main.rand.NextBool() ? DustID.GemAmethyst : DustID.PinkTorch, new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(-0.9f, -0.3f)), 120, default, Main.rand.NextFloat(0.6f, 1.1f));
			dust.noGravity = true;
			dust.fadeIn = 1.1f;
		}

		/// <summary>Soft halos over the light sources of a casino hall (chandeliers, candles, torches).</summary>
		private static void DrawInteriorBloom()
		{
			int x0 = (int)(Main.screenPosition.X / 16) - 2, y0 = (int)(Main.screenPosition.Y / 16) - 2;
			int x1 = x0 + Main.screenWidth / 16 + 4, y1 = y0 + Main.screenHeight / 16 + 4;
			float time = Main.GlobalTimeWrappedHourly;
			Vector2 origin = new(HaloSize / 2f);
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None,
				Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
			for (int x = Math.Max(0, x0); x < Math.Min(Main.maxTilesX, x1); x++) {
				for (int y = Math.Max(0, y0); y < Math.Min(Main.maxTilesY, y1); y++) {
					Tile tile = Main.tile[x, y];
					if (!tile.HasTile)
						continue;
					float size, alpha;
					Color color;
					switch (tile.TileType) {
						case TileID.Chandeliers when tile.TileFrameX % 54 == 18 && tile.TileFrameY % 54 == 18:
							(size, alpha, color) = (2.6f, 0.22f, new Color(255, 200, 120));
							break;
						case TileID.Torches:
						case TileID.Candles:
							(size, alpha, color) = (1.1f, 0.16f, new Color(255, 170, 90));
							break;
						default:
							continue;
					}
					float flicker = 0.9f + 0.1f * (float)Math.Sin(time * 4f + x * 1.3f + y);
					Vector2 pos = new Vector2(x + 0.5f, y + 0.5f) * 16f - Main.screenPosition;
					Main.spriteBatch.Draw(halo, pos, null, color * (alpha * flicker), 0f, origin, size, SpriteEffects.None, 0f);
				}
			}
			Main.spriteBatch.End();
		}

		/// <summary>1 at night or underground, about 0.3 in full daylight.</summary>
		private static float Darkness(Rectangle area)
		{
			if (area.Center.Y / 16 > Main.worldSurface)
				return 1f;
			if (!Main.dayTime)
				return 1f;
			// Brighten towards dusk and dawn.
			float t = (float)(Main.time / Main.dayLength);
			float edge = Math.Min(t, 1f - t);
			return MathHelper.Lerp(1f, 0.3f, MathHelper.Clamp(edge * 8f, 0f, 1f));
		}

		private void DrawEntrance(CasinoEntrance entrance, Point topLeft, Rectangle area)
		{
			float dark = Darkness(area);
			Vector3 lc = entrance.LightColor;
			var tint = new Color(Math.Min(1f, lc.X), Math.Min(1f, lc.Y), Math.Min(1f, lc.Z));
			float time = Main.GlobalTimeWrappedHourly;
			Vector2 haloOrigin = new(HaloSize / 2f);

			for (int y = 0; y < entrance.EntranceHeight; y++) {
				for (int x = 0; x < entrance.EntranceWidth; x++) {
					float light = entrance.LightAt(x, y);
					if (light < 0.12f)
						continue;

					Vector2 center = new Vector2(topLeft.X + x + 0.5f, topLeft.Y + y + 0.5f) * 16f - Main.screenPosition;
					float flicker = 0.9f + 0.1f * (float)Math.Sin(time * 3.1f + x * 1.7f + y * 0.9f);
					float alpha = light * dark * 0.16f * flicker;
					float scale = (0.55f + light * 0.6f) * 48f / HaloSize * 2f;
					Main.spriteBatch.Draw(halo, center, null, tint * alpha, 0f, haloOrigin, scale, SpriteEffects.None, 0f);

					// Occasionally start a glint on a bright tile.
					if (light > 0.5f && glints.Count < MaxGlints && Main.rand.NextBool(900)) {
						Vector2 world = new Vector2(topLeft.X + x, topLeft.Y + y) * 16f + new Vector2(Main.rand.Next(16), Main.rand.Next(16));
						glints.Add((world, 0, Color.Lerp(Color.White, tint, 0.4f)));
					}
				}
			}

			if (entrance is UndergroundCasinoFacade) {
				// Soft light shafts falling from the neon sign onto the landing.
				for (int k = 0; k < 3; k++) {
					float sway = (float)Math.Sin(time * 0.7f + k * 2.1f) * 6f;
					Vector2 top = new Vector2(area.Center.X + (k - 1) * 70 + sway, area.Y + area.Height * 0.4f) - Main.screenPosition;
					float a = (0.08f + 0.03f * (float)Math.Sin(time * 1.3f + k)) * dark;
					Main.spriteBatch.Draw(halo, top, null, tint * a, 0f, new Vector2(HaloSize / 2f, 0f), new Vector2(0.9f, 2.6f), SpriteEffects.None, 0f);
				}
			}

			// Warm pool of light on the ground in front of the door.
			Rectangle door = entrance.DoorWorldRect(topLeft);
			Vector2 ground = new Vector2(door.Center.X, area.Bottom + 2) - Main.screenPosition;
			float poolWidth = door.Width * 2.6f / HaloSize;
			Main.spriteBatch.Draw(halo, ground, null, tint * (0.32f * dark), 0f, haloOrigin, new Vector2(poolWidth, poolWidth * 0.28f), SpriteEffects.None, 0f);
		}

		private void UpdateAndDrawGlints(bool begun)
		{
			for (int k = glints.Count - 1; k >= 0; k--) {
				(Vector2 pos, int age, Color color) = glints[k];
				age++;
				if (age > GlintLife) {
					glints.RemoveAt(k);
					continue;
				}
				glints[k] = (pos, age, color);
				if (!begun)
					continue;

				float life = age / (float)GlintLife;
				float fade = (float)Math.Sin(life * Math.PI);
				float scale = 0.6f + fade * 0.7f;
				Main.spriteBatch.Draw(glint, pos - Main.screenPosition, null, color * fade, life * 1.2f, new Vector2(GlintSize / 2f), scale, SpriteEffects.None, 0f);
			}
		}

		/// <summary>Builds the halo (soft radial falloff) and the four-pointed glint at runtime.</summary>
		private static void EnsureTextures()
		{
			if (halo == null) {
				var data = new Color[HaloSize * HaloSize];
				for (int y = 0; y < HaloSize; y++) {
					for (int x = 0; x < HaloSize; x++) {
						float dx = (x + 0.5f) / HaloSize * 2f - 1f, dy = (y + 0.5f) / HaloSize * 2f - 1f;
						float d = MathHelper.Clamp(1f - (float)Math.Sqrt(dx * dx + dy * dy), 0f, 1f);
						float a = d * d * (3f - 2f * d);     // smoothstep falloff
						data[y * HaloSize + x] = Color.White * a;
					}
				}
				halo = new Texture2D(Main.graphics.GraphicsDevice, HaloSize, HaloSize);
				halo.SetData(data);
			}

			if (glint == null) {
				var data = new Color[GlintSize * GlintSize];
				int c = GlintSize / 2;
				for (int y = 0; y < GlintSize; y++) {
					for (int x = 0; x < GlintSize; x++) {
						int dx = Math.Abs(x - c), dy = Math.Abs(y - c);
						float a = 0f;
						if (dx == 0)
							a = 1f - dy / (float)(c + 1);
						else if (dy == 0)
							a = 1f - dx / (float)(c + 1);
						else if (dx == dy && dx <= 2)
							a = 0.5f - dx * 0.15f;
						if (dx <= 1 && dy <= 1)
							a = 1f;
						data[y * GlintSize + x] = Color.White * MathHelper.Clamp(a, 0f, 1f);
					}
				}
				glint = new Texture2D(Main.graphics.GraphicsDevice, GlintSize, GlintSize);
				glint.SetData(data);
			}
		}
	}
}
