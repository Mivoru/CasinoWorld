using CasinoWorld.Common.Gambler;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Projectiles
{
	/// <summary>Bouncing die that explodes when it runs out of bounces or time (radius 3 tiles).</summary>
	public class ExplodingDie : ModProjectile
	{
		private const int MaxBounces = 3;
		private const int ExplosionSize = 96;

		private ref float Bounces => ref Projectile.ai[0];

		public override void SetDefaults()
		{
			Projectile.width = 12;
			Projectile.height = 12;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = -1;
			Projectile.timeLeft = 180;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
		}

		public override void AI()
		{
			Projectile.velocity.Y = System.Math.Min(Projectile.velocity.Y + 0.3f, 12f);
			Projectile.rotation += Projectile.velocity.X * 0.06f;
			if (Main.rand.NextBool(3))
				Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, Scale: 0.8f);

			// In the last few ticks, grow into the explosion so it damages everything around.
			if (Projectile.timeLeft == 3 && Projectile.owner == Main.myPlayer) {
				Projectile.tileCollide = false;
				Projectile.alpha = 255;
				Projectile.Resize(ExplosionSize, ExplosionSize);
				Projectile.velocity = Vector2.Zero;
				Projectile.knockBack *= 1.5f;
			}
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (++Bounces > MaxBounces) {
				Projectile.timeLeft = System.Math.Min(Projectile.timeLeft, 4);
				Projectile.velocity = Vector2.Zero;
				return false;
			}

			SoundEngine.PlaySound(SoundID.Dig with { Pitch = 0.6f, Volume = 0.5f }, Projectile.Center);
			if (Projectile.velocity.X != oldVelocity.X)
				Projectile.velocity.X = -oldVelocity.X * 0.8f;
			if (Projectile.velocity.Y != oldVelocity.Y)
				Projectile.velocity.Y = -oldVelocity.Y * 0.7f;
			return false;
		}

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
			for (int k = 0; k < 25; k++) {
				Dust dust = Dust.NewDustDirect(Projectile.Center - new Vector2(30), 60, 60, DustID.Torch, Scale: 1.6f);
				dust.noGravity = true;
				dust.velocity *= 3f;
			}
			for (int k = 0; k < 10; k++)
				Dust.NewDust(Projectile.Center - new Vector2(30), 60, 60, DustID.Smoke, Scale: 1.3f);
		}
	}

	/// <summary>Gold card that homes in on the nearest enemy after a short delay.</summary>
	public class RoyalCard : ModProjectile
	{
		private const float HomingRange = 16 * 30;
		private const float TurnRate = 0.08f;

		public override void SetDefaults()
		{
			Projectile.width = 10;
			Projectile.height = 10;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = 1;
			Projectile.timeLeft = 180;
			Projectile.extraUpdates = 1;
		}

		public override void AI()
		{
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
			if (++Projectile.ai[0] < 15)
				return;

			NPC target = null;
			float best = HomingRange;
			foreach (NPC npc in Main.ActiveNPCs) {
				if (!npc.CanBeChasedBy(Projectile))
					continue;
				float distance = npc.Distance(Projectile.Center);
				if (distance < best) {
					best = distance;
					target = npc;
				}
			}

			if (target != null) {
				float speed = Projectile.velocity.Length();
				Vector2 wanted = Projectile.DirectionTo(target.Center) * speed;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, wanted, TurnRate);
			}
			if (Main.rand.NextBool(4))
				Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, Scale: 0.6f);
		}

		public override void OnKill(int timeLeft)
		{
			for (int k = 0; k < 4; k++)
				Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, Scale: 0.8f);
		}
	}

	/// <summary>Heavy chip that ricochets to the nearest other enemy up to two times.</summary>
	public class JackpotChip : ModProjectile
	{
		private const float RicochetRange = 16 * 28;
		private const int MaxRicochets = 2;

		private ref float Ricochets => ref Projectile.ai[0];

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = MaxRicochets + 1;
			Projectile.timeLeft = 200;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
		}

		public override void AI()
		{
			Projectile.rotation += 0.35f * Projectile.direction;
			if (Main.rand.NextBool(3))
				Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.PurpleTorch, Scale: 0.9f);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Ricochets >= MaxRicochets || Projectile.owner != Main.myPlayer)
				return;

			Ricochets++;
			NPC next = null;
			float best = RicochetRange;
			foreach (NPC npc in Main.ActiveNPCs) {
				if (npc.whoAmI == target.whoAmI || !npc.CanBeChasedBy(Projectile) || Projectile.localNPCImmunity[npc.whoAmI] != 0)
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
				SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.3f }, Projectile.Center);
			}
		}
	}
}
