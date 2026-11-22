using CasinoWorld.Common;
using CasinoWorld.Common.Gambler;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Projectiles
{
	/// <summary>Bouncing die: falls with gravity, bounces off tiles up to 3 times.</summary>
	public class LoadedDie : ModProjectile
	{
		private const int MaxBounces = 3;

		private ref float Bounces => ref Projectile.ai[0];

		public override void SetDefaults()
		{
			Projectile.width = 12;
			Projectile.height = 12;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = 3;
			Projectile.timeLeft = 300;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
		}

		public override void AI()
		{
			Projectile.velocity.Y = System.Math.Min(Projectile.velocity.Y + 0.3f, 12f);
			Projectile.rotation += Projectile.velocity.X * 0.06f;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (++Bounces > MaxBounces)
				return true;

			SoundEngine.PlaySound(CasinoSounds.DiceBounce, Projectile.Center);
			if (Projectile.velocity.X != oldVelocity.X)
				Projectile.velocity.X = -oldVelocity.X * 0.8f;
			if (Projectile.velocity.Y != oldVelocity.Y)
				Projectile.velocity.Y = -oldVelocity.Y * 0.7f;
			return false;
		}

		public override void OnKill(int timeLeft)
		{
			for (int k = 0; k < 5; k++)
				Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke);
		}
	}

	/// <summary>Fast straight card, slightly affected by gravity late in flight.</summary>
	public class ThrownCard : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = 10;
			Projectile.height = 10;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = 1;
			Projectile.timeLeft = 120;
			Projectile.extraUpdates = 1;
		}

		public override void AI()
		{
			if (++Projectile.ai[0] > 40)
				Projectile.velocity.Y += 0.08f;
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
		}

		public override void OnKill(int timeLeft)
		{
			for (int k = 0; k < 3; k++)
				Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Cloud, Scale: 0.6f);
		}
	}

	/// <summary>Chip that, after its first hit, ricochets once towards the nearest other enemy.</summary>
	public class HighRollerChip : ModProjectile
	{
		private const float RicochetRange = 16 * 25;

		private ref float Ricocheted => ref Projectile.ai[0];

		public override void SetDefaults()
		{
			Projectile.width = 12;
			Projectile.height = 12;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = 2;
			Projectile.timeLeft = 180;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
		}

		public override void AI()
		{
			Projectile.rotation += 0.4f * Projectile.direction;
			if (Main.rand.NextBool(4))
				Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, Scale: 0.7f);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Ricocheted != 0f || Projectile.owner != Main.myPlayer)
				return;

			Ricocheted = 1f;
			NPC next = null;
			float best = RicochetRange;
			foreach (NPC npc in Main.ActiveNPCs) {
				if (npc.whoAmI == target.whoAmI || !npc.CanBeChasedBy(Projectile))
					continue;
				float distance = npc.Distance(Projectile.Center);
				if (distance < best && Collision.CanHit(Projectile.Center, 1, 1, npc.Center, 1, 1)) {
					best = distance;
					next = npc;
				}
			}

			if (next != null) {
				Projectile.velocity = Projectile.DirectionTo(next.Center) * Projectile.velocity.Length();
				Projectile.netUpdate = true;
				SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.5f }, Projectile.Center);
			}
		}
	}
}
