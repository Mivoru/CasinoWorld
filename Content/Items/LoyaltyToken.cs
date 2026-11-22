using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items
{
	/// <summary>Earned by wagering at the tables; spent at the prize counter.</summary>
	public class LoyaltyToken : ModItem
	{
		/// <summary>Custom shop currency id for prices in loyalty tokens.</summary>
		public static int CurrencyId { get; private set; } = -1;

		public override void SetStaticDefaults()
		{
			CurrencyId = CustomCurrencyManager.RegisterCurrency(new LoyaltyTokenCurrency(Type, 9999L));
			Item.ResearchUnlockCount = 100;
		}

		public override void SetDefaults()
		{
			Item.width = 14;
			Item.height = 14;
			Item.maxStack = 9999;
			Item.value = 0;
			Item.rare = ItemRarityID.Yellow;
		}
	}

	public class LoyaltyTokenCurrency : CustomCurrencySingleCoin
	{
		public LoyaltyTokenCurrency(int coinItemID, long currencyCap) : base(coinItemID, currencyCap)
		{
			CurrencyTextKey = "Mods.CasinoWorld.Items.LoyaltyToken.Currency";
			CurrencyTextColor = new Microsoft.Xna.Framework.Color(255, 215, 60);
		}
	}
}
