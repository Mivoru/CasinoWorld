using System;
using System.Collections.Generic;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Games.Blackjack;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Games.Poker
{
	/// <summary>Hold'em window: an oval table with four seats (you at the bottom), the board in the middle, actions below.</summary>
	public class PokerPanel : UIElement
	{
		public const int PanelWidth = 780, PanelHeight = 520;
		private const int CardW = 48, CardH = 68;

		private static PokerSystem S => ModContent.GetInstance<PokerSystem>();

		private Rectangle Area => GetDimensions().ToRectangle();
		private Rectangle Felt => new(Area.X + 70, Area.Y + 60, Area.Width - 140, 330);

		private readonly record struct Button(Rectangle Rect, string Label, bool Enabled, Color Color, Action Click);

		private IEnumerable<Button> Buttons()
		{
			Rectangle a = Area;
			PokerView v = S.View;
			yield return new Button(new Rectangle(a.Right - 34, a.Y + 12, 22, 22), "X", true, CasinoDraw.Red, S.Close);
			if (v == null)
				yield break;
			int y = a.Bottom - 66;
			if (!S.Seated || v.Mine.Stack <= 0 && (v.Mine.Folded || v.Street == PokerStreet.Showdown)) {
				yield return new Button(new Rectangle(a.Center.X - 130, y, 260, 44), PokerSystem.Text(S.Seated ? "Rebuy" : "Sit", Coins.Format(v.BuyIn)), true, CasinoDraw.ButtonGold, S.BuyIn);
				yield break;
			}
			bool turn = v.MyTurn;
			yield return new Button(new Rectangle(a.Center.X - 235, y, 150, 44), PokerSystem.Text("Fold"), turn, CasinoDraw.Red, () => S.Act(PokerAction.Fold));
			yield return new Button(new Rectangle(a.Center.X - 75, y, 150, 44), v.ToCall == 0 ? PokerSystem.Text("Check") : PokerSystem.Text("Call", Coins.Format(v.ToCall)), turn, CasinoDraw.FeltDark, () => S.Act(PokerAction.Call));
			yield return new Button(new Rectangle(a.Center.X + 85, y, 150, 44), PokerSystem.Text(v.HighBet == 0 ? "Bet" : "Raise", Coins.Format(v.StreetBet)), turn && v.CanRaise, CasinoDraw.ButtonGold, () => S.Act(PokerAction.Raise));
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
			foreach (Button b in Buttons())
				if (b.Enabled && b.Rect.Contains(mouse)) {
					b.Click();
					return;
				}
		}

		/// <summary>Position around the table: 0 bottom, 1 left, 2 top, 3 right.</summary>
		private Vector2 Place(int spot)
		{
			Rectangle f = Felt;
			return spot switch {
				0 => new Vector2(f.Center.X, f.Bottom - 46),
				1 => new Vector2(f.X + 70, f.Center.Y + 10),
				2 => new Vector2(f.Center.X, f.Y + 50),
				_ => new Vector2(f.Right - 70, f.Center.Y + 10),
			};
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			Rectangle a = Area;
			CasinoDraw.Panel(sb, a);
			Utils.DrawBorderString(sb, PokerSystem.Text("Title"), new Vector2(a.X + 20, a.Y + 14), CasinoDraw.Gold, 1.1f);
			Point mouse = CasinoTableGameSystem.UIMouse;
			DrawTable(sb);
			PokerView v = S.View;
			if (v != null) {
				CasinoDraw.Fit(sb, PokerSystem.Text("Stakes", Coins.Format(v.SmallBet), Coins.Format(v.SmallBet * 2)), new Vector2(a.Right - 50, a.Y + 18), Color.LightGray, 0.75f, 420, 1f);
				DrawBoard(sb, v);
				int first = Math.Max(0, v.Me);
				for (int k = 0; k < PokerTable.Seats; k++)
					DrawSeat(sb, v, (first + k) % PokerTable.Seats, k);
			}
			else
				Utils.DrawBorderString(sb, PokerSystem.Text("Waiting"), Felt.Center.ToVector2(), Color.White * 0.8f, 1f, 0.5f, 0.5f);
			CasinoDraw.Fit(sb, S.Message, new Vector2(a.Center.X, a.Bottom - 92), S.MessageColor, 0.95f, a.Width - 80, 0.5f, 0.5f);
			foreach (Button b in Buttons())
				CasinoDraw.Button(sb, b.Rect, b.Label, b.Color, b.Rect.Contains(mouse), b.Enabled, b.Label == "X" ? 0.8f : 0.85f);
			Utils.DrawBorderString(sb, PokerSystem.Text("Balance", Coins.Format(Coins.Count(Main.LocalPlayer))), new Vector2(a.X + 20, a.Bottom - 34), Color.White * 0.8f, 0.75f);
		}

		/// <summary>Oval rail and burgundy felt, drawn in pixel rows.</summary>
		private void DrawTable(SpriteBatch sb)
		{
			Rectangle f = Felt;
			Oval(sb, new Rectangle(f.X - 20, f.Y - 12, f.Width + 40, f.Height + 32), new Color(20, 10, 10) * 0.5f);
			Oval(sb, new Rectangle(f.X - 20, f.Y - 20, f.Width + 40, f.Height + 40), new Color(52, 26, 16));
			Oval(sb, new Rectangle(f.X - 16, f.Y - 16, f.Width + 32, f.Height + 32), new Color(112, 64, 34));
			Oval(sb, new Rectangle(f.X - 4, f.Y - 4, f.Width + 8, f.Height + 8), CasinoDraw.Gold);
			Oval(sb, f, new Color(104, 20, 38));
			Oval(sb, new Rectangle(f.X + 36, f.Y + 30, f.Width - 72, f.Height - 60), new Color(132, 32, 50));
			Oval(sb, new Rectangle(f.X + 38, f.Y + 32, f.Width - 76, f.Height - 64), new Color(112, 24, 42));
		}

		private static void Oval(SpriteBatch sb, Rectangle r, Color color)
		{
			const int Step = 4;
			float ry = r.Height / 2f, straight = Math.Max(0, r.Width - r.Height);
			for (int y = 0; y < r.Height; y += Step) {
				float d = (y + Step / 2f - ry) / ry;
				int half = (int)(ry * Math.Sqrt(Math.Max(0, 1 - d * d)) + straight / 2f) / 2 * 2;
				CasinoDraw.Fill(sb, new Rectangle(r.Center.X - half, r.Y + y, half * 2, Math.Min(Step, r.Height - y)), color);
			}
		}

		private void DrawBoard(SpriteBatch sb, PokerView v)
		{
			Vector2 middle = Felt.Center.ToVector2() + new Vector2(0, -2);
			for (int k = 0; k < 5; k++) {
				var pos = new Vector2(middle.X - 2.5f * (CardW + 6) + k * (CardW + 6) + 3, middle.Y - CardH / 2f);
				if (v.Board[k] >= 0)
					BlackjackPanel.DrawCard(sb, pos, v.Board[k], false);
				else {
					CasinoDraw.Fill(sb, new Rectangle((int)pos.X, (int)pos.Y, CardW, CardH), Color.White * 0.08f);
					CasinoDraw.Fill(sb, new Rectangle((int)pos.X + 2, (int)pos.Y + 2, CardW - 4, CardH - 4), new Color(112, 24, 42));
				}
			}
			if (v.Pot > 0) {
				Texture2D chip = CasinoDraw.Chip;
				sb.Draw(chip, new Vector2(middle.X - 62, middle.Y + 52), null, CasinoDraw.ChipColor(5), 0f, chip.Size() / 2f, 1f, SpriteEffects.None, 0f);
				Utils.DrawBorderString(sb, PokerSystem.Text("Pot", Coins.Format(v.Pot)), new Vector2(middle.X - 46, middle.Y + 55), CasinoDraw.Gold, 0.85f, 0f, 0.5f);
			}
		}

		private void DrawSeat(SpriteBatch sb, PokerView v, int index, int spot)
		{
			PokerView.SeatView s = v.Seat[index];
			Vector2 at = Place(spot);
			bool turn = v.Turn == index, mine = index == v.Me;
			var plate = new Rectangle((int)at.X - 74, (int)at.Y + 14, 148, 44);
			float t = Main.GlobalTimeWrappedHourly;
			Color rim = turn ? Color.Lerp(CasinoDraw.Gold, Color.White, 0.5f + 0.5f * (float)Math.Sin(t * 8f)) : mine ? CasinoDraw.Gold : new Color(90, 60, 40);
			float fade = s.Folded ? 0.5f : 1f;

			// Cards above the name plate.
			bool dealt = v.Street != PokerStreet.Waiting && !s.Folded;
			for (int c = 0; dealt && c < 2; c++) {
				int card = c == 0 ? s.CardA : s.CardB;
				BlackjackPanel.DrawCard(sb, new Vector2(at.X - CardW + c * (CardW - 14) + 7, at.Y - CardH + 12), Math.Max(0, card), card < 0);
			}
			CasinoDraw.Fill(sb, plate, rim * fade);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(plate, 2), new Color(24, 16, 22));
			CasinoDraw.Fit(sb, s.Name, new Vector2(plate.Center.X, plate.Y + 13), (s.Bot ? Color.LightGray : mine ? CasinoDraw.Gold : Color.LightGreen) * fade, 0.8f, 136, 0.5f, 0.5f);
			CasinoDraw.Fit(sb, s.AllIn && dealt ? PokerSystem.Text("AllIn") : Coins.Format(s.Stack), new Vector2(plate.Center.X, plate.Y + 32), Color.Wheat * fade, 0.72f, 136, 0.5f, 0.5f);

			// Time left to act.
			if (turn) {
				int total = s.Bot ? 90 : 1800;
				float left = MathHelper.Clamp((v.Timer - (Main.GameUpdateCount - S.ViewAt)) / (float)total, 0f, 1f);
				CasinoDraw.Fill(sb, new Rectangle(plate.X + 2, plate.Bottom - 5, (int)((plate.Width - 4) * left), 3), left < 0.25f ? Color.Red : Color.LightGreen);
			}
			// Dealer button, bet, last action or result.
			if (v.Button == index && v.Street != PokerStreet.Waiting) {
				Texture2D chip = CasinoDraw.Chip;
				sb.Draw(chip, new Vector2(plate.Right + 4, plate.Y), null, Color.White, 0f, chip.Size() / 2f, 1f, SpriteEffects.None, 0f);
				Utils.DrawBorderString(sb, "D", new Vector2(plate.Right + 4, plate.Y + 3), Color.Black, 0.7f, 0.5f, 0.5f);
			}
			Vector2 tag = spot switch { 0 => new Vector2(at.X + 150, at.Y + 4), 1 => new Vector2(at.X + 40, at.Y - 70), 2 => new Vector2(at.X + 150, at.Y + 30), _ => new Vector2(at.X - 40, at.Y - 70) };
			if (v.Street == PokerStreet.Showdown && (s.Net > 0 || s.Value > 0)) {
				string text = s.Value > 0 ? PokerSystem.Text("Hand" + PokerHands.Category(s.Value)) : "";
				if (s.Net > 0) {
					text = (text == "" ? "" : text + "  ") + "+" + Coins.Format(s.Net);
					PixelFx.Glow(sb, at + new Vector2(0, -14), 48f, CasinoDraw.Gold * 0.35f);
				}
				CasinoDraw.Fit(sb, text, tag, s.Net > 0 ? Color.LightGreen : Color.LightGray, 0.8f, 200, 0.5f, 0.5f);
			}
			else if (s.Last != "") {
				string text = PokerSystem.Text("Did" + s.Last) + (s.Bet > 0 ? "  " + Coins.Format(s.Bet) : "");
				CasinoDraw.Fit(sb, text, tag, s.Last is "Raise" or "Bet" ? Color.Orange : Color.White * fade, 0.8f, 170, 0.5f, 0.5f);
			}
		}
	}
}
