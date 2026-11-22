using CasinoWorld.Content.Mounts;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items.Mounts
{
	/// <summary>Minecart prize from the casino prize counter.</summary>
	public class Gamblorghini : ModItem
	{
		public override void SetDefaults()
		{
			Item.mountType = ModContent.MountType<GamblorghiniMount>();
			Item.width = 50;
			Item.height = 34;
			Item.value = Item.sellPrice(gold: 1);
			Item.rare = ItemRarityID.Yellow;
		}
	}
}
