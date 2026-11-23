using System;
using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Gambler;
using CasinoWorld.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items.Weapons
{
	/// <summary>
	/// Lucky Penny (very early pre-hardmode, copper or tin): flips a coin. Heads, it splits in two on its
	/// first hit. DPS sits with copper / tin tier weapons.
	/// </summary>
	public class LuckyPenny : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 10;
			Item.knockBack = 2f;
			Item.useTime = Item.useAnimation = 20;
			Item.shoot = ModContent.ProjectileType<PennyCoin>();
			Item.shootSpeed = 10f;
			Item.UseSound = CasinoSounds.ChipFlick with { Pitch = 0.4f };
			Item.value = Item.sellPrice(silver: 20);
			Item.rare = ItemRarityID.White;
		}

		public override void AddRecipes()
		{
			foreach (int bar in new[] { ItemID.CopperBar, ItemID.TinBar })
				CreateRecipe().AddIngredient(bar, 8).AddIngredient(ModContent.ItemType<LoyaltyToken>(), 5).AddTile(TileID.Anvils).Register();
		}
	}

	/// <summary>
	/// Hellfire Dice (hellstone tier): two burning dice per throw that set enemies on fire and leave
	/// embers where they bounce. Sits with the Molten / Hellstone weapons before the Wall of Flesh.
	/// </summary>
	public class HellfireDice : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 24;
			Item.knockBack = 4f;
			Item.useTime = Item.useAnimation = 26;
			Item.shoot = ModContent.ProjectileType<HellfireDie>();
			Item.shootSpeed = 9.5f;
			Item.UseSound = CasinoSounds.DiceThrow;
			Item.value = Item.sellPrice(gold: 1, silver: 20);
			Item.rare = ItemRarityID.Orange;
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			for (int k = 0; k < 2; k++)
				Projectile.NewProjectile(source, position, velocity.RotatedBy((k - 0.5f) * 0.2f) * (1f - k * 0.1f), type, damage, knockback, player.whoAmI);
			return false;
		}

		public override void AddRecipes()
			=> CreateRecipe().AddIngredient(ItemID.HellstoneBar, 15).AddIngredient(ModContent.ItemType<LoyaltyToken>(), 30).AddTile(TileID.Anvils).Register();
	}
}

namespace CasinoWorld.Content.Projectiles
{
	/// <summary>A flipped coin: heads (ai[0] = 0, chosen at random on spawn) splits into two half coins on the first hit.</summary>
	public class PennyCoin : ModProjectile
	{
		public override void SetStaticDefaults()
		{
			Main.projFrames[Type] = 4;
			CasinoFx.EnableTrail(Type, 6);
		}

		public override void SetDefaults()
		{
			Projectile.width = Projectile.height = 10;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = 1;
			Projectile.timeLeft = 120;
		}

		public override void AI()
		{
			if (Projectile.localAI[0] == 0f) {
				Projectile.localAI[0] = 1f;
				if (Projectile.ai[1] == 0f && Projectile.owner == Main.myPlayer) {
					Projectile.ai[0] = Main.rand.Next(2);   // 0 heads, 1 tails
					Projectile.netUpdate = true;
				}
			}
			if (++Projectile.ai[2] > 25)
				Projectile.velocity.Y += 0.2f;
			if (++Projectile.frameCounter >= 3) {
				Projectile.frameCounter = 0;
				Projectile.frame = (Projectile.frame + 1) % 4;
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Projectile.ai[0] != 0f || Projectile.ai[1] != 0f || Projectile.owner != Main.myPlayer)
				return;
			CombatText.NewText(target.Hitbox, new Color(255, 220, 90), "Heads!");
			for (int side = -1; side <= 1; side += 2)
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(side * 5f, -4f), Type, Projectile.damage / 2, 1f, Projectile.owner, 1f, 1f);
		}

		public override void OnKill(int timeLeft) => CasinoFx.ChipImpact(Projectile.Center, new Color(230, 150, 80), Projectile.ai[1] == 0f);

		public override bool PreDraw(ref Color lightColor)
		{
			CasinoFx.Draw(Projectile, new Color(255, 190, 110), Color.Transparent, Projectile.ai[1] == 1f ? 0.75f : 1f);
			return false;
		}
	}

	/// <summary>Burning die: bounces twice, sets enemies on fire, drops embers on each bounce.</summary>
	public class HellfireDie : ModProjectile
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/ExplodingDie";

		public override void SetStaticDefaults() => CasinoFx.EnableTrail(Type, 6);

		public override void SetDefaults()
		{
			Projectile.width = Projectile.height = 12;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = 2;
			Projectile.timeLeft = 200;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
		}

		public override void AI()
		{
			Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.3f, 12f);
			Projectile.rotation += Projectile.velocity.X * 0.07f;
			Lighting.AddLight(Projectile.Center, 0.6f, 0.3f, 0.05f);
			if (Main.rand.NextBool(2)) {
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0, -1f, 0, default, 1.2f);
				d.noGravity = true;
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.OnFire, 180);

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (++Projectile.localAI[0] > 2)
				return true;
			SoundEngine.PlaySound(CasinoSounds.DiceBounce, Projectile.Center);
			for (int k = 0; k < 6; k++)
				Dust.NewDustPerfect(Projectile.Bottom, DustID.Torch, Main.rand.NextVector2Circular(2, 1) - Vector2.UnitY, 0, default, 1.4f);
			if (Projectile.velocity.X != oldVelocity.X)
				Projectile.velocity.X = -oldVelocity.X * 0.8f;
			if (Projectile.velocity.Y != oldVelocity.Y)
				Projectile.velocity.Y = -oldVelocity.Y * 0.65f;
			return false;
		}

		public override void OnKill(int timeLeft) => CasinoFx.Explosion(Projectile.Center, 40, new Color(255, 150, 50), new Color(255, 230, 150), 0f);

		public override bool PreDraw(ref Color lightColor)
		{
			CasinoFx.Draw(Projectile, new Color(255, 120, 40), new Color(255, 100, 30) * 0.6f, 1f, new Color(255, 190, 140));
			return false;
		}
	}
}
