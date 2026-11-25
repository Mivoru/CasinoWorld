using System;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Gambler;
using CasinoWorld.Common.Games;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasinoWorld.Content.NPCs.Boss;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Club
{
	/// <summary>The Ledger's contract and Fate pages: drawing and clicks share the same rectangles.</summary>
	public static class LedgerPages
	{
		private static int selected = -1, choice;

		// ------------------------------------------------------------------ contracts

		private static ContractPlayer Mine => Main.LocalPlayer.GetModPlayer<ContractPlayer>();

		private static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.Contracts." + key, args);

		private static Rectangle Row(Rectangle page, int k) => new(page.X, page.Y + k * 35, 250, 32);

		private static Rectangle Detail(Rectangle page) => new(page.X + 260, page.Y, page.Width - 260, page.Height + 44);

		private static Rectangle Reward(Rectangle d, int k) => new(d.X + 16 + k * 66, d.Bottom - 124, 58, 58);

		private static Rectangle Action(Rectangle d) => new(d.X + 16, d.Bottom - 52, 200, 40);

		private static int Selected
		{
			get {
				if (selected < 0) {
					selected = Array.FindIndex(ContractBook.All, c => c.Key == Mine.Active);
					if (selected < 0)
						selected = Math.Max(0, Array.FindIndex(ContractBook.All, c => c.Open() && !Mine.Done.Contains(c.Key)));
				}
				return selected;
			}
		}

		public static void Reset() => selected = -1;

		public static void ClickContracts(Rectangle page, Point m, bool atConcierge)
		{
			for (int k = 0; k < ContractBook.All.Length; k++)
				if (Row(page, k).Contains(m)) {
					selected = k;
					choice = 0;
					SoundEngine.PlaySound(SoundID.MenuTick);
					return;
				}
			Contract c = ContractBook.All[Selected];
			Rectangle d = Detail(page);
			int rewards = c.Rewards().Length;
			for (int k = 0; k < rewards; k++)
				if (Reward(d, k).Contains(m)) {
					choice = k;
					SoundEngine.PlaySound(SoundID.MenuTick);
					return;
				}
			if (!Action(d).Contains(m))
				return;
			if (Mine.Active == c.Key) {
				if (!Mine.Complete)
					Mine.Abandon();
				else if (atConcierge)
					Mine.Claim(choice);
			}
			else if (atConcierge)
				Mine.Accept(c);
		}

		public static void DrawContracts(SpriteBatch sb, Rectangle page, Point m, bool atConcierge)
		{
			ContractPlayer mine = Mine;
			for (int k = 0; k < ContractBook.All.Length; k++) {
				Contract c = ContractBook.All[k];
				Rectangle r = Row(page, k);
				bool open = c.Open(), done = mine.Done.Contains(c.Key), active = mine.Active == c.Key;
				CasinoDraw.Fill(sb, r, k == Selected ? CasinoDraw.Gold : active ? new Color(120, 200, 255) : new Color(60, 60, 60));
				CasinoDraw.Fill(sb, CasinoDraw.Shrink(r, 2), r.Contains(m) ? new Color(30, 60, 40) : new Color(14, 34, 22));
				CasinoDraw.Fit(sb, c.Name, new Vector2(r.X + 12, r.Center.Y + 3), !open ? Color.Gray : done ? CasinoDraw.Gold : Color.White, 0.8f, 150, 0f, 0.5f);
				string state = active ? Text(mine.Complete ? "Ready" : "Running") : done ? Text("DoneMark") : open ? "" : Text("LockedMark");
				CasinoDraw.Fit(sb, state, new Vector2(r.Right - 10, r.Center.Y + 3), active ? new Color(120, 200, 255) : done ? Color.LightGreen : Color.Gray, 0.68f, 74, 1f, 0.5f);
			}

			Contract contract = ContractBook.All[Selected];
			Rectangle d = Detail(page);
			CasinoDraw.Fill(sb, d, CasinoDraw.Gold * 0.6f);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(d, 2), new Color(12, 40, 24));
			CasinoDraw.Fit(sb, contract.Name, new Vector2(d.X + 16, d.Y + 12), CasinoDraw.Gold, 1.1f, d.Width - 32);
			bool isOpen = contract.Open(), isActive = mine.Active == contract.Key, isDone = mine.Done.Contains(contract.Key);
			float y = d.Y + 46;
			y += CasinoDraw.Wrapped(sb, isOpen ? contract.Text : contract.Locked, new Vector2(d.X + 16, y), isOpen ? Color.White : Color.Gray, 0.8f, d.Width - 32) + 14;
			for (int k = 0; k < contract.Steps.Length; k++) {
				bool past = isActive && k < mine.Step, now = isActive && k == mine.Step;
				Color color = past ? Color.LightGreen : now ? Color.White : Color.LightGray;
				CasinoDraw.Fill(sb, new Rectangle(d.X + 16, (int)y + 2, 14, 14), past ? Color.LightGreen : now ? CasinoDraw.Gold : new Color(80, 80, 80));
				CasinoDraw.Fill(sb, new Rectangle(d.X + 18, (int)y + 4, 10, 10), past ? new Color(40, 120, 60) : new Color(14, 34, 22));
				string progress = "";
				if (now)
					progress = contract.Steps[k].Money ? "  " + Coins.Format(mine.Progress) : $"  {mine.Progress} / {contract.Goal(k)}";
				y += Math.Max(22f, CasinoDraw.Wrapped(sb, contract.StepText(k) + progress, new Vector2(d.X + 40, y), color, 0.78f, d.Width - 56) + 6);
			}

			(int type, int stack)[] rewards = contract.Rewards();
			string pay = isDone ? Text("PayAgain", contract.Tokens / 2) : rewards.Length > 1 ? Text("PayChoice", contract.Tokens) : Text("Pay", contract.Tokens);
			CasinoDraw.Fit(sb, pay, new Vector2(d.X + 16, d.Bottom - 148), Color.Wheat, 0.8f, d.Width - 32);
			choice = Math.Clamp(choice, 0, rewards.Length - 1);
			for (int k = 0; k < rewards.Length; k++) {
				Rectangle r = Reward(d, k);
				bool picked = k == choice && !isDone;
				CasinoDraw.Fill(sb, r, picked ? CasinoDraw.Gold : new Color(70, 70, 70));
				CasinoDraw.Fill(sb, CasinoDraw.Shrink(r, 2), picked ? new Color(60, 46, 14) : new Color(20, 24, 22));
				LedgerPanel.DrawItem(sb, rewards[k].type, r.Center.ToVector2(), 40f, isDone ? new Color(90, 90, 90) : Color.White);
				if (rewards[k].stack > 1)
					Utils.DrawBorderString(sb, rewards[k].stack.ToString(), new Vector2(r.Right - 6, r.Bottom - 8), Color.White, 0.75f, 1f, 0.5f);
				if (r.Contains(m)) {
					Main.HoverItem = new Item(rewards[k].type, rewards[k].stack);
					Main.hoverItemName = Main.HoverItem.Name;
				}
			}

			Rectangle button = Action(d);
			bool hover = button.Contains(m);
			if (isActive && mine.Complete)
				CasinoDraw.Button(sb, button, Text("Claim"), CasinoDraw.ButtonGold, hover, atConcierge, 0.9f);
			else if (isActive)
				CasinoDraw.Button(sb, button, Text("Abandon"), CasinoDraw.Red, hover, true, 0.9f);
			else
				CasinoDraw.Button(sb, button, Text("Accept"), CasinoDraw.ButtonGold, hover, atConcierge && isOpen && mine.Active == "", 0.9f);
			if (!atConcierge && (!isActive || mine.Complete))
				CasinoDraw.Wrapped(sb, Text("AtConcierge"), new Vector2(button.Right + 16, button.Y + 2), Color.LightGray, 0.72f, d.Right - button.Right - 30);
			else if (!isActive && mine.Active != "")
				CasinoDraw.Wrapped(sb, Text("OneAtATime"), new Vector2(button.Right + 16, button.Y + 2), Color.LightGray, 0.72f, d.Right - button.Right - 30);
		}

		// ------------------------------------------------------------------ fates

		private static readonly Fate[] fates = { Fate.Control, Fate.Dealer, Fate.Fool };
		private static string fateMessage = "";

		private static Rectangle Card(Rectangle page, int k) => new(page.X + k * (page.Width / 3 + 4), page.Y + 4, page.Width / 3 - 8, page.Height - 4);

		public static void ClickFates(Rectangle page, Point m)
		{
			for (int k = 0; k < fates.Length; k++)
				if (Card(page, k).Contains(m))
					fateMessage = Main.LocalPlayer.GetModPlayer<FatePlayer>().Choose(fates[k]) ?? "";
		}

		public static void DrawFates(SpriteBatch sb, Rectangle page, Point m)
		{
			FatePlayer mine = Main.LocalPlayer.GetModPlayer<FatePlayer>();
			bool open = CasinoDowned.DownedFortuna || mine.Owned.Count > 0;
			float t = Main.GlobalTimeWrappedHourly;
			for (int k = 0; k < fates.Length; k++) {
				Fate fate = fates[k];
				Rectangle r = Card(page, k);
				bool owned = mine.Owned.Contains(fate), active = mine.Active == fate;
				Color c = FatePlayer.ColorOf(fate);
				CasinoDraw.Fill(sb, r, active ? c : owned || r.Contains(m) && open ? Color.Lerp(c, Color.Gray, 0.5f) : new Color(60, 60, 60));
				CasinoDraw.Fill(sb, CasinoDraw.Shrink(r, 3), Color.Lerp(new Color(16, 14, 26), c, active ? 0.2f : 0.05f));
				var icon = new Vector2(r.Center.X, r.Y + 62);
				if (active) {
					PixelFx.Glow(sb, icon, 72f, c * (0.45f + 0.12f * (float)Math.Sin(t * 3f)));
					PixelFx.Burst(sb, icon, c * 0.35f, t * 0.4f, 2);
				}
				DrawSymbol(sb, fate, icon, owned ? c : open ? Color.Lerp(c, Color.Gray, 0.55f) : new Color(60, 60, 64));
				Utils.DrawBorderString(sb, FatePlayer.Text(fate + ".Name"), new Vector2(r.Center.X, r.Y + 124), open ? c : Color.Gray, 0.95f, 0.5f, 0.5f);
				float y = r.Y + 146;
				y += CasinoDraw.Wrapped(sb, FatePlayer.Text(fate + ".Gain"), new Vector2(r.X + 14, y), open ? Color.LightGreen : Color.Gray, 0.76f, r.Width - 28) + 10;
				CasinoDraw.Wrapped(sb, FatePlayer.Text(fate + ".Loss"), new Vector2(r.X + 14, y), open ? new Color(255, 150, 140) : Color.Gray, 0.76f, r.Width - 28);
				string state = !open ? FatePlayer.Text("LockedMark") : active ? FatePlayer.Text("Active") : owned ? FatePlayer.Text("Wear") : FatePlayer.Text("Buy", FatePlayer.Price);
				CasinoDraw.Fit(sb, state, new Vector2(r.Center.X, r.Bottom - 22), active ? c : owned ? Color.White : CasinoDraw.Gold, 0.85f, r.Width - 24, 0.5f, 0.5f);
			}
			int shards = Main.LocalPlayer.CountItem(ModContent.ItemType<Content.Items.Fortuna.FateShard>());
			string line = fateMessage != "" ? fateMessage : open ? FatePlayer.Text("Hint", shards) : FatePlayer.Text("Locked");
			CasinoDraw.Fit(sb, line, new Vector2(page.Center.X, page.Bottom + 24), Color.LightGray, 0.85f, page.Width, 0.5f, 0.5f);
		}

		/// <summary>Pixel symbols: scales for Control, a fan of cards for the Dealer, a split mask for the Fool.</summary>
		private static void DrawSymbol(SpriteBatch sb, Fate fate, Vector2 at, Color color)
		{
			Color dark = Color.Lerp(color, Color.Black, 0.55f), light = Color.Lerp(color, Color.White, 0.5f);
			string[] rows = fate switch {
				Fate.Control => new[] { "......#......", "......#......", "..#########..", "..#...#...#..", ".#.#..#..#.#.", "#...#.#.#...#", "ooooo.#.ooooo", ".ooo..#..ooo.", "......#......", "......#......", "....#####....", "...#######..." },
				Fate.Dealer => new[] { "....#####....", "....#ooo#....", ".####o*o####.", ".#oo#ooo#oo#.", ".#o*#o*o#*o#.", ".#oo#ooo#oo#.", ".#o*#o*o#*o#.", ".#oo#ooo#oo#.", ".####o*o####.", "....#ooo#....", "....#####....", "............." },
				_ => new[] { "##.........##", "#o##.....##*#", ".#oo##.##**#.", ".#ooo###***#.", "..#oooo****#.", "..#o##o*##*#.", "..#o##o*##*#.", "..#oooo****#.", "..#oo#o*#**#.", "...#oo#####..", "....#oo**#...", ".....####...." },
			};
			const int P = 6;
			var corner = new Vector2((int)(at.X - rows[0].Length * P / 2f), (int)(at.Y - rows.Length * P / 2f));
			for (int y = 0; y < rows.Length; y++)
				for (int x = 0; x < rows[y].Length; x++) {
					char ch = rows[y][x];
					if (ch != '.')
						CasinoDraw.Fill(sb, new Rectangle((int)corner.X + x * P, (int)corner.Y + y * P, P, P), ch == '#' ? color : ch == 'o' ? light : dark);
				}
		}
	}
}
