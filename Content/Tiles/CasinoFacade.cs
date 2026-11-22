using System.Linq;
using CasinoWorld.Common.Systems;
using Microsoft.Xna.Framework;
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
	/// Front view of the surface casino: one big decorative tile the player walks in front of.
	/// Only the door area reacts to clicks. The tile cannot be mined or blown up,
	/// and it has no anchors, so digging the ground under it does not break it.
	/// </summary>
	public class CasinoFacade : ModTile
	{
		public const int Width = 24;
		public const int Height = 16;

		// Door area in local tile coordinates (inclusive).
		public const int DoorLeft = 10;
		public const int DoorRight = 13;
		public const int DoorTop = 11;
		public const int DoorBottom = 15;

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = false;
			Main.tileWaterDeath[Type] = false;
			TileID.Sets.PreventsTileRemovalIfOnTopOfIt[Type] = true;
			TileID.Sets.PreventsTileReplaceIfOnTopOfIt[Type] = true;

			TileObjectData.newTile.Width = Width;
			TileObjectData.newTile.Height = Height;
			TileObjectData.newTile.CoordinateWidth = 16;
			TileObjectData.newTile.CoordinatePadding = 2;
			TileObjectData.newTile.CoordinateHeights = Enumerable.Repeat(16, Height).ToArray();
			TileObjectData.newTile.Origin = new Point16(0, Height - 1);
			TileObjectData.newTile.AnchorBottom = AnchorData.Empty;
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.newTile.LavaDeath = false;
			TileObjectData.addTile(Type);

			AddMapEntry(new Color(140, 22, 38), CreateMapEntryName());
			DustType = DustID.GoldCoin;
			MinPick = int.MaxValue;
		}

		public override bool CanKillTile(int i, int j, ref bool blockDamaged) => false;

		public override bool CanExplode(int i, int j) => false;

		public override bool CanReplace(int i, int j, int tileTypeBeingPlaced) => false;

		public static bool IsDoorArea(int i, int j)
		{
			Tile tile = Main.tile[i, j];
			int x = tile.TileFrameX / 18;
			int y = tile.TileFrameY / 18;
			return x >= DoorLeft && x <= DoorRight && y >= DoorTop && y <= DoorBottom;
		}

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => IsDoorArea(i, j);

		public override bool RightClick(int i, int j)
		{
			if (!IsDoorArea(i, j))
				return false;

			CasinoTravel.UseDoor();
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			if (!IsDoorArea(i, j))
				return;

			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = -1;
			player.cursorItemIconText = Language.GetTextValue("Mods.CasinoWorld.UI.EnterCasino");
		}
	}
}
