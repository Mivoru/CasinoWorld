using CasinoWorld.Common.Gambler;
using CasinoWorld.Common.Upgrades;
using CasinoWorld.Content.Projectiles;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items.Weapons
{
	// Endgame Gambler weapons, dropped by jackpots in the underground casino once Plantera
	// (tier 3) or the Moon Lord (tier 4) is down. Numbers checked with tools/gambler_dps.py:
	// tier 3 about 240 DPS with set Luck (vanilla post-Plantera average 193),
	// tier 4 about 1200 DPS (vanilla post-Moon Lord average 1186).

	/// <summary>Tier 3: one big spectral card that homes in and pierces twice.</summary>
	public class AceOfSpades : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 72;
			Item.knockBack = 3f;
			Item.useTime = Item.useAnimation = 24;
			Item.shoot = ModContent.ProjectileType<SpectralCard>();
			Item.shootSpeed = 13f;
			Item.width = 30;
			Item.height = 38;
			Item.value = Item.sellPrice(gold: 8);
			Item.rare = ItemRarityID.Yellow;
		}
	}

	/// <summary>Tier 3: a die that splits into two half-damage dice on its first hit.</summary>
	public class DoublingDice : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 55;
			Item.knockBack = 4f;
			Item.useTime = Item.useAnimation = 28;
			Item.shoot = ModContent.ProjectileType<DoublingDie>();
			Item.shootSpeed = 11f;
			Item.width = 34;
			Item.height = 32;
			Item.value = Item.sellPrice(gold: 8);
			Item.rare = ItemRarityID.Yellow;
		}
	}

	/// <summary>Tier 3: fires chips; every sixth chamber is the jackpot round with triple damage.</summary>
	public class RouletteRevolver : GamblerWeapon
	{
		private int chamber;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 36;
			Item.knockBack = 2f;
			Item.useTime = Item.useAnimation = 16;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.noUseGraphic = false;
			Item.UseSound = SoundID.Item41;
			Item.shoot = ModContent.ProjectileType<RevolverChip>();
			Item.shootSpeed = 16f;
			Item.width = 44;
			Item.height = 30;
			Item.value = Item.sellPrice(gold: 8);
			Item.rare = ItemRarityID.Yellow;
		}

		public override Vector2? HoldoutOffset() => new Vector2(-2f, 0f);

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			bool jackpot = ++chamber % 6 == 0;
			Projectile.NewProjectile(source, position, velocity, type, jackpot ? damage * 3 : damage, knockback, player.whoAmI, jackpot ? 1f : 0f);
			return false;
		}
	}

	/// <summary>Tier 4: galaxy cards that burst into three smaller cards on hit.</summary>
	public class StardustDeck : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 115;
			Item.knockBack = 3f;
			Item.useTime = Item.useAnimation = 16;
			Item.shoot = ModContent.ProjectileType<GalaxyCard>();
			Item.shootSpeed = 15f;
			Item.width = 36;
			Item.height = 34;
			Item.value = Item.sellPrice(gold: 20);
			Item.rare = ItemRarityID.Red;
		}
	}

	/// <summary>Tier 4: calls a burning die down from the sky onto the cursor; it explodes on impact.</summary>
	public class MeteorDice : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 220;
			Item.knockBack = 6f;
			Item.useTime = Item.useAnimation = 24;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.shoot = ModContent.ProjectileType<MeteorDie>();
			Item.shootSpeed = 18f;
			Item.width = 38;
			Item.height = 36;
			Item.UseSound = SoundID.Item88;
			Item.value = Item.sellPrice(gold: 20);
			Item.rare = ItemRarityID.Red;
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			Vector2 target = Main.MouseWorld;
			Vector2 start = target + new Vector2(Main.rand.Next(-200, 200), -700);
			Projectile.NewProjectile(source, start, (target - start).SafeNormalize(Vector2.UnitY) * Item.shootSpeed, type, damage, knockback, player.whoAmI, target.Y);
			return false;
		}
	}

	/// <summary>Tier 4: a slot-machine blaster firing two reel symbols; rarer symbols hit harder.</summary>
	public class InfinitySlot : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 110;
			Item.knockBack = 3f;
			Item.useTime = Item.useAnimation = 12;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.noUseGraphic = false;
			Item.UseSound = SoundID.Item11;
			Item.shoot = ModContent.ProjectileType<SymbolShot>();
			Item.shootSpeed = 17f;
			Item.width = 46;
			Item.height = 32;
			Item.value = Item.sellPrice(gold: 20);
			Item.rare = ItemRarityID.Red;
		}

		public override Vector2? HoldoutOffset() => new Vector2(-6f, 0f);

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			for (int k = 0; k < 2; k++) {
				int symbol = (int)Common.Games.Slots.SlotRules.Roll(Main.rand);
				Projectile.NewProjectile(source, position, velocity.RotatedByRandom(0.06f), type, damage, knockback, player.whoAmI, symbol);
			}
			return false;
		}
	}
}

namespace CasinoWorld.Content.Items.Armor
{
	/// <summary>
	/// Tier 3 (post-Plantera) Gambler armour, crafted at the upgrade machine.
	/// Defence 46, +20 % gambler damage, Luck 7 + 3 from the set (10 in total).
	/// Set bonus: jackpots release two homing spectral cards.
	/// </summary>
	[AutoloadEquip(EquipType.Head)]
	public class SpectralFedora : ModItem, IGamblerUpgradeable
	{
		public override void SetStaticDefaults() => ArmorIDs.Head.Sets.DrawHatHair[Item.headSlot] = true;

		public override void SetDefaults()
		{
			Item.width = 34;
			Item.height = 24;
			Item.defense = 14;
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.sellPrice(gold: 6);
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 2;
			player.GetDamage<GamblerDamageClass>() += 0.06f;
		}

		public override bool IsArmorSet(Item head, Item body, Item legs)
			=> body.type == ModContent.ItemType<SpectralCoat>() && legs.type == ModContent.ItemType<SpectralLoafers>();

		public override void UpdateArmorSet(Player player)
		{
			player.setBonus = Language.GetTextValue("Mods.CasinoWorld.Items.SpectralFedora.SetBonus");
			GamblerPlayer gambler = player.GetModPlayer<GamblerPlayer>();
			gambler.Luck += 3;
			gambler.SpectralSet = true;
		}

		public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.SpectreBar, 12).AddIngredient(ModContent.ItemType<LoyaltyToken>(), 100)
			.AddTile(ModContent.TileType<UpgradeMachine>()).Register();
	}

	[AutoloadEquip(EquipType.Body)]
	public class SpectralCoat : ModItem, IGamblerUpgradeable
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 30;
			Item.defense = 18;
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.sellPrice(gold: 8);
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 3;
			player.GetDamage<GamblerDamageClass>() += 0.08f;
		}

		public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.SpectreBar, 18).AddIngredient(ModContent.ItemType<LoyaltyToken>(), 150)
			.AddTile(ModContent.TileType<UpgradeMachine>()).Register();
	}

	[AutoloadEquip(EquipType.Legs)]
	public class SpectralLoafers : ModItem, IGamblerUpgradeable
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 22;
			Item.defense = 14;
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.sellPrice(gold: 6);
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 2;
			player.GetDamage<GamblerDamageClass>() += 0.06f;
			player.moveSpeed += 0.08f;
		}

		public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.SpectreBar, 14).AddIngredient(ModContent.ItemType<LoyaltyToken>(), 100)
			.AddTile(ModContent.TileType<UpgradeMachine>()).Register();
	}

	/// <summary>
	/// Tier 4 (post-Moon Lord) Gambler armour. Defence 76, +36 % gambler damage, Luck 10 + 3 (13).
	/// Set bonus: jackpots call down a meteor die on the target.
	/// </summary>
	[AutoloadEquip(EquipType.Head)]
	public class CelestialFedora : ModItem, IGamblerUpgradeable
	{
		public override void SetStaticDefaults() => ArmorIDs.Head.Sets.DrawHatHair[Item.headSlot] = true;

		public override void SetDefaults()
		{
			Item.width = 34;
			Item.height = 24;
			Item.defense = 22;
			Item.rare = ItemRarityID.Red;
			Item.value = Item.sellPrice(gold: 12);
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 3;
			player.GetDamage<GamblerDamageClass>() += 0.12f;
		}

		public override bool IsArmorSet(Item head, Item body, Item legs)
			=> body.type == ModContent.ItemType<CelestialCoat>() && legs.type == ModContent.ItemType<CelestialLoafers>();

		public override void UpdateArmorSet(Player player)
		{
			player.setBonus = Language.GetTextValue("Mods.CasinoWorld.Items.CelestialFedora.SetBonus");
			GamblerPlayer gambler = player.GetModPlayer<GamblerPlayer>();
			gambler.Luck += 3;
			gambler.CelestialSet = true;
		}

		public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.LunarBar, 8).AddIngredient(ItemID.FragmentNebula, 5).AddIngredient(ItemID.FragmentStardust, 5)
			.AddIngredient(ModContent.ItemType<LoyaltyToken>(), 200).AddTile(ModContent.TileType<UpgradeMachine>()).Register();
	}

	[AutoloadEquip(EquipType.Body)]
	public class CelestialCoat : ModItem, IGamblerUpgradeable
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 30;
			Item.defense = 30;
			Item.rare = ItemRarityID.Red;
			Item.value = Item.sellPrice(gold: 16);
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 4;
			player.GetDamage<GamblerDamageClass>() += 0.14f;
		}

		public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.LunarBar, 16).AddIngredient(ItemID.FragmentSolar, 8).AddIngredient(ItemID.FragmentVortex, 8)
			.AddIngredient(ModContent.ItemType<LoyaltyToken>(), 300).AddTile(ModContent.TileType<UpgradeMachine>()).Register();
	}

	[AutoloadEquip(EquipType.Legs)]
	public class CelestialLoafers : ModItem, IGamblerUpgradeable
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 22;
			Item.defense = 24;
			Item.rare = ItemRarityID.Red;
			Item.value = Item.sellPrice(gold: 12);
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 3;
			player.GetDamage<GamblerDamageClass>() += 0.10f;
			player.moveSpeed += 0.12f;
		}

		public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.LunarBar, 12).AddIngredient(ItemID.FragmentNebula, 6).AddIngredient(ItemID.FragmentStardust, 6)
			.AddIngredient(ModContent.ItemType<LoyaltyToken>(), 200).AddTile(ModContent.TileType<UpgradeMachine>()).Register();
	}
}
