using System;
using System.Collections.Generic;
using CasinoWorld.Common.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Games
{
	/// <summary>
	/// Scratch card rules: nine hidden symbols, three of a kind wins that symbol's prize (x card price).
	/// The outcome is rolled first, then the grid is filled so no other symbol shows three times.
	/// Win 36.1 % of cards, return 86 % of the price (lottery tickets pay less than the tables).
	/// </summary>
	public static class ScratchRules
	{
		public static readonly int[] Symbols = { ItemID.Cherry, ItemID.Bell, ItemID.Emerald, ItemID.LuckyCoin, ItemID.LargeDiamond };
		public static readonly int[] Multipliers = { 1, 2, 5, 20, 100 };
		// Chance per card of each prize; the rest (63.9 %) is a loss.  EV = .20 + .20 + .20 + .16 + .10 = 0.86
		private static readonly double[] Chances = { 0.20, 0.10, 0.04, 0.008, 0.001 };

		/// <summary>Returns the nine symbols (indices into Symbols) and the winning symbol or -1.</summary>
		public static (int[] grid, int winner) Roll(Random rng)
		{
			double roll = rng.NextDouble();
			int winner = -1;
			for (int k = 0; k < Chances.Length; k++) {
				if (roll < Chances[k]) {
					winner = k;
					break;
				}
				roll -= Chances[k];
			}
			var grid = new int[9];
			var counts = new int[Symbols.Length];
			var cells = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
			if (winner >= 0) {
				for (int k = 0; k < 3; k++) {
					int c = cells[rng.Next(cells.Count)];
					cells.Remove(c);
					grid[c] = winner;
				}
				counts[winner] = 3;
			}
			foreach (int c in cells) {
				int s;
				do
					s = rng.Next(Symbols.Length);
				while (s == winner || counts[s] >= 2);
				counts[s]++;
				grid[c] = s;
			}
			return (grid, winner);
		}
	}

	/// <summary>The scratch card window: hold the mouse button over a cell to scratch its foil off.</summary>
	public class ScratchCardSystem : ModSystem
	{
		private UserInterface ui;
		private static ScratchCardSystem Instance => ModContent.GetInstance<ScratchCardSystem>();

		public long Price;
		public int[] Grid;
		public int Winner;
		public float[] Scratched = new float[9];
		public bool Paid;
		public int RevealTimer;

		public bool IsOpen => ui?.CurrentState != null;

		public override void Load()
		{
			if (!Main.dedServ)
				ui = new UserInterface();
		}

		public static void Open(long price)
		{
			ScratchCardSystem s = Instance;
			if (s.IsOpen)
				s.Close();
			(s.Grid, s.Winner) = ScratchRules.Roll(new Random(Main.rand.Next()));
			s.Price = price;
			s.Scratched = new float[9];
			s.Paid = false;
			s.RevealTimer = 0;
			var state = new UIState();
			var panel = new ScratchCardPanel();
			panel.Width.Set(ScratchCardPanel.PanelWidth, 0f);
			panel.Height.Set(ScratchCardPanel.PanelHeight, 0f);
			panel.HAlign = panel.VAlign = 0.5f;
			state.Append(panel);
			s.ui.SetState(state);
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		/// <summary>Pays the card (once), revealing everything that is still covered.</summary>
		public void Settle()
		{
			if (Paid || Grid == null)
				return;
			Paid = true;
			for (int k = 0; k < 9; k++)
				Scratched[k] = 1f;
			Player player = Main.LocalPlayer;
			if (Winner >= 0) {
				long prize = Price * ScratchRules.Multipliers[Winner];
				Coins.Give(player, prize);
				CombatText.NewText(player.Hitbox, Color.Gold, "+" + Coins.Format(prize), Winner >= 3);
				SoundEngine.PlaySound(Winner >= 3 ? CasinoSounds.Jackpot : CasinoSounds.SmallWin);
				CasinoRecords.Report(player, CasinoGame.Slots, prize - Price);
				UiFx.Win(prize, Price);
			}
			else {
				SoundEngine.PlaySound(CasinoSounds.BustProc);
				CasinoRecords.Report(player, CasinoGame.Slots, -Price);
			}
		}

		public void Close()
		{
			Settle();
			ui?.SetState(null);
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (!IsOpen)
				return;
			if (Main.LocalPlayer.dead || Main.gameMenu) {
				Close();
				return;
			}
			ui.Update(gameTime);
			if (!Paid && Array.TrueForAll(Scratched, v => v >= 1f))
				Settle();
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			if (index < 0 || !IsOpen)
				return;
			layers.Insert(index, new LegacyGameInterfaceLayer("CasinoWorld: Scratch Card", () => {
				ui.Draw(Main.spriteBatch, new GameTime());
				return true;
			}, InterfaceScaleType.UI));
		}

		// Also when the server drops the connection: a round must not carry over to another character.
		public override void OnWorldUnload()
		{
			if (IsOpen)
				Close();
		}

		public override void PreSaveAndQuit()
		{
			if (IsOpen)
				Close();
		}
	}

	public class ScratchCardPanel : UIElement
	{
		public const int PanelWidth = 330, PanelHeight = 420;
		private const int Cell = 82;

		private static ScratchCardSystem S => ModContent.GetInstance<ScratchCardSystem>();

		private Rectangle Area => GetDimensions().ToRectangle();

		private Rectangle CellRect(int k) => new(Area.X + 24 + k % 3 * (Cell + 9), Area.Y + 70 + k / 3 * (Cell + 9), Cell, Cell);

		private Rectangle CloseButton => new(Area.Right - 34, Area.Y + 12, 22, 22);

		private Rectangle RevealButton => new(Area.Center.X - 70, Area.Bottom - 56, 140, 36);

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
			if (!Main.mouseLeft || S.Paid)
				return;
			Point mouse = Main.MouseScreen.ToPoint();
			for (int k = 0; k < 9; k++) {
				if (!CellRect(k).Contains(mouse) || S.Scratched[k] >= 1f)
					continue;
				S.Scratched[k] = Math.Min(1f, S.Scratched[k] + 0.06f);
				if (Main.rand.NextBool(4))
					SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.3f, Pitch = 0.6f });
			}
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			Point mouse = evt.MousePosition.ToPoint();
			if (CloseButton.Contains(mouse) || S.Paid && RevealButton.Contains(mouse))
				S.Close();
			else if (RevealButton.Contains(mouse))
				S.Settle();
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			Rectangle a = Area;
			float t = Main.GlobalTimeWrappedHourly;
			CasinoDraw.Panel(sb, a);
			Utils.DrawBorderString(sb, Language.GetTextValue("Mods.CasinoWorld.UI.Scratch.Title"), new Vector2(a.Center.X, a.Y + 26), CasinoDraw.Gold, 1.1f, 0.5f, 0.5f);
			CasinoDraw.Fit(sb, Language.GetTextValue("Mods.CasinoWorld.UI.Scratch.Rule", Coins.Format(S.Price)), new Vector2(a.Center.X, a.Y + 48), Color.LightGray, 0.7f, a.Width - 40, 0.5f, 0.5f);
			bool won = S.Paid && S.Winner >= 0;
			for (int k = 0; k < 9; k++) {
				Rectangle r = CellRect(k);
				CasinoDraw.Fill(sb, r, Color.Black * 0.5f);
				CasinoDraw.Fill(sb, CasinoDraw.Shrink(r, 2), new Color(250, 245, 230));
				bool highlight = won && S.Grid[k] == S.Winner;
				if (highlight)
					CasinoDraw.Fill(sb, CasinoDraw.Shrink(r, 2), Color.Gold * (0.4f + 0.2f * (float)Math.Sin(t * 10)));
				Videoslots_DrawItem(sb, ScratchRules.Symbols[S.Grid[k]], r.Center.ToVector2() + new Vector2(0, -6), 44f);
				Utils.DrawBorderString(sb, "x" + ScratchRules.Multipliers[S.Grid[k]], new Vector2(r.Center.X, r.Bottom - 12), Color.DarkGoldenrod, 0.6f, 0.5f, 0.5f);
				// Silver foil, scratched off as a grid of flakes in a fixed order.
				float left = 1f - S.Scratched[k];
				if (left > 0f) {
					int n = 6;
					for (int fy = 0; fy < n; fy++) {
						for (int fx = 0; fx < n; fx++) {
							int order = (fx * 7 + fy * 11 + k * 3) % (n * n);
							if (order >= left * n * n)
								continue;
							var flake = new Rectangle(r.X + 2 + fx * (r.Width - 4) / n, r.Y + 2 + fy * (r.Height - 4) / n, (r.Width - 4) / n + 1, (r.Height - 4) / n + 1);
							Color foil = (fx + fy) % 2 == 0 ? new Color(178, 180, 190) : new Color(160, 162, 174);
							CasinoDraw.Fill(sb, flake, foil);
						}
					}
					if (left > 0.95f)
						Utils.DrawBorderString(sb, "?", r.Center.ToVector2() + new Vector2(0, 4), Color.White, 1.4f, 0.5f, 0.5f);
				}
			}
			string label = S.Paid ? Language.GetTextValue("Mods.CasinoWorld.UI.Scratch.Close") : Language.GetTextValue("Mods.CasinoWorld.UI.Scratch.RevealAll");
			Point mouse = Main.MouseScreen.ToPoint();
			CasinoDraw.Button(sb, RevealButton, label, CasinoDraw.ButtonGold, RevealButton.Contains(mouse), true, 0.9f);
			CasinoDraw.Button(sb, CloseButton, "X", CasinoDraw.Red, CloseButton.Contains(mouse), true, 0.8f);
			if (S.Paid) {
				string result = won ? Language.GetTextValue("Mods.CasinoWorld.UI.Scratch.Won", Coins.Format(S.Price * ScratchRules.Multipliers[S.Winner])) : Language.GetTextValue("Mods.CasinoWorld.UI.Scratch.Lost");
				Utils.DrawBorderString(sb, result, new Vector2(a.Center.X, a.Bottom - 76), won ? Color.Gold : Color.LightGray, 0.9f, 0.5f, 0.5f);
			}
		}

		private static void Videoslots_DrawItem(SpriteBatch sb, int type, Vector2 center, float size)
			=> VideoSlots.VideoSlotPanel.DrawIcon(sb, type, center, size, Color.White);
	}
}
