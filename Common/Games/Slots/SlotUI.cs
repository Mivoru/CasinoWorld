using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Games.Slots
{
	public class SlotUIState : UIState
	{
		public override void OnInitialize()
		{
			var panel = new SlotPanel();
			panel.Width.Set(SlotPanel.PanelWidth, 0f);
			panel.Height.Set(SlotPanel.PanelHeight, 0f);
			panel.HAlign = 0.5f;
			panel.VAlign = 0.5f;
			Append(panel);
		}
	}

	/// <summary>
	/// Slot machine window: neon marquee, three reels, a pull lever, the pay table, bet chips and Spin.
	/// </summary>
	public class SlotPanel : UIElement
	{
		public const int PanelWidth = 600;
		public const int PanelHeight = 430;

		private const int ReelW = 100, ReelH = 112, ReelGap = 10, Cell = 92;
		// Symbols are 64 px pixel art rendered from 3D models (blender/CasinoWorld/render_slot_symbols.py).
		private const int SymbolPx = 64;
		private const float SymbolScale = 1.2f;

		private static readonly Color Cabinet = new(70, 34, 104);
		private static readonly Color CabinetDark = new(40, 18, 62);
		private static readonly Color Neon = new(255, 80, 200);

		private enum Action { Close, Bet, Spin, Lever }

		private readonly record struct Button(Rectangle Rect, Action Kind, string Label, bool Enabled, int Index = 0);

		private static Asset<Texture2D> symbols;

		private static SlotSystem System => ModContent.GetInstance<SlotSystem>();

		private static long[] Chips => CasinoTableGameSystem.Casino?.ChipValues ?? new long[] { 100 };

		private static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.UI.Slots." + key, args);

		private Rectangle Area => GetDimensions().ToRectangle();

		private Rectangle ReelBox(int k) => new(Area.X + 34 + k * (ReelW + ReelGap), Area.Y + 74, ReelW, ReelH);

		private Rectangle LeverRect => new(ReelBox(2).Right + 14, Area.Y + 60, 26, 150);

		public override void OnInitialize()
		{
			symbols ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/SlotSymbols");
		}

		private IEnumerable<Button> Buttons()
		{
			Rectangle a = Area;
			SlotRound round = System.Round;
			yield return new Button(new Rectangle(a.Right - 32, a.Y + 10, 22, 22), Action.Close, "X", true);

			long[] chips = Chips;
			int y = a.Bottom - 112;
			for (int k = 0; k < chips.Length; k++)
				yield return new Button(new Rectangle(a.X + 34 + k * 64, y, 58, 44), Action.Bet, Coins.Format(chips[k]), round.CanSpin, k);

			yield return new Button(new Rectangle(a.X + 34 + chips.Length * 64 + 8, y, 110, 44), Action.Spin, Text("Spin"), round.CanSpin);
			yield return new Button(LeverRect, Action.Lever, "", round.CanSpin);
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
			SlotRound round = System.Round;
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
				case Action.Lever:
					round.TrySpin(Main.LocalPlayer, chips[round.BetIndex]);
					break;
			}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;

			Rectangle a = Area;
			SlotRound round = System.Round;
			Point mouse = CasinoTableGameSystem.UIMouse;
			long[] chips = Chips;
			round.BetIndex = Math.Clamp(round.BetIndex, 0, chips.Length - 1);

			// Cabinet body.
			CasinoDraw.Fill(sb, a, CasinoDraw.Gold);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(a, 3), CabinetDark);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(a, 6), Cabinet);

			DrawMarquee(sb, new Rectangle(a.X + 30, a.Y + 14, 360, 44));

			// Reels.
			bool flash = round.WinFlash > 0 && round.WinFlash / 8 % 2 == 0;
			for (int k = 0; k < SlotRules.Reels; k++) {
				Rectangle box = ReelBox(k);
				CasinoDraw.Fill(sb, CasinoDraw.Shrink(box, -4), flash ? Color.White : CasinoDraw.Gold);
				CasinoDraw.Fill(sb, box, new Color(250, 248, 240));
				DrawReel(sb, box, round.Offset[k]);
				// Shading at the top and bottom of the reel, like a curved drum.
				CasinoDraw.Fill(sb, new Rectangle(box.X, box.Y, box.Width, 10), Color.Black * 0.18f);
				CasinoDraw.Fill(sb, new Rectangle(box.X, box.Bottom - 10, box.Width, 10), Color.Black * 0.18f);
			}
			Rectangle first = ReelBox(0), last = ReelBox(2);
			Color line = flash ? Color.Red : Color.Red * 0.35f;
			CasinoDraw.Fill(sb, new Rectangle(first.X - 8, first.Center.Y - 1, last.Right - first.X + 16, 3), line);

			DrawLever(sb, round);
			DrawPayTable(sb, new Vector2(a.Right - 150, a.Y + 66));

			foreach (Button button in Buttons()) {
				bool hover = button.Rect.Contains(mouse);
				switch (button.Kind) {
					case Action.Bet:
						CasinoDraw.ChipButton(sb, button.Rect, button.Label, button.Index, hover && button.Enabled, button.Index == round.BetIndex);
						break;
					case Action.Spin:
						CasinoDraw.Button(sb, button.Rect, button.Label, CasinoDraw.ButtonGold, hover, button.Enabled, 1.15f);
						break;
					case Action.Close:
						CasinoDraw.Button(sb, button.Rect, button.Label, CasinoDraw.Red, hover, true, 0.8f);
						break;
				}
			}

			Utils.DrawBorderString(sb, Text("Money", Coins.Format(Coins.Count(Main.LocalPlayer))), new Vector2(a.X + 34, a.Bottom - 56), Color.White, 0.8f);
			Utils.DrawBorderString(sb, Text("Bet", Coins.Format(chips[round.BetIndex])), new Vector2(a.X + 260, a.Bottom - 56), Color.White, 0.8f);
			string message = string.IsNullOrEmpty(round.Message) ? Text("Hint") : round.Message;
			Utils.DrawBorderString(sb, message, new Vector2(a.X + 34, a.Bottom - 32), round.MessageColor, 0.9f);
		}

		private static void DrawMarquee(SpriteBatch sb, Rectangle r)
		{
			CasinoDraw.Fill(sb, r, CasinoDraw.Gold);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(r, 3), new Color(20, 10, 28));
			int phase = (int)(Main.GlobalTimeWrappedHourly * 8f);
			for (int x = r.X + 6, k = 0; x < r.Right - 6; x += 10, k++) {
				Color c = (k + phase) % 3 == 0 ? Color.White : Neon;
				CasinoDraw.Fill(sb, new Rectangle(x, r.Y + 4, 4, 4), c);
				CasinoDraw.Fill(sb, new Rectangle(x, r.Bottom - 8, 4, 4), c);
			}
			Utils.DrawBorderString(sb, Text("Title"), r.Center.ToVector2() + new Vector2(0, 4), Neon, 1.3f, 0.5f, 0.5f);
		}

		/// <summary>Draws the symbols around the reel's offset, clipped to the reel box.</summary>
		private static void DrawReel(SpriteBatch sb, Rectangle box, float offset)
		{
			int index = (int)Math.Floor(offset);
			float fraction = offset - index;
			float center = box.Center.Y;
			for (int k = -1; k <= 1; k++) {
				int symbol = ((index + k) % SlotRules.SymbolCount + SlotRules.SymbolCount) % SlotRules.SymbolCount;
				DrawSymbolClipped(sb, symbol, center + (fraction - k) * Cell, box);
			}
		}

		private static void DrawSymbolClipped(SpriteBatch sb, int symbol, float centerY, Rectangle box)
		{
			Texture2D tex = symbols.Value;
			float size = SymbolPx * SymbolScale;
			float top = centerY - size / 2f;
			float clipTop = Math.Max(top, box.Top), clipBottom = Math.Min(top + size, box.Bottom);
			if (clipBottom <= clipTop)
				return;

			int srcY = (int)((clipTop - top) / SymbolScale);
			int srcH = Math.Min(SymbolPx - srcY, (int)Math.Ceiling((clipBottom - clipTop) / SymbolScale));
			if (srcH <= 0)
				return;
			var src = new Rectangle(symbol * SymbolPx, srcY, SymbolPx, srcH);
			sb.Draw(tex, new Vector2(box.Center.X - size / 2f, top + srcY * SymbolScale), src, Color.White, 0f, Vector2.Zero, SymbolScale, SpriteEffects.None, 0f);
		}

		private void DrawLever(SpriteBatch sb, SlotRound round)
		{
			Rectangle r = LeverRect;
			// The handle drops while the reels start, then comes back up.
			float pull = round.Phase == SlotPhase.Spinning && !round.Stopped[0] ? 1f : 0f;
			int knobY = (int)MathHelper.Lerp(r.Y + 12, r.Y + 92, pull);
			CasinoDraw.Fill(sb, new Rectangle(r.X + 9, Math.Min(knobY, r.Y + 92), 8, r.Bottom - 40 - Math.Min(knobY, r.Y + 92)), new Color(170, 170, 180));
			CasinoDraw.Fill(sb, new Rectangle(r.X + 2, r.Bottom - 44, 22, 30), CasinoDraw.Gold);
			Texture2D chip = CasinoDraw.Chip;
			sb.Draw(chip, new Vector2(r.X + 13, knobY), null, CasinoDraw.Red, 0f, chip.Size() / 2f, 1.3f, SpriteEffects.None, 0f);
		}

		private static void DrawPayTable(SpriteBatch sb, Vector2 pos)
		{
			Texture2D tex = symbols.Value;
			Utils.DrawBorderString(sb, Text("Pays"), pos, CasinoDraw.Gold, 0.85f);
			float y = pos.Y + 24;
			for (int s = SlotRules.SymbolCount - 1; s >= 0; s--) {
				for (int k = 0; k < 3; k++)
					sb.Draw(tex, new Vector2(pos.X + k * 18, y), new Rectangle(s * SymbolPx, 0, SymbolPx, SymbolPx), Color.White, 0f, Vector2.Zero, 0.25f, SpriteEffects.None, 0f);
				Utils.DrawBorderString(sb, "x" + SlotRules.TriplePays[s], new Vector2(pos.X + 62, y + 2), Color.White, 0.75f);
				y += 20;
			}
			// Two of a kind on reels 1-2: one compact line for all symbols.
			for (int k = 0; k < 2; k++)
				sb.Draw(tex, new Vector2(pos.X + k * 18, y), new Rectangle(6 * SymbolPx, 0, SymbolPx, SymbolPx), Color.White, 0f, Vector2.Zero, 0.25f, SpriteEffects.None, 0f);
			Utils.DrawBorderString(sb, Text("PairPays", SlotRules.PairPays[1], SlotRules.PairPays[6]), new Vector2(pos.X + 40, y + 2), Color.White, 0.65f);
			y += 20;
			sb.Draw(tex, new Vector2(pos.X, y), new Rectangle(0, 0, SymbolPx, SymbolPx), Color.White, 0f, Vector2.Zero, 0.25f, SpriteEffects.None, 0f);
			Utils.DrawBorderString(sb, "x" + SlotRules.OneCherryPay, new Vector2(pos.X + 62, y + 2), Color.White, 0.75f);
		}
	}
}
