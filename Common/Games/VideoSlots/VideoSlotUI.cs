using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Games.VideoSlots
{
	public class VideoSlotUIState : UIState
	{
		public override void OnInitialize()
		{
			var panel = new VideoSlotPanel();
			panel.Width.Set(VideoSlotPanel.PanelWidth, 0f);
			panel.Height.Set(VideoSlotPanel.PanelHeight, 0f);
			panel.HAlign = 0.5f;
			panel.VAlign = 0.5f;
			Append(panel);
		}
	}

	/// <summary>
	/// Video slot window: themed marquee, 5x3 grid of item symbols with spinning reels, tumbles and win
	/// lines, a free-spin banner, the pay table, bet chips and Spin.
	/// </summary>
	public class VideoSlotPanel : UIElement
	{
		public const int PanelWidth = 720, PanelHeight = 470;
		private const int Cell = 66;

		private enum Action { Close, Bet, Spin }

		private readonly record struct Button(Rectangle Rect, Action Kind, string Label, bool Enabled, int Index = 0);

		private static VideoSlotSystem System => ModContent.GetInstance<VideoSlotSystem>();

		private static long[] Chips => CasinoTableGameSystem.Casino?.ChipValues ?? new long[] { 100 };

		private static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.UI.VideoSlots." + key, args);

		private Rectangle Area => GetDimensions().ToRectangle();

		private Rectangle Grid => new(Area.X + 26, Area.Y + 76, Cell * VideoSlotEngine.Reels, Cell * VideoSlotEngine.Rows);

		private Rectangle CellRect(int reel, int row) => new(Grid.X + reel * Cell, Grid.Y + row * Cell, Cell, Cell);

		private IEnumerable<Button> Buttons()
		{
			Rectangle a = Area;
			VideoSlotRound round = System.Round;
			yield return new Button(new Rectangle(a.Right - 32, a.Y + 10, 22, 22), Action.Close, "X", true);
			long[] chips = Chips;
			int y = a.Bottom - 96;
			for (int k = 0; k < chips.Length; k++)
				yield return new Button(new Rectangle(a.X + 26 + k * 64, y, 58, 44), Action.Bet, Games.Coins.Format(chips[k]), round.CanSpin, k);
			yield return new Button(new Rectangle(a.X + 26 + chips.Length * 64 + 8, y, 120, 44), Action.Spin, Language.GetTextValue("Mods.CasinoWorld.UI.Slots.Spin"), round.CanSpin);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			Point mouse = evt.MousePosition.ToPoint();
			foreach (Button button in Buttons()) {
				if (!button.Enabled || !button.Rect.Contains(mouse))
					continue;
				VideoSlotRound round = System.Round;
				long[] chips = Chips;
				round.BetIndex = Math.Clamp(round.BetIndex, 0, chips.Length - 1);
				switch (button.Kind) {
					case Action.Close:
						System.Close();
						break;
					case Action.Bet:
						round.BetIndex = button.Index;
						SoundEngine.PlaySound(SoundID.MenuTick);
						break;
					case Action.Spin:
						round.TrySpin(Main.LocalPlayer, chips[round.BetIndex]);
						break;
				}
				break;
			}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			VideoSlotRound round = System.Round;
			SlotDefinition def = round.Definition;
			if (def == null)
				return;
			Rectangle a = Area;
			Color accent = SlotThemes.Accent(def.Key), body = SlotThemes.Body(def.Key);
			Point mouse = CasinoTableGameSystem.UIMouse;
			float t = Main.GlobalTimeWrappedHourly;

			// Cabinet frame with a glowing accent edge.
			CasinoDraw.Fill(sb, a, accent * 0.9f);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(a, 3), body);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(a, 6), Color.Lerp(body, Color.Black, 0.35f));

			// Marquee with chasing lights.
			var marquee = new Rectangle(a.X + 26, a.Y + 14, Cell * 5, 50);
			CasinoDraw.Fill(sb, marquee, Color.Black * 0.7f);
			for (int k = 0; k < 24; k++) {
				bool on = ((int)(t * 8) + k) % 3 == 0;
				var bulb = new Rectangle(marquee.X + 4 + k * (marquee.Width - 8) / 24, marquee.Y + 2, 5, 5);
				CasinoDraw.Fill(sb, bulb, on ? accent : accent * 0.25f);
				CasinoDraw.Fill(sb, new Rectangle(bulb.X, marquee.Bottom - 7, 5, 5), !on ? accent : accent * 0.25f);
			}
			Utils.DrawBorderString(sb, Text("Title" + def.Key), marquee.Center.ToVector2() + new Vector2(0, 4), accent, 1.15f, 0.5f, 0.5f);

			DrawGrid(sb, round, def, accent, t);
			DrawPayTable(sb, def, new Vector2(Grid.Right + 22, a.Y + 18), accent);

			// Free-spin banner.
			if (round.InBonus || round.Engine?.InBonus == true) {
				var banner = new Rectangle(Grid.X, Grid.Bottom + 6, Grid.Width, 24);
				CasinoDraw.Fill(sb, banner, accent * (0.55f + 0.2f * (float)Math.Sin(t * 6)));
				string mult = round.Engine.BonusMultiplier > 1 ? "   x" + round.Engine.BonusMultiplier : "";
				Utils.DrawBorderString(sb, Text("FreeSpinsLeft", round.Engine.FreeSpinsLeft) + mult + "   " + Games.Coins.Format(round.BonusTotal), banner.Center.ToVector2() + new Vector2(0, 3), Color.White, 0.85f, 0.5f, 0.5f);
			}

			foreach (Button button in Buttons()) {
				bool hover = button.Rect.Contains(mouse);
				if (button.Kind == Action.Close)
					CasinoDraw.Button(sb, button.Rect, "X", CasinoDraw.Red, hover, true, 0.8f);
				else if (button.Kind == Action.Bet)
					CasinoDraw.ChipButton(sb, button.Rect, button.Label, button.Index, hover, round.BetIndex == button.Index);
				else
					CasinoDraw.Button(sb, button.Rect, button.Label, CasinoDraw.ButtonGold, hover, button.Enabled, 1f);
			}

			long money = Games.Coins.Count(Main.LocalPlayer);
			Utils.DrawBorderString(sb, Language.GetTextValue("Mods.CasinoWorld.UI.Slots.Money", Games.Coins.Format(money)), new Vector2(a.X + 26, a.Bottom - 44), Color.White, 0.85f);
			Utils.DrawBorderString(sb, Text("Bet", Games.Coins.Format(Chips[Math.Clamp(round.BetIndex, 0, Chips.Length - 1)])), new Vector2(a.X + 300, a.Bottom - 44), Color.White, 0.85f);
			Utils.DrawBorderString(sb, round.Message, new Vector2(a.X + 26, a.Bottom - 22), round.MessageColor, 0.85f);
		}

		private void DrawGrid(SpriteBatch sb, VideoSlotRound round, SlotDefinition def, Color accent, float t)
		{
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(Grid, -4), accent * 0.6f);
			CasinoDraw.Fill(sb, Grid, new Color(12, 10, 18));
			int[] icons = SlotThemes.Icons(def.Key);
			SpinOutcome outcome = round.Outcome;
			SlotCell[,] grid = outcome != null ? outcome.Steps[Math.Min(round.Step, outcome.Steps.Count - 1)] : null;

			for (int reel = 0; reel < VideoSlotEngine.Reels; reel++) {
				bool spinning = round.Phase == VideoPhase.Spinning && round.Timer < VideoSlotRound.ReelStopStart + VideoSlotRound.ReelStopStep * reel;
				for (int row = 0; row < VideoSlotEngine.Rows; row++) {
					Rectangle cell = CellRect(reel, row);
					CasinoDraw.Fill(sb, CasinoDraw.Shrink(cell, 2), (reel + row) % 2 == 0 ? new Color(28, 24, 38) : new Color(22, 19, 30));
					int symbol;
					int value = 0;
					bool stickyCell = false;
					if (spinning)
						symbol = (int)(t * 22 + reel * 3 + row * 5) % def.SymbolCount;
					else if (grid != null) {
						symbol = grid[reel, row].Symbol;
						value = grid[reel, row].Value;
						stickyCell = grid[reel, row].Sticky;
					}
					else
						symbol = (reel * 2 + row) % def.SymbolCount;

					float bounce = !spinning && round.Phase == VideoPhase.Spinning && round.Timer < VideoSlotRound.ReelStopStart + VideoSlotRound.ReelStopStep * reel + 6 ? -4 : 0;
					DrawItem(sb, icons[symbol], cell.Center.ToVector2() + new Vector2(0, bounce + (spinning ? (float)Math.Sin(t * 40 + reel) * 6 : 0)), 46f, spinning ? Color.White * 0.6f : Color.White);
					if (symbol == def.Wild && !spinning)
						Utils.DrawBorderString(sb, Text("Wild"), new Vector2(cell.Center.X, cell.Bottom - 10), Color.Gold, 0.6f, 0.5f, 0.5f);
					if (symbol == def.Scatter && !spinning)
						Utils.DrawBorderString(sb, Text("Scatter"), new Vector2(cell.Center.X, cell.Bottom - 10), accent, 0.55f, 0.5f, 0.5f);
					if (value > 0 && !spinning) {
						string label = def.Feature == SlotFeature.CascadeOrbs ? "x" + value : Games.Coins.Format(value * Math.Max(1, round.LastBet) / VideoSlotEngine.LineCount);
						Utils.DrawBorderString(sb, label, new Vector2(cell.Center.X, cell.Bottom - 10), Color.Gold, 0.65f, 0.5f, 0.5f);
					}
					if (stickyCell)
						DrawFrame(sb, cell, new Color(170, 230, 255));
					if (!spinning && round.Engine?.ExpandingSymbol == symbol && round.Engine.InBonus && def.Feature == SlotFeature.ExpandingSymbol)
						DrawFrame(sb, cell, Color.Red * (0.6f + 0.4f * (float)Math.Sin(t * 8)));
				}
			}

			// Winning lines of the current step.
			if (outcome != null && round.Phase is VideoPhase.Tumbling or VideoPhase.Result) {
				var wins = outcome.StepWins[Math.Min(round.Step, outcome.StepWins.Count - 1)];
				foreach (LineWin win in wins) {
					int[] line = VideoSlotEngine.Lines[win.Line];
					Color c = Color.Lerp(Color.Gold, Color.White, 0.5f + 0.5f * (float)Math.Sin(t * 10 + win.Line));
					for (int reel = 0; reel < win.Count; reel++)
						DrawFrame(sb, CellRect(reel, line[reel]), c);
					for (int reel = 0; reel < VideoSlotEngine.Reels - 1; reel++)
						DrawLine(sb, CellRect(reel, line[reel]).Center.ToVector2(), CellRect(reel + 1, line[reel + 1]).Center.ToVector2(), c * 0.7f);
				}
			}
		}

		private static void DrawFrame(SpriteBatch sb, Rectangle r, Color c)
		{
			CasinoDraw.Fill(sb, new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, 2), c);
			CasinoDraw.Fill(sb, new Rectangle(r.X + 2, r.Bottom - 4, r.Width - 4, 2), c);
			CasinoDraw.Fill(sb, new Rectangle(r.X + 2, r.Y + 2, 2, r.Height - 4), c);
			CasinoDraw.Fill(sb, new Rectangle(r.Right - 4, r.Y + 2, 2, r.Height - 4), c);
		}

		private static void DrawLine(SpriteBatch sb, Vector2 a, Vector2 b, Color c)
		{
			Vector2 d = b - a;
			sb.Draw(TextureAssets.MagicPixel.Value, a, new Rectangle(0, 0, 1, 1), c, d.ToRotation(), Vector2.Zero, new Vector2(d.Length(), 2f), SpriteEffects.None, 0f);
		}

		private static void DrawItem(SpriteBatch sb, int type, Vector2 center, float maxSize, Color color)
		{
			Main.instance.LoadItem(type);
			Texture2D tex = TextureAssets.Item[type].Value;
			Rectangle frame = Main.itemAnimations[type] != null ? Main.itemAnimations[type].GetFrame(tex) : tex.Frame();
			float scale = Math.Min(2f, maxSize / Math.Max(frame.Width, frame.Height));
			sb.Draw(tex, center, frame, color, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
		}

		private void DrawPayTable(SpriteBatch sb, SlotDefinition def, Vector2 pos, Color accent)
		{
			int[] icons = SlotThemes.Icons(def.Key);
			Utils.DrawBorderString(sb, Language.GetTextValue("Mods.CasinoWorld.UI.Slots.Pays"), pos, accent, 0.9f);
			Utils.DrawBorderString(sb, Text("PaysNote"), pos + new Vector2(0, 20), Color.LightGray, 0.6f);
			float y = pos.Y + 42;
			for (int s = def.SymbolCount - 1; s >= 0; s--) {
				if (def.Pays[s] == null && s != def.Scatter)
					continue;
				DrawItem(sb, icons[s], new Vector2(pos.X + 12, y + 10), 22f, Color.White);
				string pays = s == def.Scatter ? Text("ScatterPays", def.FreeSpins) : $"{def.Pays[s][0]} / {def.Pays[s][1]} / {def.Pays[s][2]}";
				Utils.DrawBorderString(sb, pays, new Vector2(pos.X + 30, y + 3), s == def.Wild ? Color.Gold : Color.White, 0.62f);
				y += 24;
			}
			Utils.DrawBorderString(sb, Text("Feature" + def.Key), new Vector2(pos.X, y + 6), accent, 0.6f);
		}
	}
}
