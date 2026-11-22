using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Games.Blackjack
{
	public class BlackjackUIState : UIState
	{
		public override void OnInitialize()
		{
			var panel = new BlackjackPanel();
			panel.Width.Set(BlackjackPanel.PanelWidth, 0f);
			panel.Height.Set(BlackjackPanel.PanelHeight, 0f);
			panel.HAlign = 0.5f;
			panel.VAlign = 0.5f;
			Append(panel);
		}
	}

	/// <summary>Blackjack window: dealer at the top, player hands in the middle, chips and actions at the bottom.</summary>
	public class BlackjackPanel : UIElement
	{
		public const int PanelWidth = 620;
		public const int PanelHeight = 400;

		private const int CardW = 44, CardH = 62, CardStep = 30;

		private enum Action { Close, Chip, Clear, Deal, Hit, Stand, Double, Split }

		private readonly record struct Button(Rectangle Rect, Action Kind, string Label, bool Enabled, Color Color, int Chip = 0);

		private int selectedChip;

		private static BlackjackSystem System => ModContent.GetInstance<BlackjackSystem>();

		private static long[] Chips => CasinoTableGameSystem.Casino?.ChipValues ?? new long[] { 100 };

		private static long MaxBet => CasinoTableGameSystem.Casino?.MaxBet ?? 0;

		private static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.UI.Blackjack." + key, args);

		private Rectangle Area => GetDimensions().ToRectangle();

		private IEnumerable<Button> Buttons()
		{
			Rectangle a = Area;
			BlackjackRound round = System.Round;

			yield return new Button(new Rectangle(a.Right - 30, a.Y + 8, 22, 22), Action.Close, "X", true, CasinoDraw.Red);

			int y = a.Bottom - 96;
			long[] chips = Chips;
			for (int k = 0; k < chips.Length; k++)
				yield return new Button(new Rectangle(a.X + 16 + k * 62, y, 58, 40), Action.Chip, Coins.Format(chips[k]), round.CanBet, default, k);

			int x = a.X + 16 + chips.Length * 62 + 6;
			yield return new Button(new Rectangle(x, y, 70, 19), Action.Clear, Text("Clear"), round.CanBet, CasinoDraw.FeltDark);
			yield return new Button(new Rectangle(x, y + 21, 70, 19), Action.Deal, Text("Deal"), round.CanBet, CasinoDraw.ButtonGold);

			int bx = a.Right - 16 - 4 * 74;
			yield return new Button(new Rectangle(bx, y, 70, 40), Action.Hit, Text("Hit"), round.CanHit, CasinoDraw.ButtonGold);
			yield return new Button(new Rectangle(bx + 74, y, 70, 40), Action.Stand, Text("Stand"), round.CanHit, CasinoDraw.ButtonGold);
			yield return new Button(new Rectangle(bx + 148, y, 70, 40), Action.Double, Text("Double"), round.CanDouble, CasinoDraw.ButtonGold);
			yield return new Button(new Rectangle(bx + 222, y, 70, 40), Action.Split, Text("Split"), round.CanSplit, CasinoDraw.ButtonGold);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (!IsMouseHovering)
				return;

			Main.LocalPlayer.mouseInterface = true;
			if (!(Main.mouseLeft && Main.mouseLeftRelease))
				return;

			Point mouse = Main.MouseScreen.ToPoint();
			foreach (Button button in Buttons()) {
				if (button.Enabled && button.Rect.Contains(mouse)) {
					Click(button);
					break;
				}
			}
		}

		private void Click(Button button)
		{
			BlackjackRound round = System.Round;
			Player player = Main.LocalPlayer;
			long[] chips = Chips;
			selectedChip = Math.Clamp(selectedChip, 0, chips.Length - 1);

			switch (button.Kind) {
				case Action.Close: System.Close(); break;
				case Action.Chip:
					selectedChip = button.Chip;
					round.AddChip(chips[selectedChip], MaxBet);
					break;
				case Action.Clear: round.ClearBet(); SoundEngine.PlaySound(SoundID.MenuTick); break;
				case Action.Deal: round.Deal(player, MaxBet); break;
				case Action.Hit: round.Hit(); break;
				case Action.Stand: round.Stand(); break;
				case Action.Double: round.Double(player); break;
				case Action.Split: round.Split(player); break;
			}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;

			Rectangle a = Area;
			BlackjackRound round = System.Round;
			Point mouse = Main.MouseScreen.ToPoint();

			CasinoDraw.Panel(sb, a);
			Utils.DrawBorderString(sb, Text("Title"), new Vector2(a.X + 16, a.Y + 12), CasinoDraw.Gold, 1.1f);
			Utils.DrawBorderString(sb, Text("Rules"), new Vector2(a.Center.X, a.Y + 14), Color.White * 0.7f, 0.7f, 0.5f);

			// Dealer.
			Vector2 dealerPos = new(a.X + 40, a.Y + 60);
			string dealerLabel = Text("Dealer");
			if (round.Dealer.Count > 0)
				dealerLabel += "  " + (round.HoleCardHidden ? "?" : BlackjackRules.Value(round.Dealer).ToString());
			Utils.DrawBorderString(sb, dealerLabel, dealerPos - new Vector2(0, 22), Color.White, 0.9f);
			for (int k = 0; k < round.Dealer.Count; k++)
				DrawCard(sb, dealerPos + new Vector2(k * CardStep, 0), round.Dealer[k], hidden: k == 1 && round.HoleCardHidden);

			// Player hands.
			for (int h = 0; h < round.Hands.Count; h++) {
				BlackjackHand hand = round.Hands[h];
				Vector2 pos = new(a.X + 40 + h * 280, a.Y + 186);
				bool active = round.Phase == BlackjackPhase.PlayerTurn && h == round.ActiveHand;
				if (active)
					CasinoDraw.Fill(sb, new Rectangle((int)pos.X - 6, (int)pos.Y - 6, CardW + CardStep * Math.Max(1, hand.Cards.Count - 1) + 12, CardH + 12), CasinoDraw.Gold * 0.6f);
				for (int k = 0; k < hand.Cards.Count; k++)
					DrawCard(sb, pos + new Vector2(k * CardStep, 0), hand.Cards[k], hidden: false);

				string label = Text("You") + "  " + hand.Value + "   " + Coins.Format(hand.Bet) + (hand.Doubled ? " x2" : "");
				Color color = hand.Busted ? Color.OrangeRed : Color.White;
				if (hand.Payout >= 0 && round.Phase == BlackjackPhase.Result)
					label += "  " + (hand.Payout > hand.Bet ? "+" + Coins.Format(hand.Payout) : hand.Payout == hand.Bet ? Text("PushShort") : "");
				Utils.DrawBorderString(sb, label, pos - new Vector2(0, 22), color, 0.85f);
			}

			if (round.CanBet) {
				string bet = Text("YourBet", Coins.Format(round.PendingBet), Coins.Format(MaxBet));
				Utils.DrawBorderString(sb, bet, new Vector2(a.X + 16, a.Bottom - 118), Color.White, 0.85f);
			}

			foreach (Button button in Buttons()) {
				bool hover = button.Rect.Contains(mouse);
				if (button.Kind == Action.Chip)
					CasinoDraw.ChipButton(sb, button.Rect, button.Label, button.Chip, hover && button.Enabled, button.Chip == selectedChip);
				else
					CasinoDraw.Button(sb, button.Rect, button.Label, button.Color, hover, button.Enabled, button.Kind is Action.Clear or Action.Deal or Action.Close ? 0.8f : 0.95f);
			}

			Utils.DrawBorderString(sb, Text("Money", Coins.Format(Coins.Count(Main.LocalPlayer))), new Vector2(a.X + 16, a.Bottom - 46), Color.White, 0.8f);
			if (!string.IsNullOrEmpty(round.Message))
				Utils.DrawBorderString(sb, round.Message, new Vector2(a.X + 16, a.Bottom - 26), round.MessageColor, 0.9f);
		}

		private static void DrawCard(SpriteBatch sb, Vector2 pos, int card, bool hidden)
		{
			var rect = new Rectangle((int)pos.X, (int)pos.Y, CardW, CardH);
			CasinoDraw.Fill(sb, new Rectangle(rect.X + 2, rect.Y + 2, CardW, CardH), Color.Black * 0.35f);
			if (hidden) {
				sb.Draw(CasinoDraw.CardBack, rect, Color.White);
				return;
			}

			CasinoDraw.Fill(sb, rect, new Color(60, 60, 60));
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(rect, 1), new Color(245, 242, 235));

			int suit = BlackjackRules.Suit(card);
			Color ink = suit is 1 or 2 ? CasinoDraw.Red : CasinoDraw.Black;
			string rank = BlackjackRules.RankName(card);
			Utils.DrawBorderString(sb, rank, new Vector2(rect.X + 4, rect.Y + 3), ink, 0.85f);

			// Plain text without the border reads better on a white card.
			Texture2D suits = CasinoDraw.Suits;
			var src = new Rectangle(suit * 12, 0, 12, 12);
			sb.Draw(suits, new Vector2(rect.X + 5, rect.Y + 24), src, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
			sb.Draw(suits, new Vector2(rect.Center.X - 6, rect.Center.Y), src, Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
		}
	}
}
