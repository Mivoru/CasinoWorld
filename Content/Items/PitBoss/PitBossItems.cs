using System;
using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Gambler;
using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Items.Crates;
using CasinoWorld.Content.Items.Weapons;
using CasinoWorld.Content.NPCs.Boss;
using CasinoWorld.Content.Projectiles;
using CasinoWorld.Content.Subworlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using SubworldLibrary;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CasinoWorld.Content.Items.PitBoss
{
	/// <summary>Summons The Pit Boss. Works only inside the High Rollers casino, after any mechanical boss.</summary>
	public class VaultKey : ModItem
	{
		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 3;
		}

		public override void SetDefaults()
		{
			Item.width = 20;
			Item.height = 32;
			Item.maxStack = 20;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = Item.useAnimation = 30;
			Item.consumable = true;
			Item.rare = ItemRarityID.Pink;
			Item.value = Item.buyPrice(gold: 10);
		}

		public override bool CanUseItem(Player player)
			=> SubworldSystem.Current is UndergroundCasino && NPC.downedMechBossAny && !NPC.AnyNPCs(ModContent.NPCType<ThePitBoss>());

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer)
				return true;
			SoundEngine.PlaySound(CasinoSounds.VaultDoor, player.position);
			int type = ModContent.NPCType<ThePitBoss>();
			if (Main.netMode != NetmodeID.MultiplayerClient)
				NPC.SpawnOnPlayer(player.whoAmI, type);
			else
				NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: player.whoAmI, number2: type);
			return true;
		}

		public override void AddRecipes()
		{
			foreach (int soul in new[] { ItemID.SoulofFright, ItemID.SoulofMight, ItemID.SoulofSight }) {
				CreateRecipe()
					.AddIngredient(ItemID.HallowedBar, 6)
					.AddIngredient(soul, 3)
					.AddIngredient(ModContent.ItemType<LoyaltyToken>(), 60)
					.AddTile(TileID.MythrilAnvil)
					.Register();
			}
		}
	}

	public class PitBossBag : ModItem
	{
		public override void SetStaticDefaults()
		{
			ItemID.Sets.BossBag[Type] = true;
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
			itemLoot.Add(ItemDropRule.OneFromOptions(1, ModContent.ItemType<CroupiersRake>(), ModContent.ItemType<BankBreaker>(), ModContent.ItemType<VaultBuster>()));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 1, 200, 280));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<HighRollerCrate>(), 1, 3, 4));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<InsiderInfo>()));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<PitBossMask>(), 7));
			itemLoot.Add(ItemDropRule.CoinsBasedOnNPCValue(ModContent.NPCType<ThePitBoss>()));
		}
	}

	/// <summary>Expert: +6 % jackpot chance and the Pot fills 30 % faster.</summary>
	public class InsiderInfo : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = Item.height = 28;
			Item.accessory = true;
			Item.expert = true;
			Item.rare = ItemRarityID.Expert;
			Item.value = Item.sellPrice(gold: 6);
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetModPlayer<GamblerPlayer>().BonusJackpotChance += 0.06f;
			player.GetModPlayer<GamblerPot>().GainMultiplier += 0.3f;
		}
	}

	[AutoloadEquip(EquipType.Head)]
	public class PitBossMask : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = Item.height = 28;
			Item.vanity = true;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.sellPrice(gold: 1);
		}
	}

	public class PitBossTrophy : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<PitBossTrophyTile>());
			Item.width = Item.height = 32;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.buyPrice(gold: 1);
		}
	}

	public class PitBossTrophyTile : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.FramesOnKillWall[Type] = true;
			TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3Wall);
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(120, 85, 60), Language.GetText("MapObject.Trophy"));
			DustType = DustID.WoodFurniture;
		}
	}

	public class PitBossRelicItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<PitBossRelic>());
			Item.width = 30;
			Item.height = 40;
			Item.rare = ItemRarityID.Master;
			Item.master = true;
			Item.value = Item.buyPrice(gold: 5);
		}
	}

	/// <summary>Master mode relic: golden Pit Boss on the vanilla relic pedestal.</summary>
	public class PitBossRelic : ModTile
	{
		private const int FrameWidth = 18 * 3, FrameHeight = 18 * 4;
		private Asset<Texture2D> statue;

		public override string Texture => "Terraria/Images/Tiles_" + TileID.MasterTrophyBase;

		public override void Load()
		{
			if (!Main.dedServ)
				statue = ModContent.Request<Texture2D>("CasinoWorld/Content/Items/PitBoss/PitBossRelicStatue");
		}

		public override void Unload() => statue = null;

		public override void SetStaticDefaults()
		{
			Main.tileShine[Type] = 400;
			Main.tileFrameImportant[Type] = true;
			TileID.Sets.InteractibleByNPCs[Type] = true;
			TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
			TileObjectData.newTile.LavaDeath = false;
			TileObjectData.newTile.DrawYOffset = 2;
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(233, 207, 94), Language.GetText("MapObject.Relic"));
		}

		public override bool CreateDust(int i, int j, ref int type) => false;

		public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
		{
			if (drawData.tileFrameX % FrameWidth == 0 && drawData.tileFrameY % FrameHeight == 0)
				Main.instance.TilesRenderer.AddSpecialLegacyPoint(i, j);
		}

		public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			Texture2D texture = statue.Value;
			Vector2 worldPos = new Point(i, j).ToWorldCoordinates(24f, 64f);
			Color color = Lighting.GetColor(i, j);
			Vector2 origin = new(texture.Width / 2f, texture.Height);
			float offset = (float)Math.Sin(Main.GlobalTimeWrappedHourly * MathHelper.TwoPi / 5f);
			Vector2 drawPos = worldPos + offScreen - Main.screenPosition + new Vector2(0f, -40f + offset * 4f);
			spriteBatch.Draw(texture, drawPos, null, color, 0f, origin, 1f, SpriteEffects.None, 0f);
			float scale = (float)Math.Sin(Main.GlobalTimeWrappedHourly * MathHelper.TwoPi / 2f) * 0.3f + 0.7f;
			Color effect = color;
			effect.A = 0;
			effect *= 0.1f * scale;
			for (float n = 0f; n < 1f; n += 1f / 6f)
				spriteBatch.Draw(texture, drawPos + (MathHelper.TwoPi * n).ToRotationVector2() * (6f + offset * 2f), null, effect, 0f, origin, 1f, SpriteEffects.None, 0f);
		}
	}

	// ------------------------------------------------------------------ weapons (one drops per kill)

	/// <summary>Croupier's Rake: sweeps a fan of three chips that roll along the floor.</summary>
	public class CroupiersRake : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 44;
			Item.knockBack = 4f;
			Item.useTime = Item.useAnimation = 24;
			Item.shoot = ModContent.ProjectileType<RakeChip>();
			Item.shootSpeed = 11f;
			Item.UseSound = CasinoSounds.ChipStack;
			Item.width = Item.height = 40;
			Item.value = Item.sellPrice(gold: 6);
			Item.rare = ItemRarityID.Pink;
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			for (int k = -1; k <= 1; k++)
				Projectile.NewProjectile(source, position, velocity.RotatedBy(k * 0.18f) * (1f - Math.Abs(k) * 0.08f), type, damage, knockback, player.whoAmI, k + 1);
			return false;
		}
	}

	/// <summary>Bank Breaker: a chip shotgun, five chips per blast.</summary>
	public class BankBreaker : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 30;
			Item.knockBack = 3f;
			Item.useTime = Item.useAnimation = 34;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.noMelee = true;
			Item.shoot = ModContent.ProjectileType<RevolverChip>();
			Item.shootSpeed = 14f;
			Item.UseSound = SoundID.Item36;
			Item.width = 50;
			Item.height = 22;
			Item.value = Item.sellPrice(gold: 6);
			Item.rare = ItemRarityID.Pink;
		}

		public override Vector2? HoldoutOffset() => new Vector2(-6, 0);

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			for (int k = 0; k < 5; k++)
				Projectile.NewProjectile(source, position, velocity.RotatedByRandom(0.22f) * Main.rand.NextFloat(0.85f, 1.1f), type, damage, knockback, player.whoAmI);
			return false;
		}
	}

	/// <summary>Vault Buster: lobs a gold bar that bursts into a shower of coins.</summary>
	public class VaultBuster : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 70;
			Item.knockBack = 6f;
			Item.useTime = Item.useAnimation = 30;
			Item.shoot = ModContent.ProjectileType<VaultBar>();
			Item.shootSpeed = 12f;
			Item.UseSound = SoundID.Item1;
			Item.width = 32;
			Item.height = 20;
			Item.value = Item.sellPrice(gold: 6);
			Item.rare = ItemRarityID.Pink;
		}
	}
}

namespace CasinoWorld.Content.Projectiles
{
	/// <summary>Rake chip: rolls along the ground after landing. ai[0] colour.</summary>
	public class RakeChip : ModProjectile
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/HighRollerChip";

		private Color Tint => (int)Projectile.ai[0] switch { 0 => new Color(255, 90, 90), 1 => new Color(255, 220, 90), _ => new Color(110, 160, 255) };

		public override void SetStaticDefaults() => CasinoFx.EnableTrail(Type, 8);

		public override void SetDefaults()
		{
			Projectile.width = Projectile.height = 14;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = 3;
			Projectile.timeLeft = 150;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 15;
		}

		public override void AI()
		{
			Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.25f, 12f);
			Projectile.rotation += Projectile.velocity.X * 0.1f;
			if (Main.rand.NextBool(3))
				CasinoFx.Spark(Projectile.Center, -Projectile.velocity * 0.1f, Tint, 0.6f);
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (Projectile.velocity.X != oldVelocity.X)
				return true;   // a wall stops the roll
			Projectile.velocity.Y = 0f;
			Projectile.velocity.X = oldVelocity.X * 0.98f;
			return false;
		}

		public override void OnKill(int timeLeft) => CasinoFx.ChipImpact(Projectile.Center, Tint);

		public override bool PreDraw(ref Color lightColor)
		{
			CasinoFx.Draw(Projectile, Tint, Tint * 0.5f, 1f, Tint);
			return false;
		}
	}

	/// <summary>Gold bar thrown in an arc; on any hit it bursts into six coins.</summary>
	public class VaultBar : ModProjectile
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/Boss/GoldBarDrop";

		public override void SetStaticDefaults() => CasinoFx.EnableTrail(Type, 6);

		public override void SetDefaults()
		{
			Projectile.width = 24;
			Projectile.height = 14;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = 1;
			Projectile.timeLeft = 180;
		}

		public override void AI()
		{
			Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.25f, 14f);
			Projectile.rotation += 0.2f * Math.Sign(Projectile.velocity.X);
		}

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Coins with { Volume = 0.8f }, Projectile.Center);
			CasinoFx.Ring(Projectile.Center, new Color(255, 210, 90), 12, 4f, 1.1f);
			if (Projectile.owner != Main.myPlayer)
				return;
			for (int k = 0; k < 6; k++)
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(Main.rand.NextFloat(-5, 5), Main.rand.NextFloat(-7, -3)),
					ModContent.ProjectileType<CoinShrapnel>(), Projectile.damage * 3 / 10, 1f, Projectile.owner);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			CasinoFx.Draw(Projectile, new Color(255, 210, 90), new Color(255, 200, 80) * 0.4f);
			return false;
		}
	}

	/// <summary>Coin from a Vault Buster burst: bounces once, then fades.</summary>
	public class CoinShrapnel : ModProjectile
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/HighRollerChip";

		public override bool PreDraw(ref Color lightColor)
		{
			CasinoFx.Draw(Projectile, new Color(255, 220, 90), Color.Transparent, 0.6f, new Color(255, 220, 90));
			return false;
		}

		public override void SetDefaults()
		{
			Projectile.width = Projectile.height = 10;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = 1;
			Projectile.timeLeft = 90;
		}

		public override void AI()
		{
			Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.3f, 12f);
			Projectile.rotation += 0.3f;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (Projectile.localAI[0]++ > 0)
				return true;
			Projectile.velocity.Y = -oldVelocity.Y * 0.5f;
			return false;
		}

		public override void OnKill(int timeLeft) => CasinoFx.Sparks(Projectile.Center, new Color(255, 220, 90), 3, 1.5f, 0.6f);
	}
}
