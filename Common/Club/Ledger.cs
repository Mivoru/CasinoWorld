using System;
using System.Collections.Generic;
using CasinoWorld.Common.Crates;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Games;
using CasinoWorld.Common.Games.Poker;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Club
{
	public enum LedgerTab { Stats, Achievements, Club }

	/// <summary>The Gambler's Ledger window: statistics, achievements and the VIP club.</summary>
	public class LedgerSystem : ModSystem
	{
		private UserInterface ui;
		public LedgerTab Tab;
		public int Page;
		/// <summary>Opened by the Concierge: the club page can sell the next level.</summary>
		public bool AtConcierge;

		public bool IsOpen => ui?.CurrentState != null;

		public Point Mouse => (ui?.MousePosition ?? Main.MouseScreen).ToPoint();

		public override void Load()
		{
			if (!Main.dedServ)
				ui = new UserInterface();
		}

		public void Open(LedgerTab tab, bool atConcierge = false)
		{
			if (Main.dedServ)
				return;
			var state = new UIState();
			var panel = new LedgerPanel();
			panel.Width.Set(LedgerPanel.PanelWidth, 0f);
			panel.Height.Set(LedgerPanel.PanelHeight, 0f);
			panel.HAlign = panel.VAlign = 0.5f;
			state.Append(panel);
			ui.SetState(state);
			Tab = tab;
			AtConcierge = atConcierge;
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public void Close()
		{
			if (!IsOpen)
				return;
			ui.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public override void OnWorldUnload() => ui?.SetState(null);

		public override void UpdateUI(GameTime gameTime)
		{
			if (!IsOpen)
				return;
			if (Main.LocalPlayer.dead || Main.gameMenu || Terraria.GameInput.PlayerInput.Triggers.JustPressed.Inventory) {
				Close();
				return;
			}
			ui.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			if (index < 0 || !IsOpen)
				return;
			layers.Insert(index, new LegacyGameInterfaceLayer("CasinoWorld: Ledger", () => {
				ui.Draw(Main.spriteBatch, new GameTime());
				return true;
			}, InterfaceScaleType.UI));
		}
	}

	public class LedgerPanel : UIElement
	{
		public const int PanelWidth = 780, PanelHeight = 540;
		private const int PerPage = 10;

		private static LedgerSystem S => ModContent.GetInstance<LedgerSystem>();

		private static int Pages => (Achievements.All.Length + PerPage - 1) / PerPage;

		private Rectangle Area => GetDimensions().ToRectangle();
		private Rectangle CloseButton => new(Area.Right - 34, Area.Y + 12, 22, 22);
		private Rectangle TabRect(int k) => new(Area.X + 24 + k * 170, Area.Y + 52, 162, 34);
		private Rectangle Prev => new(Area.X + 24, Area.Bottom - 52, 90, 34);
		private Rectangle Next => new(Area.Right - 114, Area.Bottom - 52, 90, 34);
		private Rectangle Buy => new(Area.Center.X - 160, Area.Bottom - 60, 320, 42);

		private static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.UI.Ledger." + key, args);

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			Point m = evt.MousePosition.ToPoint();
			if (CloseButton.Contains(m)) {
				S.Close();
				return;
			}
			for (int k = 0; k < 3; k++)
				if (TabRect(k).Contains(m)) {
					S.Tab = (LedgerTab)k;
					SoundEngine.PlaySound(CasinoSounds.CardFlip with { Volume = 0.6f });
					return;
				}
			if (S.Tab == LedgerTab.Achievements && (Prev.Contains(m) && S.Page > 0 || Next.Contains(m) && S.Page < Pages - 1)) {
				S.Page += Next.Contains(m) ? 1 : -1;
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
			else if (S.Tab == LedgerTab.Club && S.AtConcierge && Buy.Contains(m) && VipClub.LocalLevel < VipClub.MaxLevel)
				Main.NewText(VipClub.BuyNext(Main.LocalPlayer), CasinoDraw.Gold);
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			Rectangle a = Area;
			Point m = S.Mouse;
			CasinoStats stats = Main.LocalPlayer.GetModPlayer<CasinoStats>();
			CasinoDraw.Panel(sb, a);
			Utils.DrawBorderString(sb, Text("Title", Main.LocalPlayer.name), new Vector2(a.X + 24, a.Y + 18), CasinoDraw.Gold, 1.1f);
			for (int k = 0; k < 3; k++)
				CasinoDraw.Button(sb, TabRect(k), Text("Tab" + (LedgerTab)k), (int)S.Tab == k ? CasinoDraw.ButtonGold : CasinoDraw.FeltDark, TabRect(k).Contains(m), true, 0.9f);
			CasinoDraw.Button(sb, CloseButton, "X", CasinoDraw.Red, CloseButton.Contains(m), true, 0.8f);
			var page = new Rectangle(a.X + 24, a.Y + 96, a.Width - 48, a.Height - 160);
			switch (S.Tab) {
				case LedgerTab.Stats: DrawStats(sb, page, stats); break;
				case LedgerTab.Achievements: DrawAchievements(sb, page, stats, m); break;
				case LedgerTab.Club: DrawClub(sb, page, m); break;
			}
		}

		// ------------------------------------------------------------------ statistics

		private static void Sheet(SpriteBatch sb, Rectangle r)
		{
			CasinoDraw.Fill(sb, r, CasinoDraw.Gold * 0.5f);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(r, 2), new Color(12, 40, 24));
		}

		private static string Signed(long net) => net < 0 ? "-" + Coins.Format(-net) : Coins.Format(net);

		private static void DrawStats(SpriteBatch sb, Rectangle page, CasinoStats s)
		{
			var left = new Rectangle(page.X, page.Y, page.Width / 2 - 6, page.Height + 40);
			var right = new Rectangle(page.Center.X + 6, page.Y, page.Width / 2 - 6, page.Height + 40);
			Sheet(sb, left);
			Sheet(sb, right);
			long net = s.WonTotal - s.LostTotal;
			var lines = new (string key, string value, Color color)[] {
				("Rounds", s.Rounds.ToString("N0"), Color.White),
				("Wagered", Coins.Format(s.Wagered), Color.Wheat),
				("Net", Signed(net), net >= 0 ? Color.LightGreen : new Color(255, 140, 130)),
				("BiggestWin", Coins.Format(s.BiggestWin), CasinoDraw.Gold),
				("BiggestStake", Coins.Format(s.BiggestStake), Color.White),
				("BestStreak", s.BestStreak.ToString(), Color.White),
				("Crates", s.Crates.ToString("N0"), Color.White),
				("Legendary", s.CrateRarities[4].ToString(), CrateLoot.RarityColors[4]),
				("JackpotHits", s.JackpotHits.ToString("N0"), CasinoDraw.Gold),
				("Triples", s.TripleJackpots.ToString("N0"), Color.White),
				("AllIns", s.AllIns.ToString("N0"), Color.White),
				("Challenges", s.Challenges.ToString("N0"), Color.White),
				("PokerBest", s.BestPokerHand < 0 ? "-" : PokerSystem.Text("Hand" + s.BestPokerHand), Color.White),
				("Bosses", $"{s.WildCardKills} / {s.PitBossKills} / {s.FortunaKills}", Color.White),
			};
			Utils.DrawBorderString(sb, Text("Life"), new Vector2(left.X + 14, left.Y + 10), CasinoDraw.Gold, 0.95f);
			for (int k = 0; k < lines.Length; k++) {
				int y = left.Y + 40 + k * 26;
				if (k % 2 == 0)
					CasinoDraw.Fill(sb, new Rectangle(left.X + 6, y - 4, left.Width - 12, 25), Color.White * 0.04f);
				CasinoDraw.Fit(sb, Text(lines[k].key), new Vector2(left.X + 14, y), Color.LightGray, 0.8f, 180);
				CasinoDraw.Fit(sb, lines[k].value, new Vector2(left.Right - 14, y), lines[k].color, 0.8f, 160, 1f);
			}

			Utils.DrawBorderString(sb, Text("ByGame"), new Vector2(right.X + 14, right.Y + 10), CasinoDraw.Gold, 0.95f);
			long worst = 1;
			foreach (long v in s.GameNet)
				worst = Math.Max(worst, Math.Abs(v));
			for (int g = 0; g < CasinoStats.Games; g++) {
				int y = right.Y + 44 + g * 58;
				CasinoDraw.Fit(sb, CasinoRecords.GameName((byte)g), new Vector2(right.X + 14, y), Color.White, 0.85f, 130);
				string rounds = Text("RoundsWon", s.GameRounds[g].ToString("N0"), s.GameRounds[g] == 0 ? 0 : 100 * s.GameWins[g] / s.GameRounds[g]);
				CasinoDraw.Fit(sb, rounds, new Vector2(right.Right - 14, y), Color.LightGray, 0.72f, 190, 1f);
				// Profit to the right of the middle line, loss to the left.
				var bar = new Rectangle(right.X + 14, y + 22, right.Width - 28, 14);
				CasinoDraw.Fill(sb, bar, Color.Black * 0.4f);
				int half = bar.Width / 2, w = (int)(half * Math.Abs(s.GameNet[g]) / (double)worst) / 2 * 2;
				bool up = s.GameNet[g] >= 0;
				CasinoDraw.Fill(sb, new Rectangle(up ? bar.Center.X : bar.Center.X - w, bar.Y + 2, w, 10), up ? new Color(70, 190, 100) : new Color(200, 60, 60));
				CasinoDraw.Fill(sb, new Rectangle(bar.Center.X - 1, bar.Y, 2, bar.Height), CasinoDraw.Gold * 0.7f);
				CasinoDraw.Fit(sb, Signed(s.GameNet[g]), new Vector2(bar.Center.X + (up ? -8 : 8), bar.Y + 9), up ? Color.LightGreen : new Color(255, 170, 160), 0.68f, half - 12, up ? 1f : 0f, 0.5f);
			}
		}

		// ------------------------------------------------------------------ achievements

		private void DrawAchievements(SpriteBatch sb, Rectangle page, CasinoStats s, Point mouse)
		{
			S.Page = Math.Clamp(S.Page, 0, Pages - 1);
			int w = page.Width / 2 - 5;
			for (int k = 0; k < PerPage; k++) {
				int index = S.Page * PerPage + k;
				if (index >= Achievements.All.Length)
					break;
				Achievement a = Achievements.All[index];
				bool done = a.Done(s);
				var r = new Rectangle(page.X + k % 2 * (w + 10), page.Y + k / 2 * 76, w, 70);
				CasinoDraw.Fill(sb, r, done ? CasinoDraw.Gold : new Color(70, 70, 70));
				CasinoDraw.Fill(sb, CasinoDraw.Shrink(r, 2), done ? new Color(44, 34, 10) : new Color(22, 26, 24));
				var icon = new Vector2(r.X + 36, r.Center.Y);
				if (done)
					PixelFx.Glow(sb, icon, 32f, CasinoDraw.Gold * 0.4f);
				DrawItem(sb, a.Icon(), icon, 40f, done ? Color.White : new Color(50, 50, 55));
				CasinoDraw.Fit(sb, a.Name, new Vector2(r.X + 70, r.Y + 8), done ? CasinoDraw.Gold : Color.White, 0.85f, w - 130);
				CasinoDraw.Fit(sb, Text("Tokens", a.Tokens), new Vector2(r.Right - 10, r.Y + 10), done ? Color.LightGreen : Color.LightGray, 0.65f, 60, 1f);
				CasinoDraw.Fit(sb, a.Description, new Vector2(r.X + 70, r.Y + 30), Color.LightGray, 0.7f, w - 82);
				long have = Math.Min(a.Goal, a.Progress(s));
				var bar = new Rectangle(r.X + 70, r.Bottom - 18, w - 82, 10);
				CasinoDraw.Fill(sb, bar, Color.Black * 0.5f);
				CasinoDraw.Fill(sb, new Rectangle(bar.X, bar.Y, done ? bar.Width : (int)(bar.Width * (have / (double)a.Goal)), bar.Height), done ? CasinoDraw.Gold : new Color(60, 140, 90));
				if (!done)
					CasinoDraw.Fit(sb, a.Money ? Coins.Format(have) + " / " + Coins.Format(a.Goal) : have + " / " + a.Goal, new Vector2(bar.Center.X, bar.Center.Y + 2), Color.White, 0.6f, bar.Width, 0.5f, 0.5f);
			}
			CasinoDraw.Button(sb, Prev, "<", CasinoDraw.ButtonGold, Prev.Contains(mouse), S.Page > 0, 1f);
			CasinoDraw.Button(sb, Next, ">", CasinoDraw.ButtonGold, Next.Contains(mouse), S.Page < Pages - 1, 1f);
			Utils.DrawBorderString(sb, Text("Unlocked", Achievements.Count(s), Achievements.All.Length, S.Page + 1, Pages), new Vector2(Area.Center.X, Area.Bottom - 35), Color.LightGray, 0.85f, 0.5f, 0.5f);
		}

		// ------------------------------------------------------------------ club

		private void DrawClub(SpriteBatch sb, Rectangle page, Point mouse)
		{
			int level = VipClub.LocalLevel;
			float t = Main.GlobalTimeWrappedHourly;
			for (int k = 1; k <= VipClub.MaxLevel; k++) {
				var r = new Rectangle(page.X, page.Y + (k - 1) * 88, page.Width, 82);
				bool have = level >= k;
				Color c = VipClub.Colors[k];
				CasinoDraw.Fill(sb, r, have ? c : new Color(70, 70, 70));
				CasinoDraw.Fill(sb, CasinoDraw.Shrink(r, 2), Color.Lerp(new Color(16, 18, 22), c, have ? 0.18f : 0.04f));
				var badge = new Vector2(r.X + 46, r.Center.Y);
				if (have)
					PixelFx.Glow(sb, badge, 32f, c * (0.4f + 0.1f * (float)Math.Sin(t * 3f + k)));
				VipBadges.Draw(sb, k, badge, 2f);
				CasinoDraw.Fit(sb, VipClub.LevelName(k), new Vector2(r.X + 90, r.Y + 10), have ? c : Color.Gray, 1f, 200);
				CasinoDraw.Wrapped(sb, VipClub.Text("Perks" + k), new Vector2(r.X + 90, r.Y + 36), have ? Color.White : Color.LightGray, 0.75f, r.Width - 260);
				string state = have ? VipClub.Text("Member") : VipClub.Open(k) ? Coins.Format(VipClub.Price[k]) : VipClub.Text("Locked" + k);
				CasinoDraw.Fit(sb, state, new Vector2(r.Right - 14, r.Center.Y + 3), have ? Color.LightGreen : VipClub.Open(k) ? CasinoDraw.Gold : Color.Gray, 0.8f, 150, 1f, 0.5f);
			}
			if (level >= VipClub.MaxLevel)
				Utils.DrawBorderString(sb, VipClub.Text("Top"), new Vector2(Area.Center.X, Area.Bottom - 38), VipClub.Colors[4], 0.9f, 0.5f, 0.5f);
			else if (S.AtConcierge)
				CasinoDraw.Button(sb, Buy, VipClub.Text("Join", VipClub.LevelName(level + 1), Coins.Format(VipClub.Price[level + 1])), CasinoDraw.ButtonGold, Buy.Contains(mouse), VipClub.Open(level + 1), 0.9f);
			else
				Utils.DrawBorderString(sb, VipClub.Text("Where"), new Vector2(Area.Center.X, Area.Bottom - 38), Color.LightGray, 0.85f, 0.5f, 0.5f);
		}

		/// <summary>Item icon centred and scaled down to fit.</summary>
		public static void DrawItem(SpriteBatch sb, int type, Vector2 center, float maxSize, Color color)
		{
			Main.instance.LoadItem(type);
			Texture2D tex = TextureAssets.Item[type].Value;
			Rectangle frame = Main.itemAnimations[type] != null ? Main.itemAnimations[type].GetFrame(tex) : tex.Frame();
			float scale = Math.Min(1.5f, maxSize / Math.Max(frame.Width, frame.Height));
			sb.Draw(tex, center, frame, color, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
		}
	}
}

namespace CasinoWorld.Content.Items
{
	/// <summary>Opens the Gambler's Ledger. Every character gets one.</summary>
	public class GamblersLedger : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 28;
			Item.height = 32;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = Item.useAnimation = 20;
			Item.rare = ItemRarityID.Orange;
			Item.value = Item.buyPrice(silver: 10);
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer) {
				var ledger = ModContent.GetInstance<Common.Club.LedgerSystem>();
				if (ledger.IsOpen)
					ledger.Close();
				else
					ledger.Open(Common.Club.LedgerTab.Stats);
			}
			return true;
		}

		public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.Book).AddIngredient(ModContent.ItemType<LoyaltyToken>(), 2).Register();
	}
}
