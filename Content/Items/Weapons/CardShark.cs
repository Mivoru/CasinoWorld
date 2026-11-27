using CasinoWorld.Common.Effects;
using CasinoWorld.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items.Weapons
{
	/// <summary>
	/// Duke Fishron's Gambler weapon, the step between Plantera's tier and the Moon Lord (vanilla gets the Tsunami and
	/// the Razorblade Typhoon from the same fight). A big homing card that pierces three enemies (each once); every hit lets two small
	/// fins loose. The progression test (tools/selftest/SelfTestProg.cs) showed the Gambler at 2.5-2.9x the time of a
	/// ranger or mage against the Moon Lord without it.
	/// </summary>
	public class CardShark : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 80;
			Item.knockBack = 3.5f;
			Item.useTime = Item.useAnimation = 18;
			Item.shoot = ModContent.ProjectileType<SharkCard>();
			Item.shootSpeed = 14f;
			Item.width = 32;
			Item.height = 32;
			Item.value = Item.sellPrice(gold: 10);
			Item.rare = ItemRarityID.Yellow;
		}
	}

	/// <summary>Duke Fishron drops the Card Shark one time in three, beside his own weapons (the bag in Expert).</summary>
	public class CardSharkDrop : GlobalNPC
	{
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.DukeFishron;

		public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
		{
			var notExpert = new LeadingConditionRule(new Conditions.NotExpert());
			notExpert.OnSuccess(ItemDropRule.Common(ModContent.ItemType<CardShark>(), 3));
			npcLoot.Add(notExpert);
		}
	}

	public class CardSharkBag : GlobalItem
	{
		public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.FishronBossBag;

		public override void ModifyItemLoot(Item item, ItemLoot itemLoot) => itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<CardShark>(), 3));
	}
}

namespace CasinoWorld.Content.Projectiles
{
	/// <summary>
	/// Card Shark shot. ai[0] = 0: the big card, homes in and pierces three times; each hit lets two fins loose (25 %).
	/// ai[0] = 1: a fin, small, homes hard, one hit. Against a wall the big card bursts into its fins, away from it.
	/// </summary>
	public class SharkCard : GamblerShot
	{
		private static readonly Color Sea = new(70, 200, 255);

		public override string Texture => "CasinoWorld/Content/Projectiles/RoyalCard";
		private bool Fin => Projectile.ai[0] == 1f;
		protected override Color Tint => Fin ? new Color(150, 230, 255, 210) : new Color(90, 210, 255);
		protected override float DrawScale => Fin ? 0.8f : 1.5f;
		protected override Color GlowColor => Sea;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 16;
			Projectile.penetrate = 3;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;   // each enemy once: a pass through the Moon Lord's huge body is not three hits
			Projectile.extraUpdates = 1;
		}

		public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
		{
			if (Fin) {
				Projectile.penetrate = 1;
				Projectile.timeLeft = 90;
			}
		}

		public override void AI()
		{
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
			if (++Projectile.localAI[0] > (Fin ? 4 : 8))
				Home(Fin ? 500f : 700f, Fin ? 0.12f : 0.08f);
			Lighting.AddLight(Projectile.Center, 0.15f, 0.45f, 0.7f);
			if (Main.rand.NextBool(Fin ? 5 : 2))
				Dust.NewDustPerfect(Projectile.Center, DustID.DungeonWater, -Projectile.velocity * 0.1f, 100, default, Fin ? 0.8f : 1.1f).noGravity = true;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!Fin)
				Fins(Projectile.Center, Vector2.Zero, 2, 1f);
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			Collision.HitTiles(Projectile.position, oldVelocity, Projectile.width, Projectile.height);
			if (!Fin)
				Fins(Projectile.Center - oldVelocity.SafeNormalize(Vector2.Zero) * 10f, -oldVelocity.SafeNormalize(Vector2.UnitY) * 9f, 3, 0.7f);
			return true;
		}

		public override void OnKill(int timeLeft)
		{
			CasinoFx.CardImpact(Projectile.Center, Projectile.velocity, Sea, !Fin);
			for (int k = 0; k < (Fin ? 4 : 10); k++)
				Dust.NewDustPerfect(Projectile.Center, DustID.DungeonWater, Main.rand.NextVector2Circular(3, 3), 100, default, 1.1f).noGravity = true;
		}

		/// <summary>Small fins: all around after a hit, away from the surface on a wall.</summary>
		private void Fins(Vector2 at, Vector2 away, int count, float share)
		{
			CasinoFx.Ring(at, Sea, 12, 4f, 1f);
			if (Projectile.owner != Main.myPlayer)
				return;
			for (int k = 0; k < count; k++) {
				Vector2 velocity = away == Vector2.Zero ? Main.rand.NextVector2CircularEdge(9f, 9f) : away.RotatedBy((k - (count - 1) / 2f) * 0.6f);
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), at, velocity, Type, (int)(Projectile.damage * 0.25f * share), 1f, Projectile.owner, 1f);
			}
		}
	}
}
