using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Projectiles.Boss
{
	/// <summary>Base for The House's projectiles: hostile, no tile collision unless a projectile wants it.</summary>
	public abstract class HouseProjectile : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 300;
		}
	}

	/// <summary>Bouncing cherry bomb that explodes after two seconds.</summary>
	public class CherryBomb : HouseProjectile
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 18;
			Projectile.tileCollide = true;
			Projectile.timeLeft = 120;
		}

		public override void AI()
		{
			Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.25f, 12f);
			Projectile.rotation += Projectile.velocity.X * 0.05f;
			if (Main.rand.NextBool(3))
				Dust.NewDust(Projectile.Center + new Vector2(4, -10), 2, 2, DustID.Torch);
			if (Projectile.timeLeft == 3) {
				Projectile.tileCollide = false;
				Projectile.alpha = 255;
				Projectile.Resize(90, 90);
				Projectile.velocity = Vector2.Zero;
			}
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (Projectile.velocity.X != oldVelocity.X)
				Projectile.velocity.X = -oldVelocity.X * 0.7f;
			if (Projectile.velocity.Y != oldVelocity.Y)
				Projectile.velocity.Y = -oldVelocity.Y * 0.6f;
			return false;
		}

		public override bool CanHitPlayer(Player target) => Projectile.timeLeft <= 3;

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
			for (int k = 0; k < 20; k++)
				Dust.NewDust(Projectile.Center - new Vector2(40), 80, 80, k % 2 == 0 ? DustID.Torch : DustID.RedTorch, 0, 0, 0, default, 1.6f);
		}
	}

	/// <summary>Sour lemon drop: falls in an arc and covers the player in Ichor.</summary>
	public class SourDrop : HouseProjectile
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 10;
			Projectile.timeLeft = 180;
		}

		public override void AI()
		{
			Projectile.velocity.Y += 0.12f;
			if (Main.rand.NextBool(4))
				Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Ichor, Scale: 0.7f);
		}

		public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(BuffID.Ichor, 240);
	}

	/// <summary>Expanding bell ring with one gap; it hurts only near its edge, outside the gap.</summary>
	public class BellWave : HouseProjectile
	{
		private const float GrowSpeed = 5f;
		private const float GapHalfAngle = 0.42f;
		private float Radius => (300 - Projectile.timeLeft) * GrowSpeed;

		public override string Texture => "Terraria/Images/Projectile_0";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 8;
			Projectile.timeLeft = 300;
		}

		public override void AI()
		{
			Projectile.velocity = Vector2.Zero;
			if (Radius > 1400)
				Projectile.Kill();
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			Vector2 to = targetHitbox.Center.ToVector2() - Projectile.Center;
			float dist = to.Length();
			if (Math.Abs(dist - Radius) > 18f)
				return false;
			float diff = MathHelper.WrapAngle(to.ToRotation() - Projectile.ai[0]);
			return Math.Abs(diff) > GapHalfAngle;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D px = TextureAssets.MagicPixel.Value;
			int segments = 96;
			float alpha = Math.Min(1f, Projectile.timeLeft / 60f);
			for (int k = 0; k < segments; k++) {
				float a = MathHelper.TwoPi * k / segments;
				if (Math.Abs(MathHelper.WrapAngle(a - Projectile.ai[0])) < GapHalfAngle)
					continue;
				Vector2 pos = Projectile.Center + a.ToRotationVector2() * Radius - Main.screenPosition;
				Main.EntitySpriteDraw(px, pos, new Rectangle(0, 0, 1, 1), new Color(255, 210, 90) * alpha, a, new Vector2(0.5f), new Vector2(6f, Radius * MathHelper.TwoPi / segments), SpriteEffects.None);
			}
			return false;
		}
	}

	/// <summary>Flashing column warning; after a short delay a gold bar drops down the column.</summary>
	public class GoldRushMarker : HouseProjectile
	{
		public override string Texture => "Terraria/Images/Projectile_0";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 4;
			Projectile.timeLeft = 50;
		}

		public override bool CanHitPlayer(Player target) => false;

		public override void OnKill(int timeLeft)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(0, 16f), ModContent.ProjectileType<GoldBarDrop>(), Projectile.damage, 3f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			float pulse = 0.25f + 0.25f * (float)Math.Sin(Projectile.timeLeft * 0.6f);
			Vector2 top = Projectile.Center - Main.screenPosition;
			Main.EntitySpriteDraw(TextureAssets.MagicPixel.Value, top, new Rectangle(0, 0, 1, 1), new Color(255, 200, 60) * pulse, 0f, new Vector2(0.5f, 0f), new Vector2(26f, 1300f), SpriteEffects.None);
			return false;
		}
	}

	public class GoldBarDrop : HouseProjectile
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = 22;
			Projectile.height = 12;
			Projectile.timeLeft = 120;
		}

		public override void AI()
		{
			Projectile.rotation = 0f;
			if (Main.rand.NextBool(3))
				Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, Scale: 0.6f);
		}
	}

	/// <summary>A lucky "7" that chases the player for a few seconds (drawn from the slot symbol sheet).</summary>
	public class LuckySeven : HouseProjectile
	{
		public override string Texture => "CasinoWorld/Assets/UI/SlotSymbols";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 22;
			Projectile.timeLeft = 240;
		}

		public override void AI()
		{
			Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
			Vector2 wanted = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 8.5f;
			Projectile.velocity = Vector2.Lerp(Projectile.velocity, wanted, 0.035f);
			Projectile.rotation = Projectile.velocity.X * 0.05f;
			Lighting.AddLight(Projectile.Center, 0.6f, 0.1f, 0.1f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, new Rectangle(5 * 64, 0, 64, 64), Color.White, Projectile.rotation, new Vector2(32), 0.45f, SpriteEffects.None);
			return false;
		}
	}

	public class DiamondShard : HouseProjectile
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 10;
			Projectile.timeLeft = 200;
		}

		public override void AI()
		{
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
			Lighting.AddLight(Projectile.Center, 0.2f, 0.4f, 0.6f);
		}
	}

	/// <summary>Playing card thrown by a croupier hand.</summary>
	public class BossCard : HouseProjectile
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/RoyalCard";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 12;
			Projectile.timeLeft = 180;
		}

		public override void AI() => Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
	}

	/// <summary>
	/// Red or Black: one floor-to-ceiling stripe. All stripes flash for two seconds, then the stripes
	/// of the chosen colour strike. ai[0] = this stripe's colour, ai[1] = chosen colour (0 red, 1 black).
	/// </summary>
	public class RouletteStripe : HouseProjectile
	{
		public const int StripeWidth = 112;
		private const int Warn = 120;
		private const int Strike = 25;

		public override string Texture => "Terraria/Images/Projectile_0";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = StripeWidth;
			Projectile.height = 1400;
			Projectile.timeLeft = Warn + Strike;
		}

		private bool Striking => Projectile.timeLeft <= Strike && (int)Projectile.ai[0] == (int)Projectile.ai[1];

		public override bool CanHitPlayer(Player target) => Striking;

		public override void AI()
		{
			Projectile.velocity = Vector2.Zero;
			if (Projectile.timeLeft == Strike && Striking) {
				SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f }, Projectile.Center);
				for (int k = 0; k < 30; k++)
					Dust.NewDust(Projectile.position + new Vector2(0, Projectile.height / 2 - 300), Projectile.width, 600,
						(int)Projectile.ai[0] == 0 ? DustID.RedTorch : DustID.Smoke, 0, -4, 0, default, 1.5f);
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			bool red = (int)Projectile.ai[0] == 0;
			Color c = red ? new Color(220, 30, 40) : new Color(30, 30, 40);
			float alpha;
			if (Projectile.timeLeft > Strike)
				alpha = 0.18f + 0.12f * (float)Math.Sin(Projectile.timeLeft * (Projectile.timeLeft < 50 ? 0.9f : 0.35f));
			else
				alpha = Striking ? 0.75f : 0.05f;
			if (Striking)
				c = Color.Lerp(c, Color.White, 0.35f);
			Vector2 pos = Projectile.position - Main.screenPosition;
			Main.EntitySpriteDraw(TextureAssets.MagicPixel.Value, pos, new Rectangle(0, 0, 1, 1), c * alpha, 0f, Vector2.Zero, new Vector2(Projectile.width, Projectile.height), SpriteEffects.None);
			return false;
		}
	}

	/// <summary>The croupier's ball: circles the wheel, then shoots at the player and bounces three times.</summary>
	public class CroupierBall : HouseProjectile
	{
		private const int OrbitTicks = 90;

		private ref float Bounces => ref Projectile.localAI[0];

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 16;
			Projectile.timeLeft = 400;
		}

		public override void AI()
		{
			int age = 400 - Projectile.timeLeft;
			NPC body = Main.npc[(int)Projectile.ai[0]];
			if (age < OrbitTicks && body.active) {
				float a = age * 0.14f;
				Projectile.Center = body.Center + a.ToRotationVector2() * 72f;
				Projectile.velocity = Vector2.Zero;
				return;
			}
			if (age == OrbitTicks) {
				Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
				Projectile.velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 14f;
				Projectile.tileCollide = true;
				SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);
			}
			Projectile.velocity.Y += 0.2f;
			Projectile.rotation += 0.3f;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (++Bounces > 3)
				return true;
			SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.5f }, Projectile.Center);
			if (Projectile.velocity.X != oldVelocity.X)
				Projectile.velocity.X = -oldVelocity.X * 0.9f;
			if (Projectile.velocity.Y != oldVelocity.Y)
				Projectile.velocity.Y = -oldVelocity.Y * 0.85f;
			return false;
		}
	}

	/// <summary>Chip in an expanding ring around the boss. ai[0] = start angle, ai[1] = body index.</summary>
	public class OrbitChip : HouseProjectile
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/JackpotChip";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 16;
			Projectile.timeLeft = 260;
		}

		public override void AI()
		{
			NPC body = Main.npc[(int)Projectile.ai[1]];
			if (!body.active) {
				Projectile.Kill();
				return;
			}
			int age = 260 - Projectile.timeLeft;
			float radius = 90f + age * 1.6f;
			float angle = Projectile.ai[0] + age * 0.025f;
			Projectile.Center = body.Center + angle.ToRotationVector2() * radius;
			Projectile.rotation += 0.2f;
		}
	}

	/// <summary>Falling die; when it lands it bursts into as many shards as the number it rolled (ai[0]).</summary>
	public class BossDie : HouseProjectile
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/ExplodingDie";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 16;
			Projectile.tileCollide = true;
			Projectile.timeLeft = 180;
		}

		public override void AI()
		{
			Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.3f, 14f);
			Projectile.rotation += 0.15f;
		}

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f }, Projectile.Center);
			int count = Math.Clamp((int)Projectile.ai[0], 1, 6);
			CombatText.NewText(Projectile.Hitbox, Color.White, count.ToString());
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			for (int k = 0; k < count; k++) {
				float a = -MathHelper.PiOver2 + (k - (count - 1) / 2f) * 0.45f;
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center - new Vector2(0, 8), a.ToRotationVector2() * 7f,
					ModContent.ProjectileType<DiamondShard>(), Projectile.damage, 1f);
			}
		}
	}
}
