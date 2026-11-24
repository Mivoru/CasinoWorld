using System;
using System.Collections.Generic;
using CasinoWorld.Common.Games;
using CasinoWorld.Content.Items.Crates;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Crates
{
	/// <summary>Hosts the crate opening window. Opened by right-clicking a crate item.</summary>
	public class CrateSystem : ModSystem
	{
		public CrateOpening Opening { get; } = new();

		private UserInterface ui;
		private UIState state;

		public bool IsOpen => ui?.CurrentState != null;

		public bool IsSpinning => IsOpen && Opening.Active && !Opening.Done;

		public static Point UIMouse => (ModContent.GetInstance<CrateSystem>().ui?.MousePosition ?? Main.MouseScreen).ToPoint();

		public override void Load()
		{
			if (Main.dedServ)
				return;
			ui = new UserInterface();
			state = new CrateUIState();
			state.Activate();
		}

		public override void Unload()
		{
			ui = null;
			state = null;
			CrateLoot.Unload();
		}

		public override void PreSaveAndQuit()
		{
			if (IsOpen)
				Close();
		}

		public override void OnWorldUnload()
		{
			if (IsOpen)
				Close();
		}

		public void Open(CrateKind kind)
		{
			if (Main.dedServ)
				return;
			ui.SetState(state);
			Opening.Start(kind, Main.LocalPlayer);
		}

		public void Close()
		{
			Opening.FinishImmediately(Main.LocalPlayer);
			Opening.Close();
			ui?.SetState(null);
		}

		/// <summary>Consumes one more crate of the same kind from the inventory and opens it.</summary>
		public bool TryOpenAnother()
		{
			int type = CrateItem.ItemTypeFor(Opening.Kind);
			Player player = Main.LocalPlayer;
			for (int k = 0; k < 58; k++) {
				Item item = player.inventory[k];
				if (item.type == type && item.stack > 0) {
					item.stack--;
					if (item.stack <= 0)
						item.TurnToAir();
					Opening.Start(Opening.Kind, player);
					return true;
				}
			}
			return false;
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (!IsOpen)
				return;
			Opening.Update(Main.LocalPlayer);
			if (Main.LocalPlayer.dead) {
				Close();
				return;
			}
			ui.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			if (index < 0)
				return;
			layers.Insert(index, new LegacyGameInterfaceLayer("CasinoWorld: Crate", () => {
				if (IsOpen)
					ui.Draw(Main.spriteBatch, new GameTime());
				return true;
			}, InterfaceScaleType.UI));
		}
	}

	public class CrateUIState : UIState
	{
		public override void OnInitialize()
		{
			var panel = new CratePanel();
			panel.Width.Set(CratePanel.PanelWidth, 0f);
			panel.Height.Set(CratePanel.PanelHeight, 0f);
			panel.HAlign = 0.5f;
			panel.VAlign = 0.42f;
			Append(panel);
		}
	}

	/// <summary>
	/// CS:GO style opening window: a strip of item cards with rarity colours scrolls under a gold
	/// pointer and stops on the prize, then the prize is shown large with its rarity.
	/// </summary>
	public class CratePanel : UIElement
	{
		public const int PanelWidth = 720;
		public const int PanelHeight = 366;

		private static readonly Color Back = new(22, 18, 30);
		private static readonly Color Strip = new(34, 30, 44);

		private static Asset<Texture2D> luckyBig, highRollerBig, glow;

		private static Texture2D BigCrate(CrateKind kind) => (kind == CrateKind.HighRoller
			? highRollerBig ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/HighRollerCrateBig", AssetRequestMode.ImmediateLoad)
			: luckyBig ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/LuckyCrateBig", AssetRequestMode.ImmediateLoad)).Value;

		private static Texture2D Glow => (glow ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/Fx/Glow72", AssetRequestMode.ImmediateLoad)).Value;

		private static CrateSystem System => ModContent.GetInstance<CrateSystem>();

		private static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.UI.Crate." + key, args);

		private Rectangle Area => GetDimensions().ToRectangle();

		private Rectangle StripRect => new(Area.X + 20, Area.Y + 56, Area.Width - 40, 120);

		private Rectangle CloseButton => new(Area.Right - 32, Area.Y + 10, 22, 22);

		private Rectangle AgainButton => new(Area.Center.X - 170, Area.Bottom - 54, 160, 38);

		private Rectangle DoneButton => new(Area.Center.X + 10, Area.Bottom - 54, 160, 38);

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			Point mouse = evt.MousePosition.ToPoint();
			CrateOpening opening = System.Opening;
			if (CloseButton.Contains(mouse) || (opening.Done && DoneButton.Contains(mouse))) {
				System.Close();
				SoundEngine.PlaySound(SoundID.MenuClose);
			}
			else if (opening.Done && AgainButton.Contains(mouse) && !System.TryOpenAnother()) {
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;

			Rectangle a = Area;
			CrateOpening opening = System.Opening;
			Point mouse = CrateSystem.UIMouse;
			Color accent = CrateLoot.RarityColors[(int)opening.PrizeRarity];

			DrawFrame(sb, a, opening.Kind);
			string title = Lang.GetItemNameValue(CrateItem.ItemTypeFor(opening.Kind));
			Utils.DrawBorderString(sb, title, new Vector2(a.X + 20, a.Y + 16), CasinoDraw.Gold, 1.1f);
			CasinoDraw.Button(sb, CloseButton, "X", CasinoDraw.Red, CloseButton.Contains(mouse), true, 0.8f);

			DrawStrip(sb, opening);

			if (opening.Done) {
				// Prize banner.
				var banner = new Rectangle(a.X + 20, a.Y + 196, a.Width - 40, 92);
				float pulse = 0.55f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f);
				CasinoDraw.Fill(sb, banner, accent * pulse);
				CasinoDraw.Fill(sb, CasinoDraw.Shrink(banner, 3), Back);
				// Colour washing in from both ends, then the open crate with light in the prize's colour.
				for (int s = 0; s < 12; s++) {
					CasinoDraw.Fill(sb, new Rectangle(banner.X + 3 + s * 14, banner.Y + 3, 14, banner.Height - 6), accent * (0.26f - s * 0.02f));
					CasinoDraw.Fill(sb, new Rectangle(banner.Right - 17 - s * 14, banner.Y + 3, 14, banner.Height - 6), accent * (0.26f - s * 0.02f));
				}
				var crateAt = new Vector2(banner.Right - 60, banner.Center.Y);
				DrawGlow(sb, crateAt - new Vector2(0, 14), accent, 0.8f + 0.1f * pulse);
				DrawCrate(sb, opening.Kind, true, crateAt);
				DrawGlow(sb, new Vector2(banner.X + 44, banner.Center.Y), accent, 0.55f);
				DrawItem(sb, opening.Prize.ItemType, new Vector2(banner.X + 44, banner.Center.Y), 52f);
				string name = Lang.GetItemNameValue(opening.Prize.ItemType) + (opening.PrizeStack > 1 ? "  x" + opening.PrizeStack : "");
				CasinoDraw.Fit(sb, name, new Vector2(banner.X + 86, banner.Y + 22), accent, 1.15f, banner.Width - 200);
				Utils.DrawBorderString(sb, Text("Rarity" + opening.PrizeRarity), new Vector2(banner.X + 86, banner.Y + 54), accent * 0.85f, 0.8f);

				CasinoDraw.Button(sb, AgainButton, Text("OpenAnother"), CasinoDraw.ButtonGold, AgainButton.Contains(mouse), true, 0.9f);
				CasinoDraw.Button(sb, DoneButton, Text("Done"), CasinoDraw.FeltDark, DoneButton.Contains(mouse), true, 0.9f);
			}
			else {
				// The crate rattles harder as the reel slows down; its colour does not give the prize away.
				float p = Math.Min(1f, opening.Timer / (float)CrateOpening.SpinTicks);
				var crateAt = new Vector2(a.Center.X, a.Y + 236) + new Vector2((float)Math.Sin(opening.Timer * 0.9f), -Math.Abs((float)Math.Sin(opening.Timer * 0.45f))) * (1f + p * 3f);
				DrawGlow(sb, new Vector2(a.Center.X, a.Y + 236), KindColor(opening.Kind), 0.8f + p * 0.4f);
				DrawCrate(sb, opening.Kind, false, crateAt);
				Utils.DrawBorderString(sb, Text("Opening"), new Vector2(a.Center.X, a.Y + 276), Color.White * 0.8f, 0.85f, 0.5f);
				DrawOdds(sb, new Vector2(a.X + 20, a.Bottom - 38), opening.Kind);
			}
		}

		private static Color KindColor(CrateKind kind) => kind == CrateKind.HighRoller ? new Color(190, 90, 240) : new Color(255, 190, 70);

		private static Color KindBack(CrateKind kind) => kind == CrateKind.HighRoller ? new Color(52, 24, 84) : new Color(84, 34, 28);

		/// <summary>Window in the crate's own colours: lacquer border, two gold beads, studs, a soft gradient inside.</summary>
		private static void DrawFrame(SpriteBatch sb, Rectangle a, CrateKind kind)
		{
			Color lacquer = KindBack(kind);
			CasinoDraw.Fill(sb, new Rectangle(a.X + 6, a.Y + 8, a.Width, a.Height), Color.Black * 0.35f);
			CasinoDraw.Fill(sb, a, new Color(146, 88, 26));
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(a, 2), CasinoDraw.Gold);
			CasinoDraw.Fill(sb, new Rectangle(a.X + 2, a.Y + 2, a.Width - 4, 2), new Color(255, 236, 160));
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(a, 4), lacquer);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(a, 8), CasinoDraw.Gold);
			Rectangle inner = CasinoDraw.Shrink(a, 9);
			CasinoDraw.Fill(sb, inner, Back);
			for (int k = 0; k < 10; k++)
				CasinoDraw.Fill(sb, new Rectangle(inner.X, inner.Y + k * 10, inner.Width, 10), lacquer * (0.6f * (1f - k / 10f)));
			foreach (Point c in new[] { new Point(a.X + 6, a.Y + 6), new Point(a.Right - 6, a.Y + 6), new Point(a.X + 6, a.Bottom - 6), new Point(a.Right - 6, a.Bottom - 6) }) {
				CasinoDraw.Fill(sb, new Rectangle(c.X - 5, c.Y - 5, 10, 10), new Color(146, 88, 26));
				CasinoDraw.Fill(sb, new Rectangle(c.X - 4, c.Y - 4, 8, 8), CasinoDraw.Gold);
				CasinoDraw.Fill(sb, new Rectangle(c.X - 4, c.Y - 4, 4, 4), new Color(255, 236, 160));
			}
		}

		private static void DrawGlow(SpriteBatch sb, Vector2 center, Color color, float scale)
		{
			Texture2D tex = Glow;
			sb.Draw(tex, center, null, new Color(color.R, color.G, color.B, 0) * 0.7f, 0f, tex.Size() / 2f, scale, SpriteEffects.None, 0f);
		}

		private static void DrawCrate(SpriteBatch sb, CrateKind kind, bool open, Vector2 center)
		{
			Texture2D tex = BigCrate(kind);
			Rectangle frame = tex.Frame(1, 2, 0, open ? 1 : 0);
			sb.Draw(tex, new Vector2((int)center.X, (int)center.Y), frame, Color.White, 0f, frame.Size() / 2f, 1f, SpriteEffects.None, 0f);
		}

		private void DrawStrip(SpriteBatch sb, CrateOpening opening)
		{
			Rectangle strip = StripRect;
			CasinoDraw.Fill(sb, new Rectangle(strip.X - 2, strip.Y - 2, strip.Width + 4, strip.Height + 4), CasinoDraw.Gold);
			CasinoDraw.Fill(sb, strip, Strip);
			float centerX = strip.Center.X;
			float slotW = CrateOpening.SlotWidth;

			for (int k = 0; k < opening.Strip.Count; k++) {
				float left = centerX + k * slotW - opening.Offset - slotW / 2f;
				if (left + slotW < strip.X || left > strip.Right)
					continue;

				(CrateDrop drop, CrateRarity rarity) = opening.Strip[k];
				Color color = CrateLoot.RarityColors[(int)rarity];
				int x0 = (int)Math.Max(strip.X, left + 3), x1 = (int)Math.Min(strip.Right, left + slotW - 3);
				if (x1 <= x0)
					continue;
				var card = new Rectangle(x0, strip.Y + 8, x1 - x0, strip.Height - 16);
				bool won = opening.Done && k == CrateOpening.WinnerIndex;
				var cardColor = new Color(48, 44, 60);
				CasinoDraw.Fill(sb, card, Color.Lerp(cardColor, color, won ? 0.75f : 0.3f));
				CasinoDraw.Fill(sb, new Rectangle(card.X, card.Y + 2, card.Width, card.Height - 2), won ? Color.Lerp(cardColor, color, 0.25f) : cardColor);
				// Rarity glow from the bottom.
				for (int s = 0; s < 6; s++)
					CasinoDraw.Fill(sb, new Rectangle(card.X, card.Bottom - 8 - s * 10, card.Width, 10), color * (0.22f - s * 0.035f));
				CasinoDraw.Fill(sb, new Rectangle(card.X, card.Bottom - 6, card.Width, 6), color);

				float itemX = left + slotW / 2f;
				if (itemX > strip.X + 20 && itemX < strip.Right - 20)
					DrawItem(sb, drop.ItemType, new Vector2(itemX, card.Center.Y - 6), 50f);
			}

			// Darken the ends so the strip fades out.
			for (int s = 0; s < 8; s++) {
				CasinoDraw.Fill(sb, new Rectangle(strip.X + s * 8, strip.Y, 8, strip.Height), Back * (0.85f - s * 0.1f));
				CasinoDraw.Fill(sb, new Rectangle(strip.Right - (s + 1) * 8, strip.Y, 8, strip.Height), Back * (0.85f - s * 0.1f));
			}

			// Pointer.
			CasinoDraw.Fill(sb, new Rectangle((int)centerX - 1, strip.Y - 6, 3, strip.Height + 12), CasinoDraw.Gold);
			for (int s = 0; s < 6; s++) {
				CasinoDraw.Fill(sb, new Rectangle((int)centerX - 6 + s, strip.Y - 10 + s, 13 - s * 2, 1), CasinoDraw.Gold);
				CasinoDraw.Fill(sb, new Rectangle((int)centerX - 6 + s, strip.Bottom + 9 - s, 13 - s * 2, 1), CasinoDraw.Gold);
			}
		}

		private static void DrawOdds(SpriteBatch sb, Vector2 pos, CrateKind kind)
		{
			// Stage and pity counters above the odds row.
			CratePity pity = Main.LocalPlayer.GetModPlayer<CratePity>();
			string info = Text("Stage" + CrateLoot.CurrentStage) + "   " + Text("Pity", CrateLoot.PityRare - pity.SinceRare, CrateLoot.PityRed - pity.SinceRed, CrateLoot.PityGold - pity.SinceGold);
			CasinoDraw.Fit(sb, info, pos - new Vector2(0, 20), Color.White * 0.7f, 0.7f, PanelWidth - 40);
			int[] weights = CrateLoot.Weights(kind);
			float x = pos.X;
			for (int k = 0; k < weights.Length; k++) {
				string label = Text("Rarity" + (CrateRarity)k) + " " + weights[k] / 10f + "%";
				Color c = CrateLoot.RarityColors[k];
				CasinoDraw.Fill(sb, new Rectangle((int)x, (int)pos.Y + 4, 10, 10), c);
				Utils.DrawBorderString(sb, label, new Vector2(x + 14, pos.Y), c, 0.75f);
				x += FontAssets.MouseText.Value.MeasureString(label).X * 0.75f + 34;
			}
		}

		/// <summary>Draws an item icon centred, scaled down to fit <paramref name="maxSize"/>.</summary>
		private static void DrawItem(SpriteBatch sb, int type, Vector2 center, float maxSize)
		{
			Main.instance.LoadItem(type);
			Texture2D tex = TextureAssets.Item[type].Value;
			Rectangle frame = Main.itemAnimations[type] != null ? Main.itemAnimations[type].GetFrame(tex) : tex.Frame();
			float scale = Math.Min(1.6f, maxSize / Math.Max(frame.Width, frame.Height));
			sb.Draw(tex, center, frame, Color.White, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
		}
	}
}
