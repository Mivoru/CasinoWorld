using CasinoWorld.Common.Gambler;
using CasinoWorld.Common.Systems;
using CasinoWorld.Common.Upgrades;
using CasinoWorld.Content.Items.Accessories;
using CasinoWorld.Content.Items.Crates;
using CasinoWorld.Content.Items.Weapons;
using CasinoWorld.Content.NPCs.Boss;
using CasinoWorld.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items
{
	/// <summary>Summons The House at night.</summary>
	public class GoldenChip : ModItem
	{
		public override void SetStaticDefaults()
		{
			ItemID.Sets.SortingPriorityBossSpawns[Type] = 12;
			Item.ResearchUnlockCount = 3;
		}

		public override void SetDefaults()
		{
			Item.width = Item.height = 24;
			Item.maxStack = 20;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = Item.useAnimation = 30;
			Item.consumable = true;
			Item.rare = ItemRarityID.Orange;
			Item.value = Item.buyPrice(gold: 2);
		}

		public override bool CanUseItem(Player player)
			=> !Main.dayTime && !CasinoTravel.InCasino && !NPC.AnyNPCs(ModContent.NPCType<TheWildCard>());

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer)
				return true;
			SoundEngine.PlaySound(SoundID.Roar, player.position);
			int type = ModContent.NPCType<TheWildCard>();
			if (Main.netMode != NetmodeID.MultiplayerClient)
				NPC.SpawnOnPlayer(player.whoAmI, type);
			else
				NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: player.whoAmI, number2: type);
			return true;
		}

		public override void AddRecipes()
		{
			foreach (int bar in new[] { ItemID.GoldBar, ItemID.PlatinumBar }) {
				CreateRecipe()
					.AddIngredient(ModContent.ItemType<LoyaltyToken>(), 50)
					.AddIngredient(bar, 10)
					.AddIngredient(ItemID.Bone, 5)
					.AddTile(TileID.WorkBenches)
					.Register();
			}
		}
	}

	/// <summary>Expert treasure bag of The House.</summary>
	public class WildCardBag : ModItem
	{
		public override void SetStaticDefaults()
		{
			ItemID.Sets.BossBag[Type] = true;
			ItemID.Sets.PreHardmodeLikeBossBag[Type] = true;
			Item.ResearchUnlockCount = 3;
		}

		public override void SetDefaults()
		{
			Item.maxStack = Item.CommonMaxStack;
			Item.consumable = true;
			Item.width = 32;
			Item.height = 34;
			Item.rare = ItemRarityID.Purple;
			Item.expert = true;
		}

		public override bool CanRightClick() => true;

		public override void ModifyItemLoot(ItemLoot itemLoot)
		{
			itemLoot.Add(ItemDropRule.OneFromOptions(1, ModContent.ItemType<JackpotLever>(), ModContent.ItemType<DealersHand>(), ModContent.ItemType<WheelOfFortune>()));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 1, 120, 180));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<LuckyCrate>(), 1, 3, 4));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<GoldenChip>(), 2));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<HouseEdge>()));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<Vanity.WildCardMask>(), 7));
			itemLoot.Add(ItemDropRule.CoinsBasedOnNPCValue(ModContent.NPCType<TheWildCard>()));
		}
	}
}

namespace CasinoWorld.Content.Items.Weapons
{
	/// <summary>
	/// Heavy lever hammer. Every third hit spins mini reels and adds a random effect:
	/// fire, a small heal, a burst of coins worth of damage (explosion) or a gold sparkle shower.
	/// </summary>
	public class JackpotLever : ModItem, IGamblerUpgradeable
	{
		private int hits;

		public override void SetDefaults()
		{
			Item.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Item.damage = 26;
			Item.knockBack = 7f;
			Item.useTime = Item.useAnimation = 26;
			Item.useStyle = ItemUseStyleID.Swing;
			Item.autoReuse = true;
			Item.width = Item.height = 40;
			Item.UseSound = SoundID.Item1;
			Item.value = Item.sellPrice(gold: 3);
			Item.rare = ItemRarityID.Orange;
		}

		public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (++hits < 3)
				return;
			hits = 0;
			switch (Main.rand.Next(4)) {
				case 0:
					target.AddBuff(BuffID.OnFire, 300);
					break;
				case 1:
					player.Heal(6);
					break;
				case 2:
					Projectile.NewProjectile(player.GetSource_ItemUse(Item), target.Center, Vector2.Zero, ModContent.ProjectileType<ExplodingDie>(), damageDone / 2, 4f, player.whoAmI);
					break;
				default:
					for (int k = 0; k < 12; k++)
						Dust.NewDust(target.position, target.width, target.height, DustID.GoldCoin, Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(-4, -1));
					target.AddBuff(BuffID.Midas, 600);
					break;
			}
			SoundEngine.PlaySound(SoundID.CoinPickup, target.Center);
			SoundEngine.PlaySound(Common.CasinoSounds.LeverPull with { Volume = 0.6f }, target.Center);
			Common.Effects.CasinoFx.Ring(target.Center, new Color(255, 220, 90), 12, 4f, 1.2f);
			CombatText.NewText(target.Hitbox, new Color(255, 215, 60), "7 7 7");
		}

		public override void MeleeEffects(Player player, Rectangle hitbox)
		{
			if (Main.rand.NextBool(3))
				Common.Effects.CasinoFx.Spark(hitbox.Center.ToVector2() + Main.rand.NextVector2Circular(hitbox.Width / 2f, hitbox.Height / 2f), Vector2.Zero, new Color(255, 210, 90), 0.8f);
		}
	}

	/// <summary>Deals a fast stream of cards; every eighth throw is a whole fan.</summary>
	public class DealersHand : GamblerWeapon
	{
		private int throws;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 8;
			Item.knockBack = 1f;
			Item.useTime = Item.useAnimation = 10;
			Item.shoot = ModContent.ProjectileType<ThrownCard>();
			Item.shootSpeed = 14f;
			Item.UseSound = Common.CasinoSounds.CardThrow;
			Item.width = 32;
			Item.height = 34;
			Item.value = Item.sellPrice(gold: 3);
			Item.rare = ItemRarityID.Orange;
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			if (++throws % 8 != 0)
				return true;
			for (int k = -3; k <= 3; k++)
				Projectile.NewProjectile(source, position, velocity.RotatedBy(k * 0.12f), type, damage, knockback, player.whoAmI);
			return false;
		}
	}

	/// <summary>Roulette disc thrown like a boomerang; it explodes red or black when it comes back.</summary>
	public class WheelOfFortune : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 18;
			Item.knockBack = 4f;
			Item.useTime = Item.useAnimation = 22;
			Item.shoot = ModContent.ProjectileType<WheelOfFortuneDisc>();
			Item.shootSpeed = 12f;
			Item.width = Item.height = 30;
			Item.value = Item.sellPrice(gold: 3);
			Item.rare = ItemRarityID.Orange;
		}

		public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] < 1;
	}
}

namespace CasinoWorld.Content.Items.Accessories
{
	/// <summary>Expert: +5 % jackpot chance, and the first bust every 10 seconds becomes a normal hit.</summary>
	public class HouseEdge : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = Item.height = 26;
			Item.accessory = true;
			Item.expert = true;
			Item.rare = ItemRarityID.Expert;
			Item.value = Item.sellPrice(gold: 4);
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			GamblerPlayer gambler = player.GetModPlayer<GamblerPlayer>();
			gambler.BonusJackpotChance += 0.05f;
			gambler.HouseEdge = true;
		}
	}
}

namespace CasinoWorld.Content.Projectiles
{
	public class WheelOfFortuneDisc : ModProjectile
	{
		public override void SetStaticDefaults() => Common.Effects.CasinoFx.EnableTrail(Type, 8);

		public override bool PreDraw(ref Color lightColor)
		{
			Common.Effects.CasinoFx.Draw(Projectile, new Color(255, 80, 80), new Color(255, 200, 80) * 0.5f);
			return false;
		}

		public override void AI()
		{
			if (Main.rand.NextBool(3))
				Common.Effects.CasinoFx.Spark(Projectile.Center + Main.rand.NextVector2Circular(10, 10), Vector2.Zero, Main.rand.NextBool() ? new Color(255, 70, 70) : new Color(200, 200, 210), 0.7f);
		}

		public override void SetDefaults()
		{
			Projectile.CloneDefaults(ProjectileID.WoodenBoomerang);
			AIType = ProjectileID.WoodenBoomerang;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.width = Projectile.height = 20;
		}

		public override void OnKill(int timeLeft)
		{
			bool red = Main.rand.NextBool();
			for (int k = 0; k < 18; k++)
				Dust.NewDust(Projectile.Center - new Vector2(24), 48, 48, red ? DustID.RedTorch : DustID.Smoke, Main.rand.NextFloat(-4, 4), Main.rand.NextFloat(-4, 4), 0, default, 1.4f);
			Common.Effects.CasinoFx.Explosion(Projectile.Center, 80, red ? new Color(255, 70, 70) : new Color(120, 120, 140), new Color(255, 220, 120), 1.5f);
			SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f }, Projectile.Center);
			if (Projectile.owner != Main.myPlayer)
				return;
			foreach (NPC npc in Main.ActiveNPCs) {
				if (npc.CanBeChasedBy(Projectile) && npc.Distance(Projectile.Center) < 80f)
					Main.player[Projectile.owner].ApplyDamageToNPC(npc, Projectile.damage / 2, 0f, 0, false, ModContent.GetInstance<GamblerDamageClass>());
			}
		}
	}
}
