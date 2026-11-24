using CasinoWorld.Common.Gambler;
using CasinoWorld.Common.Upgrades;
using CasinoWorld.Content.Items.PitBoss;
using CasinoWorld.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items.Armor
{
	/// <summary>
	/// High Roller set (adamantite / titanium tier). Defence 26, +10 % gambler damage, Luck 5 + 3 from the set (8).
	/// Set bonus: jackpots feed the Pot twice as much.
	/// </summary>
	[AutoloadEquip(EquipType.Head)]
	public class HighRollerTopHat : ModItem, IGamblerUpgradeable
	{
		public override void SetStaticDefaults() => ArmorIDs.Head.Sets.DrawHatHair[Item.headSlot] = true;

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 26;
			Item.defense = 8;
			Item.rare = ItemRarityID.LightRed;
			Item.value = Item.sellPrice(gold: 3);
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 2;
			player.GetDamage<GamblerDamageClass>() += 0.04f;
		}

		public override bool IsArmorSet(Item head, Item body, Item legs)
			=> body.type == ModContent.ItemType<HighRollerTailcoat>() && legs.type == ModContent.ItemType<HighRollerTrousers>();

		public override void UpdateArmorSet(Player player)
		{
			player.setBonus = Language.GetTextValue("Mods.CasinoWorld.Items.HighRollerTopHat.SetBonus");
			player.GetModPlayer<GamblerPlayer>().Luck += 3;
			player.GetModPlayer<GamblerPot>().JackpotFeed = 2f;
		}

		public override void AddRecipes()
		{
			foreach (int bar in new[] { ItemID.AdamantiteBar, ItemID.TitaniumBar })
				CreateRecipe().AddIngredient(bar, 12).AddIngredient<LoyaltyToken>(60).AddTile<UpgradeMachine>().Register();
		}
	}

	[AutoloadEquip(EquipType.Body)]
	public class HighRollerTailcoat : ModItem, IGamblerUpgradeable
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 30;
			Item.defense = 11;
			Item.rare = ItemRarityID.LightRed;
			Item.value = Item.sellPrice(gold: 4);
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 2;
			player.GetDamage<GamblerDamageClass>() += 0.04f;
		}

		public override void AddRecipes()
		{
			foreach (int bar in new[] { ItemID.AdamantiteBar, ItemID.TitaniumBar })
				CreateRecipe().AddIngredient(bar, 20).AddIngredient<LoyaltyToken>(90).AddTile<UpgradeMachine>().Register();
		}
	}

	[AutoloadEquip(EquipType.Legs)]
	public class HighRollerTrousers : ModItem, IGamblerUpgradeable
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 22;
			Item.defense = 7;
			Item.rare = ItemRarityID.LightRed;
			Item.value = Item.sellPrice(gold: 3);
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 1;
			player.GetDamage<GamblerDamageClass>() += 0.02f;
			player.moveSpeed += 0.08f;
		}

		public override void AddRecipes()
		{
			foreach (int bar in new[] { ItemID.AdamantiteBar, ItemID.TitaniumBar })
				CreateRecipe().AddIngredient(bar, 16).AddIngredient<LoyaltyToken>(60).AddTile<UpgradeMachine>().Register();
		}
	}

	/// <summary>
	/// Pit Boss set (hallowed bars and Vault Bullion from the boss). Defence 34, +15 % gambler damage, Luck 6 + 3 (9).
	/// Set bonus: All In deals a hand one rank better.
	/// </summary>
	[AutoloadEquip(EquipType.Head)]
	public class PitBossFedora : ModItem, IGamblerUpgradeable
	{
		public override void SetStaticDefaults() => ArmorIDs.Head.Sets.DrawHatHair[Item.headSlot] = true;

		public override void SetDefaults()
		{
			Item.width = 34;
			Item.height = 22;
			Item.defense = 10;
			Item.rare = ItemRarityID.Pink;
			Item.value = Item.sellPrice(gold: 5);
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 2;
			player.GetDamage<GamblerDamageClass>() += 0.05f;
		}

		public override bool IsArmorSet(Item head, Item body, Item legs)
			=> body.type == ModContent.ItemType<PitBossSuit>() && legs.type == ModContent.ItemType<PitBossShoes>();

		public override void UpdateArmorSet(Player player)
		{
			player.setBonus = Language.GetTextValue("Mods.CasinoWorld.Items.PitBossFedora.SetBonus");
			player.GetModPlayer<GamblerPlayer>().Luck += 3;
			player.GetModPlayer<GamblerPot>().BetterHands = true;
		}

		public override void AddRecipes()
			=> CreateRecipe().AddIngredient(ItemID.HallowedBar, 10).AddIngredient<VaultBullion>(5).AddIngredient<LoyaltyToken>(80).AddTile<UpgradeMachine>().Register();
	}

	[AutoloadEquip(EquipType.Body)]
	public class PitBossSuit : ModItem, IGamblerUpgradeable
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 30;
			Item.defense = 14;
			Item.rare = ItemRarityID.Pink;
			Item.value = Item.sellPrice(gold: 6);
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 2;
			player.GetDamage<GamblerDamageClass>() += 0.05f;
		}

		public override void AddRecipes()
			=> CreateRecipe().AddIngredient(ItemID.HallowedBar, 16).AddIngredient<VaultBullion>(8).AddIngredient<LoyaltyToken>(120).AddTile<UpgradeMachine>().Register();
	}

	[AutoloadEquip(EquipType.Legs)]
	public class PitBossShoes : ModItem, IGamblerUpgradeable
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 22;
			Item.defense = 10;
			Item.rare = ItemRarityID.Pink;
			Item.value = Item.sellPrice(gold: 5);
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 2;
			player.GetDamage<GamblerDamageClass>() += 0.05f;
			player.moveSpeed += 0.08f;
		}

		public override void AddRecipes()
			=> CreateRecipe().AddIngredient(ItemID.HallowedBar, 12).AddIngredient<VaultBullion>(6).AddIngredient<LoyaltyToken>(80).AddTile<UpgradeMachine>().Register();
	}
}

namespace CasinoWorld.Content.Items.PitBoss
{
	/// <summary>Bars from the Pit Boss's vault: material of the Pit Boss set.</summary>
	public class VaultBullion : ModItem
	{
		public override void SetStaticDefaults() => Item.ResearchUnlockCount = 25;

		public override void SetDefaults()
		{
			Item.width = 30;
			Item.height = 22;
			Item.maxStack = Item.CommonMaxStack;
			Item.rare = ItemRarityID.Pink;
			Item.value = Item.sellPrice(silver: 40);
		}
	}
}
