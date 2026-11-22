using CasinoWorld.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items.Weapons
{
	// Hardmode Gambler weapons from the underground casino. First-pass numbers aimed at
	// the mechanical bosses (early hardmode); the Jackpot/Bust roll averages to 1.0.

	/// <summary>Throws a die that bounces and explodes when it stops.</summary>
	public class SnakeEyes : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 44;
			Item.knockBack = 5f;
			Item.useTime = Item.useAnimation = 24;
			Item.shoot = ModContent.ProjectileType<ExplodingDie>();
			Item.shootSpeed = 10f;
			Item.value = Item.sellPrice(gold: 5);
			Item.rare = ItemRarityID.Pink;
		}
	}

	/// <summary>Throws five homing cards in a wide fan.</summary>
	public class RoyalFlush : GamblerWeapon
	{
		private const int Cards = 5;
		private const float Spread = 0.16f;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 21;
			Item.knockBack = 2f;
			Item.useTime = Item.useAnimation = 24;
			Item.shoot = ModContent.ProjectileType<RoyalCard>();
			Item.shootSpeed = 12f;
			Item.value = Item.sellPrice(gold: 5);
			Item.rare = ItemRarityID.Pink;
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

	/// <summary>Fires a heavy chip that ricochets to two more enemies.</summary>
	public class JackpotCannon : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 58;
			Item.knockBack = 6f;
			Item.useTime = Item.useAnimation = 30;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.noUseGraphic = false;
			Item.width = 36;
			Item.height = 22;
			Item.shoot = ModContent.ProjectileType<JackpotChip>();
			Item.shootSpeed = 15f;
			Item.UseSound = SoundID.Item61;
			Item.value = Item.sellPrice(gold: 6);
			Item.rare = ItemRarityID.LightPurple;
		}

		public override Vector2? HoldoutOffset() => new Vector2(-4f, 0f);
	}
}
