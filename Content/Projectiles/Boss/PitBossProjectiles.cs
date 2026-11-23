using System;
using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using CasinoWorld.Content.NPCs.Boss;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Projectiles.Boss
{
	/// <summary>Chip thrown in an arc (gravity 0.3); bounces twice off the floor. ai[0] colour 0 red, 1 blue, 2 gold.</summary>
	public class BouncingChip : WildCardProjectile
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/HighRollerChip";

		private Color Tint => (int)Projectile.ai[0] switch { 0 => new Color(255, 90, 90), 1 => new Color(110, 160, 255), _ => new Color(255, 220, 90) };
		protected override Color TrailColor => Tint;
		protected override Color GlowColor => Tint * 0.6f;
		protected override Color ImpactColor => Tint;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 16;
			Projectile.timeLeft = 300;
			Projectile.tileCollide = true;
		}

		public override void AI()
		{
			Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.3f, 14f);
			Projectile.rotation += Projectile.velocity.X * 0.08f;
			Lighting.AddLight(Projectile.Center, Tint.ToVector3() * 0.3f);
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (++Projectile.localAI[0] > 2)
				return true;
			SoundEngine.PlaySound(CasinoSounds.ChipFlick with { Volume = 0.4f }, Projectile.Center);
			CasinoFx.Sparks(Projectile.Center, Tint, 3, 2f, 0.7f);
			if (Projectile.velocity.X != oldVelocity.X)
				Projectile.velocity.X = -oldVelocity.X * 0.8f;
			if (Projectile.velocity.Y != oldVelocity.Y)
				Projectile.velocity.Y = -oldVelocity.Y * 0.65f;
			return false;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			CasinoFx.Draw(Projectile, TrailColor, GlowColor, 1f, Tint);
			return false;
		}
	}

	/// <summary>
	/// Place Your Bets floor zone. ai[0] own colour (0 red, 1 black), ai[1] winning colour, ai[2] telegraph ticks.
	/// Shows its colour, the winners flash for the last BetReveal ticks, then the winning zones erupt.
	/// </summary>
	public class BetZone : WildCardProjectile
	{
		private const int Width = 96, PillarHeight = 340, EruptTicks = 36;

		public override string Texture => "CasinoWorld/Content/Projectiles/HighRollerChip";

		private bool Winner => (int)Projectile.ai[0] == (int)Projectile.ai[1];
		private int Age => (int)Projectile.localAI[0];
		private bool Erupting => Age >= Projectile.ai[2] && Winner;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Width;
			Projectile.height = PillarHeight;
			Projectile.timeLeft = 400;
		}

		public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
		{
			// Anchor: the spawn position is the floor point under the zone's left edge.
			Projectile.position = new Vector2(Projectile.position.X, Projectile.position.Y - PillarHeight + Projectile.height / 2f);
		}

		public override bool CanHitPlayer(Player target) => Erupting && Age < Projectile.ai[2] + EruptTicks;

		public override void AI()
		{
			Projectile.velocity = Vector2.Zero;
			Projectile.localAI[0]++;
			if (Age == (int)Projectile.ai[2]) {
				if (Winner) {
					SoundEngine.PlaySound(CasinoSounds.ChipExplode with { Volume = 0.6f }, Projectile.Bottom);
					if (!Main.dedServ)
						Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Bottom, Vector2.UnitY, 3f, 6f, 10, 1200f, "BetZone"));
				}
				else
					Projectile.timeLeft = Math.Min(Projectile.timeLeft, 12);
			}
			if (Erupting) {
				for (int k = 0; k < 3; k++)
					CasinoFx.Spark(new Vector2(Projectile.position.X + Main.rand.Next(Width), Projectile.Bottom.Y - Main.rand.Next(PillarHeight)), new Vector2(0, -6f),
						(int)Projectile.ai[0] == 0 ? new Color(255, 80, 80) : new Color(170, 170, 200), 1.1f);
				if (Age > Projectile.ai[2] + EruptTicks)
					Projectile.Kill();
			}
		}

		public override void OnKill(int timeLeft) { }

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D px = TextureAssets.MagicPixel.Value;
			float t = Main.GlobalTimeWrappedHourly;
			bool red = (int)Projectile.ai[0] == 0;
			Color baseColor = red ? new Color(220, 40, 50) : new Color(40, 40, 52);
			Vector2 bottom = Projectile.Bottom - Main.screenPosition;
			var floor = new Rectangle((int)(Projectile.position.X - Main.screenPosition.X) + 2, (int)bottom.Y - 10, Width - 4, 10);
			int revealAt = (int)Projectile.ai[2] - ThePitBoss.BetReveal;
			float alpha = Math.Min(1f, Age / 15f);
			if (Age < Projectile.ai[2]) {
				Color c = baseColor * (0.55f * alpha);
				if (Age >= revealAt && Winner)
					c = Color.Lerp(baseColor, Color.White, 0.5f + 0.5f * (float)Math.Sin(t * 30)) * alpha;
				else if (Age >= revealAt)
					c *= 0.4f;
				Main.spriteBatch.Draw(px, floor, c);
				Main.spriteBatch.Draw(px, new Rectangle(floor.X, floor.Y - 2, floor.Width, 2), Color.Gold * (0.6f * alpha));
				// Rising columns of light show where the pillars will be.
				if (Age >= revealAt && Winner) {
					Color col = baseColor * 0.18f;
					col.A = 0;
					Main.spriteBatch.Draw(px, new Rectangle(floor.X, floor.Y - PillarHeight, floor.Width, PillarHeight), col);
				}
			}
			else if (Erupting) {
				float k = 1f - (Age - Projectile.ai[2]) / EruptTicks;
				Color col = (red ? new Color(255, 90, 80) : new Color(200, 200, 230)) * (0.8f * k);
				col.A = 0;
				Main.spriteBatch.Draw(px, new Rectangle(floor.X + 8, floor.Y - PillarHeight, floor.Width - 16, PillarHeight + 10), col);
				Main.spriteBatch.Draw(px, new Rectangle(floor.X + 24, floor.Y - PillarHeight, floor.Width - 48, PillarHeight + 10), Color.White * (0.6f * k));
			}
			return false;
		}
	}

	/// <summary>Gold bar: hangs at the ceiling for ai[0] ticks (shaking, dust line below), then drops and shatters.</summary>
	public class GoldBarDrop : WildCardProjectile
	{
		protected override Color TrailColor => new(255, 210, 90);
		protected override Color ImpactColor => new(255, 210, 90);

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = 30;
			Projectile.height = 16;
			Projectile.timeLeft = 400;
		}

		public override bool CanHitPlayer(Player target) => Projectile.localAI[0] >= Projectile.ai[0];

		public override void AI()
		{
			Projectile.localAI[0]++;
			if (Projectile.localAI[0] < Projectile.ai[0]) {
				Projectile.velocity = Vector2.Zero;
				Projectile.rotation = (float)Math.Sin(Projectile.localAI[0] * 1.3f) * 0.1f;
				if (Main.rand.NextBool(3))
					Dust.NewDustPerfect(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-12, 12), 0), DustID.GoldCoin, new Vector2(0, 2f), 0, default, 0.8f);
				return;
			}
			Projectile.tileCollide = Projectile.localAI[0] > Projectile.ai[0] + 8;
			Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.6f, 18f);
			Projectile.rotation = 0f;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			SoundEngine.PlaySound(SoundID.Item52 with { Pitch = -0.3f }, Projectile.Center);
			CasinoFx.Ring(Projectile.Bottom, new Color(255, 210, 90), 10, 3.5f, 1.1f);
			for (int k = 0; k < 10; k++)
				Dust.NewDustPerfect(Projectile.Bottom, DustID.GoldCoin, Main.rand.NextVector2Circular(4, 2) - Vector2.UnitY * 2, 0, default, 1.2f);
			return true;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Projectile.localAI[0] < Projectile.ai[0]) {
				// Warning line straight down.
				Color line = new Color(255, 210, 90) * (0.2f + 0.3f * Projectile.localAI[0] / Projectile.ai[0]);
				line.A = 0;
				Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)(Projectile.Center.X - Main.screenPosition.X) - 1, (int)(Projectile.Center.Y - Main.screenPosition.Y), 3, 800), line);
			}
			CasinoFx.Draw(Projectile, TrailColor, new Color(255, 200, 80) * 0.4f);
			return false;
		}
	}

	/// <summary>
	/// Double or Nothing coin. ai[0] boss, ai[1] 1 = tails, ai[2] target player. Spins over the player
	/// (its shadow follows them), locks, slams down: heads = shockwave, tails = the boss is stunned.
	/// </summary>
	public class GiantCoin : WildCardProjectile
	{
		private const int Follow = 70, Lock = 20;

		public override void SetStaticDefaults() => Main.projFrames[Type] = 3;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 64;
			Projectile.timeLeft = 400;
		}

		private bool Tails => Projectile.ai[1] == 1f;
		private ref float Age => ref Projectile.localAI[0];
		private bool landed;

		public override bool CanHitPlayer(Player target) => Age > Follow + Lock && !Tails && !landed;

		public override void AI()
		{
			Age++;
			Player target = Main.player[(int)Projectile.ai[2]];
			if (Age < Follow) {
				Vector2 goal = target.Center + new Vector2(0, -300);
				Projectile.Center = Vector2.Lerp(Projectile.Center, goal, 0.12f);
				Projectile.frame = (int)(Age / 4) % 3;
			}
			else if (Age < Follow + Lock) {
				Projectile.velocity = Vector2.Zero;
				Projectile.frame = Tails ? 2 : 0;
				if ((int)Age == Follow)
					SoundEngine.PlaySound(CasinoSounds.BossBell with { Pitch = 0.6f }, Projectile.Center);
			}
			else if (!landed) {
				Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 1.5f, 26f);
				float floor = ThePitBoss.FloorBelow(Projectile.Center);
				if (Projectile.Bottom.Y >= floor) {
					landed = true;
					Projectile.velocity = Vector2.Zero;
					Projectile.Bottom = new Vector2(Projectile.Center.X, floor);
					Projectile.timeLeft = 40;
					Land();
				}
			}
		}

		private void Land()
		{
			SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f }, Projectile.Center);
			if (!Main.dedServ)
				Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center, Vector2.UnitY, 10f, 8f, 25, 1500f, "GiantCoin"));
			CasinoFx.Ring(Projectile.Bottom, new Color(255, 220, 90), 24, 9f, 1.4f);
			if (Tails) {
				if (Main.npc[(int)Projectile.ai[0]].ModNPC is ThePitBoss boss && Main.netMode != NetmodeID.MultiplayerClient)
					boss.CoinTails();
				return;
			}
			// Heads: a shockwave runs along the floor both ways.
			if (Main.netMode != NetmodeID.MultiplayerClient)
				for (int side = -1; side <= 1; side += 2)
					Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Bottom - new Vector2(0, 20), new Vector2(side * 9f, 0),
						ModContent.ProjectileType<CoinShockwave>(), Projectile.damage * 2 / 3, 2f, Main.myPlayer);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			int fh = tex.Height / 3;
			var frame = new Rectangle(0, Projectile.frame * fh, tex.Width, fh);
			// Shadow on the floor under the coin while it hovers.
			if (!landed) {
				float floor = ThePitBoss.FloorBelow(Projectile.Center);
				float k = MathHelper.Clamp(Age / (Follow + Lock), 0f, 1f);
				Color shadow = Color.Black * (0.25f + 0.35f * k);
				Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)(Projectile.Center.X - Main.screenPosition.X) - (int)(20 + 20 * k), (int)(floor - Main.screenPosition.Y) - 4, (int)(40 + 40 * k), 6), shadow);
			}
			CasinoFx.Glow(Projectile, tex, frame, new Color(255, 210, 90), 1f);
			Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, Color.White * Projectile.Opacity, 0f, frame.Size() / 2f, 1f, SpriteEffects.None);
			if (landed && Projectile.timeLeft < 20)
				Projectile.alpha = Math.Min(255, Projectile.alpha + 13);
			return false;
		}
	}

	/// <summary>Floor shockwave from a heads coin: a fast wall of gold sparks running along the ground.</summary>
	public class CoinShockwave : WildCardProjectile
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/HighRollerChip";
		protected override Color ImpactColor => new(255, 220, 90);

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = 26;
			Projectile.height = 60;
			Projectile.timeLeft = 70;
		}

		public override void AI()
		{
			for (int k = 0; k < 3; k++)
				CasinoFx.Spark(Projectile.Bottom - new Vector2(Main.rand.NextFloat(-10, 10), Main.rand.NextFloat(0, 60)), new Vector2(0, -2f), new Color(255, 220, 90), 1f);
			Lighting.AddLight(Projectile.Center, 0.8f, 0.6f, 0.2f);
		}

		public override bool PreDraw(ref Color lightColor) => false;
	}
}
