using CasinoWorld.Common.Upgrades;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CasinoWorld.Content.Tiles
{
	/// <summary>Upgrade machine in the underground casino (3x4). Right-click to upgrade Gambler gear.</summary>
	public class UpgradeMachine : ModTile
	{
		public const int Width = 3;
		public const int Height = 4;

		private static Asset<Texture2D> glow;

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = false;
			TileObjectData.newTile.Width = Width;
			TileObjectData.newTile.Height = Height;
			TileObjectData.newTile.CoordinateWidth = 16;
			TileObjectData.newTile.CoordinatePadding = 2;
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16, 16 };
			TileObjectData.newTile.Origin = new Point16(0, Height - 1);
			TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, Width, 0);
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(90, 170, 220), CreateMapEntryName());
			DustType = DustID.Iron;
		}

		public override void Unload()
		{
			glow = null;
		}

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

		public override bool RightClick(int i, int j)
		{
			Tile tile = Main.tile[i, j];
			var topLeft = new Point16(i - tile.TileFrameX / 18 % Width, j - tile.TileFrameY / 18 % Height);
			ModContent.GetInstance<UpgradeSystem>().Open(topLeft);
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = -1;
			player.cursorItemIconText = Language.GetTextValue("Mods.CasinoWorld.Upgrade.Hover");
		}

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			r = 0.15f;
			g = 0.35f;
			b = 0.5f;
		}

		public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Tile tile = Main.tile[i, j];
			glow ??= ModContent.Request<Texture2D>(Texture + "_Glow");
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			Vector2 position = new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen;
			float pulse = 0.8f + 0.2f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 3f);
			spriteBatch.Draw(glow.Value, position, new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16), Color.White * pulse);
		}
	}
}
