using System;
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
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, Tint * Projectile.Opacity, Projectile.rotation, tex.Size() / 2f, DrawScale, SpriteEffects.None);
			return false;
		}
	}

	/// <summary>Spectral card: homes in, pierces twice, ghostly teal.</summary>
	public class SpectralCard : GamblerShot
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/RoyalCard";
		protected override Color Tint => new Color(150, 255, 255, 180);
		protected override float DrawScale => 1.4f;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 16;
			Projectile.penetrate = 3;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 15;
			Projectile.extraUpdates = 1;
		}

		public override void AI()
		{
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
			if (++Projectile.ai[0] > 10)
				Home(600f, 0.07f);
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
			for (int side = -1; side <= 1; side += 2)
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(side * 4f, -6f), Type, Projectile.damage / 2, Projectile.knockBack, Projectile.owner, 1f);
		}
	}

	/// <summary>Revolver chip; ai[0] = 1 is the golden jackpot round.</summary>
	public class RevolverChip : GamblerShot
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/HighRollerChip";
		protected override Color Tint => Projectile.ai[0] == 1f ? new Color(255, 220, 90) : Color.White;
		protected override float DrawScale => Projectile.ai[0] == 1f ? 1.4f : 0.9f;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 10;
			Projectile.extraUpdates = 2;
			Projectile.tileCollide = true;
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

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 16;
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

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Projectile.ai[0] != 0f || Projectile.owner != Main.myPlayer)
				return;
			for (int k = 0; k < 3; k++)
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Main.rand.NextVector2CircularEdge(9f, 9f),
					Type, (int)(Projectile.damage * 0.35f), 1f, Projectile.owner, 1f);
		}
	}

	/// <summary>Burning die falling from the sky; explodes on impact (radius 5 tiles). ai[0] = target height.</summary>
	public class MeteorDie : GamblerShot
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/ExplodingDie";
		protected override Color Tint => new(255, 190, 120);
		protected override float DrawScale => 1.8f;

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
			}
		}

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
			for (int k = 0; k < 40; k++)
				Dust.NewDust(Projectile.Center - new Vector2(70), 140, 140, k % 3 == 0 ? DustID.SolarFlare : DustID.Torch, 0, 0, 0, default, 1.8f);
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

		public override void AI()
		{
			Projectile.rotation = Projectile.velocity.X * 0.03f;
			Lighting.AddLight(Projectile.Center, 0.5f, 0.3f, 0.5f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			int symbol = Math.Clamp((int)Projectile.ai[0], 0, 6);
			Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, new Rectangle(symbol * 64, 0, 64, 64), Color.White, Projectile.rotation, new Vector2(32), 0.38f, SpriteEffects.None);
			return false;
		}
	}
}
