using System;
using CasinoWorld.Common.Games;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Upgrades
{
	public enum UpgradePhase { Idle, Working, Result }

	/// <summary>
	/// Upgrade machine window. Works like a crafting station: the inventory stays open, the player
	/// drops a Gambler item into the slot and pays materials and a fee for a chance at +1 level.
	/// The item is given back when the window closes.
	/// </summary>
	public class UpgradeSystem : CasinoTableGameSystem
	{
		private const int WorkTicks = 90;

		public Item Slot = new();
		public bool Boost;
		public UpgradePhase Phase { get; private set; } = UpgradePhase.Idle;
		public bool LastSucceeded { get; private set; }
		public string Message { get; private set; } = "";
		public Color MessageColor { get; private set; } = Color.White;
		public int Timer { get; private set; }
		private float rolledChance;

		protected override int TableTileType => ModContent.TileType<UpgradeMachine>();

		protected override Point TableSize => new(UpgradeMachine.Width, UpgradeMachine.Height);

		protected override bool KeepInventoryOpen => true;

		protected override UIState CreateState() => new UpgradeUIState();

		public override void Unload()
		{
			base.Unload();
			UpgradeRules.Unload();
		}

		protected override void OnClose(Player player)
		{
			if (Phase == UpgradePhase.Working)
				Resolve();
			if (!Slot.IsAir) {
				player.QuickSpawnItem(player.GetSource_Misc("CasinoWorld"), Slot, Slot.stack);
				Slot = new Item();
			}
			Phase = UpgradePhase.Idle;
		}

		/// <summary>Swaps the mouse item with the slot (only upgradeable items go in).</summary>
		public void ClickSlot()
		{
			if (Phase == UpgradePhase.Working)
				return;

			Item mouse = Main.mouseItem;
			if (!mouse.IsAir && mouse.ModItem is not IGamblerUpgradeable) {
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.Upgrade.NotUpgradeable"), Color.OrangeRed);
				return;
			}

			(Main.mouseItem, Slot) = (Slot, mouse);
			Phase = UpgradePhase.Idle;
			SetMessage("", Color.White);
			SoundEngine.PlaySound(SoundID.Grab);
		}

		public int Level => Slot.IsAir ? 0 : UpgradeGlobalItem.LevelOf(Slot);

		public int Required => UpgradeRules.TierFor(Level).Count * (Boost ? 2 : 1);

		public float Chance => Math.Min(1f, UpgradeRules.TierFor(Level).Chance + (Boost ? UpgradeRules.BoostChance : 0f));

		public static int CountMaterials(Player player, int[] types)
		{
			int total = 0;
			for (int k = 0; k < 58; k++) {
				if (Array.IndexOf(types, player.inventory[k].type) >= 0)
					total += player.inventory[k].stack;
			}
			return total;
		}

		public void TryUpgrade(Player player)
		{
			if (Phase == UpgradePhase.Working || Slot.IsAir)
				return;
			if (Level >= UpgradeRules.MaxLevel) {
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.Upgrade.Maxed"), Color.Gold);
				return;
			}

			UpgradeTier tier = UpgradeRules.TierFor(Level);
			if (CountMaterials(player, tier.Materials) < Required) {
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.Upgrade.NoMaterials"), Color.OrangeRed);
				return;
			}
			if (!Coins.TryTake(player, UpgradeRules.Fee(Level))) {
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.Upgrade.NoMoney"), Color.OrangeRed);
				return;
			}

			int left = Required;
			for (int k = 0; k < 58 && left > 0; k++) {
				Item item = player.inventory[k];
				if (Array.IndexOf(tier.Materials, item.type) < 0)
					continue;
				int take = Math.Min(left, item.stack);
				item.stack -= take;
				left -= take;
				if (item.stack <= 0)
					item.TurnToAir();
			}

			rolledChance = Chance;
			Phase = UpgradePhase.Working;
			Timer = 0;
			SetMessage(Language.GetTextValue("Mods.CasinoWorld.Upgrade.Working"), new Color(120, 220, 255));
			SoundEngine.PlaySound(SoundID.Item37);
		}

		protected override void UpdateGame(Player player)
		{
			if (Phase != UpgradePhase.Working)
				return;
			Timer++;
			if (Timer % 15 == 0)
				SoundEngine.PlaySound(SoundID.Item37 with { Pitch = Timer / (float)WorkTicks - 0.3f, Volume = 0.5f });
			if (Timer >= WorkTicks)
				Resolve();
		}

		private void Resolve()
		{
			Phase = UpgradePhase.Result;
			LastSucceeded = Main.rand.NextFloat() < rolledChance;
			if (LastSucceeded && Slot.TryGetGlobalItem(out UpgradeGlobalItem g)) {
				g.Level++;
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.Upgrade.Success", g.Level), new Color(255, 215, 60));
				SoundEngine.PlaySound(SoundID.Item4);
				CombatText.NewText(Main.LocalPlayer.Hitbox, new Color(255, 215, 60), "+" + g.Level, dramatic: true);
			}
			else {
				SetMessage(Language.GetTextValue("Mods.CasinoWorld.Upgrade.Fail"), Color.LightGray);
				SoundEngine.PlaySound(SoundID.Item27);
			}
		}

		private void SetMessage(string text, Color color)
		{
			Message = text;
			MessageColor = color;
		}
	}

	public class UpgradeUIState : UIState
	{
		public override void OnInitialize()
		{
			var panel = new UpgradePanel();
			panel.Width.Set(UpgradePanel.PanelWidth, 0f);
			panel.Height.Set(UpgradePanel.PanelHeight, 0f);
			panel.HAlign = 0.78f;
			panel.VAlign = 0.38f;
			Append(panel);
		}
	}

	public class UpgradePanel : UIElement
	{
		public const int PanelWidth = 420;
		public const int PanelHeight = 330;

		private static readonly Color Steel = new(36, 42, 56);
		private static readonly Color SteelDark = new(22, 26, 36);
		private static readonly Color Glow = new(120, 220, 255);

		private static UpgradeSystem System => ModContent.GetInstance<UpgradeSystem>();

		private static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.Upgrade." + key, args);

		private Rectangle Area => GetDimensions().ToRectangle();
		private Rectangle SlotRect => new(Area.X + 24, Area.Y + 56, 64, 64);
		private Rectangle UpgradeButton => new(Area.X + 24, Area.Bottom - 70, 180, 44);
		private Rectangle BoostButton => new(Area.X + 216, Area.Bottom - 70, 180, 44);
		private Rectangle CloseButton => new(Area.Right - 32, Area.Y + 10, 22, 22);

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (!IsMouseHovering)
				return;
			Main.LocalPlayer.mouseInterface = true;
			if (!(Main.mouseLeft && Main.mouseLeftRelease))
				return;

			Point mouse = CasinoTableGameSystem.UIMouse;
			if (CloseButton.Contains(mouse))
				System.Close();
			else if (SlotRect.Contains(mouse))
				System.ClickSlot();
			else if (UpgradeButton.Contains(mouse))
				System.TryUpgrade(Main.LocalPlayer);
			else if (BoostButton.Contains(mouse) && System.Phase != UpgradePhase.Working) {
				System.Boost = !System.Boost;
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;

			Rectangle a = Area;
			UpgradeSystem sys = System;
			Point mouse = CasinoTableGameSystem.UIMouse;
			Player player = Main.LocalPlayer;

			CasinoDraw.Fill(sb, a, CasinoDraw.Gold);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(a, 3), SteelDark);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(a, 6), Steel);
			Utils.DrawBorderString(sb, Text("Title"), new Vector2(a.X + 20, a.Y + 16), Glow, 1.1f);
			CasinoDraw.Button(sb, CloseButton, "X", CasinoDraw.Red, CloseButton.Contains(mouse), true, 0.8f);

			// Item slot with a pulsing frame while working.
			Rectangle slot = SlotRect;
			float pulse = sys.Phase == UpgradePhase.Working ? 0.5f + 0.5f * (float)Math.Sin(sys.Timer * 0.4f) : 0f;
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(slot, -3), Color.Lerp(CasinoDraw.Gold, Glow, pulse));
			CasinoDraw.Fill(sb, slot, SteelDark);
			if (!sys.Slot.IsAir)
				DrawItem(sb, sys.Slot.type, slot.Center.ToVector2(), 52f);
			else
				Utils.DrawBorderString(sb, Text("DropHere"), slot.Center.ToVector2() + new Vector2(0, 3), Color.White * 0.5f, 0.6f, 0.5f, 0.5f);

			float x = slot.Right + 18, y = slot.Y;
			if (sys.Slot.IsAir) {
				Utils.DrawBorderString(sb, Text("Hint"), new Vector2(x, y + 8), Color.White * 0.8f, 0.8f);
			}
			else {
				int level = sys.Level;
				Utils.DrawBorderString(sb, sys.Slot.AffixName() + (level > 0 ? " +" + level : ""), new Vector2(x, y), Color.White, 0.9f);
				if (level >= UpgradeRules.MaxLevel) {
					Utils.DrawBorderString(sb, Text("Maxed"), new Vector2(x, y + 26), CasinoDraw.Gold, 0.85f);
				}
				else {
					bool weapon = sys.Slot.damage > 0;
					string next = weapon
						? Text("NextWeapon", level + 1, (int)(UpgradeRules.DamagePerLevel * (level + 1) * 100))
						: Text("NextArmor", level + 1, level + 1, (level + 1) / 2);
					Utils.DrawBorderString(sb, next, new Vector2(x, y + 24), Glow, 0.8f);

					UpgradeTier tier = UpgradeRules.TierFor(level);
					int have = UpgradeSystem.CountMaterials(player, tier.Materials);
					Utils.DrawBorderString(sb, Text("Cost"), new Vector2(x, y + 54), Color.White * 0.8f, 0.8f);
					float mx = x;
					foreach (int type in tier.Materials) {
						DrawItem(sb, type, new Vector2(mx + 12, y + 92), 24f);
						mx += 28;
					}
					Color countColor = have >= sys.Required ? Color.LightGreen : Color.OrangeRed;
					Utils.DrawBorderString(sb, $"{have} / {sys.Required}", new Vector2(mx + 4, y + 84), countColor, 0.85f);
					Utils.DrawBorderString(sb, "+ " + Coins.Format(UpgradeRules.Fee(level)), new Vector2(mx + 80, y + 84), Color.Gold, 0.85f);
					Utils.DrawBorderString(sb, Text("Chance", (int)Math.Round(sys.Chance * 100)), new Vector2(x, y + 116), Color.White, 0.9f);
				}
			}

			// Progress bar while working.
			var bar = new Rectangle(a.X + 24, a.Y + 206, a.Width - 48, 14);
			CasinoDraw.Fill(sb, bar, SteelDark);
			if (sys.Phase == UpgradePhase.Working)
				CasinoDraw.Fill(sb, new Rectangle(bar.X, bar.Y, (int)(bar.Width * sys.Timer / 90f), bar.Height), Glow);
			else if (sys.Phase == UpgradePhase.Result)
				CasinoDraw.Fill(sb, bar, sys.LastSucceeded ? CasinoDraw.Gold : Color.DimGray);

			Utils.DrawBorderString(sb, sys.Message, new Vector2(a.Center.X, a.Y + 236), sys.MessageColor, 0.9f, 0.5f);

			bool canUpgrade = !sys.Slot.IsAir && sys.Phase != UpgradePhase.Working && sys.Level < UpgradeRules.MaxLevel;
			CasinoDraw.Button(sb, UpgradeButton, Text("Upgrade"), new Color(40, 120, 170), UpgradeButton.Contains(mouse), canUpgrade, 1f);
			CasinoDraw.Button(sb, BoostButton, Text(sys.Boost ? "BoostOn" : "BoostOff"), sys.Boost ? CasinoDraw.ButtonGold : CasinoDraw.FeltDark,
				BoostButton.Contains(mouse), sys.Phase != UpgradePhase.Working, 0.85f);
			Utils.DrawBorderString(sb, Text("NoLoss"), new Vector2(a.Center.X, a.Bottom - 18), Color.White * 0.6f, 0.65f, 0.5f);
		}

		private static void DrawItem(SpriteBatch sb, int type, Vector2 center, float maxSize)
		{
			Main.instance.LoadItem(type);
			Texture2D tex = TextureAssets.Item[type].Value;
			Rectangle frame = Main.itemAnimations[type] != null ? Main.itemAnimations[type].GetFrame(tex) : tex.Frame();
			float scale = Math.Min(1.5f, maxSize / Math.Max(frame.Width, frame.Height));
			sb.Draw(tex, center, frame, Color.White, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
		}
	}
}
