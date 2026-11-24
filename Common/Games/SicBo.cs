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

namespace CasinoWorld.Common.Games
{
	public enum DiceBet { Small, Big, Odd, Even, One, Two, Three, Four, Five, Six, AnyTriple }

	/// <summary>
	/// Sic Bo (three dice), played at the bar. Small 4-10 / Big 11-17 and Odd / Even pay 1:1 and lose on
	/// any triple (house edge 2.8 %). A single number pays 1:1, 2:1 or 3:1 for one, two or three dice
	/// showing it (7.9 %). Any triple pays 30:1 (13.9 %). Exact over all 216 rolls.
	/// </summary>
	public static class DiceRules
	{
		/// <summary>Total returned for a stake on this bet (0 = lost), stake included.</summary>
		public static long Payout(DiceBet bet, int[] d, long stake)
		{
			int sum = d[0] + d[1] + d[2];
			bool triple = d[0] == d[1] && d[1] == d[2];
			switch (bet) {
				case DiceBet.Small: return !triple && sum <= 10 ? stake * 2 : 0;
				case DiceBet.Big: return !triple && sum >= 11 ? stake * 2 : 0;
				case DiceBet.Odd: return !triple && sum % 2 == 1 ? stake * 2 : 0;
				case DiceBet.Even: return !triple && sum % 2 == 0 ? stake * 2 : 0;
				case DiceBet.AnyTriple: return triple ? stake * 31 : 0;
				default:
					int face = bet - DiceBet.One + 1, n = 0;
					foreach (int v in d)
						if (v == face)
							n++;
					return n > 0 ? stake * (1 + n) : 0;
			}
		}
	}

	public class SicBoSystem : ModSystem
	{
		public const int RollTicks = 70, ShowTicks = 90;
		private UserInterface ui;

		public readonly Dictionary<DiceBet, long> Bets = new();
		public readonly Dictionary<DiceBet, long> LastBets = new();
		public int[] Dice = { 1, 2, 3 };
		public int Timer = -1;   // -1 idle, 0..RollTicks rolling, then showing
		public long LastWin;
		public string Message = "";
		public Color MessageColor = Color.White;
		public int Chip;

		public bool IsOpen => ui?.CurrentState != null;
		public bool Rolling => Timer >= 0 && Timer < RollTicks;
		public static long[] Chips => CasinoTableGameSystem.Casino?.ChipValues ?? new long[] { 100, 1000, 10000 };
		public static long MaxBet => CasinoTableGameSystem.Casino?.MaxBet ?? 100000;

		public long Total
		{
			get {
				long t = 0;
				foreach (long v in Bets.Values)
					t += v;
				return t;
			}
		}

		public override void Load()
		{
			if (!Main.dedServ)
				ui = new UserInterface();
		}

		public void Open()
		{
			var state = new UIState();
			var panel = new SicBoPanel();
			panel.Width.Set(SicBoPanel.PanelWidth, 0f);
			panel.Height.Set(SicBoPanel.PanelHeight, 0f);
			panel.HAlign = panel.VAlign = 0.5f;
			state.Append(panel);
			ui.SetState(state);
			Message = Language.GetTextValue("Mods.CasinoWorld.UI.Dice.Hint");
			MessageColor = Color.White;
			Main.npcChatText = "";
			Main.LocalPlayer.SetTalkNPC(-1);
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public void Close()
		{
			if (Rolling) {
				Settle();
				Timer = RollTicks;
			}
			Bets.Clear();
			ui?.SetState(null);
		}

		public void Place(DiceBet bet, bool remove)
		{
			if (Rolling)
				return;
			if (Timer >= RollTicks) {
				Timer = -1;   // a new round after the last result
			}
			long chip = Chips[Math.Clamp(Chip, 0, Chips.Length - 1)];
			Bets.TryGetValue(bet, out long cur);
			if (remove) {
				cur = Math.Max(0, cur - chip);
				if (cur == 0)
					Bets.Remove(bet);
				else
					Bets[bet] = cur;
			}
			else if (Total + chip <= MaxBet) {
				Bets[bet] = cur + chip;
				SoundEngine.PlaySound(CasinoSounds.ChipPlace);
			}
		}

		public void Roll()
		{
			if (Rolling)
				return;
			if (Bets.Count == 0 && LastBets.Count > 0)
				foreach (var kv in LastBets)
					Bets[kv.Key] = kv.Value;   // repeat the last bets
			long total = Total;
			if (total <= 0)
				return;
			if (!CasinoRewards.TryWager(Main.LocalPlayer, total)) {
				Message = Language.GetTextValue("Mods.CasinoWorld.UI.Slots.NotEnough");
				MessageColor = Color.OrangeRed;
				return;
			}
			for (int k = 0; k < 3; k++)
				Dice[k] = Main.rand.Next(1, 7);
			Timer = 0;
			Message = "";
			SoundEngine.PlaySound(CasinoSounds.DiceThrow);
		}

		private void Settle()
		{
			Player player = Main.LocalPlayer;
			long total = Total, win = 0;
			foreach (var kv in Bets)
				win += DiceRules.Payout(kv.Key, Dice, kv.Value);
			LastWin = win;
			if (win > 0) {
				Coins.Give(player, win);
				UiFx.Win(win, total);
				CombatText.NewText(player.Hitbox, Color.Gold, "+" + Coins.Format(win), win >= total * 10);
				Message = Language.GetTextValue("Mods.CasinoWorld.UI.Dice.Win", Dice[0], Dice[1], Dice[2], Coins.Format(win));
				MessageColor = Color.LightGreen;
				SoundEngine.PlaySound(win >= total * 10 ? CasinoSounds.Jackpot : CasinoSounds.SmallWin);
				CasinoRewards.TryJackpot(player, total, win >= total * 20 ? 1f : 0f);
			}
			else {
				Message = Language.GetTextValue("Mods.CasinoWorld.UI.Dice.Lose", Dice[0], Dice[1], Dice[2]);
				MessageColor = Color.LightGray;
			}
			CasinoRecords.Report(player, CasinoGame.Roulette, win - total);
			LastBets.Clear();
			foreach (var kv in Bets)
				LastBets[kv.Key] = kv.Value;
			Bets.Clear();
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (!IsOpen)
				return;
			if (Main.LocalPlayer.dead || Main.gameMenu || !Systems.CasinoTravel.InCasino) {
				Close();
				return;
			}
			ui.Update(gameTime);
			if (Timer >= 0) {
				Timer++;
				if (Rolling && Timer % 8 == 0)
					SoundEngine.PlaySound(CasinoSounds.DiceBounce with { Volume = 0.5f });
				if (Timer == RollTicks)
					Settle();
			}
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			if (index < 0 || !IsOpen)
				return;
			layers.Insert(index, new LegacyGameInterfaceLayer("CasinoWorld: Sic Bo", () => {
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

	public class SicBoPanel : UIElement
	{
		public const int PanelWidth = 600, PanelHeight = 430;

		private static SicBoSystem S => ModContent.GetInstance<SicBoSystem>();
		private static Asset<Texture2D> dieTex;

		private Rectangle Area => GetDimensions().ToRectangle();

		private IEnumerable<(Rectangle rect, DiceBet bet, string label, Color color)> Cells()
		{
			Rectangle a = Area;
			int x0 = a.X + 30, y0 = a.Y + 180, w = (a.Width - 60) / 4;
			yield return (new Rectangle(x0, y0, w - 6, 50), DiceBet.Small, Text("Small"), CasinoDraw.FeltDark);
			yield return (new Rectangle(x0 + w, y0, w - 6, 50), DiceBet.Odd, Text("Odd"), CasinoDraw.FeltDark);
			yield return (new Rectangle(x0 + 2 * w, y0, w - 6, 50), DiceBet.Even, Text("Even"), CasinoDraw.FeltDark);
			yield return (new Rectangle(x0 + 3 * w, y0, w - 6, 50), DiceBet.Big, Text("Big"), CasinoDraw.FeltDark);
			int fw = (a.Width - 60) / 6;
			for (int k = 0; k < 6; k++)
				yield return (new Rectangle(x0 + k * fw, y0 + 58, fw - 6, 50), DiceBet.One + k, (k + 1).ToString(), new Color(30, 70, 50));
			yield return (new Rectangle(x0, y0 + 116, a.Width - 66, 34), DiceBet.AnyTriple, Text("Triple"), CasinoDraw.Red);
		}

		private Rectangle ChipRect(int k) => new(Area.X + 30 + k * 64, Area.Bottom - 70, 58, 44);
		private Rectangle RollButton => new(Area.Right - 160, Area.Bottom - 72, 130, 48);
		private Rectangle CloseButton => new(Area.Right - 34, Area.Y + 12, 22, 22);

		private static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.UI.Dice." + key, args);

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
		}

		public override void LeftClick(UIMouseEvent evt) => Click(evt.MousePosition.ToPoint(), false);

		public override void RightClick(UIMouseEvent evt) => Click(evt.MousePosition.ToPoint(), true);

		private void Click(Point m, bool right)
		{
			if (CloseButton.Contains(m)) {
				S.Close();
				return;
			}
			if (RollButton.Contains(m) && !right) {
				S.Roll();
				return;
			}
			for (int k = 0; k < SicBoSystem.Chips.Length; k++)
				if (ChipRect(k).Contains(m)) {
					S.Chip = k;
					SoundEngine.PlaySound(SoundID.MenuTick);
					return;
				}
			foreach (var c in Cells())
				if (c.rect.Contains(m)) {
					S.Place(c.bet, right);
					return;
				}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			dieTex ??= ModContent.Request<Texture2D>("CasinoWorld/Content/Projectiles/Boss/WildDie");
			Rectangle a = Area;
			Point m = Main.MouseScreen.ToPoint();
			float t = Main.GlobalTimeWrappedHourly;
			CasinoDraw.Panel(sb, a);
			Utils.DrawBorderString(sb, Text("Title"), new Vector2(a.X + 20, a.Y + 14), CasinoDraw.Gold, 1.1f);
			CasinoDraw.Fit(sb, Text("Rules"), new Vector2(a.Center.X, a.Y + 48), Color.LightGray, 0.65f, a.Width - 40, 0.5f, 0.5f);

			// The dice: tumbling while rolling, then the result.
			Texture2D die = dieTex.Value;
			for (int k = 0; k < 3; k++) {
				int face = S.Rolling ? (int)(t * 20 + k * 2) % 6 + 1 : S.Dice[k];
				float hop = S.Rolling ? -Math.Abs((float)Math.Sin((S.Timer + k * 9) * 0.25f)) * 22f * (1f - S.Timer / (float)SicBoSystem.RollTicks) : 0f;
				float rot = S.Rolling ? (S.Timer + k * 7) * 0.3f * (1f - S.Timer / (float)SicBoSystem.RollTicks) : 0f;
				Vector2 pos = new(a.Center.X + (k - 1) * 90, a.Y + 115 + hop);
				sb.Draw(die, pos + new Vector2(4, 6), new Rectangle((face - 1) * 32, 0, 32, 32), Color.Black * 0.35f, rot, new Vector2(16), 2f, SpriteEffects.None, 0f);
				sb.Draw(die, pos, new Rectangle((face - 1) * 32, 0, 32, 32), Color.White, rot, new Vector2(16), 2f, SpriteEffects.None, 0f);
			}
			if (!S.Rolling && S.Timer >= SicBoSystem.RollTicks)
				Utils.DrawBorderString(sb, (S.Dice[0] + S.Dice[1] + S.Dice[2]).ToString(), new Vector2(a.Center.X + 170, a.Y + 115), CasinoDraw.Gold, 1.4f, 0.5f, 0.5f);

			bool showing = !S.Rolling && S.Timer >= SicBoSystem.RollTicks;
			foreach (var c in Cells()) {
				bool hover = c.rect.Contains(m);
				bool won = showing && DiceRules.Payout(c.bet, S.Dice, 1) > 0;
				CasinoDraw.Fill(sb, c.rect, Color.White * 0.8f);
				Color inner = won ? Color.Lerp(c.color, CasinoDraw.Gold, 0.5f + 0.2f * (float)Math.Sin(t * 10)) : hover ? Color.Lerp(c.color, Color.White, 0.2f) : c.color;
				CasinoDraw.Fill(sb, CasinoDraw.Shrink(c.rect, 1), inner);
				if (c.bet >= DiceBet.One && c.bet <= DiceBet.Six)
					sb.Draw(die, c.rect.Center.ToVector2() + new Vector2(0, -4), new Rectangle((c.bet - DiceBet.One) * 32, 0, 32, 32), Color.White, 0f, new Vector2(16), 0.9f, SpriteEffects.None, 0f);
				else
					CasinoDraw.Fit(sb, c.label, c.rect.Center.ToVector2() + new Vector2(0, 3), Color.White, 0.8f, c.rect.Width - 8, 0.5f, 0.5f);
				if (S.Bets.TryGetValue(c.bet, out long stake))
					Utils.DrawBorderString(sb, Coins.Format(stake), new Vector2(c.rect.Right - 4, c.rect.Bottom - 2), Color.Yellow, 0.6f, 1f, 1f);
			}

			long[] chips = SicBoSystem.Chips;
			for (int k = 0; k < chips.Length; k++)
				CasinoDraw.ChipButton(sb, ChipRect(k), Coins.Format(chips[k]), k, ChipRect(k).Contains(m), k == S.Chip);
			string rollLabel = S.Bets.Count == 0 && S.LastBets.Count > 0 ? Text("Repeat") : Text("Roll");
			CasinoDraw.Button(sb, RollButton, rollLabel, CasinoDraw.ButtonGold, RollButton.Contains(m), !S.Rolling, 1f);
			CasinoDraw.Button(sb, CloseButton, "X", CasinoDraw.Red, CloseButton.Contains(m), true, 0.8f);
			Utils.DrawBorderString(sb, Text("Bet", Coins.Format(S.Total), Coins.Format(SicBoSystem.MaxBet)), new Vector2(a.X + 30 + chips.Length * 64 + 10, a.Bottom - 64), Color.White, 0.75f);
			CasinoDraw.Fit(sb, S.Message, new Vector2(a.X + 30 + chips.Length * 64 + 10, a.Bottom - 40), S.MessageColor, 0.75f, RollButton.X - (a.X + 40 + chips.Length * 64));
		}
	}
}
