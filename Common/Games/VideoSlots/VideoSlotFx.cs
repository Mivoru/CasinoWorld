using System;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CasinoWorld.Common.Games.VideoSlots
{
	/// <summary>
	/// Theme art of the video slots: symbol plates, landing dust, the scenery in the gutters beside the reels
	/// (pillars, bucket, ice, trophies), changing backdrops and the curtain that opens each bonus. Drawing only,
	/// nothing here touches the odds. Strong flashes and shakes follow the Screen effects setting.
	/// </summary>
	public partial class VideoSlotPanel
	{
		private static bool Strong => CasinoClientConfig.Instance.ScreenEffects;

		private float bonusFade;               // 0 base game .. 1 free spins
		private float[] charge = new float[4];  // Lunar pillars
		private float bucket, crack, heat, glare;
		private int glareAge = 99;

		private static Color Add(Color c)
		{
			c.A = 0;
			return c;
		}

		private static float Noise(int k) => (float)((Math.Sin(k * 127.1) * 43758.5453 % 1 + 1) % 1);

		private static int Tier(SlotMachine m, int symbol) => symbol == m.Scatter ? 3 : symbol == m.Wild ? 2 : symbol >= 4 ? 1 : 0;

		/// <summary>Advances the slow scenery values once per update.</summary>
		private void UpdateScenery()
		{
			VideoSlotRound round = Round;
			if (round.Machine == null)
				return;
			bonusFade = MathHelper.Lerp(bonusFade, round.InBonusRun && round.Phase != VideoPhase.Outro ? 1f : 0f, 0.03f);
			for (int k = 0; k < 4; k++)
				charge[k] = Math.Max(0f, charge[k] - (round.Phase == VideoPhase.Steps ? 0f : 0.01f));
			float fill = round.InBonusRun && round.LastBet > 0 ? MathHelper.Clamp(round.BonusTotal / (float)(round.LastBet * 60), 0f, 1f) : 0f;
			bucket = MathHelper.Lerp(bucket, fill, 0.05f);
			crack = Math.Max(0f, crack - 0.004f);
			heat = MathHelper.Lerp(heat, round.BigWinTimer > 0 ? 1f : round.ShownUnits > 0 && round.Phase == VideoPhase.Steps ? 0.4f : 0f, 0.06f);
			glare *= 0.93f;
			glareAge++;
		}

		// ------------------------------------------------------------------ symbols

		/// <summary>Plate behind a symbol: plain steel, coloured for high symbols, gold for wilds, glowing for scatters.</summary>
		private void DrawPlate(SpriteBatch sb, string key, Rectangle cell, int tier, float alpha)
		{
			Color accent = SlotThemes.Accent(key), body = SlotThemes.Body(key);
			Rectangle r = CasinoDraw.Shrink(cell, 3);
			Color low = tier switch { 0 => Color.Lerp(new Color(26, 26, 36), body, 0.25f), 1 => Color.Lerp(body, Color.Black, 0.25f), 2 => new Color(96, 66, 14), _ => Color.Lerp(accent, Color.Black, 0.62f) };
			Color high = tier switch { 0 => Color.Lerp(new Color(48, 48, 62), body, 0.3f), 1 => Color.Lerp(body, accent, 0.3f), 2 => new Color(190, 140, 40), _ => Color.Lerp(accent, Color.Black, 0.3f) };
			const int Bands = 5;
			for (int k = 0; k < Bands; k++)
				CasinoDraw.Fill(sb, new Rectangle(r.X, r.Y + r.Height * k / Bands, r.Width, r.Height / Bands + 1), Color.Lerp(high, low, k / (Bands - 1f)) * alpha);
			Color edge = tier switch { 0 => new Color(70, 70, 88), 1 => Color.Lerp(accent, body, 0.5f), 2 => Color.Gold, _ => accent };
			CasinoDraw.Fill(sb, new Rectangle(r.X, r.Y, r.Width, 2), Color.Lerp(edge, Color.White, 0.35f) * alpha);
			CasinoDraw.Fill(sb, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Color.Lerp(edge, Color.Black, 0.5f) * alpha);
			CasinoDraw.Fill(sb, new Rectangle(r.X, r.Y, 2, r.Height), edge * alpha);
			CasinoDraw.Fill(sb, new Rectangle(r.Right - 2, r.Y, 2, r.Height), Color.Lerp(edge, Color.Black, 0.35f) * alpha);
			if (tier < 2)
				return;
			float t = Main.GlobalTimeWrappedHourly;
			Vector2 c = r.Center.ToVector2();
			PixelFx.Burst(sb, c, edge * ((tier == 3 ? 0.75f : 0.5f) * alpha), t + cell.X * 0.01f);
			PixelFx.Glow(sb, c, 48f, edge * ((tier == 3 ? 0.4f + 0.15f * (float)Math.Sin(t * 4f) : 0.3f) * alpha));
		}

		/// <summary>Draws an icon rotated and stretched (win animations, spin blur).</summary>
		public static void DrawIconEx(SpriteBatch sb, int id, Vector2 center, float maxSize, Color color, float rotation, Vector2 stretch)
		{
			if (id == 0)
				return;
			Texture2D tex;
			Rectangle frame;
			SpriteEffects fx = SpriteEffects.None;
			if (id > 0) {
				Main.instance.LoadItem(id);
				tex = TextureAssets.Item[id].Value;
				frame = Main.itemAnimations[id] != null ? Main.itemAnimations[id].GetFrame(tex) : tex.Frame();
			}
			else {
				Main.instance.LoadNPC(-id);
				tex = TextureAssets.Npc[-id].Value;
				frame = new Rectangle(0, 0, tex.Width, tex.Height / Math.Max(1, Main.npcFrameCount[-id]));
				fx = SpriteEffects.FlipHorizontally;
			}
			float scale = Math.Min(2f, maxSize / Math.Max(frame.Width, frame.Height));
			sb.Draw(tex, center, frame, color, rotation, frame.Size() / 2f, stretch * scale, fx, 0f);
		}

		/// <summary>How a winning symbol moves in this theme: offset, rotation and stretch.</summary>
		private static void WinPose(string key, int symbol, float t, out Vector2 offset, out float rotation, out Vector2 stretch)
		{
			offset = Vector2.Zero;
			rotation = 0f;
			stretch = Vector2.One;
			switch (key) {
				case "LunarGods":   // floats and turns
					offset = new Vector2((float)Math.Cos(t * 4f + symbol) * 3f, (float)Math.Sin(t * 5f + symbol) * 4f);
					rotation = (float)Math.Sin(t * 3f + symbol) * 0.35f;
					break;
				case "Angler":      // swims: wags and stretches
					offset.X = (float)Math.Sin(t * 9f + symbol) * 4f;
					rotation = (float)Math.Sin(t * 14f + symbol) * 0.16f;
					stretch = new Vector2(1f + 0.08f * (float)Math.Sin(t * 14f), 1f - 0.08f * (float)Math.Sin(t * 14f));
					break;
				case "Boreal":      // hops and shivers
					float hop = Math.Abs((float)Math.Sin(t * 6f + symbol));
					offset = new Vector2((float)Math.Sin(t * 50f) * 0.8f, -hop * 8f);
					stretch = new Vector2(1.06f - hop * 0.1f, 0.94f + hop * 0.1f);
					break;
				default:            // heartbeat
					float beat = Math.Max(0f, (float)Math.Sin(t * 7f)) * Math.Max(0f, (float)Math.Sin(t * 7f));
					stretch = new Vector2(1f + beat * 0.2f);
					rotation = (float)Math.Sin(t * 22f) * 0.05f;
					break;
			}
		}

		/// <summary>Bright sweep across a winning cell.</summary>
		private static void DrawGlint(SpriteBatch sb, Rectangle cell, float t)
		{
			float p = (t * 1.1f + cell.X * 0.002f) % 1f;
			int x = cell.X + 3 + (int)(p * (cell.Width - 14));
			CasinoDraw.Fill(sb, new Rectangle(x, cell.Y + 3, 8, cell.Height - 6), Add(Color.White * (0.35f * (float)Math.Sin(p * Math.PI))));
		}

		/// <summary>A reel just landed: dust in the theme's colour along its foot and a small crack in the ice.</summary>
		private void LandFx(string key, int reel)
		{
			Rectangle g = Grid;
			Color c = key switch { "LunarGods" => new Color(150, 200, 255), "Angler" => new Color(170, 225, 255), "Boreal" => Color.White, _ => new Color(150, 60, 50) };
			for (int k = 0; k < 7; k++)
				particles.Add(new Particle { Pos = new Vector2(g.X + reel * Cell + Main.rand.Next(Cell), g.Bottom - 3), Vel = new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-1.8f, -0.2f)), Color = c, Max = Main.rand.Next(16, 30), Size = Main.rand.NextFloat(2, 4) });
		}

		/// <summary>Cells won by the previous step break up in a way that fits the machine.</summary>
		private void BreakCell(string key, Vector2 at, int symbol)
		{
			if (key != "LunarGods") {
				Burst(at, SlotThemes.Accent(key), 6, false);
				return;
			}
			// The dust flies into one of the four pillars.
			int pillar = symbol % 4;
			Vector2 to = Pillar(pillar).Center.ToVector2();
			for (int k = 0; k < 7; k++)
				particles.Add(new Particle { Pos = at + Main.rand.NextVector2Circular(14, 14), Vel = (to - at) / 26f + Main.rand.NextVector2Circular(1f, 1f), Color = PillarColors[pillar], Max = 34, Size = Main.rand.NextFloat(2, 5) });
			charge[pillar] = Math.Min(1f, charge[pillar] + 0.05f);
		}

		// ------------------------------------------------------------------ scenery beside the reels

		private static readonly Color[] PillarColors = { new(255, 150, 60), new(60, 230, 180), new(255, 110, 220), new(90, 190, 255) };

		private Rectangle Gutter(int side) => side < 0
			? new Rectangle(Area.X + 18, Area.Y + 136, Math.Max(8, Grid.X - Area.X - 26), 330)
			: new Rectangle(Grid.Right + 8, Area.Y + 136, Math.Max(8, Area.X + 538 - Grid.Right - 8), 330);

		private Rectangle Pillar(int k)
		{
			Rectangle g = Gutter(k < 2 ? -1 : 1);
			int w = Math.Min(22, g.Width / 2 - 3);
			return new Rectangle(g.X + (g.Width - w * 2 - 6) / 2 + k % 2 * (w + 6), g.Y + 26, w, g.Height - 44);
		}

		private void DrawScenery(SpriteBatch sb, VideoSlotRound round, string key, Color accent, float t)
		{
			switch (key) {
				case "LunarGods": SceneryLunar(sb, round, t); break;
				case "Angler": SceneryAngler(sb, round, t); break;
				case "Boreal": SceneryBoreal(sb, round, t); break;
				default: SceneryBlood(sb, round, t); break;
			}
		}

		private void SceneryLunar(SpriteBatch sb, VideoSlotRound round, float t)
		{
			for (int k = 0; k < 4; k++) {
				Rectangle p = Pillar(k);
				Color marble = new(150, 146, 168);
				for (int x = 0; x < p.Width; x++) {   // round shaft with flutes
					float u = x / (float)Math.Max(1, p.Width - 1) * 2f - 1f;
					float shade = 0.35f + 0.6f * (float)Math.Sqrt(Math.Max(0f, 1f - (u + 0.25f) * (u + 0.25f) * 0.7f)) - (x % 6 == 0 ? 0.12f : 0f);
					CasinoDraw.Fill(sb, new Rectangle(p.X + x, p.Y, 1, p.Height), Color.Lerp(Color.Black, marble, MathHelper.Clamp(shade, 0f, 1f)));
				}
				CasinoDraw.Fill(sb, new Rectangle(p.X - 4, p.Y - 8, p.Width + 8, 8), marble);
				CasinoDraw.Fill(sb, new Rectangle(p.X - 4, p.Y - 8, p.Width + 8, 2), Color.White * 0.7f);
				CasinoDraw.Fill(sb, new Rectangle(p.X - 4, p.Bottom, p.Width + 8, 8), Color.Lerp(marble, Color.Black, 0.35f));
				int h = (int)(p.Height * Math.Max(charge[k], bonusFade * 0.4f));
				Color c = PillarColors[k];
				CasinoDraw.Fill(sb, new Rectangle(p.X + 3, p.Bottom - h, p.Width - 6, h), Add(c * (0.5f + 0.15f * (float)Math.Sin(t * 4f + k))));
				FillCircle(sb, new Vector2(p.Center.X, p.Y - 14), 5 + charge[k] * 4f, Add(c * (0.35f + 0.65f * charge[k])));
				if (charge[k] > 0.3f && Main.rand.NextBool(12))
					particles.Add(new Particle { Pos = new Vector2(p.Center.X, p.Bottom - h), Vel = new Vector2(0, -1.5f), Color = c, Max = 30, Size = 3 });
			}
			// The total multiplier of the bonus hangs over the stage as a growing moon.
			if (round.InBonusRun && round.Machine.BonusMultiplier > 1) {
				float size = Math.Min(30f, 14f + (float)Math.Sqrt(round.Machine.BonusMultiplier) * 1.6f);
				var at = new Vector2(Stage.Right - 50, Stage.Y + 46);
				FillCircle(sb, at, size + 6, Add(new Color(120, 230, 255) * (0.18f + 0.08f * (float)Math.Sin(t * 5f))));
				FillCircle(sb, at, size, new Color(60, 110, 150));
				FillCircle(sb, at - new Vector2(size * 0.25f), size * 0.5f, new Color(130, 200, 230) * 0.6f);
				Utils.DrawBorderString(sb, "x" + round.Machine.BonusMultiplier, at + new Vector2(0, 3), Color.White, 0.9f, 0.5f, 0.5f);
			}
		}

		private void SceneryAngler(SpriteBatch sb, VideoSlotRound round, float t)
		{
			for (int side = -1; side <= 1; side += 2) {
				Rectangle g = Gutter(side);
				for (int y = 0; y < g.Height; y += 22) {   // plank wall
					Color wood = Color.Lerp(new Color(112, 74, 44), new Color(90, 58, 34), Noise(y + side));
					CasinoDraw.Fill(sb, new Rectangle(g.X, g.Y + y, g.Width, 20), wood);
					CasinoDraw.Fill(sb, new Rectangle(g.X, g.Y + y, g.Width, 2), Color.Lerp(wood, Color.White, 0.2f));
				}
				for (int y = g.Y + 4; y < g.Bottom - 4; y += 8)   // rope
					CasinoDraw.Fill(sb, new Rectangle(g.X + g.Width / 2 - 3 + (y / 8 % 2) * 2, y, 5, 8), y / 8 % 2 == 0 ? new Color(206, 178, 120) : new Color(160, 132, 84));
			}
			// Life ring on the left, bucket of coins on the right.
			Rectangle l = Gutter(-1), r = Gutter(1);
			var ring = new Vector2(l.Center.X, l.Y + 70);
			for (int k = 0; k < 24; k++)
				FillCircle(sb, ring + (k * MathHelper.TwoPi / 24f).ToRotationVector2() * 15f, 5, k / 3 % 2 == 0 ? new Color(220, 50, 50) : Color.White);
			var pail = new Rectangle(r.X + 3, r.Bottom - 74, r.Width - 6, 70);
			CasinoDraw.Fill(sb, pail, new Color(70, 78, 92));
			int h = (int)((pail.Height - 8) * bucket);
			if (h > 0) {
				CasinoDraw.Fill(sb, new Rectangle(pail.X + 3, pail.Bottom - 4 - h, pail.Width - 6, h), new Color(214, 160, 50));
				for (int k = 0; k < pail.Width / 7; k++)
					FillCircle(sb, new Vector2(pail.X + 6 + k * 7, pail.Bottom - 4 - h + (k % 2) * 2), 4, new Color(255, 220, 110));
				UiFx.Shimmer(new Rectangle(pail.X, pail.Bottom - 4 - h, pail.Width, 8), new Color(255, 235, 150), 20);
			}
			for (int k = 0; k < 4; k++)
				CasinoDraw.Fill(sb, new Rectangle(pail.X, pail.Y + k * 22, pail.Width, 3), new Color(140, 150, 168) * 0.8f);
			Vector2 handle = new(pail.Center.X, pail.Y);
			Line(sb, handle - new Vector2(pail.Width / 2f, 0), handle - new Vector2(0, 18), new Color(150, 156, 170), 3);
			Line(sb, handle + new Vector2(pail.Width / 2f, 0), handle - new Vector2(0, 18), new Color(150, 156, 170), 3);
		}

		private void SceneryBoreal(SpriteBatch sb, VideoSlotRound round, float t)
		{
			for (int side = -1; side <= 1; side += 2) {
				Rectangle g = Gutter(side);
				for (int y = 0; y < g.Height; y += 6)
					CasinoDraw.Fill(sb, new Rectangle(g.X, g.Y + y, g.Width, 6), Color.Lerp(new Color(120, 180, 220), new Color(40, 80, 130), y / (float)g.Height) * 0.85f);
				for (int k = 0; k < 5; k++) {   // icicles and drifts
					int x = g.X + k * g.Width / 5, w = Math.Max(4, g.Width / 5 - 2), len = 26 + (int)(Noise(k + side * 9) * 60);
					for (int y = 0; y < len; y += 3) {
						int ww = Math.Max(1, (int)(w * (1f - y / (float)len)));
						CasinoDraw.Fill(sb, new Rectangle(x + (w - ww) / 2, g.Y + y, ww, 3), Color.Lerp(Color.White, new Color(150, 210, 245), y / (float)len) * 0.9f);
					}
					int drift = 10 + (int)(Noise(k + 40 + side) * 22);
					CasinoDraw.Fill(sb, new Rectangle(x, g.Bottom - drift, g.Width / 5 + 1, drift), new Color(230, 242, 252));
				}
			}
			// Cracks spread through the ice frame after big wins.
			if (crack > 0.02f) {
				Rectangle f = CasinoDraw.Shrink(Grid, -5);
				for (int k = 0; k < 12; k++) {
					Vector2 at = k % 2 == 0 ? new Vector2(f.X + Noise(k) * f.Width, k % 4 == 0 ? f.Y : f.Bottom) : new Vector2(k % 4 == 1 ? f.X : f.Right, f.Y + Noise(k) * f.Height);
					Vector2 dir = (at - f.Center.ToVector2()).SafeNormalize(Vector2.UnitY);
					for (int s = 0; s < 4; s++) {
						Vector2 next = at + dir.RotatedBy((Noise(k * 7 + s) - 0.5f) * 1.6f) * 14f * crack;
						Line(sb, at, next, Color.White * (0.8f * crack), 2);
						at = next;
					}
				}
			}
		}

		private void SceneryBlood(SpriteBatch sb, VideoSlotRound round, float t)
		{
			for (int side = -1; side <= 1; side += 2) {
				Rectangle g = Gutter(side);
				CasinoDraw.Fill(sb, g, new Color(44, 26, 24));
				for (int k = 0; k < 4; k++) {   // rusty boards with bolts
					var plank = new Rectangle(g.X + k * g.Width / 4, g.Y, g.Width / 4 - 2, g.Height);
					Color rust = Color.Lerp(new Color(92, 48, 34), new Color(64, 38, 34), Noise(k + side * 3));
					CasinoDraw.Fill(sb, plank, rust);
					CasinoDraw.Fill(sb, new Rectangle(plank.X, plank.Y, 2, plank.Height), Color.Lerp(rust, Color.White, 0.15f));
					CasinoDraw.Fill(sb, new Rectangle(plank.Center.X - 2, g.Y + 8 + k % 2 * 10, 4, 4), new Color(150, 120, 100));
				}
			}
			// Trophy wall: every removed symbol hangs here as a plaque.
			if (round.Machine is BloodMoonSlot hunter) {
				Rectangle l = Gutter(-1);
				int[] icons = SlotThemes.Icons("BloodMoon");
				for (int k = 0; k < 4; k++) {
					var at = new Vector2(l.Center.X, l.Y + 50 + k * 66);
					bool hung = k < hunter.Upgrades;
					FillCircle(sb, at, 21, new Color(40, 22, 12));
					FillCircle(sb, at, 18, hung ? new Color(130, 76, 38) : new Color(58, 38, 28));
					DrawIcon(sb, icons[k], at + new Vector2(0, hung ? (float)Math.Sin(t * 2f + k) : 0), 24f, hung ? Color.White : Color.Black * 0.55f);
				}
			}
			// Shells on the right for wilds collected towards the next trophy.
			Rectangle r = Gutter(1);
			int loaded = round.Machine is BloodMoonSlot h && round.InBonusRun ? h.WildsCollected % 3 : 0;
			for (int k = 0; k < 3; k++) {
				var shell = new Rectangle(r.Center.X - 8, r.Bottom - 60 - k * 46, 16, 36);
				bool on = k < loaded;
				CasinoDraw.Fill(sb, shell, on ? new Color(200, 30, 36) : new Color(60, 30, 30));
				CasinoDraw.Fill(sb, new Rectangle(shell.X, shell.Bottom - 10, shell.Width, 10), on ? new Color(226, 172, 62) : new Color(90, 70, 40));
				CasinoDraw.Fill(sb, new Rectangle(shell.X + 2, shell.Y + 2, 3, 22), Color.White * (on ? 0.4f : 0.1f));
			}
			// Blood runs down the frame.
			Rectangle f = CasinoDraw.Shrink(Grid, -5);
			for (int k = 0; k < 22; k++) {
				float len = (Noise(k) * 26f + 6f) * (0.5f + 0.5f * bonusFade + 0.3f * heat);
				float drip = (t * (0.15f + Noise(k + 9) * 0.2f) + Noise(k + 3)) % 1f;
				int x = f.X + (int)(Noise(k + 70) * (f.Width - 4));
				Texture2D dripTex = PixelFx.Tex("Drip");
				int dripH = dripTex.Height / 4;
				sb.Draw(dripTex, PixelFx.Snap(new Vector2(x, f.Y)), new Rectangle(0, k % 4 * dripH, dripTex.Width, Math.Min(dripH, (int)len + 8)), Color.White);
				if (bonusFade > 0.3f)
					CasinoDraw.Fill(sb, new Rectangle((x + 8) / 2 * 2, (f.Y + (int)(len + drip * 60f)) / 2 * 2, 4, 6), new Color(170, 10, 20));
			}
		}

		// ------------------------------------------------------------------ backdrops on the stage

		/// <summary>Bonus mood over the stage sky, drawn after its gradient.</summary>
		private void DrawStageMood(SpriteBatch sb, VideoSlotRound round, string key, float t)
		{
			Rectangle s = Stage;
			switch (key) {
				case "LunarGods":
					for (int k = 0; k < 9; k++) {   // drifting nebula
						var at = new Vector2(s.X + (Noise(k) * s.Width + t * (6 + k)) % s.Width, s.Y + 20 + Noise(k + 5) * 60);
						FillCircleClipped(sb, at, 26 + Noise(k + 8) * 18, Add(PillarColors[k % 4] * (0.05f + 0.07f * bonusFade)), s);
					}
					if (bonusFade > 0.05f)   // the Moon's surface
						for (int x = 0; x < s.Width; x += 6) {
							int h = (int)((16 + Math.Sin(x * 0.05) * 5 + Math.Sin(x * 0.17) * 3) * bonusFade);
							CasinoDraw.Fill(sb, new Rectangle(s.X + x, s.Bottom - h, 6, h), Color.Lerp(new Color(120, 120, 140), new Color(80, 80, 104), Noise(x)));
						}
					break;
				case "Angler": {
					float left = round.Machine.FreeSpinsTotal > 0 ? round.Machine.FreeSpinsLeft / (float)round.Machine.FreeSpinsTotal : 1f;
					Color dusk = Color.Lerp(new Color(20, 14, 60), new Color(255, 110, 50), MathHelper.Clamp(left, 0f, 1f));
					CasinoDraw.Fill(sb, s, dusk * (0.5f * bonusFade));
					if (left < 0.5f)
						for (int k = 0; k < 16; k++)
							CasinoDraw.Fill(sb, new Rectangle(s.X + (int)(Noise(k) * s.Width), s.Y + (int)(Noise(k + 30) * 50), 2, 2), Color.White * (bonusFade * (1f - left * 2f)));
					DrawBoat(sb, new Vector2(s.X + 250 + (float)Math.Sin(t * 0.5f) * 14, s.Y + 64 + (float)Math.Sin(t * 1.8f) * 2), round.InBonusRun ? round.Machine.BonusMultiplier : 1);
					break;
				}
				case "Boreal":
					if (round.InBonusRun) {
						int f = round.Machine.Feature;
						if (f == BorealSlot.Wolf) {
							FillCircle(sb, new Vector2(s.X + 90, s.Y + 36), 24, new Color(240, 244, 220) * bonusFade);
							FillCircle(sb, new Vector2(s.X + 84, s.Y + 30), 5, new Color(205, 210, 190) * bonusFade);
						}
						else if (f == BorealSlot.Golem)
							for (int k = 0; k < 5; k++)
								CasinoDraw.Fill(sb, new Rectangle(s.X + (int)(Math.Sin(t * 0.4f + k) * 60 + 60), s.Y + 20 + k * 14, s.Width - 120, 10), Color.White * (0.10f * bonusFade));
						int flakes = f == BorealSlot.Flinx ? 70 : 34;
						for (int k = 0; k < flakes; k++) {   // snowfall, a blizzard for the Flinx
							float x = (Noise(k) * s.Width + t * (f == BorealSlot.Flinx ? 170 : 20)) % s.Width;
							float y = (Noise(k + 50) * s.Height + t * (30 + Noise(k) * 40)) % s.Height;
							CasinoDraw.Fill(sb, new Rectangle(s.X + (int)x, s.Y + (int)y, f == BorealSlot.Flinx ? 5 : 2, 2), Color.White * (0.8f * bonusFade));
						}
					}
					break;
				default: {
					int trophies = round.Machine is BloodMoonSlot h && round.InBonusRun ? h.Upgrades : 0;
					float size = 20 + trophies * 7;   // the moon swells with every trophy
					FillCircleClipped(sb, new Vector2(s.Right - 120, s.Y + 44), size + 26, Add(new Color(255, 30, 20) * (0.12f * bonusFade)), s);
					FillCircleClipped(sb, new Vector2(s.Right - 120, s.Y + 44), size, new Color(220, 24, 24) * (0.55f * bonusFade), s);
					for (int k = 0; k < 5; k++) {   // zombies shamble along the horizon
						float x = (Noise(k) * s.Width + t * (7 + k * 2)) % (s.Width - 20);
						int y = s.Bottom - 14, sway = (int)(Math.Sin(t * 3f + k) * 2);
						Color z = new Color(18, 2, 6) * 0.95f;
						CasinoDraw.Fill(sb, new Rectangle(s.X + (int)x, y - 18, 7, 18), z);
						CasinoDraw.Fill(sb, new Rectangle(s.X + (int)x + sway, y - 25, 7, 7), z);
						CasinoDraw.Fill(sb, new Rectangle(s.X + (int)x + 5, y - 15 + sway, 9, 2), z);
					}
					for (int k = 0; k < 4; k++)   // ground fog
						CasinoDraw.Fill(sb, new Rectangle(s.X + (int)((Math.Sin(t * 0.3f + k * 2) * 0.5 + 0.5) * 120), s.Bottom - 10 - k * 5, s.Width - 130, 6), new Color(180, 60, 60) * 0.10f);
					break;
				}
			}
		}

		private static void FillCircleClipped(SpriteBatch sb, Vector2 center, float radius, Color c, Rectangle clip)
		{
			for (int y = -(int)radius; y <= radius; y += 2) {
				int half = (int)Math.Sqrt(Math.Max(0, radius * radius - y * y));
				var row = Rectangle.Intersect(new Rectangle((int)center.X - half, (int)center.Y + y, half * 2, 2), clip);
				if (row.Width > 0 && row.Height > 0)
					CasinoDraw.Fill(sb, row, c);
			}
		}

		/// <summary>The Angler's boat grows with the collect multiplier: raft, rowboat, sailboat, galleon.</summary>
		private static void DrawBoat(SpriteBatch sb, Vector2 at, int multiplier)
		{
			int size = multiplier >= 10 ? 3 : multiplier >= 3 ? 2 : multiplier >= 2 ? 1 : 0;
			int w = 30 + size * 16;
			Color hull = size == 3 ? new Color(150, 60, 40) : new Color(120, 78, 44);
			for (int y = 0; y < 6 + size * 3; y++)
				CasinoDraw.Fill(sb, new Rectangle((int)at.X - w / 2 + y, (int)at.Y + y, w - y * 2, 1), Color.Lerp(hull, Color.Black, y * 0.05f));
			CasinoDraw.Fill(sb, new Rectangle((int)at.X - w / 2 - 2, (int)at.Y - 2, w + 4, 2), new Color(200, 150, 90));
			if (size == 0)
				return;
			int masts = Math.Max(1, size - 1);
			for (int m = 0; m < masts; m++) {
				int mx = (int)at.X + (masts == 1 ? 0 : m * 24 - 12), mh = 22 + size * 6;
				CasinoDraw.Fill(sb, new Rectangle(mx, (int)at.Y - mh, 2, mh), new Color(90, 60, 36));
				if (size == 1)
					continue;
				for (int y = 0; y < mh - 8; y++)   // sail
					CasinoDraw.Fill(sb, new Rectangle(mx + 2, (int)at.Y - mh + 2 + y, (int)(14 * Math.Sin(Math.PI * (y + 2) / (mh - 4.0))), 1), size == 3 ? new Color(250, 215, 90) : new Color(240, 236, 220));
			}
		}

		// ------------------------------------------------------------------ bonus curtain

		private const int CurtainTicks = 58;

		/// <summary>Covers the whole window when a bonus starts and opens again in the machine's own way.</summary>
		private void DrawCurtain(SpriteBatch sb, VideoSlotRound round, string key, float t)
		{
			if (round.Phase != VideoPhase.Intro || round.Timer > CurtainTicks)
				return;
			Rectangle a = CasinoDraw.Shrink(Area, 6);
			float p = round.Timer / (float)CurtainTicks;
			float cover = p < 0.45f ? p / 0.45f : 1f - (p - 0.45f) / 0.55f;
			cover = cover * cover * (3 - 2 * cover);
			switch (key) {
				case "LunarGods": {   // carved stone gates close and swing open
					Texture2D gate = PixelFx.Tex("CurtainGate");
					int w = (int)(a.Width / 2f * cover) / 2 * 2;
					if (w > 0) {
						sb.Draw(gate, new Rectangle(a.X, a.Y, w, a.Height), new Rectangle(gate.Width - w, 0, w, Math.Min(gate.Height, a.Height)), Color.White);
						sb.Draw(gate, new Rectangle(a.Right - w, a.Y, w, a.Height), new Rectangle(gate.Width - w, 0, w, Math.Min(gate.Height, a.Height)), Color.White, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 0f);
					}
					if (cover > 0.95f)
						PixelFx.Burst(sb, a.Center.ToVector2(), new Color(150, 240, 255), t, 5);
					break;
				}
				case "Angler": {   // the sea rises and drains
					int h = (int)(a.Height * cover) / 2 * 2;
					if (h > 24) {
						Texture2D wave = PixelFx.Tex("Wave");
						PixelFx.Tile(sb, "Water", new Rectangle(a.X, a.Bottom - h + wave.Height, a.Width, h - wave.Height), Color.White, (int)(t * 30f), (int)(t * 18f));
						PixelFx.Tile(sb, "Wave", new Rectangle(a.X, a.Bottom - h, a.Width, wave.Height), Color.White, (int)(t * 120f));
						Texture2D bubble = PixelFx.Tex("Bubble");
						for (int k = 0; k < 26; k++) {
							float y = (t * (70 + Noise(k) * 60) + Noise(k + 1) * 500) % (h - 22);
							sb.Draw(bubble, PixelFx.Snap(new Vector2(a.X + 8 + Noise(k + 4) * (a.Width - 30) + (float)Math.Sin(t * 3 + k) * 4, a.Bottom - y - 18)), Color.White);
						}
					}
					break;
				}
				case "Boreal": {   // ice grows in from all four sides, shards first
					int h = (int)(a.Height / 2f * cover) / 2 * 2;
					if (h > 4) {
						int body = Math.Max(0, h - 60);
						Texture2D shard = PixelFx.Tex("IceShard");
						int sh = shard.Height / 4;
						foreach (bool top in new[] { true, false }) {
							if (body > 0)
								PixelFx.Tile(sb, "Ice", new Rectangle(a.X, top ? a.Y : a.Bottom - body, a.Width, body), Color.White);
							for (int x = 0; x * 30 < a.Width; x++) {
								int kind = (x * 7 + (top ? 0 : 3)) % 4, sw = Math.Min(shard.Width, a.Width - x * 30), len = Math.Min(sh, Math.Min(h, a.Height / 2 + 30));
								var src = new Rectangle(0, kind * sh + (top ? Math.Max(0, sh - 120) : 0), sw, Math.Min(sh, len));
								sb.Draw(shard, new Vector2(a.X + x * 30, top ? a.Y + Math.Max(0, body - 4) : a.Bottom - Math.Max(0, body - 4) - src.Height), src, Color.White, 0f, Vector2.Zero, 1f, top ? SpriteEffects.None : SpriteEffects.FlipVertically, 0f);
							}
						}
						for (int k = 0; k < 10; k++)
							PixelFx.Twinkle(sb, new Vector2(a.X + Noise(k) * a.Width, a.Y + Noise(k + 9) * a.Height), Color.White * cover, (t * 1.4f + Noise(k + 3)) % 1f);
					}
					break;
				}
				default: {   // blood pours down the glass
					Texture2D drip = PixelFx.Tex("Drip");
					int dh = drip.Height / 4;
					for (int x = 0; x * 20 < a.Width; x++) {
						int w = Math.Min(20, a.Width - x * 20);
						int len = (int)(MathHelper.Clamp(cover * (0.7f + Noise(x) * 0.7f), 0f, 1f) * a.Height) / 2 * 2;
						if (len <= 0)
							continue;
						PixelFx.Tile(sb, "Blood", new Rectangle(a.X + x * 20, a.Y, w, len), Color.White, x * 20, -(int)(t * 60f));
						int tail = Math.Min(dh, a.Height - len);
						if (tail > 0)
							sb.Draw(drip, new Rectangle(a.X + x * 20, a.Y + len - 4, w, tail), new Rectangle(0, x * 5 % 4 * dh, w, tail), Color.White);
					}
					break;
				}
			}
		}

		private void CurtainSound(string key) => SoundEngine.PlaySound(key switch { "LunarGods" => CasinoSounds.GateOpen, "Angler" => CasinoSounds.WaterRise, "Boreal" => CasinoSounds.IceShatter, _ => CasinoSounds.HuntHorn });

		// ------------------------------------------------------------------ one-off moments

		/// <summary>Called once at the start of each step.</summary>
		private void StepMoment(VideoSlotRound round, string key, SlotStep s)
		{
			if (s.Win >= SlotMachine.Unit * 5)
				crack = Math.Min(1f, crack + 0.5f);
			if (key == "LunarGods" && s.Kind == StepKind.Multiply) {
				if (s.Multiplier >= 100) {   // eclipse: a deathray across the whole window
					glareAge = 0;
					glare = 1f;
					SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.3f });
					CasinoFx.Shake(Main.LocalPlayer.Center, Vector2.UnitX, 9f, 9f, 30, -1f, "SlotEclipse");
				}
				else if (s.Multiplier >= 10) {
					glare = 0.5f;
					CasinoFx.Shake(Main.LocalPlayer.Center, Vector2.UnitY, 4f, 8f, 14, -1f, "SlotOrbs");
				}
			}
			if (key == "Boreal" && s.Kind == StepKind.Expand) {
				CasinoFx.Shake(Main.LocalPlayer.Center, Vector2.UnitY, 6f, 8f, 18, -1f, "SlotGolem");
				SoundEngine.PlaySound(CasinoSounds.IceShatter with { Volume = 0.7f });
				crack = 1f;
			}
			if (key == "BloodMoon" && s.Kind == StepKind.Upgrade) {
				SoundEngine.PlaySound(CasinoSounds.HuntHorn with { Volume = 0.6f });
				glare = 0.6f;
			}
		}

		/// <summary>Orb revealed: the bigger the value, the bigger the break.</summary>
		private void OrbBreak(Vector2 at, int value)
		{
			Burst(at, value >= 100 ? Color.Gold : value >= 10 ? new Color(220, 130, 255) : new Color(140, 240, 255), value >= 100 ? 50 : value >= 10 ? 24 : 12, false);
			if (value >= 10)
				for (int k = 0; k < 30; k++) {   // ripple through the grid
					Vector2 dir = (k * MathHelper.TwoPi / 30f).ToRotationVector2();
					particles.Add(new Particle { Pos = at + dir * 10, Vel = dir * 7f, Color = new Color(170, 210, 255), Max = 26, Size = 4 });
				}
			if (value >= 100)
				glare = Math.Max(glare, 0.5f);
		}

		/// <summary>Screen-wide light of the machine, drawn last.</summary>
		private void DrawGlare(SpriteBatch sb, string key, Color accent, float t)
		{
			if (!Strong)
				return;
			Rectangle a = CasinoDraw.Shrink(Area, 6);
			if (key == "LunarGods" && glareAge < 50) {
				float f = glareAge / 50f, power = (float)Math.Sin(f * Math.PI);
				CasinoDraw.Fill(sb, a, Color.Black * (0.55f * power));
				Vector2 eye = MoonEye;
				for (int k = -1; k <= 1; k++)
					Beam(sb, eye, new Vector2(Grid.Center.X + k * 130 + (f - 0.5f) * 160, Grid.Bottom), power * (k == 0 ? 2.2f : 1f));
			}
			else if (glare > 0.02f)
				CasinoDraw.Fill(sb, a, Add(accent * (glare * 0.4f)));
			// Hot edges while a win is counting.
			if (heat > 0.05f)
				for (int k = 0; k < 6; k++) {
					Color c = Add(accent * (heat * 0.09f * (6 - k) / 6f * (0.8f + 0.2f * (float)Math.Sin(t * 9f))));
					CasinoDraw.Fill(sb, new Rectangle(a.X + k * 4, a.Y + k * 4, a.Width - k * 8, 4), c);
					CasinoDraw.Fill(sb, new Rectangle(a.X + k * 4, a.Bottom - k * 4 - 4, a.Width - k * 8, 4), c);
					CasinoDraw.Fill(sb, new Rectangle(a.X + k * 4, a.Y, 4, a.Height), c);
					CasinoDraw.Fill(sb, new Rectangle(a.Right - k * 4 - 4, a.Y, 4, a.Height), c);
				}
		}

		/// <summary>Extras on single cells: ice over held wilds, trails, flames, bullet holes, legendary fish.</summary>
		private void DrawCellExtras(SpriteBatch sb, VideoSlotRound round, string key, SlotCell c, Rectangle cell, Pos pos, SlotStep step, float t)
		{
			Vector2 center = cell.Center.ToVector2();
			SlotMachine m = round.Machine;
			switch (key) {
				case "Angler" when c.Symbol == AnglerSlot.Fish && c.Value >= 250:
					bool legend = c.Value >= 1000;
					for (int k = 0; k < (legend ? 10 : 6); k++)
						FillCircle(sb, center + (k * MathHelper.TwoPi / (legend ? 10 : 6) + t * 2f).ToRotationVector2() * Cell * 0.42f, legend ? 4 : 2, Add(Color.Gold * 0.8f));
					UiFx.Shimmer(cell, Color.Gold, legend ? 6 : 24);
					break;
				case "Boreal" when c.Sticky && m.Feature == BorealSlot.Wolf:
					CasinoDraw.Fill(sb, CasinoDraw.Shrink(cell, 4), new Color(170, 225, 255) * 0.28f);
					Line(sb, new Vector2(cell.X + 8, cell.Y + 12), new Vector2(cell.X + 28, cell.Y + 6), Color.White * 0.7f, 3);
					Line(sb, new Vector2(cell.X + 8, cell.Y + 22), new Vector2(cell.X + 20, cell.Y + 18), Color.White * 0.5f, 2);
					break;
				case "Boreal" when c.Sticky && m.Feature == BorealSlot.Penguin:
					for (int k = 0; k < 4; k++)   // footprints behind the walking wild
						CasinoDraw.Fill(sb, new Rectangle(cell.Right + 6 + k * 14, cell.Center.Y + 12 + k % 2 * 8, 6, 4), new Color(200, 235, 255) * (0.55f - k * 0.12f));
					break;
				case "BloodMoon":
					if (c.Symbol == m.Wild && m is BloodMoonSlot { Upgrades: >= 4 })
						for (int k = 0; k < 4; k++)   // the hunt: wilds burn
							PixelFx.Flame(sb, new Vector2(cell.X + 14 + k * (cell.Width - 28) / 3f, cell.Bottom - 4), t, k * 2 + pos.Reel + pos.Row, 0.9f);
					int hit = step == null || round.Phase != VideoPhase.Steps || step.Kind != StepKind.Shots ? -1 : step.Fx.IndexOf(pos);
					if (hit >= 0 && round.Timer >= 6 + 12 * hit) {   // bullet hole in the glass
						FillCircle(sb, center, 5, Color.Black * 0.8f);
						for (int k = 0; k < 8; k++) {
							Vector2 d = (k * MathHelper.TwoPi / 8f + Noise(k + pos.Reel) * 0.5f).ToRotationVector2();
							Line(sb, center + d * 5, center + d * (12 + Noise(k + pos.Row) * 16), Color.White * 0.55f, 1);
						}
					}
					break;
			}
		}

		/// <summary>Moon Lord's pupils follow the mouse, or the orb he is about to reveal.</summary>
		private void DrawMoonPupils(SpriteBatch sb, VideoSlotRound round, SlotStep step, int tick, bool revealing)
		{
			Vector2 look = CasinoTableGameSystem.UIMouse.ToVector2();
			if (revealing && step.Fx.Count > 0) {
				Pos p = step.Fx[Math.Clamp((tick - BeamStart) / BeamGap, 0, step.Fx.Count - 1)];
				look = CellRect(p.Reel, p.Row).Center.ToVector2();
			}
			foreach (int side in new[] { -1, 1 }) {
				Vector2 eye = MoonHeadTop + new Vector2(side * 15, 48);
				Vector2 d = (look - eye).SafeNormalize(Vector2.UnitY) * 3f;
				CasinoDraw.Fill(sb, new Rectangle((int)(eye.X + d.X) - 2, (int)(eye.Y + d.Y) - 2, 4, 4), new Color(10, 40, 50));
			}
		}

		/// <summary>A fish leaps from its cell into the bucket in an arc.</summary>
		private void FishLeap(SpriteBatch sb, Vector2 from, int icon, float p)
		{
			Rectangle r = Gutter(1);
			var to = new Vector2(r.Center.X, r.Bottom - 70);
			Vector2 at = Vector2.Lerp(from, to, p) - new Vector2(0, (float)Math.Sin(p * Math.PI) * 110f);
			DrawIconEx(sb, icon, at, Cell * 0.5f * (1f - p * 0.4f), Color.White, p * 5f, Vector2.One);
		}

		/// <summary>The hunter himself: coat, hat and recoil. The gun is drawn by DrawHunter.</summary>
		private void DrawHunterBody(SpriteBatch sb, float recoil, float t)
		{
			Main.instance.LoadNPC(NPCID.ArmsDealer);
			Texture2D tex = TextureAssets.Npc[NPCID.ArmsDealer].Value;
			var frame = new Rectangle(0, 0, tex.Width, tex.Height / Math.Max(1, Main.npcFrameCount[NPCID.ArmsDealer]));
			Vector2 feet = HunterFeet + new Vector2(recoil * 4f, 0);
			sb.Draw(tex, feet, frame, Color.Lerp(Color.White, new Color(255, 120, 110), 0.35f), recoil * 0.06f, new Vector2(frame.Width / 2f, frame.Height - 2), 1.25f, SpriteEffects.None, 0f);
		}

		/// <summary>Gun on the shoulder: stock against him, barrel towards the target, one hand on the grip and one under the barrel.</summary>
		private void DrawGun(SpriteBatch sb, Texture2D gun, float recoil)
		{
			const float Scale = 1.15f;
			Vector2 dir = gunAim.ToRotationVector2();
			bool left = dir.X < 0;
			var grip = new Vector2(gun.Width * 0.26f, gun.Height * 0.62f);
			Vector2 at = HunterShoulder + dir * (9f - recoil * 7f) + new Vector2(recoil * 4f, 3f);
			Vector2 fore = at + dir * (gun.Width * 0.36f * Scale) + new Vector2(0, -1.5f);
			Color sleeve = new(126, 128, 106), sleeveDark = new(52, 50, 44), skin = new(128, 84, 62);
			// Far arm reaches under the barrel, behind the gun.
			Vector2 farShoulder = HunterShoulder + new Vector2(-5 + recoil * 4f, 3);
			Line(sb, farShoulder, fore, sleeveDark, 8);
			Line(sb, farShoulder, fore, Color.Lerp(sleeve, Color.Black, 0.25f), 5);
			sb.Draw(gun, at, null, Color.White, gunAim, left ? new Vector2(grip.X, gun.Height - grip.Y) : grip, Scale, left ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);
			FillCircle(sb, fore, 3, skin);
			// Near arm: elbow down, hand on the trigger.
			Vector2 nearShoulder = HunterShoulder + new Vector2(6 + recoil * 4f, 3), elbow = (nearShoulder + at) / 2f + new Vector2(3, 9);
			Line(sb, nearShoulder, elbow, sleeveDark, 9);
			Line(sb, elbow, at, sleeveDark, 8);
			Line(sb, nearShoulder, elbow, sleeve, 6);
			Line(sb, elbow, at, sleeve, 5);
			FillCircle(sb, at, 3, skin);
			Muzzle = at + dir * ((gun.Width - grip.X) * Scale) + new Vector2(0, -8f);   // the barrel lies above the grip
		}

		private static Vector2 rodGrip, rodTip;
		private static bool rodMeasured;

		/// <summary>Ends of the rod sprite, read from its pixels: the lowest pixel on the left is the grip, the rightmost one the tip.</summary>
		private static void FindRodEnds(Texture2D rod, out Vector2 grip, out Vector2 tip)
		{
			if (!rodMeasured) {
				rodMeasured = true;
				rodGrip = new Vector2(2, rod.Height - 2);
				rodTip = new Vector2(rod.Width - 2, 2);
				var data = new Color[rod.Width * rod.Height];
				rod.GetData(data);
				float low = float.MinValue, right = float.MinValue;
				for (int y = 0; y < rod.Height; y++)
					for (int x = 0; x < rod.Width; x++) {
						if (data[y * rod.Width + x].A < 100)
							continue;
						if (y - x > low) {
							low = y - x;
							rodGrip = new Vector2(x + 1, y + 1);
						}
						if (x >= right) {
							right = x;
							rodTip = new Vector2(x + 1, y + 1);
						}
					}
				rodGrip = Vector2.Lerp(rodGrip, rodTip, 0.12f);   // held a little above the butt
			}
			grip = rodGrip;
			tip = rodTip;
		}
	}
}
