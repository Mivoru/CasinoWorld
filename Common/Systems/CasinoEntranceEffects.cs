using System;
using System.Collections.Generic;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
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

		private static Texture2D halo, glint;

		private readonly List<(Vector2 pos, int age, Color color)> glints = new();

		public override void Unload()
		{
			Main.QueueMainThreadAction(() => {
				halo?.Dispose();
				glint?.Dispose();
				halo = glint = null;
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
