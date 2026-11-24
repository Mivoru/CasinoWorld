using System;
using System.Collections.Generic;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Games.Arcade
{
	/// <summary>
	/// Shared host of the arcade cabinets (Plinko, Slime Derby, Higher or Lower): bet chip, message, taking the
	/// wager and paying out. The game itself rolls on the client, like the slots.
	/// </summary>
	public abstract class ArcadeGame : CasinoTableGameSystem
	{
		public int BetIndex;
		public string Message = "";
		public Color MessageColor = Color.White;

		public abstract string Key { get; }
		public abstract Color Accent { get; }
		public abstract bool Busy { get; }

		protected override Point TableSize => new(ArcadeCabinet.Width, ArcadeCabinet.Height);

		protected override UIState CreateState()
		{
			var state = new UIState();
			var panel = new ArcadePanel(this);
			panel.Width.Set(ArcadePanel.PanelWidth, 0f);
			panel.Height.Set(ArcadePanel.PanelHeight, 0f);
			panel.HAlign = panel.VAlign = 0.5f;
			state.Append(panel);
			return state;
		}

		public static long[] Chips => Casino?.ChipValues ?? new long[] { 100 };

		public long Bet => Chips[Math.Clamp(BetIndex, 0, Chips.Length - 1)];

		public string Text(string key, params object[] args) => Language.GetTextValue($"Mods.CasinoWorld.UI.{Key}.{key}", args);

		public static string Common(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.UI.Slots." + key, args);

		public void Say(string text, Color color)
		{
			Message = text;
			MessageColor = color;
		}

		protected bool TakeBet(Player player, long bet)
		{
			if (CasinoRewards.TryWager(player, bet))
				return true;
			Say(Common("NotEnough"), Color.OrangeRed);
			return false;
		}

		/// <summary>Pays a settled round and reports it.</summary>
		protected void Settle(Player player, long stake, long payout, Vector2 at)
		{
			Coins.Give(player, payout);
			CasinoRecords.Report(player, CasinoGame.Arcade, payout - stake);
			if (payout > stake) {
				Say(Language.GetTextValue("Mods.CasinoWorld.UI.Win.Paid", Coins.Format(payout)), Color.LightGreen);
				SoundEngine.PlaySound(payout >= stake * 10 ? CasinoSounds.Jackpot : CasinoSounds.SmallWin);
				SoundEngine.PlaySound(CasinoSounds.CoinsPayout);
				UiFx.Win(payout, stake, at);
				if (payout >= stake * 10)
					// About 4-6 full stakes lost per item, 20 on Plinko (roulette about 4, slots about 6.5).
					CasinoRewards.TryJackpot(player, stake, payout >= stake * 100 ? 1f : payout >= stake * 20 ? 0.2f : 0.1f);
			}
			else if (payout > 0)
				Say(Language.GetTextValue("Mods.CasinoWorld.UI.Win.Part", Coins.Format(payout)), Color.LightGray);
			else
				Say(Common("Lose"), Color.LightGray);
		}

		/// <summary>The game's own buttons for the bottom row (besides chips and close).</summary>
		public abstract IEnumerable<ArcadeButton> Buttons(Rectangle row, Rectangle screen);

		/// <summary>Draws the game inside the screen rectangle.</summary>
		public abstract void DrawGame(SpriteBatch sb, Rectangle screen, Point mouse);
	}

	public readonly record struct ArcadeButton(Rectangle Rect, string Label, bool Enabled, Action Click, Color Color, bool Hidden = false);

	/// <summary>The window around every arcade game: cabinet, blinking marquee, game screen, chips and buttons.</summary>
	public class ArcadePanel : UIElement
	{
		public const int PanelWidth = 600, PanelHeight = 520;

		private readonly ArcadeGame game;

		public ArcadePanel(ArcadeGame game) => this.game = game;

		private Rectangle Area => GetDimensions().ToRectangle();
		private Rectangle Screen => new(Area.X + 24, Area.Y + 66, PanelWidth - 48, 330);

		private IEnumerable<ArcadeButton> Buttons()
		{
			Rectangle a = Area;
			yield return new ArcadeButton(new Rectangle(a.Right - 34, a.Y + 12, 22, 22), "X", true, game.Close, CasinoDraw.Red);
			foreach (ArcadeButton b in game.Buttons(new Rectangle(a.X + 24 + ArcadeGame.Chips.Length * 62 + 6, a.Bottom - 112, PanelWidth - 54 - ArcadeGame.Chips.Length * 62, 44), Screen))
				yield return b;
		}

		private Rectangle ChipRect(int k) => new(Area.X + 24 + k * 62, Area.Bottom - 112, 56, 44);

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			Point mouse = evt.MousePosition.ToPoint();
			for (int k = 0; k < ArcadeGame.Chips.Length; k++)
				if (ChipRect(k).Contains(mouse) && !game.Busy) {
					game.BetIndex = k;
					SoundEngine.PlaySound(CasinoSounds.ChipPlace);
					return;
				}
			foreach (ArcadeButton b in Buttons())
				if (b.Enabled && b.Rect.Contains(mouse)) {
					b.Click();
					return;
				}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
			Rectangle a = Area, screen = Screen;
			Point mouse = CasinoTableGameSystem.UIMouse;
			game.BetIndex = Math.Clamp(game.BetIndex, 0, ArcadeGame.Chips.Length - 1);
			CasinoDraw.Cabinet(sb, a, CasinoDraw.Gold, Color.Lerp(game.Accent, Color.Black, 0.7f));

			// Marquee.
			var marquee = new Rectangle(a.X + 60, a.Y + 14, PanelWidth - 120, 42);
			CasinoDraw.Fill(sb, marquee, CasinoDraw.Gold);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(marquee, 3), new Color(18, 10, 24));
			int phase = (int)(Main.GlobalTimeWrappedHourly * (game.Busy ? 16f : 8f));
			for (int x = marquee.X + 6, k = 0; x < marquee.Right - 6; x += 10, k++) {
				Color c = (k + phase) % 3 == 0 ? Color.White : game.Accent;
				CasinoDraw.Fill(sb, new Rectangle(x, marquee.Y + 4, 4, 4), c);
				CasinoDraw.Fill(sb, new Rectangle(x, marquee.Bottom - 8, 4, 4), c);
			}
			Utils.DrawBorderString(sb, game.Text("Title"), marquee.Center.ToVector2() + new Vector2(0, 4), Color.Lerp(game.Accent, Color.White, 0.3f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f)), 1.25f, 0.5f, 0.5f);

			CasinoDraw.Fill(sb, CasinoDraw.Shrink(screen, -4), CasinoDraw.Gold);
			CasinoDraw.Fill(sb, screen, new Color(14, 12, 28));
			game.DrawGame(sb, screen, mouse);

			for (int k = 0; k < ArcadeGame.Chips.Length; k++)
				CasinoDraw.ChipButton(sb, ChipRect(k), Coins.Format(ArcadeGame.Chips[k]), k, !game.Busy && ChipRect(k).Contains(mouse), k == game.BetIndex);
			foreach (ArcadeButton b in Buttons())
				if (!b.Hidden)
					CasinoDraw.Button(sb, b.Rect, b.Label, b.Color, b.Rect.Contains(mouse), b.Enabled, b.Rect.Width > 90 ? 1f : 0.8f);

			Utils.DrawBorderString(sb, ArcadeGame.Common("Money", Coins.Format(Coins.Count(Main.LocalPlayer))), new Vector2(a.X + 24, a.Bottom - 58), Color.White, 0.8f);
			Utils.DrawBorderString(sb, ArcadeGame.Common("Bet", Coins.Format(game.Bet)), new Vector2(a.X + 290, a.Bottom - 58), Color.White, 0.8f);
			CasinoDraw.Fit(sb, string.IsNullOrEmpty(game.Message) ? game.Text("Hint") : game.Message, new Vector2(a.X + 24, a.Bottom - 34), game.MessageColor, 0.9f, a.Width - 48);
		}
	}
}
