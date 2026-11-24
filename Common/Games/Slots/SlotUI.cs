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
		private static readonly Color Neon = new(255, 80, 200);

		private enum Action { Close, Bet, Spin, Lever, Auto, AutoCount }

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

			yield return new Button(new Rectangle(a.X + 34 + chips.Length * 64 + 8, y, 110, 44), Action.Spin, Text("Spin"), round.CanSpin && round.AutoLeft <= 0);
			int autoX = a.X + 34 + chips.Length * 64 + 126;
			yield return new Button(new Rectangle(autoX, y, 84, 44), Action.Auto, AutoSpin.Label(round.AutoLeft), round.AutoLeft > 0 || round.CanSpin);
			yield return new Button(new Rectangle(autoX + 90, y, 50, 44), Action.AutoCount, "x" + AutoSpin.Counts[round.AutoChoice], round.AutoLeft <= 0);
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
				case Action.Auto:
					round.ToggleAuto();
					SoundEngine.PlaySound(SoundID.MenuTick);
					break;
				case Action.AutoCount:
					round.AutoChoice = (round.AutoChoice + 1) % AutoSpin.Counts.Length;
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
			CasinoDraw.Cabinet(sb, a, CasinoDraw.Gold, Cabinet);

			DrawMarquee(sb, new Rectangle(a.X + 30, a.Y + 14, 360, 44), round);
			DrawFrameBulbs(sb, a, round);

			// Reels.
			bool flash = round.WinFlash > 0 && round.WinFlash / 8 % 2 == 0;
			for (int k = 0; k < SlotRules.Reels; k++) {
				Rectangle box = ReelBox(k);
				CasinoDraw.Fill(sb, CasinoDraw.Shrink(box, -4), flash ? Color.White : CasinoDraw.Gold);
				CasinoDraw.Fill(sb, box, new Color(250, 248, 240));
				float bounce = round.Bounce[k] > 0 ? (float)Math.Sin(round.Bounce[k] * 0.45f) * round.Bounce[k] * 0.008f : 0f;
				DrawReel(sb, box, round.Offset[k] + bounce);
				if (!round.Stopped[k])   // motion streaks
					for (int g = 0; g < 6; g++)
						CasinoDraw.Fill(sb, new Rectangle(box.X + 8 + g * 15, box.Y, 4, box.Height), Color.White * (0.10f + 0.06f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 30f + g)));
				if (round.WinFlash > 0 && k < round.WinningReels) {
					Color glow = new Color(255, 215, 80) * (0.25f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f));
					glow.A = 0;
					CasinoDraw.Fill(sb, box, glow);
					UiFx.Shimmer(box, new Color(255, 235, 150), 3);
				}
				if (k == 2 && round.Tension) {
					Color heat = new Color(255, 60, 40) * (0.2f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 18f));
					heat.A = 0;
					CasinoDraw.Fill(sb, CasinoDraw.Shrink(box, -8), heat);
					UiFx.Shimmer(CasinoDraw.Shrink(box, -8), new Color(255, 140, 70), 2);
				}
				// Shading at the top and bottom of the reel, like a curved drum.
				for (int g = 0; g < 5; g++) {
					float alpha = 0.32f * (5 - g) / 5f;
					CasinoDraw.Fill(sb, new Rectangle(box.X, box.Y + g * 3, box.Width, 3), Color.Black * alpha);
					CasinoDraw.Fill(sb, new Rectangle(box.X, box.Bottom - (g + 1) * 3, box.Width, 3), Color.Black * alpha);
				}
				// Chrome rim and a reflection that travels over the glass.
				for (int g = 0; g < 4; g++) {
					Color chrome = Color.Lerp(new Color(250, 250, 255), new Color(110, 116, 136), g / 3f);
					CasinoDraw.Fill(sb, new Rectangle(box.X - 4 + g, box.Y - 4 + g, 1, box.Height + 8 - g * 2), chrome);
					CasinoDraw.Fill(sb, new Rectangle(box.Right + 3 - g, box.Y - 4 + g, 1, box.Height + 8 - g * 2), chrome);
				}
				CasinoDraw.Fill(sb, new Rectangle(box.X + 4, box.Y + 18, 3, box.Height - 36), Color.White * 0.35f);   // glass glint
				int sweep = (int)(Main.GlobalTimeWrappedHourly * 60f + k * 40) % 320;
				if (sweep < box.Width - 14)
					CasinoDraw.Fill(sb, new Rectangle(box.X + sweep, box.Y, 12, box.Height), Color.White * 0.10f);
				if (round.WinFlash > 0 && k < round.WinningReels)
					DrawWinSymbol(sb, box, (int)round.Result[k], k, round);
			}
			Rectangle first = ReelBox(0), last = ReelBox(2);
			Color line = flash ? Color.Red : Color.Red * 0.35f;
			CasinoDraw.Fill(sb, new Rectangle(first.X - 8, first.Center.Y - 1, last.Right - first.X + 16, 3), line);

			DrawLever(sb, round);
			DrawDiamondShow(sb, round);
			DrawStreak(sb, a, round);
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
					case Action.Auto:
					case Action.AutoCount:
						CasinoDraw.Button(sb, button.Rect, button.Label, AutoSpin.Tint(round.AutoLeft, button.Kind == Action.Auto), hover, button.Enabled, 0.8f);
						break;
					case Action.Close:
						CasinoDraw.Button(sb, button.Rect, button.Label, CasinoDraw.Red, hover, true, 0.8f);
						break;
				}
			}

			Utils.DrawBorderString(sb, Text("Money", Coins.Format(Coins.Count(Main.LocalPlayer))), new Vector2(a.X + 34, a.Bottom - 56), Color.White, 0.8f);
			Utils.DrawBorderString(sb, Text("Bet", Coins.Format(chips[round.BetIndex])), new Vector2(a.X + 260, a.Bottom - 56), Color.White, 0.8f);
			string message = string.IsNullOrEmpty(round.Message) ? Text("Hint") : round.Message;
			CasinoDraw.Fit(sb, message, new Vector2(a.X + 34, a.Bottom - 32), round.MessageColor, 0.9f, a.Width - 68);
		}

		/// <summary>Bulbs along the cabinet edge: slow when idle, racing while spinning, all blinking on a win.</summary>
		private static void DrawFrameBulbs(SpriteBatch sb, Rectangle a, SlotRound round)
		{
			float speed = round.WinFlash > 0 ? 0f : round.Tension ? 26f : round.Phase == SlotPhase.Spinning ? 16f : 4f;
			int step = (int)(Main.GlobalTimeWrappedHourly * speed), n = 0;
			bool blink = round.WinFlash / 6 % 2 == 0;
			void Bulb(int x, int y)
			{
				bool on = round.WinFlash > 0 ? blink : (n + step) % 4 == 0;
				Color c = on ? n % 2 == 0 ? new Color(255, 235, 150) : Neon : new Color(60, 30, 60);
				n++;
				if (on)
					CasinoDraw.Fill(sb, new Rectangle(x - 5, y - 5, 10, 10), c * 0.25f);
				CasinoDraw.Fill(sb, new Rectangle(x - 2, y - 2, 5, 5), c);
			}
			for (int x = a.X + 16; x < a.Right - 8; x += 22)
				Bulb(x, a.Y + 9);
			for (int y = a.Y + 30; y < a.Bottom - 8; y += 22)
				Bulb(a.Right - 9, y);
			for (int x = a.Right - 16; x > a.X + 8; x -= 22)
				Bulb(x, a.Bottom - 9);
			for (int y = a.Bottom - 30; y > a.Y + 8; y -= 22)
				Bulb(a.X + 9, y);
		}

		private static void DrawMarquee(SpriteBatch sb, Rectangle r, SlotRound round)
		{
			CasinoDraw.Fill(sb, r, CasinoDraw.Gold);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(r, 3), new Color(20, 10, 28));
			int phase = (int)(Main.GlobalTimeWrappedHourly * (round.Phase == SlotPhase.Spinning ? 18f : 8f));
			for (int x = r.X + 6, k = 0; x < r.Right - 6; x += 10, k++) {
				Color c = (k + phase) % 3 == 0 ? Color.White : Neon;
				CasinoDraw.Fill(sb, new Rectangle(x, r.Y + 4, 4, 4), c);
				CasinoDraw.Fill(sb, new Rectangle(x, r.Bottom - 8, 4, 4), c);
			}
			Utils.DrawBorderString(sb, round.Tension ? Text("Tension") : round.WinFlash > 0 ? Text("WinMarquee", Coins.Format(round.LastPayout)) : Text("Title"), r.Center.ToVector2() + new Vector2(0, 4), round.WinFlash > 0 ? Color.Lerp(Color.Gold, Color.White, 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 14f)) : round.Tension ? Color.OrangeRed : Neon, 1.3f, 0.5f, 0.5f);
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

		/// <summary>The winning symbol steps out of the reel and acts: cherries swing, the bell rings, sevens burn one after another, diamonds sparkle.</summary>
		private static void DrawWinSymbol(SpriteBatch sb, Rectangle box, int symbol, int reel, SlotRound round)
		{
			float t = Main.GlobalTimeWrappedHourly;
			int age = 90 - round.WinFlash;
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(box, 2), new Color(255, 250, 235));
			Vector2 at = box.Center.ToVector2();
			float rotation = 0f, scale = SymbolScale;
			Color color = Color.White;
			switch ((SlotSymbol)symbol) {
				case SlotSymbol.Cherry: rotation = (float)Math.Sin(t * 8f + reel) * 0.3f; break;
				case SlotSymbol.Lemon: scale *= 1f + 0.12f * (float)Math.Sin(t * 12f); break;
				case SlotSymbol.Bell:
					rotation = (float)Math.Sin(t * 18f) * 0.35f;
					for (int k = 1; k < 4; k++)
						UiFx.Shimmer(box, new Color(255, 220, 110), 8);
					break;
				case SlotSymbol.Clover: rotation = t * 3f; break;
				case SlotSymbol.Bar:
					float open = Math.Min(1f, age / 30f);   // the vault opens
					CasinoDraw.Fill(sb, new Rectangle(box.X + 6, (int)at.Y - (int)(26 * open), box.Width - 12, (int)(52 * open)), new Color(255, 205, 70));
					at.Y -= open * 8f;
					break;
				case SlotSymbol.Seven:
					if (age > reel * 12)
						for (int k = 0; k < 5; k++)
							Effects.PixelFx.Flame(sb, new Vector2(box.X + 14 + k * (box.Width - 28) / 4f, box.Bottom - 4), t, k * 2 + reel);
					scale *= 1.05f + 0.06f * (float)Math.Sin(t * 14f + reel);
					break;
				case SlotSymbol.Diamond:
					color = Color.Lerp(Color.White, Main.hslToRgb(t * 0.5f % 1f, 0.6f, 0.75f), 0.6f);
					Effects.PixelFx.Burst(sb, at, Main.hslToRgb(t * 0.3f % 1f, 0.8f, 0.65f) * 0.9f, t);
					Effects.PixelFx.Glow(sb, at, 48f, Main.hslToRgb((t * 0.3f + 0.4f) % 1f, 0.8f, 0.6f) * 0.5f);
					UiFx.Shimmer(box, Color.White, 3);
					break;
			}
			sb.Draw(symbols.Value, at, new Rectangle(symbol * SymbolPx, 0, SymbolPx, SymbolPx), color, rotation, new Vector2(SymbolPx / 2f), scale, SpriteEffects.None, 0f);
		}

		/// <summary>Three diamonds: the room goes dark and a beam links them.</summary>
		private void DrawDiamondShow(SpriteBatch sb, SlotRound round)
		{
			if (round.WinFlash <= 0 || round.WinningReels != 3 || round.Result[0] != SlotSymbol.Diamond || !Systems.CasinoClientConfig.Instance.ScreenEffects)
				return;
			float p = (float)Math.Sin(Math.PI * round.WinFlash / 90f);
			Rectangle a = ReelBox(0), b = ReelBox(2);
			for (int k = 0; k < 5; k++) {
				Color c = Main.hslToRgb((Main.GlobalTimeWrappedHourly * 0.4f + k * 0.2f) % 1f, 0.8f, 0.7f) * (0.35f * p);
				c.A = 0;
				CasinoDraw.Fill(sb, new Rectangle(a.Center.X - 30, a.Center.Y - 14 + k * 6, b.Center.X - a.Center.X + 60, 5), c);
			}
			if (round.WinFlash % 6 == 0)
				UiFx.Coins_(ReelBox(1).Center.ToVector2(), 6, 12f);
		}

		/// <summary>Three wins in a row set the cabinet's edge on fire.</summary>
		private static void DrawStreak(SpriteBatch sb, Rectangle a, SlotRound round)
		{
			if (round.Streak < 3)
				return;
			float t = Main.GlobalTimeWrappedHourly, power = Math.Min(1f, round.Streak / 6f);
			for (int k = 0; k * 14 < a.Width - 10; k++)
				if (k * 7 % 5 < 2 + power * 4f)
					Effects.PixelFx.Flame(sb, new Vector2(a.X + 10 + k * 14, a.Y + 4 + k * 3 % 4), t, k * 5);
			Utils.DrawBorderString(sb, Text("Streak", round.Streak), new Vector2(a.Center.X, a.Y - 24), new Color(255, 190, 80), 0.95f, 0.5f, 0.5f);
		}

		private void DrawLever(SpriteBatch sb, SlotRound round)
		{
			Rectangle r = LeverRect;
			// The handle drops while the reels start, then comes back up.
			// Pulled down fast, then it springs back and wobbles.
			float lt = round.SpinAge;
			float pull = lt < 8 ? lt / 8f : lt < 18 ? 1f : lt < 70 ? (float)(Math.Exp(-(lt - 18) / 12.0) * Math.Cos((lt - 18) * 0.28)) : 0f;
			int knobY = (int)MathHelper.Clamp(MathHelper.Lerp(r.Y + 12, r.Y + 92, pull), r.Y + 2, r.Y + 92);
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
			Utils.DrawBorderString(sb, Text("PairPays", SlotRules.PairPays[1], SlotRules.PairPays[6]), new Vector2(pos.X + 62, y + 2), Color.White, 0.75f);
			y += 20;
			sb.Draw(tex, new Vector2(pos.X, y), new Rectangle(0, 0, SymbolPx, SymbolPx), Color.White, 0f, Vector2.Zero, 0.25f, SpriteEffects.None, 0f);
			Utils.DrawBorderString(sb, "x" + SlotRules.OneCherryPay, new Vector2(pos.X + 62, y + 2), Color.White, 0.75f);
		}
	}
}
