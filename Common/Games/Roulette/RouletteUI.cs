using System;
using System.Collections.Generic;
using CasinoWorld.Content.Subworlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Games.Roulette
{
	public class RouletteUIState : UIState
	{
		public override void OnInitialize()
		{
			var panel = new RoulettePanel();
			panel.Width.Set(RoulettePanel.PanelWidth, 0f);
			panel.Height.Set(RoulettePanel.PanelHeight, 0f);
			panel.HAlign = 0.5f;
			panel.VAlign = 0.5f;
			Append(panel);
		}
	}

	/// <summary>
	/// The roulette window: wheel on the left, betting layout on the right, chips and buttons below.
	/// Left click places the selected chip, right click removes it.
	/// </summary>
	public class RoulettePanel : UIElement
	{
		public const int PanelWidth = 690;
		public const int PanelHeight = 390;

		private const int BoardX = 230, BoardY = 56;
		private const int CellW = 32, CellH = 30, ZeroW = 30, SideH = 26;

		private static Color Felt => CasinoDraw.Felt;
		private static Color FeltDark => CasinoDraw.FeltDark;
		private static Color Gold => CasinoDraw.Gold;
		private static Color RedCell => CasinoDraw.Red;
		private static Color BlackCell => CasinoDraw.Black;
		private static Color GreenCell => CasinoDraw.Green;

		private enum RegionKind { Bet, Edge, Chip, Spin, Clear, Repeat, Close }

		private readonly record struct Region(Rectangle Rect, RegionKind Kind, RouletteBet Bet = default, long Chip = 0, string Label = "", Color Color = default);

		private int selectedChip;

		private static RouletteSystem System => ModContent.GetInstance<RouletteSystem>();

		private static Asset<Texture2D> wheelTexture, ballTexture;

		public override void OnInitialize()
		{
			wheelTexture ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/RouletteWheel");
			ballTexture ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/RouletteBall");
		}

		private Rectangle Area => GetDimensions().ToRectangle();

		private static long[] Chips => RouletteSystem.Casino?.ChipValues ?? new long[] { 100 };

		private static long MaxBet => RouletteSystem.Casino?.MaxBet ?? 0;

		private IEnumerable<Region> Layout()
		{
			Rectangle a = Area;
			int bx = a.X + BoardX, by = a.Y + BoardY;

			yield return new Region(new Rectangle(a.Right - 34, a.Y + 12, 22, 22), RegionKind.Close, Label: "X", Color: RedCell);

			// Corner (4 numbers) and split (2 numbers) hot spots come first so they win the hit test.
			for (int c = 0; c < 11; c++) {
				for (int r = 0; r < 2; r++) {
					int v = c * 3 + (2 - r);   // lowest of the four numbers around this corner
					var point = new Point(bx + ZeroW + (c + 1) * CellW, by + (r + 1) * CellH);
					yield return new Region(new Rectangle(point.X - 6, point.Y - 6, 12, 12), RegionKind.Edge, new RouletteBet(RouletteBetType.Corner, v));
				}
			}
			for (int c = 0; c < 12; c++) {
				for (int r = 0; r < 3; r++) {
					int n = c * 3 + (3 - r);
					int x = bx + ZeroW + c * CellW, y = by + r * CellH;
					if (r < 2)   // with the number below: n and n - 1
						yield return new Region(new Rectangle(x + 7, y + CellH - 4, CellW - 14, 8), RegionKind.Edge, new RouletteBet(RouletteBetType.Split, (n - 1) * 100 + n));
					if (c < 11)  // with the number to the right: n and n + 3
						yield return new Region(new Rectangle(x + CellW - 4, y + 7, 8, CellH - 14), RegionKind.Edge, new RouletteBet(RouletteBetType.Split, n * 100 + n + 3));
				}
			}

			// Zero spans the three rows.
			yield return new Region(new Rectangle(bx, by, ZeroW, CellH * 3), RegionKind.Bet, new RouletteBet(RouletteBetType.Straight, 0), Label: "0", Color: GreenCell);

			for (int c = 0; c < 12; c++) {
				for (int r = 0; r < 3; r++) {
					int n = c * 3 + (3 - r);
					var rect = new Rectangle(bx + ZeroW + c * CellW, by + r * CellH, CellW, CellH);
					yield return new Region(rect, RegionKind.Bet, new RouletteBet(RouletteBetType.Straight, n), Label: n.ToString(),
						Color: RouletteRules.IsRed(n) ? RedCell : BlackCell);
				}
			}

			// Column bets ("2:1") at the end of each row. Top row holds 3, 6, 9..., so column value 2.
			for (int r = 0; r < 3; r++) {
				var rect = new Rectangle(bx + ZeroW + 12 * CellW, by + r * CellH, CellW, CellH);
				yield return new Region(rect, RegionKind.Bet, new RouletteBet(RouletteBetType.Column, 2 - r), Label: Text("Column"), Color: Felt);
			}

			int dozenY = by + 3 * CellH;
			for (int d = 0; d < 3; d++) {
				var rect = new Rectangle(bx + ZeroW + d * 4 * CellW, dozenY, 4 * CellW, SideH);
				yield return new Region(rect, RegionKind.Bet, new RouletteBet(RouletteBetType.Dozen, d), Label: Text("Dozen" + (d + 1)), Color: Felt);
			}

			int outsideY = dozenY + SideH;
			(RouletteBetType type, string label, Color color)[] outside = {
				(RouletteBetType.Low, Text("Low"), Felt),
				(RouletteBetType.Even, Text("Even"), Felt),
				(RouletteBetType.Red, Text("Red"), RedCell),
				(RouletteBetType.Black, Text("Black"), BlackCell),
				(RouletteBetType.Odd, Text("Odd"), Felt),
				(RouletteBetType.High, Text("High"), Felt),
			};
			for (int k = 0; k < outside.Length; k++) {
				var rect = new Rectangle(bx + ZeroW + k * 2 * CellW, outsideY, 2 * CellW, SideH);
				yield return new Region(rect, RegionKind.Bet, new RouletteBet(outside[k].type), Label: outside[k].label, Color: outside[k].color);
			}

			// Chips.
			int chipY = outsideY + SideH + 18;
			long[] chips = Chips;
			for (int k = 0; k < chips.Length; k++) {
				var rect = new Rectangle(bx + k * 64, chipY, 58, 40);
				yield return new Region(rect, RegionKind.Chip, Chip: k, Label: Coins.Format(chips[k]));
			}

			// Buttons.
			int buttonX = bx + ZeroW + 12 * CellW + CellW - 90;
			yield return new Region(new Rectangle(buttonX, chipY - 2, 90, 44), RegionKind.Spin, Label: Text("Spin"), Color: CasinoDraw.ButtonGold);
			yield return new Region(new Rectangle(buttonX - 84, chipY, 78, 19), RegionKind.Clear, Label: Text("Clear"), Color: FeltDark);
			yield return new Region(new Rectangle(buttonX - 84, chipY + 21, 78, 19), RegionKind.Repeat, Label: Text("Repeat"), Color: FeltDark);
		}

		private static string Text(string key) => Language.GetTextValue("Mods.CasinoWorld.UI.Roulette." + key);

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
		}

		// Clicks come from UI events (in UI coordinates), not from polling the mouse.
		public override void LeftClick(UIMouseEvent evt) => HandleClick(evt.MousePosition.ToPoint(), false);

		public override void RightClick(UIMouseEvent evt) => HandleClick(evt.MousePosition.ToPoint(), true);

		private void HandleClick(Point mouse, bool right)
		{
			foreach (Region region in Layout()) {
				if (region.Rect.Contains(mouse)) {
					Click(region, right);
					break;
				}
			}
		}

		private void Click(Region region, bool right)
		{
			RouletteRound round = System.Round;
			long[] chips = Chips;
			selectedChip = Math.Clamp(selectedChip, 0, chips.Length - 1);

			switch (region.Kind) {
				case RegionKind.Bet:
				case RegionKind.Edge:
					if (right)
						round.RemoveChip(region.Bet, chips[selectedChip]);
					else
						round.AddChip(region.Bet, chips[selectedChip], MaxBet);
					break;
				case RegionKind.Chip:
					selectedChip = (int)region.Chip;
					Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick);
					break;
				case RegionKind.Spin:
					if (!right)
						System.Spin();
					break;
				case RegionKind.Clear:
					round.Clear();
					break;
				case RegionKind.Repeat:
					round.Repeat(MaxBet);
					break;
				case RegionKind.Close:
					System.Close();
					break;
			}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;

			Rectangle a = Area;
			RouletteRound round = System.Round;
			Point mouse = CasinoTableGameSystem.UIMouse;

			CasinoDraw.Panel(sb, a);

			Utils.DrawBorderString(sb, Text("Title"), new Vector2(a.X + 16, a.Y + 12), Gold, 1.1f);

			DrawWheel(sb, round, new Vector2(a.X + 118, a.Y + 150));
			DrawHistory(sb, round, new Vector2(a.X + 20, a.Y + 262));

			int winning = round.Phase == RoulettePhase.ShowingResult ? round.Result : -1;
			var edges = new List<Region>();
			foreach (Region region in Layout()) {
				bool hover = region.Rect.Contains(mouse);
				switch (region.Kind) {
					case RegionKind.Edge:
						edges.Add(region);
						break;
					case RegionKind.Bet:
						bool won = winning >= 0 && RouletteRules.Wins(region.Bet, winning);
						DrawCell(sb, region, hover && !edges.Exists(e => e.Rect.Contains(mouse)), won);
						if (round.Bets.TryGetValue(region.Bet, out long stake))
							DrawStake(sb, region.Rect, stake);
						break;
					case RegionKind.Chip:
						CasinoDraw.ChipButton(sb, region.Rect, region.Label, (int)region.Chip, hover, (int)region.Chip == selectedChip);
						break;
					default:
						bool enabled = region.Kind != RegionKind.Spin || round.CanEditBets;
						CasinoDraw.Button(sb, region.Rect, region.Label, region.Color, hover, enabled, region.Kind == RegionKind.Spin ? 1.1f : 0.8f);
						break;
				}
			}

			// Split and corner hot spots: a gold marker on hover, a chip when bet on.
			foreach (Region edge in edges) {
				bool hover = edge.Rect.Contains(mouse);
				bool won = winning >= 0 && RouletteRules.Wins(edge.Bet, winning);
				if (hover || won)
					Fill(sb, edge.Rect, (won ? Color.White : Gold) * 0.85f);
				if (round.Bets.TryGetValue(edge.Bet, out long stake)) {
					Texture2D chip = CasinoDraw.Chip;
					Vector2 c = edge.Rect.Center.ToVector2();
					sb.Draw(chip, c, null, Gold, 0f, chip.Size() / 2f, 0.75f, SpriteEffects.None, 0f);
					Utils.DrawBorderString(sb, Coins.Format(stake), c + new Vector2(0, 14), Color.Yellow, 0.55f, 0.5f, 0.5f);
				}
			}

			DrawInfo(sb, round, a);
		}

		private void DrawWheel(SpriteBatch sb, RouletteRound round, Vector2 center)
		{
			Texture2D wheel = wheelTexture.Value;
			// Static bowl: the wheel silhouette, larger and darkened, plus a soft drop shadow.
			sb.Draw(wheel, center + new Vector2(4, 6), null, Color.Black * 0.35f, 0f, wheel.Size() / 2f, 1.08f, SpriteEffects.None, 0f);
			sb.Draw(wheel, center, null, new Color(70, 40, 22), 0f, wheel.Size() / 2f, 1.08f, SpriteEffects.None, 0f);
			sb.Draw(wheel, center, null, new Color(30, 16, 10), 0f, wheel.Size() / 2f, 1.03f, SpriteEffects.None, 0f);
			sb.Draw(wheel, center, null, Color.White, round.WheelAngle, wheel.Size() / 2f, 1f, SpriteEffects.None, 0f);

			// Bulbs around the bowl: they race with the ball and all light up on a win.
			bool celebrate = round.Phase == RoulettePhase.ShowingResult && round.LastPayout > 0;
			float race = round.Phase == RoulettePhase.Spinning ? round.BallAngle : Main.GlobalTimeWrappedHourly * 1.2f;
			for (int k = 0; k < 28; k++) {
				float ba = k * MathHelper.TwoPi / 28f;
				float near = Math.Abs(MathHelper.WrapAngle(ba - race));
				float lit = celebrate ? 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 14f + k * MathHelper.Pi) : MathHelper.Clamp(1f - near / 0.9f, 0.12f, 1f);
				Vector2 bp = center + 101f * new Vector2((float)Math.Cos(ba), (float)Math.Sin(ba));
				Color bulb = Color.Lerp(new Color(70, 44, 24), new Color(255, 228, 130), lit);
				Fill(sb, new Rectangle((int)bp.X - 2, (int)bp.Y - 2, 5, 5), bulb);
				if (lit > 0.6f)
					Fill(sb, new Rectangle((int)bp.X - 5, (int)bp.Y - 5, 11, 11), bulb * 0.2f);
			}

			// Pocket numbers on the outer rim, turning with the wheel (text stays upright).
			float step = MathHelper.TwoPi / RouletteRules.Pockets;
			for (int i = 0; i < RouletteRules.Pockets; i++) {
				int num = RouletteRules.WheelOrder[i];
				float a = round.WheelAngle + i * step;
				Vector2 p = center + 79f * new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
				Color nc = num == 0 ? new Color(120, 255, 140) : RouletteRules.IsRed(num) ? new Color(255, 120, 120) : Color.White;
				Utils.DrawBorderString(sb, num.ToString(), p + new Vector2(0, 2), nc, 0.4f, 0.5f, 0.5f);
			}

			if (round.Phase == RoulettePhase.ShowingResult && round.Result >= 0) {
				float a = round.WheelAngle + RouletteRules.WheelIndexOf(round.Result) * step;
				Vector2 p = center + 65f * new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
				float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f);
				Fill(sb, new Rectangle((int)p.X - 8, (int)p.Y - 8, 16, 16), Gold * (0.35f + 0.35f * pulse));
			}

			bool ballVisible = round.Phase is RoulettePhase.Spinning or RoulettePhase.ShowingResult;
			if (ballVisible) {
				// The ball rattles over the frets while it drops into the pocket band.
				float drop = round.BallDrop;
				float bounce = drop > 0.05f && drop < 0.95f ? Math.Abs((float)Math.Sin(drop * 38f)) * 4f * (1f - drop) : 0f;
				float radius = MathHelper.Lerp(74f, 65f, drop) + bounce;
				Vector2 ball = center + radius * new Vector2((float)Math.Cos(round.BallAngle), (float)Math.Sin(round.BallAngle));
				Texture2D tex = ballTexture.Value;
				// Comet tail: long while the ball is fast.
				if (round.Phase == RoulettePhase.Spinning) {
					float tail = (1f - round.Progress) * (1f - round.Progress);
					for (int k = 1; k < 14; k++) {
						float ta = round.BallAngle + k * 0.07f * (0.15f + tail);
						Color tc = new Color(255, 240, 200) * (0.35f * (1f - k / 14f));
						tc.A = 0;
						sb.Draw(tex, center + radius * new Vector2((float)Math.Cos(ta), (float)Math.Sin(ta)), null, tc, 0f, tex.Size() / 2f, 1f - k * 0.04f, SpriteEffects.None, 0f);
					}
					if (drop > 0f)
						UiFx.Shimmer(new Rectangle((int)ball.X - 6, (int)ball.Y - 6, 12, 12), new Color(255, 230, 160), 3);
				}
				sb.Draw(tex, ball + new Vector2(2, 3), null, Color.Black * 0.4f, 0f, tex.Size() / 2f, 1f, SpriteEffects.None, 0f);
				sb.Draw(tex, ball, null, Color.White, 0f, tex.Size() / 2f, 1f, SpriteEffects.None, 0f);
			}

			// Pointer above the wheel.
			for (int k = 0; k < 5; k++)
				Fill(sb, new Rectangle((int)center.X - 5 + k, (int)center.Y - 104 + k * 2, 10 - k * 2, 2), k == 0 ? Color.White : Gold);

			if (round.Phase == RoulettePhase.ShowingResult && round.Result >= 0) {
				int n = round.Result;
				Color c = n == 0 ? GreenCell : RouletteRules.IsRed(n) ? RedCell : BlackCell;
				var box = new Rectangle((int)center.X - 26, (int)center.Y - 20, 52, 40);
				Fill(sb, box, Gold);
				Fill(sb, new Rectangle(box.X + 2, box.Y + 2, box.Width - 4, box.Height - 4), c);
				Utils.DrawBorderString(sb, n.ToString(), center + new Vector2(0, 4), Color.White, 1.4f, 0.5f, 0.5f);
			}
		}

		private static void DrawHistory(SpriteBatch sb, RouletteRound round, Vector2 pos)
		{
			Utils.DrawBorderString(sb, Text("History"), pos, Color.White, 0.8f);
			for (int k = 0; k < round.History.Count; k++) {
				int n = round.History[k];
				Color c = n == 0 ? GreenCell : RouletteRules.IsRed(n) ? RedCell : BlackCell;
				var rect = new Rectangle((int)pos.X + (k % 6) * 30, (int)pos.Y + 22 + (k / 6) * 26, 26, 22);
				Fill(sb, rect, c);
				Utils.DrawBorderString(sb, n.ToString(), rect.Center.ToVector2() + new Vector2(0, 3), Color.White, 0.75f, 0.5f, 0.5f);
			}
		}

		private static void DrawCell(SpriteBatch sb, Region region, bool hover, bool won)
		{
			Rectangle r = region.Rect;
			Fill(sb, r, Color.White * 0.85f);
			Color inner = region.Color;
			if (hover)
				inner = Color.Lerp(inner, Color.White, 0.25f);
			if (won) {
				inner = Color.Lerp(inner, Gold, 0.45f + 0.35f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f));
				UiFx.Shimmer(r, new Color(255, 235, 150), 10);
			}
			Fill(sb, new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), inner);
			float scale = region.Label.Length > 3 ? 0.7f : 0.85f;
			Utils.DrawBorderString(sb, region.Label, r.Center.ToVector2() + new Vector2(0, 3), Color.White, scale, 0.5f, 0.5f);
		}

		private static void DrawStake(SpriteBatch sb, Rectangle cell, long stake)
		{
			Texture2D chip = CasinoDraw.Chip;
			Vector2 pos = new(cell.Right - 9, cell.Top + 9);
			sb.Draw(chip, pos, null, Gold, 0f, chip.Size() / 2f, 0.8f, SpriteEffects.None, 0f);
			Utils.DrawBorderString(sb, Coins.Format(stake), new Vector2(cell.Center.X, cell.Bottom - 2), Color.Yellow, 0.6f, 0.5f, 1f);
		}

		private static void DrawInfo(SpriteBatch sb, RouletteRound round, Rectangle a)
		{
			float x = a.X + BoardX;
			float y = a.Bottom - 50;
			Utils.DrawBorderString(sb, Text("Money") + " " + Coins.Format(Coins.Count(Main.LocalPlayer)), new Vector2(x, y), Color.White, 0.8f);
			Utils.DrawBorderString(sb, Text("Bet") + " " + Coins.Format(round.TotalBet) + "  /  " + Text("Max") + " " + Coins.Format(MaxBet),
				new Vector2(x + 200, y), Color.White, 0.8f);
			if (!string.IsNullOrEmpty(round.Message))
				CasinoDraw.Fit(sb, round.Message, new Vector2(x, y + 22), round.MessageColor, 0.85f, a.Right - 20 - x);
		}

		private static void Fill(SpriteBatch sb, Rectangle rect, Color color) => CasinoDraw.Fill(sb, rect, color);
	}
}
