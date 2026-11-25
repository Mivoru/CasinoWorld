using System;
using System.Collections.Generic;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Games.Arcade
{
	/// <summary>
	/// Slime Derby: bet on one of five racing slimes. The winner is rolled first (36 / 25 / 18 / 13 / 8 %), the race is
	/// then shaped so that slime crosses the line first. Every bet returns 96-97.5 %.
	/// </summary>
	public class SlimeDerby : ArcadeGame
	{
		public const int Slimes = 5;
		private static readonly int[] Weights = { 36, 25, 18, 13, 8 };
		/// <summary>Total return in tenths of the bet.</summary>
		private static readonly int[] Pays = { 27, 39, 54, 75, 120 };
		private static readonly Color[] Colors = { new(110, 230, 110), new(90, 150, 255), new(255, 90, 90), new(190, 100, 255), new(255, 215, 70) };

		private const int CountdownTicks = 60, RaceTicks = 460, ResultTicks = 150;

		public int Pick;
		private int timer = -1, winner;
		private long stake;
		private bool paid;
		private readonly float[] finish = new float[Slimes], wobble = new float[Slimes], burst = new float[Slimes];
		private readonly int[] lastHop = new int[Slimes];
		private Rectangle lastScreen;

		public override string Key => "Derby";
		public override Color Accent => new(120, 240, 130);
		public override bool Busy => timer >= 0 && timer < CountdownTicks + RaceTicks;
		protected override int TableTileType => ModContent.TileType<DerbyCabinet>();

		public override long Owed => timer >= 0 && !paid && Pick == winner ? stake * Pays[winner] / 10 : 0;

		private void Start()
		{
			long bet = Bet;
			if (Busy || !TakeBet(Main.LocalPlayer, bet))
				return;
			stake = bet;
			paid = false;
			timer = 0;
			Message = "";
			int roll = Main.rand.Next(100);
			for (winner = 0; winner < Slimes - 1 && (roll -= Weights[winner]) >= 0; winner++) { }
			for (int k = 0; k < Slimes; k++) {
				// Share of the race at which each slime reaches the line: the winner at 0.93, the others after it.
				finish[k] = k == winner ? 0.93f : Main.rand.NextFloat(0.955f, 1.1f);
				wobble[k] = Main.rand.NextFloat(MathHelper.TwoPi);
				burst[k] = Main.rand.NextFloat(0.6f, 1f);
				lastHop[k] = 0;
			}
			SoundEngine.PlaySound(CasinoSounds.DerbyBell);
		}

		/// <summary>Race time 0..1 for the current tick: runs a little fast, then slow motion into the finish.</summary>
		private float RaceTime
		{
			get {
				float f = Math.Max(0, timer - CountdownTicks) / (float)RaceTicks;
				return f < 0.75f ? f * 1.1f : 0.825f + (f - 0.75f) * 0.7f;
			}
		}

		/// <summary>Track position 0..1 of a slime at race time p (0..1). Wobbles in the middle so the lead changes.</summary>
		private float Position(int k, float p)
		{
			float f = p / finish[k];
			float sway = (float)Math.Sin(p * 9f + wobble[k]) * 0.06f * burst[k] + (float)Math.Sin(p * 21f + wobble[k] * 2f) * 0.02f;
			float fade = MathHelper.Clamp(1f - f, 0f, 1f) * MathHelper.Clamp(f * 6f, 0f, 1f);   // no sway at the start and the line
			return MathHelper.Clamp(f + sway * Math.Min(1f, fade * 3f), 0f, 1f);
		}

		protected override void UpdateGame(Player player)
		{
			if (timer < 0)
				return;
			timer++;
			if (IsOpen && timer <= CountdownTicks && timer % 20 == 0)
				SoundEngine.PlaySound(CasinoSounds.CrateTick with { Volume = 1f, Pitch = timer / 60f - 0.5f });
			if (timer == CountdownTicks)
				SoundEngine.PlaySound(CasinoSounds.DerbyStart);
			if (RaceTime >= finish[winner] + 0.015f && !paid)
				Pay(player);
			if (timer > CountdownTicks + RaceTicks + ResultTicks)
				timer = -1;
		}

		private void Pay(Player player)
		{
			paid = true;
			SoundEngine.PlaySound(CasinoSounds.CrowdCheer);
			Vector2 at = lastScreen.Width > 0 ? new Vector2(lastScreen.Right - 60, Lane(lastScreen, winner).Center.Y) : UiFx.ScreenCenter;
			Settle(player, stake, Pick == winner ? stake * Pays[winner] / 10 : 0, at);
			Say(Text(Pick == winner ? "Won" : "Lost", Text("Slime" + winner)) + " " + Message, MessageColor);
		}

		protected override void OnClose(Player player)
		{
			if (timer >= 0 && !paid)
				Pay(player);
			timer = -1;
		}

		public override IEnumerable<ArcadeButton> Buttons(Rectangle row, Rectangle screen)
		{
			yield return new ArcadeButton(new Rectangle(row.Right - 130, row.Y, 130, row.Height), Text("Race"), !Busy, Start, CasinoDraw.ButtonGold);
			for (int k = 0; k < Slimes; k++) {
				int pick = k;
				yield return new ArcadeButton(Lane(screen, k), "", !Busy, () => {
					Pick = pick;
					SoundEngine.PlaySound(SoundID.Item154 with { Volume = 0.6f });
				}, Color.White, true);
			}
		}

		private static Rectangle Lane(Rectangle s, int k) => new(s.X + 4, s.Y + 34 + k * 58, s.Width - 8, 54);

		public override void DrawGame(SpriteBatch sb, Rectangle s, Point mouse)
		{
			lastScreen = s;
			float t = Main.GlobalTimeWrappedHourly;
			// Night sky with stands and waving spectators.
			// Day, dusk and night pass behind the stands.
			float day = 0.5f + 0.5f * (float)Math.Sin(t * 0.12f);
			CasinoDraw.Fill(sb, new Rectangle(s.X, s.Y, s.Width, 32), Color.Lerp(new Color(24, 24, 60), new Color(120, 180, 235), day));
			CasinoDraw.Fill(sb, new Rectangle(s.X, s.Y + 16, s.Width, 16), Color.Lerp(new Color(40, 24, 70), new Color(255, 190, 130), day) * 0.5f);
			int sunX = s.X + (int)(t * 6f % s.Width);
			CasinoDraw.Fill(sb, new Rectangle(sunX, s.Y + 5, 12, 12), Color.Lerp(new Color(230, 235, 255), new Color(255, 230, 120), day));
			for (int x = 0; x < s.Width / 12; x++) {
				Color c = Main.hslToRgb(x * 0.37f % 1f, 0.5f, 0.55f);
				int up = (int)(Math.Abs(Math.Sin(t * (Busy ? 7f : 2f) + x * 1.7f)) * (Busy ? 7 : 2));
				CasinoDraw.Fill(sb, new Rectangle(s.X + x * 12 + 3, s.Y + 20 - up, 7, 12 + up), c * 0.8f);
			}
			float p = Math.Min(1.1f, RaceTime);
			int lead = 0;
			for (int k = 1; k < Slimes; k++)
				if (Position(k, p) > Position(lead, p))
					lead = k;
			Main.instance.LoadNPC(NPCID.BlueSlime);
			Texture2D slime = TextureAssets.Npc[NPCID.BlueSlime].Value;
			int fh = slime.Height / Main.npcFrameCount[NPCID.BlueSlime];
			for (int k = 0; k < Slimes; k++) {
				Rectangle lane = Lane(s, k);
				bool picked = k == Pick, won = timer >= 0 && paid && k == winner;
				Color turf = k % 2 == 0 ? new Color(46, 104, 54) : new Color(38, 90, 48);
				if (!Busy && lane.Contains(mouse))
					turf = Color.Lerp(turf, Color.White, 0.15f);
				if (won)
					turf = Color.Lerp(turf, Colors[k], 0.35f + 0.3f * (float)Math.Sin(t * 10));
				CasinoDraw.Fill(sb, lane, turf);
				for (int x = lane.X + 130; x < lane.Right; x += 60)
					CasinoDraw.Fill(sb, new Rectangle(x, lane.Bottom - 4, 26, 2), Color.White * 0.12f);
				if (picked) {
					CasinoDraw.Fill(sb, new Rectangle(lane.X, lane.Y, 4, lane.Height), CasinoDraw.Gold);
					CasinoDraw.Fill(sb, new Rectangle(lane.X, lane.Y, lane.Width, 2), CasinoDraw.Gold * 0.8f);
					CasinoDraw.Fill(sb, new Rectangle(lane.X, lane.Bottom - 2, lane.Width, 2), CasinoDraw.Gold * 0.8f);
				}
				Utils.DrawBorderString(sb, Text("Slime" + k), new Vector2(lane.X + 10, lane.Y + 8), Colors[k], 0.8f);
				Utils.DrawBorderString(sb, "x" + (Pays[k] / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), new Vector2(lane.X + 10, lane.Y + 30), Color.White, 0.8f);
				// Finish line.
				for (int q = 0; q < 12; q++)
					CasinoDraw.Fill(sb, new Rectangle(lane.Right - 46 + q % 2 * 5, lane.Y + q / 2 * 9, 5, 9), (q / 2 + q) % 2 == 0 ? Color.White : Color.Black);

				float pos = Position(k, p);
				float startX = lane.X + 130, endX = lane.Right - 42;
				bool racing = timer >= CountdownTicks && p < 1.05f && pos < 1f;
				float hop = racing ? Math.Abs((float)Math.Sin(p * 70f * burst[k] + wobble[k])) : Math.Abs((float)Math.Sin(t * 2f + k)) * 0.15f;
				if (won)
					hop = Math.Abs((float)Math.Sin(t * 8f));
				var at = new Vector2(MathHelper.Lerp(startX, endX, pos), lane.Bottom - 6 - hop * 16f);
				int hopCount = (int)((p * 70f * burst[k] + wobble[k]) / MathHelper.Pi);
				if (racing && hopCount != lastHop[k]) {
					lastHop[k] = hopCount;
					UiFx.Sparkle(new Vector2(at.X - 10, lane.Bottom - 6), Colors[k], 2, 1.5f);
					if (k == Pick)
						SoundEngine.PlaySound(SoundID.Item154 with { Volume = 0.25f, Pitch = 0.2f, PitchVariance = 0.3f });
				}
				if (racing && k == lead)   // speed streaks behind the leader
					for (int q = 1; q < 4; q++)
						CasinoDraw.Fill(sb, new Rectangle((int)at.X - 16 - q * 16, lane.Bottom - 12 - q * 5, 14, 2), Colors[k] * (0.6f - q * 0.15f));
				sb.Draw(slime, at, new Rectangle(0, hop > 0.4f ? fh : 0, slime.Width, fh), Colors[k] * 0.9f, 0f, new Vector2(slime.Width / 2f, fh), new Vector2(1f + (0.3f - hop) * 0.25f, 1f - (0.3f - hop) * 0.25f), SpriteEffects.None, 0f);
				if (racing && k == lead)
					Utils.DrawBorderString(sb, "1.", at + new Vector2(0, -38), Color.Gold, 0.7f, 0.5f, 0.5f);
				if (won) {
					UiFx.Shimmer(lane, Colors[k], 2);
					// Crown and winner's podium.
					CasinoDraw.Fill(sb, new Rectangle((int)at.X - 26, lane.Bottom - 6, 52, 6), CasinoDraw.Gold);
					for (int q = -1; q <= 1; q++)
						CasinoDraw.Fill(sb, new Rectangle((int)at.X - 3 + q * 8, (int)at.Y - 34 - (q == 0 ? 5 : 0), 6, 12), new Color(255, 215, 80));
					CasinoDraw.Fill(sb, new Rectangle((int)at.X - 12, (int)at.Y - 26, 24, 5), new Color(230, 170, 40));
				}
			}
			if (timer >= 0 && timer < CountdownTicks) {
				int n = 3 - timer / 20;
				float pulse = 1f - timer % 20 / 20f;
				Utils.DrawBorderStringBig(sb, n.ToString(), s.Center.ToVector2(), Color.Gold * (0.4f + 0.6f * pulse), 0.8f + pulse * 0.6f, 0.5f, 0.5f);
			}
		}
	}
}
