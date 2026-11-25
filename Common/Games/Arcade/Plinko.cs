using System;
using System.Collections.Generic;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Games.Arcade
{
	/// <summary>
	/// Plinko: a chip falls through 12 rows of pegs, left or right 50:50 at each, into one of 13 pockets.
	/// Pays in percent of the bet. Returns over the 4096 paths: low 97.9 %, medium 98.3 %, high 96.6 %.
	/// Several chips can fall at once.
	/// </summary>
	public class Plinko : ArcadeGame
	{
		public const int Rows = 12;
		private const int StepTicks = 11, MaxBalls = 6;

		/// <summary>Pockets from the edge to the centre, in percent of the bet.</summary>
		private static readonly int[][] Half = {
			new[] { 900, 300, 160, 130, 110, 100, 50 },
			new[] { 3000, 1000, 400, 200, 110, 60, 30 },
			new[] { 15000, 2200, 800, 200, 70, 20, 20 },
		};

		/// <summary>Major scale in semitones: every row of pegs plays the next note.</summary>
		private static readonly int[] Scale = { 0, 2, 4, 5, 7, 9, 11, 12, 14, 16, 17, 19 };

		private sealed class Ball
		{
			public int Path;        // bit k: right at row k
			public int Timer;
			public long Stake;
			public int Risk;
			public readonly Queue<Vector2> Trail = new();
		}

		public int Risk = 1;
		private readonly List<Ball> balls = new();
		private readonly int[] pocketFlash = new int[Rows + 1];
		private readonly Dictionary<int, int> pegFlash = new();
		private Rectangle lastScreen;

		public override string Key => "Plinko";
		public override Color Accent => new(80, 200, 255);
		public override bool Busy => balls.Count > 0;
		protected override int TableTileType => ModContent.TileType<PlinkoCabinet>();

		private static int Percent(int risk, int pocket) => Half[risk][pocket <= 6 ? pocket : 12 - pocket];

		private static int Pocket(int path) => System.Numerics.BitOperations.PopCount((uint)path);

		public override long Owed
		{
			get {
				long total = 0;
				foreach (Ball b in balls.ToArray())   // saving runs on another thread
					total += b.Stake * Percent(b.Risk, Pocket(b.Path)) / 100;
				return total;
			}
		}

		private void Drop()
		{
			long bet = Bet;
			if (balls.Count >= MaxBalls || !TakeBet(Main.LocalPlayer, bet))
				return;
			balls.Add(new Ball { Path = Main.rand.Next(1 << Rows), Stake = bet, Risk = Risk });
			SoundEngine.PlaySound(CasinoSounds.ChipFlick);
		}

		protected override void UpdateGame(Player player)
		{
			for (int k = 0; k < pocketFlash.Length; k++)
				if (pocketFlash[k] > 0)
					pocketFlash[k]--;
			foreach (int key in new List<int>(pegFlash.Keys))
				if (--pegFlash[key] <= 0)
					pegFlash.Remove(key);
			for (int k = balls.Count - 1; k >= 0; k--) {
				Ball b = balls[k];
				b.Timer++;
				int row = b.Timer / StepTicks;
				if (b.Timer % StepTicks == 0 && row <= Rows) {
					int at = Pocket(b.Path & ((1 << (row - 1)) - 1));
					pegFlash[(row - 1) * 32 + at] = 14;
					if (IsOpen)
						SoundEngine.PlaySound(CasinoSounds.PlinkoPeg with { Pitch = Math.Clamp(Scale[(row - 1) % Scale.Length] / 12f - 0.5f, -1f, 1f) });
				}
				if (b.Timer >= (Rows + 1) * StepTicks) {
					balls.RemoveAt(k);
					Land(player, b);
				}
			}
		}

		private void Land(Player player, Ball b)
		{
			int pocket = Pocket(b.Path);
			pocketFlash[pocket] = 45;
			SoundEngine.PlaySound(CasinoSounds.PlinkoLand);
			Settle(player, b.Stake, b.Stake * Percent(b.Risk, pocket) / 100, lastScreen.Width > 0 ? PocketRect(lastScreen, pocket).Center.ToVector2() : UiFx.ScreenCenter);
		}

		protected override void OnClose(Player player)
		{
			foreach (Ball b in balls.ToArray())
				Land(player, b);
			balls.Clear();
		}

		public override IEnumerable<ArcadeButton> Buttons(Rectangle row, Rectangle screen)
		{
			yield return new ArcadeButton(new Rectangle(row.Right - 110, row.Y, 110, row.Height), Text("Drop"), balls.Count < MaxBalls, Drop, CasinoDraw.ButtonGold);
			Color[] colors = { new(40, 130, 70), new(170, 120, 30), new(160, 40, 50) };
			for (int k = 0; k < 3; k++) {
				int risk = k;
				yield return new ArcadeButton(new Rectangle(screen.X + 8, screen.Y + 8 + k * 30, 72, 24), Text("Risk" + k), !Busy, () => {
					Risk = risk;
					SoundEngine.PlaySound(SoundID.MenuTick);
				}, Risk == k ? Color.Lerp(colors[k], Color.White, 0.25f) : Color.Lerp(colors[k], Color.Black, 0.5f));
			}
		}

		private const int Gap = 38;

		private static Vector2 Peg(Rectangle s, int row, float right) => new(s.Center.X + (right - row / 2f) * Gap, s.Y + 24 + row * 21);

		private static Rectangle PocketRect(Rectangle s, int pocket) => new((int)(s.Center.X + (pocket - Rows / 2f) * Gap) - Gap / 2 + 1, s.Bottom - 38, Gap - 2, 30);

		public override void DrawGame(SpriteBatch sb, Rectangle s, Point mouse)
		{
			lastScreen = s;
			float t = Main.GlobalTimeWrappedHourly;
			// Soft spotlight cone.
			for (int k = 0; k < 8; k++)
				CasinoDraw.Fill(sb, new Rectangle(s.Center.X - 40 - k * 28, s.Y + k * 40, 80 + k * 56, 40), Accent * 0.035f);

			for (int row = 0; row < Rows; row++)
				for (int c = 0; c <= row; c++) {
					Vector2 p = Peg(s, row, c);
					bool hot = pegFlash.TryGetValue(row * 32 + c, out int f);
					int size = hot ? 8 : 5;
					if (hot)
						CasinoDraw.Fill(sb, new Rectangle((int)p.X - 8, (int)p.Y - 8, 16, 16), Accent * (f / 40f));
					CasinoDraw.Fill(sb, new Rectangle((int)p.X - size / 2, (int)p.Y - size / 2, size, size), hot ? Color.White : Color.Lerp(new Color(150, 170, 220), Color.White, 0.2f + 0.2f * (float)Math.Sin(t * 3 + row)));
				}

			for (int k = 0; k <= Rows; k++) {
				Rectangle r = PocketRect(s, k);
				int percent = Percent(Risk, k);
				float edge = Math.Abs(k - 6) / 6f;
				Color c = Color.Lerp(new Color(60, 90, 170), new Color(255, 60, 70), edge);
				// Outer pockets smoulder while a chip could still reach them.
				if (edge >= 0.6f)
					foreach (Ball near in balls) {
						int row = Math.Min(near.Timer / StepTicks, Rows), at = Pocket(near.Path & ((1 << row) - 1));
						if (row >= 6 && k >= at && k <= at + Rows - row) {
							c = Color.Lerp(c, new Color(255, 200, 80), 0.4f + 0.3f * (float)Math.Sin(t * 16f));
							UiFx.Shimmer(r, new Color(255, 170, 60), 6);
						}
					}
				if (pocketFlash[k] > 0) {
					c = Color.Lerp(c, Color.White, 0.5f + 0.5f * (float)Math.Sin(pocketFlash[k] * 0.6f));
					r.Y += 4;
					UiFx.Shimmer(r, new Color(255, 230, 140), 2);
				}
				CasinoDraw.Fill(sb, r, Color.Lerp(c, Color.Black, 0.4f));
				CasinoDraw.Fill(sb, new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 4), c);
				string label = "x" + (percent % 100 == 0 ? (percent / 100).ToString() : (percent / 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
				Utils.DrawBorderString(sb, label, r.Center.ToVector2() + new Vector2(0, 2), Color.White, 0.65f, 0.5f, 0.5f);
			}

			Texture2D chip = CasinoDraw.Chip;
			foreach (Ball b in balls) {
				float f = b.Timer / (float)StepTicks;
				int row = Math.Min((int)f, Rows);
				float frac = Math.Min(1f, f - row);
				int from = Pocket(b.Path & ((1 << row) - 1));
				int to = row < Rows ? Pocket(b.Path & ((1 << (row + 1)) - 1)) : from;
				Vector2 a = row == 0 ? new Vector2(s.Center.X, s.Y + 4) : Peg(s, row - 1, Pocket(b.Path & ((1 << (row - 1)) - 1))) + new Vector2(0, -9);
				Vector2 z = row < Rows ? Peg(s, row, from) + new Vector2(0, -9) : PocketRect(s, from).Center.ToVector2();
				Vector2 p = Vector2.Lerp(a, z, frac);
				p.Y += (frac * frac - frac) * (row == 0 ? 0 : 34f);   // hop off the peg
				Color glow = new Color(255, 220, 120) * 0.35f;
				glow.A = 0;
				int step = 0;
				foreach (Vector2 old in b.Trail) {   // rainbow tail
					Color tail = Main.hslToRgb((t * 0.4f + step * 0.04f) % 1f, 0.8f, 0.6f) * (0.04f * step);
					tail.A = 0;
					sb.Draw(chip, old, null, tail, 0f, chip.Size() / 2f, 0.4f + step * 0.04f, SpriteEffects.None, 0f);
					step++;
				}
				sb.Draw(chip, p, null, glow, 0f, chip.Size() / 2f, 1.2f, SpriteEffects.None, 0f);
				sb.Draw(chip, p, null, CasinoDraw.ChipColor(BetIndex + 1), t * 8f, chip.Size() / 2f, 0.9f, SpriteEffects.None, 0f);
				b.Trail.Enqueue(p);
				while (b.Trail.Count > 12)
					b.Trail.Dequeue();
			}
		}
	}
}
