using CasinoWorld.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items
{
	/// <summary>Places a casino door. Development helper until the casino building generates in the world.</summary>
	public class CasinoDoorItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<CasinoDoor>());
			Item.width = 16;
			Item.height = 24;
			Item.value = Item.buyPrice(gold: 1);
			Item.rare = ItemRarityID.Yellow;
		}

		// TEMP: cheap recipe for testing. Remove once the casino generates in the world.
		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Wood)
				.Register();
		}
	}
}
