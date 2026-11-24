using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
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

		public override void SetStaticDefaults() => CasinoFx.EnableTrail(Type, 6);

		public override bool PreDraw(ref Color lightColor)
		{
			// The fuse glows faster as the die is about to blow.
			float urgency = Projectile.timeLeft < 60 ? (float)System.Math.Abs(System.Math.Sin(Projectile.timeLeft * 0.4f)) : 0.3f;
			CasinoFx.Draw(Projectile, new Color(255, 140, 60), new Color(255, 120, 40) * urgency);
			return false;
		}

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
			if (Projectile.ai[1] == 1f) {
				Projectile.velocity = Vector2.Zero;
				if (Projectile.timeLeft % 8 == 0)
					CasinoFx.Ring(Projectile.Center, new Color(255, 70, 50), 6, 1.5f, 0.7f);
				// An enemy that steps on it sets it off.
				foreach (NPC npc in Main.ActiveNPCs)
					if (Projectile.timeLeft > 4 && npc.CanBeChasedBy(Projectile) && npc.Distance(Projectile.Center) < 32f + npc.width / 2f)
						Projectile.timeLeft = 4;
			}
			else
				Projectile.velocity.Y = System.Math.Min(Projectile.velocity.Y + 0.3f, 12f);
			Projectile.rotation += Projectile.velocity.X * 0.06f;
			if (Main.rand.NextBool(2))
				CasinoFx.Spark(Projectile.Center + new Vector2(0, -6).RotatedBy(Projectile.rotation), new Vector2(0, -1f), new Color(255, 170, 60), 0.6f);
			Lighting.AddLight(Projectile.Center, 0.4f, 0.2f, 0.05f);

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
				// Out of bounces: it sticks where it lands and the fuse burns down (blinking eyes).
				if (Projectile.ai[1] == 0f) {
					Projectile.ai[1] = 1f;
					Projectile.timeLeft = System.Math.Min(Projectile.timeLeft, 34);
					SoundEngine.PlaySound(SoundID.Item17 with { Pitch = 0.3f }, Projectile.Center);
				}
				Projectile.velocity = Vector2.Zero;
				return false;
			}

			SoundEngine.PlaySound(CasinoSounds.DiceBounce, Projectile.Center);
			CasinoFx.Sparks(Projectile.Center, new Color(255, 170, 60), 4, 2f, 0.8f);
			if (Projectile.velocity.X != oldVelocity.X)
				Projectile.velocity.X = -oldVelocity.X * 0.8f;
			if (Projectile.velocity.Y != oldVelocity.Y)
				Projectile.velocity.Y = -oldVelocity.Y * 0.7f;
			return false;
		}

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f }, Projectile.Center);
			SoundEngine.PlaySound(CasinoSounds.ChipExplode, Projectile.Center);
			CasinoFx.Explosion(Projectile.Center, ExplosionSize, new Color(255, 170, 60), new Color(255, 240, 200), 2.5f);
		}
	}

	/// <summary>Gold card that homes in on the nearest enemy after a short delay.</summary>
	public class RoyalCard : ModProjectile
	{
		private const float HomingRange = 16 * 30;
		private const float TurnRate = 0.08f;

		public override void SetStaticDefaults() => CasinoFx.EnableTrail(Type, 10);

		public override bool PreDraw(ref Color lightColor)
		{
			CasinoFx.Draw(Projectile, new Color(255, 210, 80), new Color(255, 200, 80));
			return false;
		}

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
			if (Main.rand.NextBool(3))
				CasinoFx.Spark(Projectile.Center, -Projectile.velocity * 0.1f, new Color(255, 210, 80), 0.6f);
			Lighting.AddLight(Projectile.Center, 0.35f, 0.28f, 0.08f);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// ai[1]: volley number. The fifth card of a volley in the same enemy completes the flush.
			if (Projectile.owner != Main.myPlayer || Projectile.ai[1] == 0f || !Main.player[Projectile.owner].GetModPlayer<RoyalFlushCombo>().Hit((int)Projectile.ai[1], target))
				return;
			WallHit.Blast(Projectile, 80f, 1.2f);
			CasinoFx.Ring(Projectile.Center, new Color(255, 220, 80), 24, 6f, 1.3f);
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			// The card burns up in a golden flash.
			CasinoFx.Ring(Projectile.Center, new Color(255, 210, 80), 14, 3.5f, 1f);
			for (int k = 0; k < 8; k++)
				Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, -oldVelocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.8f) * Main.rand.NextFloat(1f, 4f), 0, default, 1.3f).noGravity = true;
			return true;
		}

		public override void OnKill(int timeLeft)
		{
			if (timeLeft > 0)
				Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
			CasinoFx.CardImpact(Projectile.Center, Projectile.velocity, new Color(255, 200, 70));
			CasinoFx.Sparks(Projectile.Center, new Color(255, 230, 140), 6, 3f);
		}
	}

	/// <summary>Heavy chip that ricochets to the nearest other enemy up to two times.</summary>
	public class JackpotChip : ModProjectile
	{
		private const float RicochetRange = 16 * 28;
		private const int MaxRicochets = 2;

		private ref float Ricochets => ref Projectile.ai[0];

		public override void SetStaticDefaults() => CasinoFx.EnableTrail(Type, 10);

		public override bool PreDraw(ref Color lightColor)
		{
			CasinoFx.Draw(Projectile, new Color(190, 110, 255), new Color(180, 90, 255));
			return false;
		}

		public override void OnKill(int timeLeft)
		{
			CasinoFx.ChipImpact(Projectile.Center, new Color(200, 120, 255));
			CasinoFx.Ring(Projectile.Center, new Color(255, 220, 120), 12, 4f, 1.1f);
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			// The wall pays out two coins.
			WallHit.Coins(Projectile, oldVelocity, 2, 0.2f);
			SoundEngine.PlaySound(CasinoSounds.CoinsPayout with { Volume = 0.35f }, Projectile.Center);
			return true;
		}

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
			if (Main.rand.NextBool(2))
				CasinoFx.Spark(Projectile.Center, -Projectile.velocity * 0.1f, new Color(190, 110, 255), 0.7f);
			Lighting.AddLight(Projectile.Center, 0.3f, 0.15f, 0.45f);
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
				SoundEngine.PlaySound(CasinoSounds.ChipFlick with { Pitch = 0.3f }, Projectile.Center);
				for (int k = 0; k < 8; k++)   // arc of sparks towards the next target
					CasinoFx.Spark(Vector2.Lerp(Projectile.Center, next.Center, k / 8f), Main.rand.NextVector2Circular(1, 1), new Color(220, 150, 255), 0.8f);
			}
		}
	}
}
