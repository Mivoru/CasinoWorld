using System;
using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Gambler;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Projectiles
{
	/// <summary>Base for friendly Gambler projectiles that reuse an existing texture with a tint.</summary>
	public abstract class GamblerShot : ModProjectile
	{
		protected virtual Color Tint => Color.White;
		protected virtual float DrawScale => 1f;
		protected virtual Color TrailColor => Tint;
		protected virtual Color GlowColor => Color.Transparent;

		public override void SetStaticDefaults() => CasinoFx.EnableTrail(Type, 10);

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			Collision.HitTiles(Projectile.position, oldVelocity, Projectile.width, Projectile.height);
			return true;
		}

		public override void SetDefaults()
		{
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.timeLeft = 180;
		}

		protected NPC ClosestTarget(float range)
		{
			NPC best = null;
			foreach (NPC npc in Main.ActiveNPCs) {
				if (!npc.CanBeChasedBy(Projectile))
					continue;
				float d = npc.Distance(Projectile.Center);
				if (d < range) {
					range = d;
					best = npc;
				}
			}
			return best;
		}

		protected void Home(float range, float turn)
		{
			NPC target = ClosestTarget(range);
			if (target != null) {
				float speed = Projectile.velocity.Length();
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, Projectile.DirectionTo(target.Center) * speed, turn);
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			CasinoFx.Draw(Projectile, TrailColor, GlowColor, DrawScale, Tint);
			return false;
		}
	}

	/// <summary>Spectral card: homes in, pierces twice, ghostly teal.</summary>
	public class SpectralCard : GamblerShot
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/RoyalCard";
		protected override Color Tint => new Color(150, 255, 255, 180);
		protected override float DrawScale => 1.4f;
		protected override Color GlowColor => new(120, 255, 255);

		public override void OnKill(int timeLeft)
		{
			CasinoFx.CardImpact(Projectile.Center, Projectile.velocity, new Color(120, 255, 255));
			for (int k = 0; k < 10; k++)
				Dust.NewDustPerfect(Projectile.Center, DustID.DungeonSpirit, Main.rand.NextVector2Circular(3, 3), 100, default, 1.1f).noGravity = true;
		}

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 16;
			Projectile.penetrate = 3;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 15;
			Projectile.extraUpdates = 1;
			Projectile.tileCollide = false;   // a ghost: walls do not stop it
		}

		public override void AI()
		{
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
			if (++Projectile.ai[0] > 10)
				Home(600f, 0.07f);
			// Passing a wall: a spirit burst on the way in and out, half transparent inside.
			bool inside = Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height);
			if (inside != (Projectile.localAI[1] == 1f)) {
				Projectile.localAI[1] = inside ? 1f : 0f;
				CasinoFx.Ring(Projectile.Center, new Color(120, 255, 255), 10, 2.5f, 0.9f);
			}
			Projectile.Opacity = inside ? 0.45f : 1f;
			Lighting.AddLight(Projectile.Center, 0.2f, 0.5f, 0.6f);
			if (Main.rand.NextBool(3))
				Dust.NewDustPerfect(Projectile.Center, DustID.DungeonSpirit, Vector2.Zero, 100, default, 0.8f).noGravity = true;
		}
	}

	/// <summary>Die that splits into two half-damage dice on its first hit (ai[0] = 1 for the halves).</summary>
	public class DoublingDie : GamblerShot
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/LoadedDie";
		protected override Color Tint => new(140, 255, 160);
		protected override Color GlowColor => new(90, 255, 120);

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			CasinoFx.DieImpact(Projectile.Center, new Color(140, 255, 160));
			if (Projectile.ai[0] == 0f)
				Split(-oldVelocity.SafeNormalize(-Vector2.UnitY) * 3f, 0.7f);   // the wall doubles it as well, for less
			return true;
		}

		public override void OnKill(int timeLeft) => CasinoFx.Sparks(Projectile.Center, new Color(140, 255, 160), 8, 3f);

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 14;
			Projectile.penetrate = 1;
			Projectile.tileCollide = true;
		}

		public override void AI()
		{
			Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.2f, 12f);
			Projectile.rotation += Projectile.velocity.X * 0.06f;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Projectile.ai[0] != 0f || Projectile.owner != Main.myPlayer)
				return;
			Split(Vector2.Zero, 1f);
		}

		private void Split(Vector2 push, float share)
		{
			CasinoFx.Ring(Projectile.Center, new Color(160, 255, 170), 12, 3.5f);
			if (Projectile.owner != Main.myPlayer)
				return;
			for (int side = -1; side <= 1; side += 2)
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(side * 4f, -6f) + push, Type, (int)(Projectile.damage * 0.5f * share), Projectile.knockBack, Projectile.owner, 1f);
		}
	}

	/// <summary>Revolver chip; ai[0] = 1 is the golden jackpot round.</summary>
	public class RevolverChip : GamblerShot
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/NeutralChip";
		protected override Color Tint => Projectile.ai[0] == 1f ? new Color(255, 220, 90) : Color.White;
		protected override float DrawScale => Projectile.ai[0] == 1f ? 1.4f : 0.9f;
		protected override Color TrailColor => Projectile.ai[0] == 1f ? new Color(255, 220, 90) : new Color(255, 240, 200);
		protected override Color GlowColor => Projectile.ai[0] == 1f ? new Color(255, 200, 60) : Color.Transparent;

		public override void OnKill(int timeLeft)
		{
			if (Projectile.ai[0] == 1f) {
				CasinoFx.Explosion(Projectile.Center, 60, new Color(255, 210, 80), new Color(255, 250, 200), 1.5f);
				SoundEngine.PlaySound(CasinoSounds.JackpotProc with { Volume = 0.6f }, Projectile.Center);
			}
			else
				CasinoFx.ChipImpact(Projectile.Center, new Color(255, 230, 160));
		}

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 10;
			Projectile.extraUpdates = 1;
			Projectile.tileCollide = true;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (Projectile.ai[0] == 1f) {
				WallHit.Blast(Projectile, 72f, 0.4f);   // OnKill draws the explosion
				return true;
			}
			if (Projectile.ai[0] == 2f) {
				WallHit.Coins(Projectile, oldVelocity, 1, 0.35f);
				return true;
			}
			CasinoFx.Sparks(Projectile.Center, new Color(255, 230, 160), 8, 3.5f, 0.9f);
			return base.OnTileCollide(oldVelocity);
		}

		public override void AI()
		{
			Projectile.rotation += 0.5f;
			if (Projectile.ai[0] == 1f && Main.rand.NextBool(2))
				Dust.NewDustPerfect(Projectile.Center, DustID.GoldCoin, Vector2.Zero, 0, default, 0.9f).noGravity = true;
		}
	}

	/// <summary>Galaxy card: on hit bursts into three small cards (ai[0] = 1 for the small ones).</summary>
	public class GalaxyCard : GamblerShot
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/RoyalCard";
		protected override Color Tint => Projectile.ai[0] == 1f ? new Color(200, 160, 255, 200) : new Color(170, 120, 255);
		protected override float DrawScale => Projectile.ai[0] == 1f ? 0.9f : 1.5f;
		protected override Color GlowColor => new(170, 110, 255);

		public override void OnKill(int timeLeft)
		{
			CasinoFx.CardImpact(Projectile.Center, Projectile.velocity, new Color(190, 140, 255), Projectile.ai[0] == 0f);
			CasinoFx.Sparks(Projectile.Center, new Color(255, 240, 200), 5, 3f, 0.8f);
		}

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 16;
			Projectile.ArmorPenetration = 25;
			Projectile.penetrate = 1;
			Projectile.extraUpdates = 1;
		}

		public override void AI()
		{
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
			if (Projectile.ai[0] == 1f)
				Home(500f, 0.08f);
			Lighting.AddLight(Projectile.Center, 0.4f, 0.25f, 0.7f);
			if (Main.rand.NextBool(3))
				Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, Vector2.Zero, 0, default, 0.9f).noGravity = true;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (Projectile.ai[0] == 0f)
				Burst(Projectile.Center - oldVelocity.SafeNormalize(Vector2.Zero) * 10f, -oldVelocity.SafeNormalize(Vector2.UnitY) * 9f, 0.7f);
			return true;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Projectile.ai[0] == 0f)
				Burst(Projectile.Center, Vector2.Zero, 1f);
		}

		/// <summary>Three small cards: all around on a hit, away from the surface on a wall.</summary>
		private void Burst(Vector2 at, Vector2 away, float share)
		{
			CasinoFx.Ring(Projectile.Center, new Color(200, 150, 255), 16, 5f, 1.2f);
			if (Projectile.owner != Main.myPlayer)
				return;
			for (int k = 0; k < 3; k++)
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), at, away == Vector2.Zero ? Main.rand.NextVector2CircularEdge(9f, 9f) : away.RotatedBy((k - 1) * 0.6f),
					Type, (int)(Projectile.damage * 0.35f * share), 1f, Projectile.owner, 1f);
		}
	}

	/// <summary>Burning die falling from the sky; explodes on impact (radius 5 tiles). ai[0] = target height.</summary>
	/// <summary>Falling die. ai[0] = height to explode at. ai[1] = 1 when thrown by the weapon: the explosion then hits again after the impact.</summary>
	public class MeteorDie : GamblerShot
	{
		public bool FromWeapon => Projectile.ai[1] == 1f;

		public override string Texture => "CasinoWorld/Content/Projectiles/ExplodingDie";
		protected override Color Tint => new(255, 190, 120);
		protected override float DrawScale => 1.8f;
		protected override Color TrailColor => new(255, 140, 50);
		protected override Color GlowColor => new(255, 120, 40);

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 24;
			Projectile.penetrate = -1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
		}

		public override void AI()
		{
			Projectile.rotation += 0.25f;
			for (int k = 0; k < 2; k++)
				Dust.NewDustPerfect(Projectile.Center, DustID.Torch, -Projectile.velocity * 0.2f, 0, default, 1.8f).noGravity = true;
			Projectile.tileCollide = Projectile.Center.Y > Projectile.ai[0] - 16;
			if (Projectile.timeLeft == 3 || (Projectile.Center.Y >= Projectile.ai[0] && Projectile.localAI[0] == 0f)) {
				Projectile.localAI[0] = 1f;
				Explode();
			}
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (Projectile.timeLeft > 3 && Projectile.owner == Main.myPlayer)
				for (int k = -1; k <= 1; k++)
					Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + new Vector2(k * 44f, 4f), Vector2.Zero, ModContent.ProjectileType<EmberPatch>(), Projectile.damage / 8, 1f, Projectile.owner);
			Explode();
			return false;
		}

		private void Explode()
		{
			if (Projectile.timeLeft > 3) {
				Projectile.timeLeft = 3;
				Projectile.velocity = Vector2.Zero;
				Projectile.tileCollide = false;
				Projectile.alpha = 255;
				Projectile.Resize(160, 160);
				if (FromWeapon)
					Projectile.ResetLocalNPCHitImmunity();
			}
		}

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
			SoundEngine.PlaySound(CasinoSounds.ChipExplode, Projectile.Center);
			CasinoFx.Explosion(Projectile.Center, 160, new Color(255, 150, 50), new Color(255, 240, 180), 5f);
			for (int k = 0; k < 20; k++)
				Dust.NewDust(Projectile.Center - new Vector2(70), 140, 140, DustID.SolarFlare, 0, 0, 0, default, 1.6f);
		}
	}

	/// <summary>Slot symbol shot (ai[0] = SlotSymbol). Rarer symbols deal more: cherry x0.7 up to diamond x2.2.</summary>
	public class SymbolShot : GamblerShot
	{
		private static readonly float[] Multipliers = { 0.7f, 0.85f, 1.0f, 1.15f, 1.5f, 1.8f, 2.2f };

		public override string Texture => "CasinoWorld/Assets/UI/SlotSymbols";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 18;
			Projectile.penetrate = 1;
			Projectile.extraUpdates = 1;
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			modifiers.SourceDamage *= Multipliers[Math.Clamp((int)Projectile.ai[0], 0, 6)];
		}

		private Color SymbolColor => (int)Projectile.ai[0] switch {
			0 => new Color(255, 60, 70), 1 => new Color(255, 240, 80), 2 => new Color(255, 200, 70), 3 => new Color(90, 230, 110),
			4 => new Color(220, 220, 230), 5 => new Color(255, 70, 70), _ => new Color(120, 220, 255),
		};

		public override void AI()
		{
			Projectile.rotation = Projectile.velocity.X * 0.03f;
			Lighting.AddLight(Projectile.Center, SymbolColor.ToVector3() * 0.5f);
			if (Main.rand.NextBool(3))
				CasinoFx.Spark(Projectile.Center, -Projectile.velocity * 0.1f, SymbolColor, 0.7f);
		}

		/// <summary>Cherry and lemon splatter, the bell rings, the clover bounces once, BAR pays coins, 7 explodes, the diamond shatters.</summary>
		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			bool again = Projectile.ai[1]++ > 0f;
			switch ((int)Projectile.ai[0]) {
				case 0 or 1:
					CasinoFx.Sparks(Projectile.Center, SymbolColor, 14, 4f, 1.1f);
					return true;
				case 2:
					WallHit.Blast(Projectile, 70f, 0.35f);
					CasinoFx.Ring(Projectile.Center, SymbolColor, 22, 5f, 1.2f);
					SoundEngine.PlaySound(CasinoSounds.BossBell with { Volume = 0.25f, Pitch = 0.7f }, Projectile.Center);
					return true;
				case 3 when !again:
					WallHit.Bounce(Projectile, oldVelocity, 1f, 1f);
					return false;
				case 4:
					WallHit.Coins(Projectile, oldVelocity, 2, 0.25f);
					return true;
				case 5:
					WallHit.Blast(Projectile, 80f, 0.5f);
					CasinoFx.Explosion(Projectile.Center, 80, SymbolColor, new Color(255, 230, 160), 1f);
					return true;
				case 6:
					WallHit.Coins(Projectile, oldVelocity, 3, 0.35f);
					CasinoFx.Ring(Projectile.Center, SymbolColor, 18, 5f, 1.2f);
					return true;
			}
			return base.OnTileCollide(oldVelocity);
		}

		public override void OnKill(int timeLeft)
		{
			CasinoFx.Ring(Projectile.Center, SymbolColor, 12, 3f);
			SoundEngine.PlaySound(CasinoSounds.ReelStop with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			int symbol = Math.Clamp((int)Projectile.ai[0], 0, 6);
			CasinoFx.Trail(Projectile, tex, new Rectangle(symbol * 64, 0, 64, 64), SymbolColor, 0.38f);
			Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, new Rectangle(symbol * 64, 0, 64, 64), Color.White, Projectile.rotation, new Vector2(32), 0.38f, SpriteEffects.None);
			return false;
		}
	}
}
