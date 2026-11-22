using CasinoWorld.Common.Gambler;
using CasinoWorld.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items.Weapons
{
	/// <summary>
	/// Shared setup for thrown Gambler weapons. Pre-hardmode set, tuned to sit between
	/// the Eye of Cthulhu and Skeletron (average damage per second similar to vanilla
	/// weapons of that stage; the Jackpot/Bust roll averages to 1.0).
	/// </summary>
	public abstract class GamblerWeapon : ModItem, Common.Upgrades.IGamblerUpgradeable
	{
		public override void SetDefaults()
		{
			Item.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Item.useStyle = ItemUseStyleID.Swing;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.autoReuse = true;
			Item.UseSound = SoundID.Item1;
			Item.width = 24;
			Item.height = 24;
		}
	}

	/// <summary>Throws a die that bounces along the ground. 3 bounces, pierces 3 enemies.</summary>
	public class LoadedDice : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 17;
			Item.knockBack = 4f;
			Item.useTime = Item.useAnimation = 24;
			Item.shoot = ModContent.ProjectileType<LoadedDie>();
			Item.shootSpeed = 9f;
			Item.value = Item.sellPrice(gold: 1);
			Item.rare = ItemRarityID.Green;
		}
	}

	/// <summary>Throws three cards in a fan.</summary>
	public class CardDeck : GamblerWeapon
	{
		private const int Cards = 3;
		private const float Spread = 0.18f;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 7;
			Item.knockBack = 1.5f;
			Item.useTime = Item.useAnimation = 20;
			Item.shoot = ModContent.ProjectileType<ThrownCard>();
			Item.shootSpeed = 12f;
			Item.value = Item.sellPrice(gold: 1);
			Item.rare = ItemRarityID.Green;
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			for (int k = 0; k < Cards; k++) {
				float angle = (k - (Cards - 1) / 2f) * Spread;
				Projectile.NewProjectile(source, position, velocity.RotatedBy(angle), type, damage, knockback, player.whoAmI);
			}
			return false;
		}
	}

	/// <summary>Flicks a chip that ricochets to the next enemy once after a hit.</summary>
	public class HighRoller : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 22;
			Item.knockBack = 3f;
			Item.useTime = Item.useAnimation = 26;
			Item.shoot = ModContent.ProjectileType<HighRollerChip>();
			Item.shootSpeed = 13f;
			Item.value = Item.sellPrice(gold: 1, silver: 50);
			Item.rare = ItemRarityID.Orange;
			Item.UseSound = SoundID.Item7;
		}
	}
}
