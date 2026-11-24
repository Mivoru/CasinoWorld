using System.Collections.Generic;
using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Gambler;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Projectiles
{
	/// <summary>Helpers for what Gambler projectiles do when they hit tiles.</summary>
	public static class WallHit
	{
		/// <summary>Reflects the velocity off the tile, keeping part of the speed.</summary>
		public static void Bounce(Projectile p, Vector2 oldVelocity, float keepX = 0.9f, float keepY = 0.9f)
		{
			Collision.HitTiles(p.position, oldVelocity, p.width, p.height);
			if (p.velocity.X != oldVelocity.X)
				p.velocity.X = -oldVelocity.X * keepX;
			if (p.velocity.Y != oldVelocity.Y)
				p.velocity.Y = -oldVelocity.Y * keepY;
		}

		/// <summary>Bank shot: bounces, then turns to the nearest visible enemy within a 45 degree cone of the rebound. True if there was one.</summary>
		public static bool Bank(Projectile p, Vector2 oldVelocity, float range)
		{
			Bounce(p, oldVelocity, 1f, 1f);
			float rebound = p.velocity.ToRotation();
			NPC best = null;
			foreach (NPC npc in Main.ActiveNPCs) {
				float d = npc.Distance(p.Center);
				if (d < range && System.Math.Abs(MathHelper.WrapAngle(p.DirectionTo(npc.Center).ToRotation() - rebound)) < MathHelper.Pi / 8f && npc.CanBeChasedBy(p) && (!p.usesLocalNPCImmunity || p.localNPCImmunity[npc.whoAmI] == 0) && Collision.CanHit(p.Center, 1, 1, npc.Center, 1, 1)) {
					range = d;
					best = npc;
				}
			}
			if (best != null)
				p.velocity = p.DirectionTo(best.Center) * oldVelocity.Length();
			return best != null;
		}

		/// <summary>One hit on everything within the radius.</summary>
		public static void Blast(Projectile p, float radius, float damageShare)
		{
			if (p.owner == Main.myPlayer)
				Projectile.NewProjectile(p.GetSource_FromThis(), p.Center, Vector2.Zero, ModContent.ProjectileType<GamblerBlast>(), System.Math.Max(1, (int)(p.damage * damageShare)), p.knockBack, p.owner, radius);
		}

		/// <summary>Coins thrown back from the surface.</summary>
		public static void Coins(Projectile p, Vector2 oldVelocity, int count, float damageShare)
		{
			if (p.owner != Main.myPlayer)
				return;
			Vector2 back = -oldVelocity.SafeNormalize(-Vector2.UnitY) * 5f;
			for (int k = 0; k < count; k++)
				Projectile.NewProjectile(p.GetSource_FromThis(), p.Center - oldVelocity.SafeNormalize(Vector2.Zero) * 8f, back.RotatedByRandom(0.9f) - Vector2.UnitY * 2f,
					ModContent.ProjectileType<CoinShrapnel>(), System.Math.Max(1, (int)(p.damage * damageShare)), 1f, p.owner);
		}
	}

	/// <summary>Invisible area hit, ai[0] = radius. The spawner draws the effect.</summary>
	public class GamblerBlast : ModProjectile
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/HighRollerChip";

		public override void SetDefaults()
		{
			Projectile.width = Projectile.height = 16;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.timeLeft = 3;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
			Projectile.alpha = 255;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
			=> Vector2.Distance(Projectile.Center, targetHitbox.ClosestPointInRect(Projectile.Center)) < Projectile.ai[0];

		public override bool PreDraw(ref Color lightColor) => false;
	}

	/// <summary>Ember left on the floor by burning dice: burns in place for a moment.</summary>
	public class EmberPatch : ModProjectile
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/ExplodingDie";

		public override void SetDefaults()
		{
			Projectile.width = 26;
			Projectile.height = 16;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = 2;
			Projectile.tileCollide = false;
			Projectile.timeLeft = 75;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 30;
		}

		public override void AI()
		{
			Projectile.velocity = Vector2.Zero;
			Lighting.AddLight(Projectile.Center, 0.6f, 0.3f, 0.05f);
			Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0, -1.5f, 0, default, 1.5f);
			d.noGravity = true;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.OnFire, 120);

		public override bool PreDraw(ref Color lightColor) => false;
	}

	/// <summary>Counts Royal Flush volleys: all five cards of one volley in the same enemy complete the flush.</summary>
	public class RoyalFlushCombo : ModPlayer
	{
		private int volley;
		private readonly Dictionary<int, (int npc, int hits)> running = new();

		public int NextVolley()
		{
			running.Remove(volley - 6);
			return ++volley;
		}

		/// <summary>True when this hit completes the flush.</summary>
		public bool Hit(int id, NPC target)
		{
			int hits = running.TryGetValue(id, out var state) && state.npc == target.whoAmI ? state.hits + 1 : 1;
			running[id] = (target.whoAmI, hits);
			if (hits != 5)
				return false;
			CombatText.NewText(target.Hitbox, new Color(255, 220, 80), Language.GetTextValue("Mods.CasinoWorld.Gambler.Hands.RoyalFlush"), true);
			SoundEngine.PlaySound(CasinoSounds.RareReveal with { Volume = 0.5f }, target.Center);
			return true;
		}
	}
}
