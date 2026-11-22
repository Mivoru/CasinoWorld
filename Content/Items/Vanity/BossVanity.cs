using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;
using Terraria.ObjectData;

namespace CasinoWorld.Content.Items.Vanity
{
	/// <summary>Vanity mask dropped by The House (1/7, in the treasure bag in expert).</summary>
	[AutoloadEquip(EquipType.Head)]
	public class TheHouseMask : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = Item.height = 28;
			Item.vanity = true;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.sellPrice(silver: 75);
		}
	}

	/// <summary>Wall trophy of The House (1/10).</summary>
	public class TheHouseTrophy : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<TheHouseTrophyTile>());
			Item.width = Item.height = 32;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.buyPrice(gold: 1);
		}
	}
}

namespace CasinoWorld.Content.Tiles
{
	public class TheHouseTrophyTile : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.FramesOnKillWall[Type] = true;
			TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3Wall);
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(120, 85, 60), Language.GetText("MapObject.Trophy"));
			DustType = DustID.WoodFurniture;
		}
	}
}
