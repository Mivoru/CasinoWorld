using System;
using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using Terraria.Graphics.CameraModifiers;
using CasinoWorld.Content.NPCs.Boss;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Projectiles.Boss
{
	/// <summary>Base for The Wild Card's hostile projectiles.</summary>
	public abstract class WildCardProjectile : ModProjectile
	{
		/// <summary>Trail and glow colours; Transparent = none. Every hostile shot gets a readable trail and glow.</summary>
		protected virtual Color TrailColor => Color.Transparent;
		protected virtual Color GlowColor => Color.Transparent;
		protected virtual Color ImpactColor => new(200, 120, 255);

		public override void SetStaticDefaults() => CasinoFx.EnableTrail(Type, 10);

		public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
		{
			if (Projectile.hostile)
				CasinoFx.Sparks(Projectile.Center, ImpactColor, 3, 2f, 0.8f);
		}

		public override void OnKill(int timeLeft)
		{
			if (Projectile.hostile)
				CasinoFx.Sparks(Projectile.Center, ImpactColor, 6, 2.5f, 0.9f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			CasinoFx.Draw(Projectile, TrailColor, GlowColor);
			return false;
		}

		public override void SetDefaults()
		{
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 300;
		}

		protected void DrawCentered(Texture2D tex, Rectangle? frame, Color color, float rotation, float scale = 1f, SpriteEffects effects = SpriteEffects.None)
		{
			Vector2 origin = frame.HasValue ? frame.Value.Size() / 2f : tex.Size() / 2f;
			Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, color, rotation, origin, scale, effects);
		}
	}

	/// <summary>A dealt card: flies straight and slowly speeds up.</summary>
	public class DealtCard : WildCardProjectile
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/Boss/PickupCard";
		protected override Color TrailColor => new(200, 120, 255);
		protected override Color GlowColor => new(170, 90, 255);

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 14;
			Projectile.timeLeft = 220;
		}

		public override void AI()
		{
			Projectile.velocity *= 1.012f;
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
			Lighting.AddLight(Projectile.Center, 0.3f, 0.15f, 0.45f);
		}

		public override void OnKill(int timeLeft) => CasinoFx.CardImpact(Projectile.Center, Projectile.velocity, new Color(170, 90, 255), false);
	}

	/// <summary>
	/// A big card in the shell game. Its position comes from the boss's shuffle (same seed on every
	/// client). The real card glows while the cards are shown; after the shuffle the fakes burst into
	/// a ring of spades and the real one turns back into the boss.
	/// ai[0] boss index, ai[1] starting slot, ai[2] 1 if real.
	/// </summary>
	public class ShellCard : WildCardProjectile
	{
		public override string Texture => "CasinoWorld/Content/NPCs/Boss/WildCard_ShellCard";

		private bool burst;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.hostile = false;
			Projectile.width = 64;
			Projectile.height = 92;
			Projectile.timeLeft = 2000;
		}

		private TheWildCard Boss => Main.npc[(int)Projectile.ai[0]].ModNPC as TheWildCard;

		private bool Real => Projectile.ai[2] == 1f;

		public override void AI()
		{
			NPC npc = Main.npc[(int)Projectile.ai[0]];
			TheWildCard boss = Boss;
			if (!npc.active || boss == null || npc.ai[0] != (float)TheWildCard.State.ShellGame) {
				Projectile.Kill();
				return;
			}
			float t = npc.ai[1];
			Projectile.Center = boss.ShellCardPosition((int)Projectile.ai[1], t);
			int revealAt = TheWildCard.ShellShowTicks + boss.ShellShuffleTicks;
			if (Real && t < TheWildCard.ShellShowTicks && Main.rand.NextBool(3))
				Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30, 44), DustID.GoldFlame, Vector2.Zero, 0, default, 1.2f).noGravity = true;

			if (!Real && !burst && t >= revealAt + 28) {
				burst = true;
				SoundEngine.PlaySound(CasinoSounds.CardSoldierDeath, Projectile.Center);
				if (Main.netMode != NetmodeID.MultiplayerClient) {
					int count = boss.Phase == 1 ? 8 : 12;
					for (int k = 0; k < count; k++) {
						Vector2 v = Vector2.UnitX.RotatedBy(MathHelper.TwoPi * k / count) * 4.5f;
						Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, v, ModContent.ProjectileType<SpadeShot>(), Projectile.damage, 1f, Main.myPlayer);
					}
				}
				for (int k = 0; k < 20; k++)
					Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, Main.rand.NextVector2Circular(5, 5), 0, default, 1.3f).noGravity = true;
				Projectile.Kill();
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			NPC npc = Main.npc[(int)Projectile.ai[0]];
			TheWildCard boss = Boss;
			if (boss == null)
				return false;
			float t = npc.ai[1];
			int revealAt = TheWildCard.ShellShowTicks + boss.ShellShuffleTicks;
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			// Flip animation at the reveal: the card turns sideways and back.
			float flip = t >= revealAt && t < revealAt + 20 ? Math.Abs((float)Math.Cos((t - revealAt) / 20f * Math.PI)) : 1f;
			float wobble = (float)Math.Sin(t * 0.3f + Projectile.ai[1]) * 0.05f;
			Vector2 scale = new(Math.Max(0.08f, flip), 1f);
			Color color = Color.White;
			if (Real && t < TheWildCard.ShellShowTicks) {
				float glow = 0.5f + 0.5f * (float)Math.Sin(t * 0.4f);
				Color halo = new Color(255, 220, 120) * glow;
				halo.A = 0;
				for (int k = 0; k < 4; k++)
					Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition + Vector2.UnitX.RotatedBy(k * MathHelper.PiOver2) * 4f, null, halo, wobble, tex.Size() / 2f, scale, SpriteEffects.None);
			}
			if (t >= revealAt + 10 && !Real)
				color = Color.Lerp(Color.White, new Color(200, 80, 255), (t - revealAt - 10) / 18f);
			Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, color, wobble, tex.Size() / 2f, scale, SpriteEffects.None);
			return false;
		}
	}

	/// <summary>Spade bullets from a burst fake card.</summary>
	public class SpadeShot : WildCardProjectile
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 12;
			Projectile.timeLeft = 200;
		}

		protected override Color TrailColor => new(190, 100, 255);
		protected override Color GlowColor => new(160, 80, 255);

		public override void AI()
		{
			Projectile.velocity *= 1.02f;
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
			Lighting.AddLight(Projectile.Center, 0.4f, 0.2f, 0.6f);
		}
	}

	/// <summary>52 Pickup: shows a red line for ai[0] ticks, then the card drops along it.</summary>
	public class PickupCard : WildCardProjectile
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 14;
			Projectile.timeLeft = 260;
		}

		protected override Color TrailColor => new(255, 90, 90);
		protected override Color ImpactColor => new(255, 90, 90);

		public override bool CanHitPlayer(Player target) => Projectile.localAI[0] >= Projectile.ai[0];

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			Collision.HitTiles(Projectile.position, oldVelocity, Projectile.width, Projectile.height);
			CasinoFx.CardImpact(Projectile.Center, oldVelocity, new Color(255, 90, 90), Main.rand.NextBool(3));
			return true;
		}

		public override void AI()
		{
			Projectile.localAI[0]++;
			if (Projectile.localAI[0] < Projectile.ai[0]) {
				Projectile.velocity = Vector2.Zero;
				return;
			}
			if (Projectile.localAI[0] == Projectile.ai[0])
				SoundEngine.PlaySound(CasinoSounds.CardThrow with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
			Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.9f, 15f);
			Projectile.rotation += 0.3f;
			// Only collide once it has dropped past where it was dealt, so cards above ceilings still fall in.
			Projectile.tileCollide = Projectile.localAI[0] > Projectile.ai[0] + 30;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Projectile.localAI[0] < Projectile.ai[0]) {
				// Telegraph: a pulsing red line straight down.
				float p = Projectile.localAI[0] / Projectile.ai[0];
				Color line = new Color(255, 60, 60) * (0.25f + 0.35f * p);
				line.A = 0;
				PixelFx.DropWarning(Main.spriteBatch, Projectile.Center.X - Main.screenPosition.X, Projectile.Center.Y - Main.screenPosition.Y, Projectile.Center.Y - Main.screenPosition.Y + 1000f, line * 2f, Main.GlobalTimeWrappedHourly);
				Color star = new Color(255, 120, 120) * (0.5f + 0.5f * (float)Math.Sin(Projectile.localAI[0] * 0.5f));
				star.A = 0;
				Main.EntitySpriteDraw(TextureAssets.Projectile[Type].Value, Projectile.Center - Main.screenPosition, null, star, 0f, TextureAssets.Projectile[Type].Value.Size() / 2f, 1.6f + p * 0.6f, SpriteEffects.None);
			}
			CasinoFx.Draw(Projectile, TrailColor, Color.Transparent);
			return false;
		}
	}

	/// <summary>Shared drawing for the dice (6 faces in one row).</summary>
	public abstract class DieProjectile : WildCardProjectile
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/Boss/WildDie";

		protected virtual float DrawScale => 1f;

		/// <summary>Face to show: tumbling while airborne, the rolled face once settled.</summary>
		protected int Face(bool settled) => settled ? Math.Clamp((int)Projectile.ai[0], 1, 6) : (int)(Projectile.localAI[1] / 4) % 6 + 1;

		protected void DrawDie(bool settled, Color color)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			var frame = new Rectangle((Face(settled) - 1) * 32, 0, 32, 32);
			DrawCentered(tex, frame, color, Projectile.rotation, DrawScale);
		}
	}

	/// <summary>The two dice of a roll (harmless): they bounce, settle and show the rolled faces. ai[0] face.</summary>
	public class RollingDie : DieProjectile
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.hostile = false;
			Projectile.tileCollide = true;
			Projectile.width = Projectile.height = 28;
			Projectile.timeLeft = 170;
		}

		private bool Settled => Projectile.localAI[0] > 0;

		public override void AI()
		{
			Projectile.localAI[1]++;
			if (!Settled) {
				Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.35f, 12f);
				Projectile.rotation += Projectile.velocity.X * 0.08f;
			}
			if (Projectile.timeLeft < 30)
				Projectile.alpha = Math.Min(255, Projectile.alpha + 9);
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (Math.Abs(oldVelocity.Y) > 2f) {
				SoundEngine.PlaySound(CasinoSounds.DiceBounce, Projectile.Center);
				CasinoFx.DieImpact(Projectile.Bottom, new Color(255, 240, 200), false);
				Projectile.velocity.Y = -oldVelocity.Y * 0.45f;
				Projectile.velocity.X *= 0.7f;
			}
			else {
				Projectile.velocity = Vector2.Zero;
				if (!Settled) {
					Projectile.localAI[0] = 1;
					Projectile.rotation = 0f;
					if (!Main.dedServ) {
						CombatText.NewText(Projectile.Hitbox, Color.White, ((int)Projectile.ai[0]).ToString(), true);
						CasinoFx.Ring(Projectile.Center, new Color(255, 240, 200), 10, 2.5f);
					}
				}
			}
			return false;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			DrawDie(Settled, Color.White * (1f - Projectile.alpha / 255f));
			return false;
		}
	}

	/// <summary>Bouncing die (dice roll of 3-6): bounces off the ground a few times.</summary>
	public class BouncingDie : DieProjectile
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.tileCollide = true;
			Projectile.width = Projectile.height = 26;
			Projectile.timeLeft = 300;
		}

		public override void AI()
		{
			Projectile.localAI[1]++;
			Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.3f, 12f);
			Projectile.rotation += Projectile.velocity.X * 0.07f;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (++Projectile.localAI[0] > 4)
				return true;
			SoundEngine.PlaySound(CasinoSounds.DiceBounce, Projectile.Center);
			CasinoFx.DieImpact(Projectile.Bottom, new Color(200, 120, 255), false);
			Collision.HitTiles(Projectile.position, oldVelocity, Projectile.width, Projectile.height);
			if (Projectile.velocity.X != oldVelocity.X)
				Projectile.velocity.X = -oldVelocity.X;
			if (Projectile.velocity.Y != oldVelocity.Y)
				Projectile.velocity.Y = -Math.Max(7f, Math.Abs(oldVelocity.Y) * 0.85f);
			return false;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			CasinoFx.Trail(Projectile, tex, new Rectangle((Face(false) - 1) * 32, 0, 32, 32), new Color(200, 120, 255), DrawScale);
			DrawDie(false, Color.White);
			return false;
		}
	}

	/// <summary>Boxcars (roll of 12): a giant die that drops and rolls along the ground; jump over it.</summary>
	public class BoxcarDie : DieProjectile
	{
		protected override float DrawScale => 2.4f;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.tileCollide = true;
			Projectile.width = Projectile.height = 70;
			Projectile.timeLeft = 330;
		}

		public override void AI()
		{
			Projectile.localAI[1]++;
			if (Projectile.localAI[0] == 0)
				Projectile.localAI[0] = Math.Sign(Projectile.velocity.X);
			Projectile.velocity.X = Projectile.localAI[0] * 7.5f;
			Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.5f, 14f);
			Projectile.rotation += Projectile.velocity.X * 0.02f;
			if ((int)Projectile.localAI[1] % 14 == 0) {
				SoundEngine.PlaySound(CasinoSounds.DiceBounce with { Pitch = -0.6f, Volume = 0.8f }, Projectile.Center);
				if (!Main.dedServ && Projectile.velocity.Y == 0f) {
					global::CasinoWorld.Common.Effects.CasinoFx.Shake(Projectile.Center, Vector2.UnitY, 3f, 6f, 10, 900f, "Boxcar");
					for (int k = 0; k < 8; k++)
						Dust.NewDustPerfect(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-30, 30), 0), DustID.Smoke, new Vector2(-Projectile.velocity.X * 0.1f, -1.5f), 120, default, 1.4f);
				}
			}
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (Projectile.velocity.X != oldVelocity.X && Math.Abs(oldVelocity.X) > 1f) {
				// Climb small steps; stop at walls.
				Projectile.position.Y -= 16f;
				if (Collision.SolidCollision(Projectile.position + new Vector2(oldVelocity.X, 0), Projectile.width, Projectile.height))
					return true;
			}
			if (Projectile.velocity.Y != oldVelocity.Y)
				Projectile.velocity.Y = 0f;
			return false;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			CasinoFx.Trail(Projectile, tex, new Rectangle((Face(false) - 1) * 32, 0, 32, 32), new Color(255, 80, 80), DrawScale);
			DrawDie(false, Color.Lerp(Lighting.GetColor(Projectile.Center.ToTileCoordinates()), Color.White, 0.5f));
			return false;
		}
	}

	/// <summary>Snake Eyes: a snake of poker chips that hunts the player for five seconds.</summary>
	public class ChipSnake : WildCardProjectile
	{
		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 16;
			ProjectileID.Sets.TrailingMode[Type] = 0;
		}

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 18;
			Projectile.timeLeft = 330;
		}

		public override void AI()
		{
			Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
			if (Projectile.timeLeft > 30) {
				Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 7.5f;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.035f);
			}
			Projectile.rotation = Projectile.velocity.ToRotation();
			if (Projectile.timeLeft < 30)
				Projectile.alpha += 8;
			Lighting.AddLight(Projectile.Center, 0.5f, 0.15f, 0.15f);
			if (Main.rand.NextBool(3))
				CasinoFx.Spark(Projectile.Center, -Projectile.velocity * 0.15f, new Color(255, 80, 80), 0.7f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			var head = new Rectangle(0, 0, 20, 20);
			var body = new Rectangle(20, 0, 20, 20);
			float fade = 1f - Projectile.alpha / 255f;
			for (int k = Projectile.oldPos.Length - 1; k >= 1; k -= 2) {
				if (Projectile.oldPos[k] == Vector2.Zero)
					continue;
				float s = 1f - k * 0.03f;
				Main.EntitySpriteDraw(tex, Projectile.oldPos[k] + Projectile.Size / 2f - Main.screenPosition, body, Color.White * fade, Projectile.oldRot[k], new Vector2(10), s, SpriteEffects.None);
			}
			Color eye = new Color(255, 60, 60) * fade;
			eye.A = 0;
			DrawCentered(tex, head, eye * 0.6f, Projectile.rotation, 1.3f);
			DrawCentered(tex, head, Color.White * fade, Projectile.rotation);
			return false;
		}
	}

	/// <summary>Lucky Seven: bursts outwards, slows, then homes in briefly.</summary>
	public class LuckySeven : WildCardProjectile
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 16;
			Projectile.timeLeft = 300;
		}

		public override void AI()
		{
			Projectile.ai[1]++;
			if (Projectile.ai[1] < 40)
				Projectile.velocity *= 0.95f;
			else if (Projectile.ai[1] < 100) {
				Player target = Main.player[Player.FindClosest(Projectile.position, 1, 1)];
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 9f, 0.05f);
			}
			Projectile.rotation = (float)Math.Sin(Projectile.ai[1] * 0.2f) * 0.3f;
			Lighting.AddLight(Projectile.Center, 0.8f, 0.2f, 0.2f);
			if (Projectile.ai[1] == 40)
				CasinoFx.Ring(Projectile.Center, new Color(255, 80, 80), 8, 2f);
		}

		protected override Color TrailColor => new(255, 90, 90);
		protected override Color GlowColor => new(255, 60, 60);
		protected override Color ImpactColor => new(255, 90, 90);
	}

	/// <summary>All In barrage chip: red (ai[0] 0) or black (1).</summary>
	public class AllInChip : WildCardProjectile
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/Boss/ChipSnake";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 16;
			Projectile.timeLeft = 240;
		}

		protected override Color ImpactColor => Projectile.ai[0] == 0 ? new Color(255, 80, 80) : new Color(160, 160, 200);

		public override void AI()
		{
			Projectile.rotation += 0.2f;
			Lighting.AddLight(Projectile.Center, Projectile.ai[0] == 0 ? new Vector3(0.4f, 0.1f, 0.1f) : new Vector3(0.15f, 0.15f, 0.25f));
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Color tint = Projectile.ai[0] == 0 ? Color.White : new Color(70, 70, 90);
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			CasinoFx.Trail(Projectile, tex, new Rectangle(20, 0, 20, 20), Projectile.ai[0] == 0 ? new Color(255, 70, 70) : new Color(140, 140, 190), 0.85f);
			DrawCentered(tex, new Rectangle(20, 0, 20, 20), tint, Projectile.rotation, 0.85f);
			return false;
		}
	}

	/// <summary>Harmless card from The Wild Card's death burst: spins, falls and fades.</summary>
	public class DeathCard : WildCardProjectile
	{
		public override string Texture => "CasinoWorld/Content/NPCs/Boss/WildCard_Card";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.hostile = false;
			Projectile.width = Projectile.height = 16;
			Projectile.timeLeft = 150;
		}

		public override void AI()
		{
			Projectile.velocity *= 0.97f;
			Projectile.velocity.Y += 0.12f;
			Projectile.rotation += Projectile.velocity.X * 0.05f + 0.05f;
			if (Projectile.timeLeft < 40)
				Projectile.alpha += 6;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			DrawCentered(TextureAssets.Projectile[Type].Value, null, Color.White * (1f - Projectile.alpha / 255f), Projectile.rotation);
			return false;
		}
	}
}
