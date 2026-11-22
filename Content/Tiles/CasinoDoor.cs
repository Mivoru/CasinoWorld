using CasinoWorld.Common.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CasinoWorld.Content.Tiles
{
	/// <summary>
	/// Door that leads into a casino dimension, or back out when used inside one.
	/// Placeholder entrance until the building facade exists.
	/// </summary>
	public class CasinoDoor : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = false;

			TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
			TileObjectData.newTile.Height = 3;
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
			TileObjectData.newTile.Origin = new Point16(0, 2);
			TileObjectData.addTile(Type);

			AddMapEntry(new Color(150, 20, 30), CreateMapEntryName());
			DustType = DustID.GoldCoin;
		}

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

		public override bool RightClick(int i, int j)
		{
			CasinoTravel.UseDoor();
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = -1;
			player.cursorItemIconText = Language.GetTextValue(CasinoTravel.InCasino
				? "Mods.CasinoWorld.UI.ExitCasino"
				: "Mods.CasinoWorld.UI.EnterCasino");
		}
	}
}
