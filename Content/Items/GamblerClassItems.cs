using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Gambler;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items.Accessories
{
	/// <summary>The Gambler's class emblem: +15 % gambler damage, like the Warrior / Ranger / Sorcerer / Summoner emblems.</summary>
	public class GamblerEmblem : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = Item.height = 28;
			Item.accessory = true;
			Item.rare = ItemRarityID.LightRed;
			Item.value = Item.sellPrice(gold: 2);
		}

		public override void SetStaticDefaults()
		{
			// Joins the vanilla emblem shimmer cycle (... Summoner -> Gambler -> Warrior ...).
			ItemID.Sets.ShimmerTransformToItem[ItemID.SummonerEmblem] = Type;
			ItemID.Sets.ShimmerTransformToItem[Type] = ItemID.WarriorEmblem;
		}

		public override void UpdateAccessory(Player player, bool hideVisual) => player.GetDamage<GamblerDamageClass>() += 0.15f;

		public override void AddRecipes()
		{
			CreateRecipe().AddIngredient(ItemID.WarriorEmblem).AddIngredient(ModContent.ItemType<LoyaltyToken>(), 30).AddTile(TileID.TinkerersWorkbench).Register();
			CreateRecipe().AddIngredient(ItemID.RangerEmblem).AddIngredient(ModContent.ItemType<LoyaltyToken>(), 30).AddTile(TileID.TinkerersWorkbench).Register();
			CreateRecipe().AddIngredient(ItemID.SorcererEmblem).AddIngredient(ModContent.ItemType<LoyaltyToken>(), 30).AddTile(TileID.TinkerersWorkbench).Register();
			CreateRecipe().AddIngredient(ItemID.SummonerEmblem).AddIngredient(ModContent.ItemType<LoyaltyToken>(), 30).AddTile(TileID.TinkerersWorkbench).Register();
		}
	}

	/// <summary>The Wall of Flesh can drop the Gambler Emblem in place of a vanilla emblem.</summary>
	public class GamblerEmblemDrop : GlobalNPC
	{
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.WallofFlesh;

		public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
		{
			var notExpert = new LeadingConditionRule(new Conditions.NotExpert());
			notExpert.OnSuccess(ItemDropRule.Common(ModContent.ItemType<GamblerEmblem>(), 5));
			npcLoot.Add(notExpert);
		}
	}

	public class GamblerEmblemBag : GlobalItem
	{
		public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.WallOfFleshBossBag;

		public override void ModifyItemLoot(Item item, ItemLoot itemLoot) => itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<GamblerEmblem>(), 5));
	}
}

namespace CasinoWorld.Content.Projectiles
{
	/// <summary>All In opening burst: one hit on everything within 15 tiles, a ring of cards and chips.</summary>
	public class AllInBurst : ModProjectile
	{
		private const int Radius = 240;

		public override string Texture => "CasinoWorld/Content/Projectiles/HighRollerChip";

		public override void SetDefaults()
		{
			Projectile.width = Projectile.height = Radius * 2;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.timeLeft = 4;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
			Projectile.alpha = 255;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
			=> Vector2.Distance(Projectile.Center, targetHitbox.ClosestPointInRect(Projectile.Center)) < Radius;

		public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
		{
			if (Main.dedServ)
				return;
			SoundEngine.PlaySound(CasinoSounds.Jackpot with { Volume = 0.8f }, Projectile.Center);
			SoundEngine.PlaySound(CasinoSounds.CardShuffle, Projectile.Center);
			CasinoFx.Ring(Projectile.Center, new Color(255, 70, 70), 32, 12f, 1.6f);
			CasinoFx.Ring(Projectile.Center, new Color(255, 220, 90), 24, 8f, 1.4f);
			for (int k = 0; k < 30; k++)
				Dust.NewDustPerfect(Projectile.Center, CasinoFx.ShardType, Main.rand.NextVector2CircularEdge(8, 8) * Main.rand.NextFloat(0.5f, 1f), 0, k % 2 == 0 ? new Color(220, 40, 50) : Color.White, 1.2f);
			Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center, Main.rand.NextVector2Unit(), 6f, 8f, 20, 1000f, "AllIn"));
			BossTitle.Flash(new Color(255, 200, 80));
		}

		public override bool PreDraw(ref Color lightColor) => false;
	}
}
