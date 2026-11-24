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
	/// <summary>Bouncing die: falls with gravity, bounces off tiles up to 3 times.</summary>
	public class LoadedDie : ModProjectile
	{
		private const int MaxBounces = 3;

		private ref float Bounces => ref Projectile.ai[0];

		/// <summary>Rolled face, 0 before the first bounce. Damage x(0.65 + 0.1 * face): 0.75 to 1.25, 1.0 on average.</summary>
		private ref float Face => ref Projectile.ai[1];

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			if (Face > 0)
				modifiers.SourceDamage *= 0.65f + 0.1f * Face;
		}

		public override void SetStaticDefaults() => CasinoFx.EnableTrail(Type, 6);

		public override bool PreDraw(ref Color lightColor)
		{
			CasinoFx.Draw(Projectile, new Color(255, 240, 220), Color.Transparent);
			return false;
		}

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
			if (Projectile.owner == Main.myPlayer) {
				Face = Main.rand.Next(1, 7);
				Projectile.netUpdate = true;
				CombatText.NewText(Projectile.Hitbox, Face == 6 ? new Color(255, 220, 90) : new Color(225, 225, 225), (int)Face, Face == 6, true);
				if (Face == 6)
					CasinoFx.Ring(Projectile.Center, new Color(255, 220, 90), 10, 3f);
			}
			CasinoFx.Sparks(Projectile.Center, new Color(255, 230, 200), 3, 1.5f, 0.7f);
			Collision.HitTiles(Projectile.position, oldVelocity, Projectile.width, Projectile.height);
			if (Projectile.velocity.X != oldVelocity.X)
				Projectile.velocity.X = -oldVelocity.X * 0.8f;
			if (Projectile.velocity.Y != oldVelocity.Y)
				Projectile.velocity.Y = -oldVelocity.Y * 0.7f;
			return false;
		}

		public override void OnKill(int timeLeft) => CasinoFx.DieImpact(Projectile.Center, new Color(255, 230, 200));
	}

	/// <summary>Fast straight card, slightly affected by gravity late in flight.</summary>
	public class ThrownCard : ModProjectile
	{
		public override void SetStaticDefaults() => CasinoFx.EnableTrail(Type, 7);

		public override bool PreDraw(ref Color lightColor)
		{
			CasinoFx.Draw(Projectile, new Color(255, 120, 120), Color.Transparent);
			return false;
		}

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
			if (Projectile.ai[2] > 0f) {   // stuck in a wall
				Projectile.velocity = Vector2.Zero;
				if (++Projectile.ai[2] > 180f)
					Projectile.Kill();
				Projectile.Opacity = Utils.GetLerpValue(180f, 140f, Projectile.ai[2], true);
				return;
			}
			if (++Projectile.ai[0] > 40)
				Projectile.velocity.Y += 0.08f;
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (Projectile.ai[1] == 1f) {
				Projectile.ai[1] = 2f;
				WallHit.Bounce(Projectile, oldVelocity, 1f, 1f);
				Projectile.damage = System.Math.Max(1, Projectile.damage * 3 / 5);
				CasinoFx.Sparks(Projectile.Center, new Color(255, 120, 120), 4, 2f, 0.7f);
				SoundEngine.PlaySound(CasinoSounds.CardFlip with { Volume = 0.4f, Pitch = 0.4f }, Projectile.Center);
				return false;
			}
			if (Projectile.ai[1] != 0f)
				return true;
			// A card from the deck slices into the surface and stays there as a trap for half damage.
			Projectile.ai[2] = 1f;
			Projectile.damage = System.Math.Max(1, Projectile.damage / 2);
			Projectile.rotation = oldVelocity.ToRotation() + MathHelper.PiOver2;
			Projectile.position += oldVelocity.SafeNormalize(Vector2.Zero) * 4f;
			Projectile.velocity = Vector2.Zero;
			Projectile.tileCollide = false;
			Projectile.timeLeft = 240;
			Projectile.netUpdate = true;
			SoundEngine.PlaySound(CasinoSounds.CardHit with { Volume = 0.4f }, Projectile.Center);
			CasinoFx.Sparks(Projectile.Center, new Color(255, 200, 200), 3, 1.5f, 0.6f);
			return false;
		}

		public override void OnKill(int timeLeft)
		{
			if (timeLeft > 0 && Projectile.ai[2] == 0f)
				Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
			CasinoFx.CardImpact(Projectile.Center, Projectile.velocity, new Color(220, 40, 50));
		}
	}

	/// <summary>Chip that, after its first hit, ricochets once towards the nearest other enemy.</summary>
	public class HighRollerChip : ModProjectile
	{
		private const float RicochetRange = 16 * 25;

		private ref float Ricocheted => ref Projectile.ai[0];

		public override void SetStaticDefaults() => CasinoFx.EnableTrail(Type, 8);

		public override bool PreDraw(ref Color lightColor)
		{
			CasinoFx.Draw(Projectile, new Color(255, 210, 90), new Color(255, 200, 80));
			return false;
		}

		public override void OnKill(int timeLeft) => CasinoFx.ChipImpact(Projectile.Center, new Color(255, 210, 90));

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (Projectile.ai[1]++ > 0f)
				return true;
			// Bank shot: off one wall, towards an enemy if one is close to its path.
			bool aimed = WallHit.Bank(Projectile, oldVelocity, RicochetRange);
			SoundEngine.PlaySound(CasinoSounds.ChipFlick with { Pitch = 0.4f }, Projectile.Center);
			CasinoFx.Ring(Projectile.Center, aimed ? new Color(120, 255, 170) : new Color(255, 220, 120), 10, 3f);
			return false;
		}

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
				CasinoFx.Spark(Projectile.Center, -Projectile.velocity * 0.1f, new Color(255, 210, 90), 0.6f);
			Lighting.AddLight(Projectile.Center, 0.3f, 0.24f, 0.08f);
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
				SoundEngine.PlaySound(CasinoSounds.ChipFlick with { Pitch = 0.5f }, Projectile.Center);
				CasinoFx.Ring(Projectile.Center, new Color(255, 220, 120), 10, 3f);
			}
		}
	}
}
