using System;
using System.Collections.Generic;
using CasinoWorld.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Effects
{
	/// <summary>
	/// Screen-level punch for big moments: hit stop and slow motion (single player only), impact frames,
	/// zoom kicks, a camera pull towards a point, a dark vignette, anime speed lines (drawn on the pixel layer of
	/// <see cref="Particles"/>), arrows at the screen edge pointing at off-screen danger, the music dipping under
	/// a cinematic, and a small scheduler for effects that follow each other. Client only, visual only; every
	/// part obeys the client settings.
	/// </summary>
	public class ScreenFx : ModSystem
	{
		private static int stop, slow, slowTick;
		private static int flashTimer, flashLength;
		private static Color flashColor;
		private static float flashStrength;
		private static int zoomTimer, zoomLength;
		private static float zoomAmount;
		private static int focusTimer, focusLength;
		private static float focusStrength;
		private static Vector2 focusAt;
		private static int vignetteTimer, vignetteLength;
		private static float vignetteStrength;
		private static int linesTimer, linesLength;
		private static Vector2 linesAt;
		private static Color linesColor;
		private static readonly List<(int at, Action act)> queue = new();
		private static int clock;
		private static readonly int[] edgeTimer = new int[Main.maxNPCs];
		private static int duckTimer, duckLength;
		private static float duckDepth, savedMusicVolume = -1f;

		private static bool SinglePlayer => Main.netMode == Terraria.ID.NetmodeID.SinglePlayer;

		public static bool LinesActive => linesTimer > 0;

		public override void Load()
		{
			if (Main.dedServ)
				return;
			Terraria.On_Main.DoUpdateInWorld += Freeze;
			Terraria.On_Main.SaveSettings += KeepRealVolume;
		}

		public override void Unload() => RestoreMusic();

		public override void OnWorldUnload()
		{
			stop = slow = flashTimer = zoomTimer = focusTimer = vignetteTimer = linesTimer = 0;
			Array.Clear(edgeTimer);
			queue.Clear();
			duckTimer = 0;
			RestoreMusic();
		}

		// ---- triggers

		/// <summary>Freezes the world for a few ticks (single player, Hit stop setting).</summary>
		public static void HitStop(int ticks)
		{
			if (!Main.dedServ && SinglePlayer && CasinoClientConfig.HitStopping)
				stop = Math.Max(stop, Math.Min(ticks, 8));
		}

		/// <summary>Runs the world at half speed for a while (single player, Hit stop setting).</summary>
		public static void SlowMotion(int ticks)
		{
			if (!Main.dedServ && SinglePlayer && CasinoClientConfig.HitStopping)
				slow = Math.Max(slow, Math.Min(ticks, 90));
		}

		/// <summary>Impact frame: the light jumps for two frames, then fades fast.</summary>
		public static void Flash(Color color, float strength = 0.6f, int ticks = 10)
		{
			if (Main.dedServ || !CasinoClientConfig.Flashing)
				return;
			flashColor = color;
			flashStrength = strength;
			flashTimer = flashLength = Math.Min(ticks, 10);
		}

		/// <summary>Quick zoom in that eases back out.</summary>
		public static void ZoomKick(float amount, int ticks)
		{
			if (Main.dedServ || !CasinoClientConfig.Zooming)
				return;
			zoomAmount = amount;
			zoomTimer = zoomLength = ticks;
		}

		/// <summary>Pulls the camera part of the way towards a point (strength 0..1), then lets go.</summary>
		public static void Focus(Vector2 at, int ticks, float strength = 0.4f)
		{
			if (Main.dedServ || !CasinoClientConfig.Zooming)
				return;
			focusAt = at;
			focusStrength = strength;
			focusTimer = focusLength = ticks;
		}

		/// <summary>Dark dithered edges around the screen.</summary>
		public static void Vignette(int ticks, float strength = 0.8f)
		{
			if (Main.dedServ || !CasinoClientConfig.Instance.ScreenEffects)
				return;
			vignetteStrength = strength;
			vignetteTimer = vignetteLength = ticks;
		}

		/// <summary>Anime speed lines rushing towards a point in the world.</summary>
		public static void SpeedLines(Vector2 at, Color color, int ticks)
		{
			if (Main.dedServ || !CasinoClientConfig.Instance.ScreenEffects)
				return;
			linesAt = at;
			linesColor = color;
			linesTimer = linesLength = ticks;
		}

		/// <summary>While the NPC is off screen, an arrow at the screen edge points at it.</summary>
		public static void EdgeWarning(NPC npc, int ticks)
		{
			if (!Main.dedServ)
				edgeTimer[npc.whoAmI] = Math.Max(edgeTimer[npc.whoAmI], ticks);
		}

		/// <summary>Music dips (depth 0..1) and comes back over the given ticks, so a cinematic's stinger cuts through.</summary>
		public static void DuckMusic(int ticks, float depth = 0.6f)
		{
			if (Main.dedServ)
				return;
			duckDepth = Math.Max(duckTimer > 0 ? duckDepth : 0f, depth);
			duckTimer = duckLength = Math.Max(duckTimer, ticks);
		}

		/// <summary>Runs an action a number of ticks later (client only, dropped when the world unloads).</summary>
		public static void After(int ticks, Action act)
		{
			if (!Main.dedServ)
				queue.Add((clock + ticks, act));
		}

		// ---- update

		private static void Freeze(Terraria.On_Main.orig_DoUpdateInWorld orig, Main self, System.Diagnostics.Stopwatch sw)
		{
			if (stop > 0 && SinglePlayer && !Main.gameMenu) {
				stop--;
				return;
			}
			if (slow > 0 && SinglePlayer && !Main.gameMenu) {
				slow--;
				if (++slowTick % 2 == 0)
					return;
			}
			orig(self, sw);
		}

		// The ducked volume must never be saved as the player's setting.
		private static bool KeepRealVolume(Terraria.On_Main.orig_SaveSettings orig)
		{
			float ducked = Main.musicVolume;
			bool active = savedMusicVolume >= 0f;
			if (active)
				Main.musicVolume = savedMusicVolume;
			bool result = orig();
			if (active)
				Main.musicVolume = ducked;
			return result;
		}

		private static void RestoreMusic()
		{
			if (savedMusicVolume >= 0f) {
				Main.musicVolume = savedMusicVolume;
				savedMusicVolume = -1f;
			}
		}

		public override void PostUpdateEverything()
		{
			if (Main.dedServ)
				return;
			clock++;
			if (flashTimer > 0)
				flashTimer--;
			if (zoomTimer > 0)
				zoomTimer--;
			if (focusTimer > 0)
				focusTimer--;
			if (vignetteTimer > 0)
				vignetteTimer--;
			if (linesTimer > 0)
				linesTimer--;
			for (int k = 0; k < edgeTimer.Length; k++)
				if (edgeTimer[k] > 0)
					edgeTimer[k]--;
			UpdateDuck();
			for (int k = queue.Count - 1; k >= 0; k--)
				if (queue[k].at <= clock) {
					Action act = queue[k].act;
					queue.RemoveAt(k);
					act();
				}
		}

		private static void UpdateDuck()
		{
			if (duckTimer <= 0) {
				RestoreMusic();
				return;
			}
			duckTimer--;
			if (savedMusicVolume < 0f)
				savedMusicVolume = Main.musicVolume;
			// Down in 8 ticks, hold, back up over the last third.
			float age = duckLength - duckTimer;
			float dip = Math.Min(1f, age / 8f) * Math.Min(1f, duckTimer / (duckLength / 3f + 1f));
			Main.musicVolume = savedMusicVolume * (1f - duckDepth * dip);
			if (duckTimer == 0)
				RestoreMusic();
		}

		/// <summary>0 -> 1 -> 0 over the effect's life, rising fast and settling slowly.</summary>
		private static float Curve(int timer, int length)
		{
			if (length <= 0)
				return 0f;
			float t = 1f - timer / (float)length;
			return t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f * ((t - 0.15f) / 0.85f);
		}

		public override void ModifyTransformMatrix(ref Terraria.Graphics.SpriteViewMatrix Transform)
		{
			if (zoomTimer > 0)
				Transform.Zoom *= 1f + zoomAmount * Curve(zoomTimer, zoomLength);
		}

		public override void ModifyScreenPosition()
		{
			if (focusTimer <= 0)
				return;
			Vector2 goal = focusAt - new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
			Main.screenPosition = Vector2.Lerp(Main.screenPosition, goal, focusStrength * Curve(focusTimer, focusLength));
		}

		/// <summary>Speed lines, drawn by <see cref="Particles"/> into its half-resolution layer (origin in world pixels).</summary>
		public static void DrawLines(SpriteBatch sb, Vector2 origin)
		{
			if (linesTimer <= 0)
				return;
			Vector2 center = (linesAt - origin) / 2f;
			float far = new Vector2(Main.screenWidth, Main.screenHeight).Length() / 2f;
			// New lines every other frame, like hand-drawn anime frames; they thin out as the effect ends.
			var rand = new Terraria.Utilities.UnifiedRandom(linesTimer / 2 * 7919);
			Ramp ramp = Ramp.For(linesColor);
			int lines = (int)(40 * Math.Min(1f, linesTimer / (linesLength * 0.5f)));
			for (int k = 0; k < lines; k++) {
				float angle = rand.NextFloat(MathHelper.TwoPi);
				float inner = far * rand.NextFloat(0.3f, 0.5f);
				float outer = inner + far * rand.NextFloat(0.2f, 0.6f);
				Vector2 dir = angle.ToRotationVector2();
				Vector2 a = center + dir * inner;
				Color c = ramp[rand.NextBool(3) ? 0 : 1];
				// Rotated one-pixel quads rasterise with hard steps at this resolution, like a drawn line.
				sb.Draw(TextureAssets.MagicPixel.Value, new Vector2((int)a.X, (int)a.Y), new Rectangle(0, 0, 1, 1), c, angle, new Vector2(0f, 0.5f),
					new Vector2(outer - inner, rand.NextBool(4) ? 2f : 1f), SpriteEffects.None, 0f);
			}
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			layers.Insert(0, new LegacyGameInterfaceLayer("CasinoWorld: Screen Effects", () => {
				Draw(Main.spriteBatch);
				return true;
			}, InterfaceScaleType.None));
		}

		private static void Draw(SpriteBatch sb)
		{
			bool any = vignetteTimer > 0 || flashTimer > 0 || Array.Exists(edgeTimer, t => t > 0);
			if (!any)
				return;
			sb.End();
			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise);
			var screen = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
			if (vignetteTimer > 0) {
				float a = vignetteStrength * Math.Min(1f, Math.Min((vignetteLength - vignetteTimer) / 12f, vignetteTimer / 25f));
				sb.Draw(PixelFx.Tex("Particles/Vignette"), screen, Color.Black * a);
			}
			DrawEdgeArrows(sb);
			if (flashTimer > 0) {
				int age = flashLength - flashTimer;
				// Two frames at full strength (the impact frame), then a quick fade. Additive and capped,
				// so it brightens the scene for a blink instead of washing it out.
				float a = Math.Min(0.18f, flashStrength * 0.3f);
				if (age >= 2)
					a *= 0.5f * flashTimer / (float)(flashLength - 2);
				Color c = flashColor * a;
				c.A = 0;
				sb.Draw(TextureAssets.MagicPixel.Value, screen, c);
			}
			sb.End();
			sb.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Matrix.Identity);
		}

		private static void DrawEdgeArrows(SpriteBatch sb)
		{
			Texture2D tex = PixelFx.Tex("Pix/EdgeArrow");
			int h = tex.Height / 2;
			Vector2 mid = new(Main.screenWidth / 2f, Main.screenHeight / 2f);
			for (int k = 0; k < edgeTimer.Length; k++) {
				if (edgeTimer[k] <= 0 || !Main.npc[k].active)
					continue;
				Vector2 rel = (Main.npc[k].Center - Main.screenPosition - mid) * Main.GameViewMatrix.Zoom.X;
				if (Math.Abs(rel.X) < mid.X - 20 && Math.Abs(rel.Y) < mid.Y - 20)
					continue;   // on screen: the boss's own telegraph is visible
				float angle = (float)Math.Round(rel.ToRotation() / MathHelper.PiOver4) * MathHelper.PiOver4;
				float scale = Math.Min((mid.X - 40) / Math.Max(1f, Math.Abs(rel.X)), (mid.Y - 40) / Math.Max(1f, Math.Abs(rel.Y)));
				Vector2 at = mid + rel * Math.Min(1f, scale);
				bool bright = edgeTimer[k] / 6 % 2 == 0;
				sb.Draw(tex, new Vector2((int)at.X / 2 * 2, (int)at.Y / 2 * 2), new Rectangle(0, bright ? 0 : h, tex.Width, h), Color.White, angle,
					new Vector2(tex.Width / 2f, h / 2f), 2f, SpriteEffects.None, 0f);
			}
		}
	}
}
