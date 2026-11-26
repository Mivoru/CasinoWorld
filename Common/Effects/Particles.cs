using System;
using CasinoWorld.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Effects
{
	public enum ParticleKind : byte { Spark, Ember, Smoke, Star, Shard, Card, Coin, Impact, Ring, Glow, Explosion, Smear, Arc, LockOn, Alert }

	/// <summary>
	/// Pixel-art combat particles. Everything is drawn into a half-resolution layer (one layer pixel = one Terraria
	/// art pixel = two world pixels) that is then scaled up 2x without filtering, so every spark, ring and line sits
	/// on the same pixel grid as the game's sprites. Colours come from five-step ramps (<see cref="Ramp"/>) in whole
	/// steps. Sprites: tools/particle_art2.py (Assets/Fx/Pix, native size) and tools/pixel_fx_art.py (glows).
	/// Client only, pooled; how many particles effects spawn follows the Particle amount setting.
	/// </summary>
	public class Particles : ModSystem
	{
		private struct Particle
		{
			public Vector2 Pos, Vel, End;
			public float Life, Max, Scale, Drag, Gravity, Size, Rot;
			public ParticleKind Kind;
			public Color Color;
			public byte Seed;
			public short Follow;   // NPC index + 1 the particle sticks to, 0 = free
			public Vector2 Offset;
		}

		private static readonly Particle[] pool = new Particle[4096];
		private static int count;
		private static RenderTarget2D layer;
		private static Vector2 layerOrigin;
		private const int Margin = 96;

		public static float Amount => CasinoClientConfig.Instance?.Particles switch {
			ParticleAmount.Low => 0.4f,
			ParticleAmount.High => 1.6f,
			_ => 1f,
		};

		private static int Cap => CasinoClientConfig.Instance?.Particles switch {
			ParticleAmount.Low => 700,
			ParticleAmount.High => 4096,
			_ => 2400,
		};

		/// <summary>Scales a particle count by the setting, never below one.</summary>
		public static int N(int baseCount) => Math.Max(1, (int)(baseCount * Amount + 0.5f));

		public override void Load()
		{
			if (Main.dedServ)
				return;
			Terraria.On_Main.DrawDust += DrawAfterDust;
			Main.OnPreDraw += Render;
		}

		public override void Unload()
		{
			count = 0;
			if (!Main.dedServ) {
				Main.OnPreDraw -= Render;
				Main.QueueMainThreadAction(() => {
					layer?.Dispose();
					layer = null;
				});
			}
		}

		public override void OnWorldUnload() => count = 0;

		private static bool OnScreen(Vector2 at, float margin)
			=> at.X > Main.screenPosition.X - margin && at.X < Main.screenPosition.X + Main.screenWidth + margin
			&& at.Y > Main.screenPosition.Y - margin && at.Y < Main.screenPosition.Y + Main.screenHeight + margin;

		private static ref Particle Add(ParticleKind kind, Vector2 pos, Vector2 vel, Color color, int life, float scale = 1f, float drag = 0.92f, float gravity = 0f, float size = 0f)
		{
			if (Main.dedServ || count >= Cap || !OnScreen(pos, 700f))
				return ref dummy;
			ref Particle p = ref pool[count++];
			p = new Particle {
				Kind = kind, Pos = pos, Vel = vel, Color = color, Life = life, Max = life, Scale = scale,
				Drag = drag, Gravity = gravity, Size = size, Seed = (byte)Main.rand.Next(256),
			};
			return ref p;
		}

		private static Particle dummy;

		// ---- spawners (positions in world pixels)

		/// <summary>Streak that flies along its velocity: white-hot head, then down its colour ramp.</summary>
		public static void Spark(Vector2 at, Vector2 vel, Color color, int life = 22, float scale = 1f, float gravity = 0f)
			=> Add(ParticleKind.Spark, at, vel, color, life, scale, 0.9f, gravity);

		/// <summary>Glowing mote that drifts, flickers and cools down its ramp.</summary>
		public static void Ember(Vector2 at, Vector2 vel, Color color, int life = 40, float scale = 1f, float gravity = -0.02f)
			=> Add(ParticleKind.Ember, at, vel, color, life, scale, 0.95f, gravity);

		/// <summary>Shaded smoke puff (size 1 small, 2 big). A grey colour uses the smoke ramp.</summary>
		public static void Smoke(Vector2 at, Vector2 vel, Color color, int life = 40, int scale = 1)
			=> Add(ParticleKind.Smoke, at, vel, color, life, scale, 0.94f, -0.015f);

		/// <summary>Four-pointed twinkle (size 1-3).</summary>
		public static void Star(Vector2 at, Color color, int life = 18, Vector2 vel = default, int size = 2)
			=> Add(ParticleKind.Star, at, vel, color, life, size, 0.9f);

		/// <summary>Spinning sliver that falls.</summary>
		public static void Shard(Vector2 at, Vector2 vel, Color color, int life = 45)
			=> Add(ParticleKind.Shard, at, vel, color, life, 1f, 0.97f, 0.12f);

		/// <summary>Spinning playing card that falls.</summary>
		public static void Card(Vector2 at, Vector2 vel, int life = 70)
			=> Add(ParticleKind.Card, at, vel, Color.White, life, 1f, 0.97f, 0.1f);

		/// <summary>Spinning gold coin that falls.</summary>
		public static void Coin(Vector2 at, Vector2 vel, int life = 60)
			=> Add(ParticleKind.Coin, at, vel, Color.Gold, life, 1f, 0.98f, 0.18f);

		/// <summary>Anime impact star: pops, holds its big frame, then hollows out.</summary>
		public static void Impact(Vector2 at, Color color, int scale = 1, int life = 16)
			=> Add(ParticleKind.Impact, at, Vector2.Zero, color, life, 1f, 1f);

		/// <summary>Shockwave ring: a bright leading edge with a dithered wake, growing to the radius.</summary>
		public static void Ring(Vector2 at, Color color, float radius, int life = 24, float thickness = 4f)
			=> Add(ParticleKind.Ring, at, Vector2.Zero, color, life, thickness, 1f, 0f, radius);

		/// <summary>Dithered additive glow (radius in world pixels).</summary>
		public static void Glow(Vector2 at, Color color, float radius, int life = 16)
			=> Add(ParticleKind.Glow, at, Vector2.Zero, color, life, 1f, 1f, 0f, radius);

		/// <summary>Hand-drawn fireball; radius above 90 uses the big sheet.</summary>
		public static void Explosion(Vector2 at, Color color, float radius, int life = 30)
			=> Add(ParticleKind.Explosion, at, new Vector2(0, -0.3f), color, life, 1f, 0.97f, 0f, radius);

		/// <summary>Motion smear behind something moving along the direction (snapped to eight directions).</summary>
		public static void Smear(Vector2 at, Vector2 direction, Color color, int life = 10)
		{
			ref Particle p = ref Add(ParticleKind.Smear, at, Vector2.Zero, color, life, 1f, 1f);
			p.Rot = (float)Math.Round(direction.ToRotation() / MathHelper.PiOver4) * MathHelper.PiOver4;
		}

		/// <summary>Jagged electric arc between two points, re-drawn every few ticks.</summary>
		public static void Arc(Vector2 from, Vector2 to, Color color, int life = 10)
		{
			ref Particle p = ref Add(ParticleKind.Arc, from, Vector2.Zero, color, life, 1f, 1f);
			p.End = to;
		}

		/// <summary>Telegraph: a dashed ring that closes in on a point (optionally on an NPC).</summary>
		public static void LockOn(Vector2 at, Color color, float radius, int life = 30, NPC follow = null)
		{
			ref Particle p = ref Add(ParticleKind.LockOn, at, Vector2.Zero, color, life, 1f, 1f, 0f, radius);
			if (follow != null) {
				p.Follow = (short)(follow.whoAmI + 1);
				p.Offset = at - follow.Center;
			}
		}

		/// <summary>Telegraph: a red "!" that pops over an NPC's head.</summary>
		public static void Alert(NPC over, int life = 40)
		{
			ref Particle p = ref Add(ParticleKind.Alert, over.Top - new Vector2(0, 26), Vector2.Zero, Color.White, life, 1f, 1f);
			p.Follow = (short)(over.whoAmI + 1);
			p.Offset = new Vector2(0, -over.height / 2f - 26);
		}

		// ---- update

		public override void PostUpdateDusts()
		{
			if (Main.dedServ)
				return;
			int k = 0;
			while (k < count) {
				ref Particle p = ref pool[k];
				p.Life--;
				if (p.Life <= 0) {
					pool[k] = pool[--count];
					continue;
				}
				if (p.Follow > 0) {
					NPC n = Main.npc[p.Follow - 1];
					if (n.active)
						p.Pos = n.Center + p.Offset;
				}
				else {
					p.Pos += p.Vel;
					p.Vel *= p.Drag;
					p.Vel.Y += p.Gravity;
				}
				k++;
			}
		}

		// ---- render: half-resolution layer

		private static void Render(GameTime time)
		{
			if (Main.gameMenu || Main.dedServ || (count == 0 && !ScreenFx.LinesActive))
				return;
			GraphicsDevice gd = Main.instance.GraphicsDevice;
			int w = (Main.screenWidth + Margin * 2) / 2, h = (Main.screenHeight + Margin * 2) / 2;
			if (layer == null || layer.IsDisposed || layer.Width != w || layer.Height != h) {
				layer?.Dispose();
				layer = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
			}
			layerOrigin = new Vector2((float)Math.Floor((Main.screenPosition.X - Margin) / 2f) * 2f, (float)Math.Floor((Main.screenPosition.Y - Margin) / 2f) * 2f);
			RenderTargetBinding[] old = gd.GetRenderTargets();
			gd.SetRenderTarget(layer);
			gd.Clear(Color.Transparent);
			SpriteBatch sb = Main.spriteBatch;
			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
			try {
				ScreenFx.DrawLines(sb, layerOrigin);
				for (int k = 0; k < count; k++)
					Draw(sb, ref pool[k]);
			}
			finally {
				sb.End();
				gd.SetRenderTargets(old);
			}
			drawn = true;
		}

		private static bool drawn;

		private static void DrawAfterDust(Terraria.On_Main.orig_DrawDust orig, Main self)
		{
			orig(self);
			if (!drawn || layer == null || layer.IsDisposed)
				return;
			drawn = false;
			SpriteBatch sb = Main.spriteBatch;
			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None,
				RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
			sb.Draw(layer, layerOrigin - Main.screenPosition, null, Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
			sb.End();
		}

		// ---- primitives in layer pixels

		private static Texture2D Px => TextureAssets.MagicPixel.Value;

		private static Vector2 ToLayer(Vector2 world) => (world - layerOrigin) / 2f;

		private static void Dot(SpriteBatch sb, int x, int y, Color c, int size = 1)
			=> sb.Draw(Px, new Rectangle(x, y, size, size), c);

		private static void Span(SpriteBatch sb, int x0, int x1, int y, Color c)
		{
			if (x1 >= x0)
				sb.Draw(Px, new Rectangle(x0, y, x1 - x0 + 1, 1), c);
		}

		/// <summary>Bresenham line, one layer pixel wide.</summary>
		public static void Line(SpriteBatch sb, int x0, int y0, int x1, int y1, Color c, int size = 1)
		{
			int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
			for (int guard = 0; guard < 2000; guard++) {
				Dot(sb, x0, y0, c, size);
				if (x0 == x1 && y0 == y1)
					break;
				int e2 = 2 * err;
				if (e2 >= dy) {
					err += dy;
					x0 += sx;
				}
				if (e2 <= dx) {
					err += dx;
					y0 += sy;
				}
			}
		}

		private static Texture2D Pix(string name) => PixelFx.Tex("Pix/" + name);

		/// <summary>Draws one frame of a toned sheet, painting tone i with ramp step (i + shift).</summary>
		private static void Toned(SpriteBatch sb, string sheet, int w, int h, int tones, int frame, Vector2 at, in Ramp ramp, int shift = 0, float rot = 0f)
		{
			Texture2D tex = Pix(sheet);
			Vector2 pos = new((int)at.X, (int)at.Y);
			for (int t = 0; t < tones; t++)
				sb.Draw(tex, pos, new Rectangle(t * w, frame * h, w, h), ramp[t + shift], rot, new Vector2(w / 2, h / 2), 1f, SpriteEffects.None, 0f);
		}

		private static void Colour(SpriteBatch sb, string sheet, int frames, int frame, Vector2 at, Color tint)
		{
			Texture2D tex = Pix(sheet);
			int h = tex.Height / frames;
			sb.Draw(tex, new Vector2((int)at.X - tex.Width / 2, (int)at.Y - h / 2), new Rectangle(0, Math.Clamp(frame, 0, frames - 1) * h, tex.Width, h), tint);
		}

		/// <summary>Filled ring between two radii, row by row (no gaps), optionally checker-dithered.</summary>
		private static void Band(SpriteBatch sb, int cx, int cy, float inner, float outer, Color c, bool dither, int seed, float broken)
		{
			int top = (int)Math.Ceiling(outer);
			for (int y = -top; y <= top; y++) {
				if (broken > 0f && ((y * 7919 + seed * 31) & 15) < broken * 16f)
					continue;
				float yy = y * y;
				if (yy > outer * outer)
					continue;
				int xo = (int)Math.Sqrt(outer * outer - yy);
				int xi = yy < inner * inner ? (int)Math.Ceiling(Math.Sqrt(inner * inner - yy)) : 0;
				if (!dither) {
					Span(sb, cx + xi, cx + xo, cy + y, c);
					Span(sb, cx - xo, cx - xi, cy + y, c);
					if (xi == 0)
						continue;
				}
				else {
					for (int x = xi; x <= xo; x++)
						if (((x + y) & 1) == 0) {
							Dot(sb, cx + x, cy + y, c);
							Dot(sb, cx - x, cy + y, c);
						}
				}
			}
		}

		// ---- particle drawing

		private static void Draw(SpriteBatch sb, ref Particle p)
		{
			float t = 1f - p.Life / p.Max;   // 0 at birth, 1 at death
			Vector2 at = ToLayer(p.Pos);
			int x = (int)at.X, y = (int)at.Y;
			Ramp ramp = Ramp.For(p.Color);
			switch (p.Kind) {
				case ParticleKind.Spark: {
					float speed = p.Vel.Length();
					Vector2 dir = speed > 0.01f ? p.Vel / speed : Vector2.UnitX;
					int len = (int)Math.Min(18f, speed * 0.9f * p.Scale) + 1;
					int size = p.Scale >= 1.4f && t < 0.5f ? 2 : 1;
					int shift = t < 0.25f ? 0 : t < 0.55f ? 1 : t < 0.8f ? 2 : 3;
					int tx = x - (int)(dir.X * len), ty = y - (int)(dir.Y * len);
					int mx = (x + tx) / 2, my = (y + ty) / 2;
					Line(sb, mx, my, tx, ty, ramp[shift + 2]);
					Line(sb, x, y, mx, my, ramp[shift + 1], size);
					Dot(sb, x, y, ramp[shift], size);
					break;
				}
				case ParticleKind.Ember: {
					if (((int)(p.Life + p.Seed) / 3) % 5 == 0)
						break;   // flicker
					int size = p.Scale >= 1.4f && t < 0.5f ? 2 : 1;
					Dot(sb, x, y, ramp.At(t, 1, 4), size);
					break;
				}
				case ParticleKind.Smoke: {
					bool grey = Math.Abs(p.Color.R - p.Color.G) < 24 && Math.Abs(p.Color.G - p.Color.B) < 24;
					// Smoke sits in the dark half of its ramp so it never outshines the fire it comes from.
					Ramp r = grey ? Ramp.Smoke : ramp;
					int frame = Math.Min(7, (int)(t * 8f));
					if (p.Scale >= 2f)
						Toned(sb, "SmokeBig", 34, 34, 3, frame, at, r, 2);
					else
						Toned(sb, "Smoke", 20, 20, 3, frame, at, r, 2);
					break;
				}
				case ParticleKind.Star: {
					int arm = Math.Max(0, (int)(p.Scale * 2f * (1f - t * t)) + 1);
					Color mid = ramp[t < 0.5f ? 1 : 2], tip = ramp[t < 0.5f ? 2 : 3];
					Dot(sb, x, y, ramp[t < 0.6f ? 0 : 1]);
					for (int k = 1; k <= arm; k++) {
						Color c = k == arm ? tip : mid;
						Dot(sb, x + k, y, c);
						Dot(sb, x - k, y, c);
						Dot(sb, x, y + k, c);
						Dot(sb, x, y - k, c);
					}
					if (arm >= 3 && t < 0.5f) {
						Dot(sb, x + 1, y + 1, tip);
						Dot(sb, x - 1, y - 1, tip);
						Dot(sb, x + 1, y - 1, tip);
						Dot(sb, x - 1, y + 1, tip);
					}
					break;
				}
				case ParticleKind.Shard: {
					int f = ((int)(p.Life / 3) + p.Seed) % 4;
					Color a = ramp[f == 0 ? 0 : 1], b = ramp[2];
					switch (f) {
						case 0: Dot(sb, x - 1, y, b); Dot(sb, x, y, a); Dot(sb, x + 1, y, a); break;
						case 1: Dot(sb, x - 1, y - 1, b); Dot(sb, x, y, a); Dot(sb, x + 1, y + 1, b); break;
						case 2: Dot(sb, x, y - 1, b); Dot(sb, x, y, a); break;
						default: Dot(sb, x + 1, y - 1, b); Dot(sb, x, y, a); Dot(sb, x - 1, y + 1, b); break;
					}
					break;
				}
				case ParticleKind.Card:
					Colour(sb, "Card", 4, ((int)(p.Life / 4) + p.Seed) % 4, at, Color.White);
					break;
				case ParticleKind.Coin:
					Colour(sb, "Coin", 4, ((int)(p.Life / 4) + p.Seed) % 4, at, Color.White);
					break;
				case ParticleKind.Impact: {
					// Pop, hold the big frame for three steps, then hollow out.
					int[] seq = { 0, 1, 1, 1, 2, 3, 4, 5 };
					int frame = seq[Math.Min(seq.Length - 1, (int)(t * seq.Length))];
					Toned(sb, "ImpactStar", 32, 32, 3, frame, at, ramp);
					break;
				}
				case ParticleKind.Ring: {
					float ease = 1f - (1f - t) * (1f - t) * (1f - t);
					float r = p.Size / 2f * ease;
					float wake = Math.Max(1f, p.Scale * (1f - t * 0.7f));
					float broken = t > 0.6f ? (t - 0.6f) * 1.6f : 0f;
					int step = t < 0.3f ? 0 : t < 0.65f ? 1 : 2;
					Band(sb, x, y, r - wake - 1f, r - 1f, ramp[step + 2], true, p.Seed, broken);
					Band(sb, x, y, r - 1f, r, ramp[step], false, p.Seed, broken);
					break;
				}
				case ParticleKind.Glow: {
					Color c = ramp[2] * (0.45f * (1f - t) * (1f - t));
					c.A = 0;
					DrawGlow(sb, at, p.Size, c);
					break;
				}
				case ParticleKind.Explosion: {
					int frame = Math.Min(11, (int)(t * 12f));
					int shift = frame >= 8 ? 1 : 0;
					if (p.Size > 90f)
						Toned(sb, "ExplosionBig", 76, 76, 4, frame, at, ramp, shift);
					else
						Toned(sb, "Explosion", 40, 40, 4, frame, at, ramp, shift);
					break;
				}
				case ParticleKind.Smear:
					Toned(sb, "Smear", 48, 24, 3, Math.Min(4, (int)(t * 5f)), at, ramp, 0, p.Rot);
					break;
				case ParticleKind.Arc: {
					Vector2 end = ToLayer(p.End);
					var rand = new Terraria.Utilities.UnifiedRandom(p.Seed * 977 + (int)p.Life / 3);
					int segments = Math.Clamp((int)(Vector2.Distance(at, end) / 6f), 3, 14);
					Vector2 prev = at;
					Vector2 normal = (end - at).SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
					for (int k = 1; k <= segments; k++) {
						Vector2 next = Vector2.Lerp(at, end, k / (float)segments);
						if (k < segments)
							next += normal * rand.NextFloat(-4f, 4f);
						Line(sb, (int)prev.X + 1, (int)prev.Y, (int)next.X + 1, (int)next.Y, ramp[3]);
						Line(sb, (int)prev.X, (int)prev.Y, (int)next.X, (int)next.Y, ramp[t < 0.5f ? 0 : 1]);
						prev = next;
					}
					break;
				}
				case ParticleKind.LockOn: {
					float r = p.Size / 2f * (1f - t) + 3f;
					int dashes = 12;
					float spin = t * 1.5f;
					Color c = ramp[t < 0.7f ? 1 : 0], d = ramp[3];
					int points = Math.Max(24, (int)(r * 6.3f));
					for (int k = 0; k < points; k++) {
						float a = k / (float)points * MathHelper.TwoPi;
						if ((int)((a / MathHelper.TwoPi + spin) * dashes * 2) % 2 == 1)
							continue;
						Vector2 v = a.ToRotationVector2();
						Dot(sb, x + (int)Math.Round(v.X * (r + 1)), y + (int)Math.Round(v.Y * (r + 1)), d);
						Dot(sb, x + (int)Math.Round(v.X * r), y + (int)Math.Round(v.Y * r), c);
					}
					break;
				}
				case ParticleKind.Alert: {
					int age = (int)(p.Max - p.Life);
					int frame = age < 3 ? 0 : age < 8 ? 1 : 2;
					int bob = age < 8 ? 0 : (int)(Math.Sin(age * 0.4f) * 1.5f);
					if (p.Life < 8 && (int)p.Life % 2 == 0)
						break;   // blinks out
					Colour(sb, "Alert", 3, frame, at + new Vector2(0, bob), Color.White);
					break;
				}
			}
		}

		private static readonly int[] GlowSizes = { 12, 20, 32, 48, 72, 110 };

		private static void DrawGlow(SpriteBatch sb, Vector2 at, float radius, Color c)
		{
			int best = GlowSizes[0];
			foreach (int s in GlowSizes)
				if (Math.Abs(s - radius) < Math.Abs(best - radius))
					best = s;
			// Glow sprites are stored doubled; at half resolution they are drawn at their art size.
			Texture2D tex = PixelFx.Tex("Glow" + best);
			sb.Draw(tex, new Vector2((int)(at.X - tex.Width / 4f), (int)(at.Y - tex.Height / 4f)), null, c, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
		}
	}
}
