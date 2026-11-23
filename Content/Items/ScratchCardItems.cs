using CasinoWorld.Common.Games;
using CasinoWorld.Common.Systems;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items
{
	/// <summary>A scratch card: use it to open the card and scratch the nine fields. Sold by the Prize Cashier.</summary>
	public abstract class ScratchCardBase : ModItem
	{
		/// <summary>Price the prizes are multiplied from, in copper (the shop price).</summary>
		public abstract long CardPrice { get; }

		public override void SetStaticDefaults() => Item.ResearchUnlockCount = 10;

		public override void SetDefaults()
		{
			Item.width = 22;
			Item.height = 28;
			Item.maxStack = Item.CommonMaxStack;
			Item.consumable = true;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = Item.useAnimation = 20;
			Item.value = (int)CardPrice;
		}

		public override bool CanUseItem(Player player)
		{
			if (CasinoConfig.Instance.GamblingEnabled)
				return !ModContent.GetInstance<ScratchCardSystem>().IsOpen;
			if (player.whoAmI == Main.myPlayer)
				Main.NewText(Language.GetTextValue("Mods.CasinoWorld.UI.GamblingDisabled"), new Microsoft.Xna.Framework.Color(255, 160, 120));
			return false;
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer)
				ScratchCardSystem.Open(CardPrice);
			return true;
		}
	}

	public class ScratchCard : ScratchCardBase { public override long CardPrice => 50 * 100; }

	public class PlatinumScratchCard : ScratchCardBase { public override long CardPrice => 5 * 10000; }
}
