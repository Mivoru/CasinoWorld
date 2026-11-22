using CasinoWorld.Content.Items.Crates;
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
	/// <summary>
	/// A crate lying in the world (2x2). Right-click or mine it to pick up the crate item.
	/// The seam and emblem glow faintly so crates can be spotted in dark caves.
	/// </summary>
	public abstract class CrateTile : ModTile
	{
		protected abstract int CrateItemType { get; }

		protected abstract Color MapColor { get; }

		protected abstract Vector3 Glow { get; }

		private Asset<Texture2D> glowTexture;

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileSpelunker[Type] = true;
			Main.tileShine2[Type] = true;
			Main.tileShine[Type] = 1200;
			Main.tileOreFinderPriority[Type] = 500;

			TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
			TileObjectData.newTile.Origin = new Point16(0, 1);
			TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, 2, 0);
			TileObjectData.addTile(Type);

			AddMapEntry(MapColor, CreateMapEntryName());
			RegisterItemDrop(CrateItemType);
			DustType = DustID.WoodFurniture;
		}

		public override void Unload()
		{
			glowTexture = null;
		}

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

		public override bool RightClick(int i, int j)
		{
			WorldGen.KillTile(i, j);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, i, j);
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = CrateItemType;
		}

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			r = Glow.X;
			g = Glow.Y;
			b = Glow.Z;
		}

		public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Tile tile = Main.tile[i, j];
			glowTexture ??= ModContent.Request<Texture2D>(Texture + "_Glow");
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			Vector2 position = new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen;
			float pulse = 0.75f + 0.25f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 2.5f + i);
			spriteBatch.Draw(glowTexture.Value, position, new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16), Color.White * pulse);
		}
	}

	public class LuckyCrateTile : CrateTile
	{
		protected override int CrateItemType => ModContent.ItemType<LuckyCrate>();
		protected override Color MapColor => new(200, 140, 60);
		protected override Vector3 Glow => new(0.25f, 0.18f, 0.06f);
	}

	public class HighRollerCrateTile : CrateTile
	{
		protected override int CrateItemType => ModContent.ItemType<HighRollerCrate>();
		protected override Color MapColor => new(150, 60, 190);
		protected override Vector3 Glow => new(0.24f, 0.08f, 0.24f);
	}
}
