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

namespace CasinoWorld.Common.Games
{
	/// <summary>The Gambler's Handbook: an in-game guide to the casinos, the class and the bosses.</summary>
	public class HandbookSystem : ModSystem
	{
		public const int Pages = 9;
		private UserInterface ui;
		public int Page;

		public bool IsOpen => ui?.CurrentState != null;

		public override void Load()
		{
			if (!Main.dedServ)
				ui = new UserInterface();
		}

		public void Toggle()
		{
			if (IsOpen) {
				ui.SetState(null);
				SoundEngine.PlaySound(SoundID.MenuClose);
				return;
			}
			var state = new UIState();
			var panel = new HandbookPanel();
			panel.Width.Set(HandbookPanel.PanelWidth, 0f);
			panel.Height.Set(HandbookPanel.PanelHeight, 0f);
			panel.HAlign = panel.VAlign = 0.5f;
			state.Append(panel);
			ui.SetState(state);
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (!IsOpen)
				return;
			if (Main.LocalPlayer.dead || Main.gameMenu || Main.playerInventory && Main.LocalPlayer.controlInv) {
				ui.SetState(null);
				return;
			}
			ui.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			if (index < 0 || !IsOpen)
				return;
			layers.Insert(index, new LegacyGameInterfaceLayer("CasinoWorld: Handbook", () => {
				ui.Draw(Main.spriteBatch, new GameTime());
				return true;
			}, InterfaceScaleType.UI));
		}
	}

	public class HandbookPanel : UIElement
	{
		public const int PanelWidth = 560, PanelHeight = 440;

		private static HandbookSystem S => ModContent.GetInstance<HandbookSystem>();

		private Rectangle Area => GetDimensions().ToRectangle();
		private Rectangle Prev => new(Area.X + 24, Area.Bottom - 54, 110, 34);
		private Rectangle Next => new(Area.Right - 134, Area.Bottom - 54, 110, 34);
		private Rectangle CloseButton => new(Area.Right - 34, Area.Y + 12, 22, 22);

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			Point m = evt.MousePosition.ToPoint();
			if (CloseButton.Contains(m))
				S.Toggle();
			else if (Prev.Contains(m) && S.Page > 0) {
				S.Page--;
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
			else if (Next.Contains(m) && S.Page < HandbookSystem.Pages - 1) {
				S.Page++;
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			Rectangle a = Area;
			CasinoDraw.Panel(sb, a);
			string key = "Mods.CasinoWorld.Handbook.Page" + S.Page;
			Utils.DrawBorderString(sb, Language.GetTextValue(key + "Title"), new Vector2(a.Center.X, a.Y + 30), CasinoDraw.Gold, 1.15f, 0.5f, 0.5f);
			CasinoDraw.Fill(sb, new Rectangle(a.X + 40, a.Y + 50, a.Width - 80, 2), CasinoDraw.Gold * 0.6f);
			CasinoDraw.Wrapped(sb, Language.GetTextValue(key + "Text"), new Vector2(a.X + 30, a.Y + 66), Color.White, 0.82f, a.Width - 60);
			Point m = Main.MouseScreen.ToPoint();
			CasinoDraw.Button(sb, Prev, "<", CasinoDraw.ButtonGold, Prev.Contains(m), S.Page > 0, 1f);
			CasinoDraw.Button(sb, Next, ">", CasinoDraw.ButtonGold, Next.Contains(m), S.Page < HandbookSystem.Pages - 1, 1f);
			CasinoDraw.Button(sb, CloseButton, "X", CasinoDraw.Red, CloseButton.Contains(m), true, 0.8f);
			Utils.DrawBorderString(sb, $"{S.Page + 1} / {HandbookSystem.Pages}", new Vector2(a.Center.X, a.Bottom - 37), Color.LightGray, 0.85f, 0.5f, 0.5f);
		}
	}
}

namespace CasinoWorld.Content.Items
{
	/// <summary>Opens the Gambler's Handbook. Every new character gets one with the Casino Blueprint.</summary>
	public class GamblersHandbook : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 26;
			Item.height = 30;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = Item.useAnimation = 20;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.buyPrice(silver: 10);
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer)
				ModContent.GetInstance<Common.Games.HandbookSystem>().Toggle();
			return true;
		}

		public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.Book).AddIngredient(ModContent.ItemType<LoyaltyToken>(), 1).Register();
	}
}
