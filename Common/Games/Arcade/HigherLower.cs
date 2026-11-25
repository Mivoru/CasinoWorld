using System;
using System.Collections.Generic;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Games.Arcade
{
	/// <summary>
	/// Higher or Lower: is the next card at least as high, or at most as low? Ace is low (1), king high (13), endless
	/// deck, equal cards win. Each right guess multiplies the pot by 0.98 / chance; cash out any time. A wrong
	/// guess loses everything. Every guess costs 2 %. The pot is capped at 100 times the bet.
	/// </summary>
	public class HigherLower : ArcadeGame
	{
		private const int FlipTicks = 34;
		private const double Keep = 0.98, Cap = 100.0;

		private static Asset<Texture2D> cardsTex;

		private bool running;
		private long stake;
		private double pot;
		private int streak, card = 12, next = -1, flip = -1, shake;
		private bool higher, lost;
		private readonly List<int> history = new();
		private Rectangle lastScreen;

		public override string Key => "HiLo";
		public override Color Accent => new(255, 120, 190);
		public override bool Busy => running;
		protected override int TableTileType => ModContent.TileType<HiLoCabinet>();

		public override void Unload()
		{
			base.Unload();
			cardsTex = null;
		}

		private static int Rank(int c) => c % 13 + 1;

		private static int Wins(int rank, bool up) => up ? 14 - rank : rank;

		private double Multiplier(bool up) => Math.Max(1.0, Keep * 13 / Wins(Rank(card), up));

		private bool Certain(bool up) => Wins(Rank(card), up) == 13;

		public override long Owed => running ? (long)(stake * pot) : 0;

		private void Deal()
		{
			long bet = Bet;
			if (running || !TakeBet(Main.LocalPlayer, bet))
				return;
			stake = bet;
			pot = 1.0;
			streak = 0;
			running = true;
			lost = false;
			history.Clear();
			card = Main.rand.Next(4) * 13 + Main.rand.Next(2, 11);   // the first card is 3 to jack, so both guesses are open
			next = -1;
			Message = "";
			SoundEngine.PlaySound(CasinoSounds.CardDeal);
		}

		private void Guess(bool up)
		{
			if (!running || flip >= 0 || Certain(up))
				return;
			higher = up;
			next = Main.rand.Next(52);
			flip = 0;
			SoundEngine.PlaySound(CasinoSounds.CardDeal);
		}

		/// <summary>The card is face up: settle the guess.</summary>
		private void Reveal(Player player)
		{
			int a = Rank(card), b = Rank(next);
			bool right = higher ? b >= a : b <= a;
			double factor = Multiplier(higher);
			history.Add(card);
			if (history.Count > 9)
				history.RemoveAt(0);
			card = next;
			next = -1;
			flip = -1;
			if (!right) {
				running = false;
				lost = true;
				shake = 24;
				SoundEngine.PlaySound(CasinoSounds.HiLoLose);
				Settle(player, stake, 0, UiFx.ScreenCenter);
				return;
			}
			pot = Math.Min(Cap, pot * factor);
			streak++;
			SoundEngine.PlaySound(CasinoSounds.HiLoWin with { Pitch = Math.Min(0.9f, streak * 0.12f) });
			if (lastScreen.Width > 0)
				UiFx.Sparkle(lastScreen.Center.ToVector2(), new Color(255, 225, 130), 10 + streak * 4, 4f);
			if (pot >= Cap)
				CashOut(player);
		}

		private void CashOut(Player player)
		{
			if (!running || flip >= 0 || streak == 0)
				return;
			running = false;
			Settle(player, stake, (long)(stake * pot), lastScreen.Width > 0 ? lastScreen.Center.ToVector2() : UiFx.ScreenCenter);
			SoundEngine.PlaySound(CasinoSounds.CashRegister);
		}

		protected override void UpdateGame(Player player)
		{
			if (shake > 0)
				shake--;
			// Heartbeat that quickens with the streak.
			if (running && IsOpen && streak >= 2 && Main.GameUpdateCount % (uint)Math.Max(24, 70 - streak * 6) == 0)
				SoundEngine.PlaySound(CasinoSounds.HeartBeat with { Volume = 0.5f });
			if (flip < 0)
				return;
			flip++;
			if (flip == FlipTicks / 2)
				SoundEngine.PlaySound(CasinoSounds.CardFlip);
			if (flip >= FlipTicks)
				Reveal(player);
		}

		// Closing never loses a pot: a flying card settles, a pot is cashed out, and an unplayed deal makes the safer guess.
		protected override void OnClose(Player player)
		{
			if (!running)
				return;
			if (flip < 0 && streak == 0)
				Guess(Wins(Rank(card), true) < 13 && (Certain(false) || Wins(Rank(card), true) >= Wins(Rank(card), false)));
			if (flip >= 0)
				Reveal(player);
			CashOut(player);
		}

		public override IEnumerable<ArcadeButton> Buttons(Rectangle row, Rectangle s)
		{
			if (running)
				yield return new ArcadeButton(new Rectangle(row.Right - 150, row.Y, 150, row.Height), streak > 0 ? Text("CashOut", Coins.Format((long)(stake * pot))) : Text("CashOutNone"), streak > 0 && flip < 0, () => CashOut(Main.LocalPlayer), new Color(40, 140, 70));
			else
				yield return new ArcadeButton(new Rectangle(row.Right - 130, row.Y, 130, row.Height), Text("Deal"), true, Deal, CasinoDraw.ButtonGold);
			bool can = running && flip < 0;
			yield return new ArcadeButton(new Rectangle(s.Right - 170, s.Y + 80, 150, 56), Label(true), can && !Certain(true), () => Guess(true), new Color(40, 120, 190));
			yield return new ArcadeButton(new Rectangle(s.Right - 170, s.Y + 160, 150, 56), Label(false), can && !Certain(false), () => Guess(false), new Color(180, 60, 60));
		}

		private string Label(bool up) => Text(up ? "Higher" : "Lower") + (running && !Certain(up) ? "  x" + Multiplier(up).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "");

		private static void Card(SpriteBatch sb, int c, Vector2 at, float scale, float squash = 1f, float rotation = 0f, Color? tint = null)
		{
			Rectangle src = c < 0 ? new Rectangle(0, 4 * 68, 48, 68) : new Rectangle(c % 13 * 48, c / 13 * 68, 48, 68);
			sb.Draw(cardsTex.Value, at + new Vector2(4, 6), src, Color.Black * 0.35f, rotation, new Vector2(24, 34), new Vector2(Math.Max(0.04f, squash), 1f) * scale, SpriteEffects.None, 0f);
			sb.Draw(cardsTex.Value, at, src, tint ?? Color.White, rotation, new Vector2(24, 34), new Vector2(Math.Max(0.04f, squash), 1f) * scale, SpriteEffects.None, 0f);
		}

		public override void DrawGame(SpriteBatch sb, Rectangle s, Point mouse)
		{
			lastScreen = s;
			cardsTex ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/Cards", AssetRequestMode.ImmediateLoad);
			float t = Main.GlobalTimeWrappedHourly;
			CasinoDraw.Fill(sb, s, Color.Lerp(CasinoDraw.FeltDark, new Color(90, 10, 60), Math.Min(1f, streak / 8f)));
			// The table heats up with the streak; from x10 the whole screen changes colour.
			if (running && streak > 0)
				for (int k = 0; k * 18 < s.Width - 10; k++)
					if (k * 7 % 10 < Math.Min(10, streak * 2))
						Effects.PixelFx.Flame(sb, new Vector2(s.X + 12 + k * 18, s.Bottom), t, k * 5);
			if (running && pot >= 10) {
				Color wash = Main.hslToRgb(t * 0.25f % 1f, 0.8f, 0.5f) * 0.22f;
				wash.A = 0;
				CasinoDraw.Fill(sb, s, wash);
			}
			// Pot ladder: one lamp per right guess.
			for (int k = 0; k < 10; k++) {
				var r = new Rectangle(s.X + 12, s.Bottom - 34 - k * 28, 18, 22);
				bool on = running && k < streak;
				CasinoDraw.Fill(sb, r, on ? Color.Lerp(new Color(255, 200, 60), new Color(255, 60, 90), k / 9f) : new Color(40, 30, 40));
				if (on && k == streak - 1)
					UiFx.Shimmer(r, Color.White, 5);
			}
			Vector2 middle = new(s.X + 200, s.Center.Y + 10);
			if (shake > 0)
				middle += Main.rand.NextVector2Circular(shake * 0.3f, shake * 0.3f);
			for (int k = 0; k < history.Count; k++)
				Card(sb, history[k], new Vector2(s.X + 60 + k * 34, s.Y + 36), 0.5f, 1f, 0f, Color.White * 0.75f);
			if (flip >= 0) {
				float f = flip / (float)FlipTicks;
				Card(sb, card, middle + new Vector2(-50, 0), 1.8f);
				float ease = 1f - (1f - f) * (1f - f);
				Vector2 at = Vector2.Lerp(new Vector2(s.Right + 10, s.Y - 10), middle + new Vector2(70, 0), Math.Min(1f, ease * 1.6f));
				Card(sb, f < 0.5f ? -1 : next, at, 1.8f, Math.Abs(f * 2f - 1f), (1f - ease) * 0.6f);
			}
			else
				Card(sb, card, middle, 2.1f, 1f, 0f, lost ? new Color(255, 150, 150) : Color.White);
			if (running) {
				float pulse = 1f + 0.06f * (float)Math.Sin(t * (4f + streak));
				Utils.DrawBorderStringBig(sb, "x" + pot.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), new Vector2(middle.X, s.Bottom - 40), Color.Lerp(Color.White, new Color(255, 200, 60), Math.Min(1f, streak / 5f)), 0.6f * pulse, 0.5f, 0.5f);
			}
		}
	}
}
