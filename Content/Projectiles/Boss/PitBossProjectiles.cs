using System;
using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using CasinoWorld.Content.NPCs.Boss;
using CasinoWorld.Content.Subworlds;
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
		public override string Texture => "CasinoWorld/Content/Projectiles/NeutralChip";

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
	/// Place Your Bets: one board over the whole hall (every storey, wall to wall, floor to ceiling), split
	/// into red and black columns. ai[0] winning colour (0 red, 1 black), ai[1] telegraph ticks. Shows the
	/// colours, the winners flash for the last BetReveal ticks, then the winning columns erupt.
	/// </summary>
	public class BetZone : WildCardProjectile
	{
		public const int BoardWidth = (CasinoInterior.HallWidth - 1) * 16;
		public static readonly int BoardHeight = (CasinoInterior.Floors[0] - CasinoInterior.Ceiling - 1) * 16;
		private const int Columns = 24, EruptTicks = 30;
		private const float ColumnWidth = BoardWidth / (float)Columns;

		public override string Texture => "CasinoWorld/Content/Projectiles/NeutralChip";

		private static ReLogic.Content.Asset<Texture2D> suitTex, ballTex;

		public override void Unload() => suitTex = ballTex = null;

		private int Age => (int)Projectile.localAI[0];
		private bool Erupting => Age >= Projectile.ai[1];
		private bool Wins(int column) => column % 2 == (int)Projectile.ai[0];

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = BoardWidth;
			Projectile.height = BoardHeight;
			Projectile.timeLeft = 400;
		}

		public override void OnSpawn(Terraria.DataStructures.IEntitySource source) { }

		public override void OnKill(int timeLeft) { }

		public override bool? CanCutTiles() => false;

		public override bool CanHitPlayer(Player target) => Erupting && Age < Projectile.ai[1] + EruptTicks;

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			if (!Projectile.Hitbox.Intersects(targetHitbox))
				return false;
			// Standing a little over the edge of a winning column is forgiven.
			int first = ColumnAt(targetHitbox.Left + 7), last = ColumnAt(targetHitbox.Right - 7);
			return Wins(first) || Wins(last);
		}

		private int ColumnAt(float x) => Math.Clamp((int)((x - Projectile.position.X) / ColumnWidth), 0, Columns - 1);

		public override void AI()
		{
			Projectile.velocity = Vector2.Zero;
			Projectile.localAI[0]++;
			if (Age == (int)Projectile.ai[1] && !Main.dedServ) {
				SoundEngine.PlaySound(CasinoSounds.ChipExplode with { Volume = 0.8f }, Main.LocalPlayer.Center);
				global::CasinoWorld.Common.Effects.CasinoFx.Shake(Main.LocalPlayer.Center, Vector2.UnitY, 5f, 6f, 12, -1f, "BetZone");
			}
			if (Erupting) {
				if (!Main.dedServ)
					for (int k = 0; k < 24; k++) {
						Vector2 at = Main.screenPosition + new Vector2(Main.rand.Next(Main.screenWidth), Main.rand.Next(Main.screenHeight));
						if (Projectile.Hitbox.Contains(at.ToPoint()) && Wins(ColumnAt(at.X)))
							CasinoFx.Spark(at, new Vector2(0, -6f), Projectile.ai[0] == 0 ? new Color(255, 80, 80) : new Color(170, 170, 200), 1.1f);
					}
				if (Age > Projectile.ai[1] + EruptTicks)
					Projectile.Kill();
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (!ThePitBoss.InHall) {
				DrawRow(Projectile.position.Y, Projectile.Bottom.Y);
				return false;
			}
			for (int storey = 0; storey < CasinoInterior.Floors.Length; storey++)
				DrawRow((CasinoInterior.CeilingOf(storey) + 1) * 16, CasinoInterior.Floors[storey] * 16);
			return false;
		}

		private void DrawRow(float top, float bottom)
		{
			if (bottom < Main.screenPosition.Y || top > Main.screenPosition.Y + Main.screenHeight)
				return;
			Texture2D px = TextureAssets.MagicPixel.Value;
			float t = Main.GlobalTimeWrappedHourly;
			bool reveal = Age >= (int)Projectile.ai[1] - ThePitBoss.BetReveal;
			float alpha = Math.Min(1f, Age / 15f);
			suitTex ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/Suits", ReLogic.Content.AssetRequestMode.ImmediateLoad);
			ballTex ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/RouletteBall", ReLogic.Content.AssetRequestMode.ImmediateLoad);
			int y = (int)(top - Main.screenPosition.Y), height = (int)(bottom - top);
			for (int column = 0; column < Columns; column++) {
				int x = (int)(Projectile.position.X + column * ColumnWidth - Main.screenPosition.X) + 2;
				const int W = (int)ColumnWidth - 4;
				if (x + W < 0 || x > Main.screenWidth)
					continue;
				bool red = column % 2 == 0, wins = Wins(column);
				Color paint = red ? new Color(220, 40, 50) : new Color(40, 40, 52);
				Color light = red ? new Color(255, 80, 70) : new Color(185, 185, 230);
				if (!Erupting) {
					Color c = paint * (0.7f * alpha);
					if (reveal)
						c = wins ? Color.Lerp(paint, Color.White, 0.5f + 0.5f * (float)Math.Sin(t * 30)) : paint * 0.3f;
					Main.spriteBatch.Draw(px, new Rectangle(x, y + height - 10, W, 10), c);
					Main.spriteBatch.Draw(px, new Rectangle(x, y + height - 12, W, 2), Color.Gold * (0.6f * alpha));
					// Columns of light reach the ceiling, so the colours also read in the air.
					Color beam = light * (reveal ? wins ? 0.3f + 0.1f * (float)Math.Sin(t * 30) : 0f : 0.09f * alpha);
					PixelFx.Tile(Main.spriteBatch, "Column", new Rectangle(x, y, W, height), beam * 1.6f, 0, -(int)(t * (reveal && wins ? 160f : 30f)));
					// Suits hang in the columns: diamonds on red, spades on black.
					Color pip = light * (reveal ? wins ? 0.9f : 0.15f : 0.45f * alpha);
					Texture2D pips = suitTex.Value;
					for (int py = y + height / 4; py < y + height; py += height / 2)
						Main.spriteBatch.Draw(pips, new Vector2(x + W / 2f, py + (float)Math.Sin(t * 3 + column) * 4f), new Rectangle(red ? 12 : 0, 0, 12, 12), red ? Color.White * (pip.A / 255f) : pip, 0f, new Vector2(6), reveal && wins ? 3.2f : 2.4f, SpriteEffects.None, 0f);
				}
				else if (wins) {
					float k = MathHelper.Clamp(1.25f - (Age - Projectile.ai[1]) / EruptTicks, 0f, 1f);
					Texture2D erupt = PixelFx.Tex("ColumnErupt");
					int eh = erupt.Height / 3;
					PixelFx.Tile(Main.spriteBatch, erupt, new Rectangle(0, (int)(t * 14f) % 3 * eh, erupt.Width, eh), new Rectangle(x, y, W, height), Color.Lerp(light, Color.White, 0.25f) * k, 0, (int)(t * 420f));
				}
			}
			// The ball: rolls past and slows down into the winning column nearest to the viewer.
			if (!Erupting) {
				float spin = MathHelper.Clamp(Age / Math.Max(1f, Projectile.ai[1] - ThePitBoss.BetReveal), 0f, 1f);
				int home = ColumnAt(Main.LocalPlayer.Center.X);
				if (!Wins(home))
					home += home + 1 < Columns ? 1 : -1;
				float travel = (1f - spin) * (1f - spin) * 2600f;
				float ballX = Projectile.position.X + (home + 0.5f) * ColumnWidth - travel;
				ballX = Projectile.position.X + ((ballX - Projectile.position.X) % BoardWidth + BoardWidth) % BoardWidth;
				float hop = Math.Abs((float)Math.Sin(Age * 0.22f)) * (8f + 46f * (1f - spin));
				Main.spriteBatch.Draw(ballTex.Value, new Vector2(ballX - Main.screenPosition.X, y + height - 24 - hop), null, Color.White * alpha, Age * 0.3f, ballTex.Size() / 2f, 2.4f, SpriteEffects.None, 0f);
			}
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
				float warn = Projectile.localAI[0] / Projectile.ai[0];
				PixelFx.DropWarning(Main.spriteBatch, Projectile.Center.X - Main.screenPosition.X, Projectile.Center.Y - Main.screenPosition.Y,
					ThePitBoss.FloorBelow(Projectile.Bottom) - Main.screenPosition.Y, Color.Lerp(new Color(255, 225, 130) * 0.5f, new Color(255, 110, 70), warn), Main.GlobalTimeWrappedHourly);
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

		/// <summary>Over the player and a little ahead of them, but always under the ceiling of their storey.</summary>
		public static Vector2 HoverSpot(Player target)
		{
			float ceiling = ThePitBoss.CeilingAbove(target.Center);
			return new Vector2(target.Center.X + target.velocity.X * 16f, Math.Max(target.Center.Y - 300f, ceiling + 40f));
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
				Projectile.Center = Vector2.Lerp(Projectile.Center, HoverSpot(target), 0.2f);
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
				global::CasinoWorld.Common.Effects.CasinoFx.Shake(Projectile.Center, Vector2.UnitY, 10f, 8f, 25, 1500f, "GiantCoin");
			CasinoFx.Ring(Projectile.Bottom, new Color(255, 220, 90), 24, 9f, 1.4f);
			if (Tails) {
				if (Main.npc[(int)Projectile.ai[0]].ModNPC is ThePitBoss boss && Main.netMode != NetmodeID.MultiplayerClient)
					boss.CoinTails();
				return;
			}
			// Heads: a shockwave runs along the floor both ways and coins burst up, which also reaches fliers.
			if (Main.netMode != NetmodeID.MultiplayerClient)
				for (int k = -3; k <= 3; k++)
					Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Bottom - new Vector2(0, 24), new Vector2(k * 3.2f, -13f + Math.Abs(k) * 0.7f),
						ModContent.ProjectileType<BouncingChip>(), Projectile.damage * 3 / 5, 1f, Main.myPlayer, 2);
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
				Color lane = Age < Follow ? new Color(255, 225, 130) * 0.35f : Tails ? new Color(90, 255, 130) * 0.8f : new Color(255, 80, 60) * 0.85f;
				PixelFx.Tile(Main.spriteBatch, "Column", new Rectangle((int)(Projectile.Center.X - Main.screenPosition.X) - 48, (int)(Projectile.Center.Y - Main.screenPosition.Y), 96, (int)Math.Max(0, floor - Projectile.Center.Y)), lane, 0, -(int)(Age * 4f));
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
		public override string Texture => "CasinoWorld/Content/Projectiles/NeutralChip";
		protected override Color ImpactColor => new(255, 220, 90);

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = 26;
			Projectile.height = 60;
			Projectile.timeLeft = 130;
		}

		public override void AI()
		{
			for (int k = 0; k < 3; k++)
				CasinoFx.Spark(Projectile.Bottom - new Vector2(Main.rand.NextFloat(-10, 10), Main.rand.NextFloat(0, 60)), new Vector2(0, -2f), new Color(255, 220, 90), 1f);
			Lighting.AddLight(Projectile.Center, 0.8f, 0.6f, 0.2f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			// A breaking wave of chips: taller in the middle, leaning forward.
			Texture2D chip = TextureAssets.Projectile[Type].Value;
			int dir = Math.Sign(Projectile.velocity.X);
			float t = Main.GlobalTimeWrappedHourly;
			for (int k = 0; k < 5; k++) {
				float up = k / 4f;
				Vector2 at = Projectile.Bottom - Main.screenPosition + new Vector2(dir * (float)Math.Sin(up * 2.2f) * 14f - dir * 6f, -8f - up * 48f + (float)Math.Sin(t * 14f + k) * 2f);
				Color glow = new Color(255, 200, 70) * 0.5f;
				glow.A = 0;
				Main.EntitySpriteDraw(chip, at, null, glow, 0f, chip.Size() / 2f, 2f - up * 0.5f, SpriteEffects.None);
				Main.EntitySpriteDraw(chip, at, null, new Color(255, 225, 110), t * 12f * dir + k, chip.Size() / 2f, 1.4f - up * 0.4f, SpriteEffects.None);
			}
			return false;
		}
	}
}
