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

	/// <summary>
	/// Blackjack window laid out like a real table: dealer and shoe at the top, the printed
	/// table arc, player hands in the middle, and a control rail with chips and actions at the bottom.
	/// </summary>
	public class BlackjackPanel : UIElement
	{
		public const int PanelWidth = 720;
		public const int PanelHeight = 440;

		private const int CardW = 48, CardH = 68, CardStep = 28;
		private const int RailHeight = 64;

		private enum Action { Close, Chip, Clear, Deal, Hit, Stand, Double, Split }

		private readonly record struct Button(Rectangle Rect, Action Kind, string Label, bool Enabled, Color Color, int Chip = 0);

		private int selectedChip;

		private static BlackjackSystem System => ModContent.GetInstance<BlackjackSystem>();

		private static long[] Chips => CasinoTableGameSystem.Casino?.ChipValues ?? new long[] { 100 };

		private static long MaxBet => CasinoTableGameSystem.Casino?.MaxBet ?? 0;

		private static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.UI.Blackjack." + key, args);

		private Rectangle Area => GetDimensions().ToRectangle();

		private Rectangle Rail => new(Area.X + 10, Area.Bottom - 56 - RailHeight, Area.Width - 20, RailHeight);

		private IEnumerable<Button> Buttons()
		{
			Rectangle a = Area;
			Rectangle rail = Rail;
			BlackjackRound round = System.Round;

			yield return new Button(new Rectangle(a.Right - 34, a.Y + 12, 22, 22), Action.Close, "X", true, CasinoDraw.Red);

			int y = rail.Y + 10;
			long[] chips = Chips;
			for (int k = 0; k < chips.Length; k++)
				yield return new Button(new Rectangle(rail.X + 8 + k * 60, y, 56, 44), Action.Chip, Coins.Format(chips[k]), round.CanBet, default, k);

			int x = rail.X + 8 + chips.Length * 60 + 6;
			yield return new Button(new Rectangle(x, y, 84, 21), Action.Clear, Text("Clear"), round.CanBet, CasinoDraw.FeltDark);
			yield return new Button(new Rectangle(x, y + 23, 84, 21), Action.Deal, Text("Deal"), round.CanBet, CasinoDraw.ButtonGold);

			int bx = rail.Right - 8 - 4 * 80;
			yield return new Button(new Rectangle(bx, y, 76, 44), Action.Hit, Text("Hit"), round.CanHit, CasinoDraw.ButtonGold);
			yield return new Button(new Rectangle(bx + 80, y, 76, 44), Action.Stand, Text("Stand"), round.CanHit, CasinoDraw.ButtonGold);
			yield return new Button(new Rectangle(bx + 160, y, 76, 44), Action.Double, Text("Double"), round.CanDouble, CasinoDraw.ButtonGold);
			yield return new Button(new Rectangle(bx + 240, y, 76, 44), Action.Split, Text("Split"), round.CanSplit, CasinoDraw.ButtonGold);
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
			Point mouse = CasinoTableGameSystem.UIMouse;

			CasinoDraw.Panel(sb, a);
			Utils.DrawBorderString(sb, Text("Title"), new Vector2(a.X + 18, a.Y + 14), CasinoDraw.Gold, 1.15f);

			DrawTablePrint(sb, a);
			DrawShoe(sb, new Vector2(a.Right - 92, a.Y + 44));

			// Dealer, centred at the top.
			int dealerY = a.Y + 70;
			string dealerLabel = Text("Dealer");
			if (round.Dealer.Count > 0)
				dealerLabel += ":  " + (round.HoleCardHidden ? "?" : BlackjackRules.Value(round.Dealer).ToString());
			Utils.DrawBorderString(sb, dealerLabel, new Vector2(a.Center.X, dealerY - 6), Color.White, 0.9f, 0.5f, 1f);
			DrawHand(sb, round.Dealer, new Vector2(a.Center.X, dealerY), round.HoleCardHidden, -1);

			// Player hands, centred in the middle.
			int handsY = a.Y + 214;
			int count = Math.Max(1, round.Hands.Count);
			for (int h = 0; h < count; h++) {
				float cx = count == 1 ? a.Center.X : a.Center.X + (h == 0 ? -150 : 150);
				if (h >= round.Hands.Count) {
					DrawHand(sb, Array.Empty<int>(), new Vector2(cx, handsY), false, h);
					continue;
				}

				BlackjackHand hand = round.Hands[h];
				bool active = round.Phase == BlackjackPhase.PlayerTurn && h == round.ActiveHand;
				if (active) {
					int width = CardW + CardStep * Math.Max(1, hand.Cards.Count - 1);
					CasinoDraw.Fill(sb, new Rectangle((int)cx - width / 2 - 6, handsY - 6, width + 12, CardH + 12), CasinoDraw.Gold * 0.55f);
				}
				DrawHand(sb, hand.Cards, new Vector2(cx, handsY), false, h);

				string label = hand.Value.ToString();
				if (hand.IsBlackjack)
					label = Text("BlackjackShort");
				else if (hand.Busted)
					label = Text("BustShort");
				Color color = hand.Busted ? Color.OrangeRed : hand.IsBlackjack ? CasinoDraw.Gold : Color.White;
				Utils.DrawBorderString(sb, label, new Vector2(cx, handsY - 6), color, 0.95f, 0.5f, 1f);

				string bet = Coins.Format(hand.Bet) + (hand.Doubled ? "  x2" : "");
				if (round.Phase == BlackjackPhase.Result && hand.Payout >= 0)
					bet += hand.Payout > hand.Bet ? "   +" + Coins.Format(hand.Payout) : hand.Payout == hand.Bet ? "   " + Text("PushShort") : "";
				DrawBetChip(sb, new Vector2(cx, handsY + CardH + 18), bet);
			}

			if (round.CanBet && round.PendingBet > 0)
				DrawBetChip(sb, new Vector2(a.Center.X, handsY + CardH + 18), Text("NextBet", Coins.Format(round.PendingBet)));

			// Control rail.
			Rectangle rail = Rail;
			CasinoDraw.Fill(sb, rail, CasinoDraw.Gold);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(rail, 2), new Color(60, 30, 20));
			foreach (Button button in Buttons()) {
				bool hover = button.Rect.Contains(mouse);
				if (button.Kind == Action.Chip)
					CasinoDraw.ChipButton(sb, button.Rect, button.Label, button.Chip, hover && button.Enabled, button.Chip == selectedChip);
				else
					CasinoDraw.Button(sb, button.Rect, button.Label, button.Color, hover, button.Enabled, button.Kind is Action.Clear or Action.Deal or Action.Close ? 0.8f : 0.95f);
			}

			// Status lines under the rail.
			Utils.DrawBorderString(sb, Text("Money", Coins.Format(Coins.Count(Main.LocalPlayer))), new Vector2(a.X + 20, a.Bottom - 46), Color.White, 0.8f);
			Utils.DrawBorderString(sb, Text("Limit", Coins.Format(MaxBet)), new Vector2(a.Right - 20, a.Bottom - 46), Color.White * 0.8f, 0.8f, 1f);
			string message = round.Message;
			if (string.IsNullOrEmpty(message))
				message = round.Phase switch {
					BlackjackPhase.Betting => Text("HintBet"),
					BlackjackPhase.PlayerTurn => Text("HintPlay"),
					_ => "",
				};
			Utils.DrawBorderString(sb, message, new Vector2(a.Center.X, a.Bottom - 22), round.MessageColor, 0.9f, 0.5f, 0.5f);
		}

		/// <summary>The printed arc and rules text on the felt.</summary>
		private static void DrawTablePrint(SpriteBatch sb, Rectangle a)
		{
			var center = new Vector2(a.Center.X, a.Y + 120);
			const float rx = 300f, ry = 140f;
			for (float t = 0.12f; t <= MathHelper.Pi - 0.12f; t += 0.012f) {
				var p = center + new Vector2((float)Math.Cos(t) * rx, (float)Math.Sin(t) * ry);
				CasinoDraw.Fill(sb, new Rectangle((int)p.X, (int)p.Y, 2, 2), CasinoDraw.Gold * 0.6f);
				var q = center + new Vector2((float)Math.Cos(t) * (rx - 16), (float)Math.Sin(t) * (ry - 12));
				CasinoDraw.Fill(sb, new Rectangle((int)q.X, (int)q.Y, 2, 2), CasinoDraw.Gold * 0.35f);
			}
			Utils.DrawBorderString(sb, Text("Rules"), new Vector2(a.Center.X, a.Y + 168), CasinoDraw.Gold * 0.75f, 0.75f, 0.5f, 0.5f);
		}

		private static void DrawShoe(SpriteBatch sb, Vector2 pos)
		{
			var body = new Rectangle((int)pos.X, (int)pos.Y + 10, 64, 40);
			shoePosition = new Vector2(body.X + 8, body.Y - 14);
			sb.Draw(Cards, new Rectangle(body.X + 8, body.Y - 14, CardW - 6, CardH - 20), BackFrame, Color.White);
			CasinoDraw.Fill(sb, body, new Color(30, 14, 40));
			CasinoDraw.Fill(sb, new Rectangle(body.X, body.Y, body.Width, 3), CasinoDraw.Gold);
			Utils.DrawBorderString(sb, Text("Shoe"), new Vector2(body.Center.X, body.Bottom - 6), Color.White * 0.8f, 0.6f, 0.5f, 1f);
		}

		/// <summary>Draws cards centred on centerX; empty hands show two outlined card slots.
		/// New cards slide in from the shoe and flip face up; the dealer's hole card flips when revealed.</summary>
		private static void DrawHand(SpriteBatch sb, IReadOnlyList<int> cards, Vector2 top, bool hideSecond, int handKey)
		{
			if (cards.Count == 0) {
				for (int k = 0; k < 2; k++) {
					var slot = new Rectangle((int)top.X - CardW - 4 + k * (CardW + 8), (int)top.Y, CardW, CardH);
					CasinoDraw.Fill(sb, slot, Color.White * 0.25f);
					CasinoDraw.Fill(sb, CasinoDraw.Shrink(slot, 2), CasinoDraw.Felt);
				}
				ForgetHand(handKey);
				return;
			}

			int width = CardW + CardStep * (cards.Count - 1);
			float x = top.X - width / 2f;
			for (int k = 0; k < cards.Count; k++) {
				bool hidden = hideSecond && k == 1;
				var key = (handKey, k);
				uint now = Main.GameUpdateCount;
				if (!dealtAt.TryGetValue(key, out var info) || info.card != cards[k]) {
					info = (cards[k], now, hidden);
					dealtAt[key] = info;
				}
				if (info.hidden && !hidden) {
					info = (cards[k], now - DealTicks / 2, false);   // hole card: flip only, no slide
					dealtAt[key] = info;
				}
				float p = Math.Min(1f, (now - info.at) / (float)DealTicks);
				Vector2 target = new(x + k * CardStep, top.Y);
				Vector2 from = shoePosition == Vector2.Zero ? target : shoePosition;
				Vector2 pos = Vector2.Lerp(from, target, Math.Min(1f, p * 2f));
				float flip = p < 0.5f ? 1f : Math.Abs((float)Math.Cos((p - 0.5f) * 2f * Math.PI));
				bool showBack = hidden || p < 0.75f;
				DrawCard(sb, pos, cards[k], showBack, p < 0.5f ? 1f : flip, (1f - Math.Min(1f, p * 2f)) * 0.6f);
			}
		}

		private const int DealTicks = 24;
		private static readonly Dictionary<(int hand, int index), (int card, uint at, bool hidden)> dealtAt = new();
		private static Vector2 shoePosition;

		private static void ForgetHand(int handKey)
		{
			foreach (var key in new List<(int, int)>(dealtAt.Keys))
				if (key.Item1 == handKey)
					dealtAt.Remove(key);
		}

		private static Texture2D Cards => (cardsTexture ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/Cards")).Value;
		private static ReLogic.Content.Asset<Texture2D> cardsTexture;
		private static Rectangle BackFrame => new(0, 4 * CardH, CardW, CardH);

		private static void DrawBetChip(SpriteBatch sb, Vector2 center, string text)
		{
			Texture2D chip = CasinoDraw.Chip;
			sb.Draw(chip, center, null, CasinoDraw.Gold, 0f, chip.Size() / 2f, 1f, SpriteEffects.None, 0f);
			Utils.DrawBorderString(sb, text, center + new Vector2(16, 3), Color.Yellow, 0.8f, 0f, 0.5f);
		}

		/// <summary>Draws one card from the atlas (or its back), squashed horizontally for the flip and tilted while flying.</summary>
		private static void DrawCard(SpriteBatch sb, Vector2 pos, int card, bool showBack, float scaleX = 1f, float tilt = 0f)
		{
			var center = pos + new Vector2(CardW / 2f, CardH / 2f);
			CasinoDraw.Fill(sb, new Rectangle((int)pos.X + 3, (int)pos.Y + 3, (int)(CardW * scaleX), CardH), Color.Black * 0.3f);
			Rectangle src = showBack ? BackFrame : new Rectangle(BlackjackRules.Rank(card) * CardW, BlackjackRules.Suit(card) * CardH, CardW, CardH);
			sb.Draw(Cards, center, src, Color.White, tilt, new Vector2(CardW / 2f, CardH / 2f), new Vector2(Math.Max(0.05f, scaleX), 1f), SpriteEffects.None, 0f);
		}
	}
}
