using System;
using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using CasinoWorld.Content.NPCs.Boss;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Projectiles.Boss
{
	/// <summary>Base for Fortuna's shots; she removes them all on a phase change.</summary>
	public abstract class FateProjectile : WildCardProjectile
	{
		protected override Color ImpactColor => Fortuna.GoldLight;
		protected ref float Age => ref Projectile.localAI[0];
		protected Texture2D Tex => TextureAssets.Projectile[Type].Value;

		public override void SetStaticDefaults()
		{
			base.SetStaticDefaults();
			ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
		}

		public override bool? CanCutTiles() => false;
	}

	/// <summary>Violet chip with a star: flies straight.</summary>
	public class FateChip : FateProjectile
	{
		protected override Color TrailColor => Fortuna.FateViolet;
		protected override Color GlowColor => Fortuna.FateViolet * 0.6f;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 18;
			Projectile.timeLeft = 260;
		}

		public override void AI()
		{
			Projectile.rotation += 0.12f * Math.Sign(Projectile.velocity.X + 0.01f);
			Lighting.AddLight(Projectile.Center, 0.4f, 0.25f, 0.6f);
		}
	}

	/// <summary>
	/// Ring Toss pocket: rides a circle that opens to <see cref="Reach"/> and closes again while turning.
	/// ai[0] angle, ai[1] turning direction, ai[2] colour frame. The centre is where it was thrown.
	/// </summary>
	public class FatePocket : FateProjectile
	{
		private const int Trip = 150;
		private const float Reach = 880f;
		public override string Texture => "CasinoWorld/Content/NPCs/Boss/Fortuna_Pocket";
		private Vector2 home;

		public override void SetStaticDefaults()
		{
			base.SetStaticDefaults();
			Main.projFrames[Type] = 3;
		}

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 22;
			Projectile.timeLeft = Trip;
		}

		public override bool CanHitPlayer(Player target) => Age > 8;

		public override void AI()
		{
			if (Age == 0)
				home = Projectile.Center;
			Age++;
			Projectile.frame = (int)Projectile.ai[2];
			float angle = Projectile.ai[0] + Projectile.ai[1] * 0.0085f * Age;
			float radius = (float)Math.Sin(MathHelper.Pi * Age / Trip) * Reach;
			Projectile.Center = home + angle.ToRotationVector2() * radius;
			Projectile.rotation = angle + MathHelper.PiOver2;
			Lighting.AddLight(Projectile.Center, 0.6f, 0.45f, 0.2f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			CasinoFx.Draw(Projectile, Fortuna.GoldLight * 0.8f, Fortuna.GoldLight * 0.6f, 1.15f);
			return false;
		}
	}

	/// <summary>
	/// Star die: hangs over its spot (dashed line and target mark), drops, bursts. ai[0] y to burst at, ai[1] number.
	/// The number is how many pairs of chips fly out.
	/// </summary>
	public class FateDie : FateProjectile
	{
		public const float DropHeight = 620f;
		private const int Hang = 42;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 44;
			Projectile.timeLeft = 200;
		}

		public override bool CanHitPlayer(Player target) => Age > Hang;

		public override void AI()
		{
			Age++;
			if (Age <= Hang) {
				Projectile.velocity = Vector2.Zero;
				Projectile.rotation = (float)Math.Sin(Age * 0.5f) * 0.15f;
				if (Age == 1)
					SoundEngine.PlaySound(CasinoSounds.DiceThrow with { Pitch = -0.5f, Volume = 1f }, Projectile.Center);
				return;
			}
			if (Age == Hang + 1)
				SoundEngine.PlaySound(CasinoSounds.MeteorCall, Projectile.Center);
			Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 2.2f, 27f);
			Projectile.rotation = 0f;
			CasinoFx.Spark(Projectile.Center + Main.rand.NextVector2Circular(18, 18), new Vector2(0, -3f), Fortuna.GoldLight, 1.1f);
			Lighting.AddLight(Projectile.Center, 0.9f, 0.7f, 0.3f);
			if (Projectile.Center.Y >= Projectile.ai[0]) {
				Projectile.Center = new Vector2(Projectile.Center.X, Projectile.ai[0]);
				Projectile.Kill();
			}
		}

		public override void OnKill(int timeLeft)
		{
			if (Age <= Hang)
				return;
			SoundEngine.PlaySound(CasinoSounds.ChipExplode, Projectile.Center);
			CasinoFx.Explosion(Projectile.Center, 90, Fortuna.GoldLight, Fortuna.FateViolet, 2f);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			int count = (int)Projectile.ai[1] * 2;
			float offset = Main.rand.NextFloat(MathHelper.TwoPi);
			for (int k = 0; k < count; k++)
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, (offset + k * MathHelper.TwoPi / count).ToRotationVector2() * 6.5f,
					ModContent.ProjectileType<FateChip>(), Projectile.damage * 4 / 5, 1f, Main.myPlayer);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			var frame = new Rectangle(((int)MathHelper.Clamp(Projectile.ai[1], 1, 6) - 1) * 48, 0, 48, 48);
			if (Age <= Hang) {
				float warn = Age / Hang;
				PixelFx.DropWarning(Main.spriteBatch, Projectile.Center.X - Main.screenPosition.X, Projectile.Center.Y - Main.screenPosition.Y, Projectile.ai[0] - Main.screenPosition.Y,
					Color.Lerp(Fortuna.GoldLight * 0.5f, new Color(255, 110, 90), warn), Main.GlobalTimeWrappedHourly);
			}
			CasinoFx.Trail(Projectile, Tex, frame, Fortuna.GoldLight, 1f);
			CasinoFx.Glow(Projectile, Tex, frame, Fortuna.GoldLight * 0.6f, 1f);
			Main.EntitySpriteDraw(Tex, Projectile.Center - Main.screenPosition, frame, Color.White, Projectile.rotation, new Vector2(24), 1f, SpriteEffects.None);
			return false;
		}
	}

	/// <summary>
	/// Three Reels: columns of light all around, repeating every three. They spin and stop one kind after another;
	/// the kind that shows the clover is safe, the others erupt. ai[0] safe kind, ai[1] and ai[2] looks of the rest.
	/// </summary>
	public class FateReels : FateProjectile
	{
		private const int FirstStop = 42, StopGap = 20, Blast = 122, BlastTicks = 26;
		private const int Half = 7;          // columns on each side of the middle one
		private const float Width = 232f, Height = 2600f;
		public override string Texture => "CasinoWorld/Content/Projectiles/Boss/ReelSymbol";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = (int)(Width * (Half * 2 + 1));
			Projectile.height = (int)Height;
			Projectile.timeLeft = Blast + BlastTicks;
		}

		private int Kind(int column) => ((column - Half) % 3 + 3) % 3;
		private bool Safe(int column) => Kind(column) == (int)Projectile.ai[0];
		private int StopTick(int kind) => FirstStop + kind * StopGap;

		private int ColumnAt(float x) => Math.Clamp((int)((x - Projectile.position.X) / Width), 0, Half * 2);

		public override void OnSpawn(Terraria.DataStructures.IEntitySource source) { }

		public override void OnKill(int timeLeft) { }

		public override bool CanHitPlayer(Player target) => Age >= Blast;

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			if (!Projectile.Hitbox.Intersects(targetHitbox))
				return false;
			// Standing a little over the edge is forgiven.
			return !Safe(ColumnAt(targetHitbox.Left + 8)) || !Safe(ColumnAt(targetHitbox.Right - 8));
		}

		public override void AI()
		{
			Projectile.velocity = Vector2.Zero;
			Age++;
			if (Main.dedServ)
				return;
			if (Age == 1)
				SoundEngine.PlaySound(CasinoSounds.BossSpin);
			for (int kind = 0; kind < 3; kind++)
				if ((int)Age == StopTick(kind))
					SoundEngine.PlaySound(CasinoSounds.FortunaReel with { Pitch = kind * 0.15f });
			if ((int)Age == Blast) {
				SoundEngine.PlaySound(CasinoSounds.FortunaBeam with { Volume = 0.7f });
				CasinoFx.Shake(Main.LocalPlayer.Center, Vector2.UnitY, 6f, 7f, 16, -1f, "FateReels");
			}
			if (Age > Blast)
				for (int k = 0; k < 20; k++) {
					Vector2 at = Main.screenPosition + new Vector2(Main.rand.Next(Main.screenWidth), Main.rand.Next(Main.screenHeight));
					if (Projectile.Hitbox.Contains(at.ToPoint()) && !Safe(ColumnAt(at.X)))
						CasinoFx.Spark(at, new Vector2(0, -7f), Fortuna.FateRed, 1.2f);
				}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			SpriteBatch sb = Main.spriteBatch;
			Texture2D symbols = Tex, px = TextureAssets.MagicPixel.Value;
			float t = Main.GlobalTimeWrappedHourly, alpha = Math.Min(1f, Age / 14f);
			int top = (int)Math.Max(0, Projectile.position.Y - Main.screenPosition.Y), bottom = (int)Math.Min(Main.screenHeight, Projectile.Bottom.Y - Main.screenPosition.Y);
			if (bottom <= top)
				return false;
			for (int column = 0; column <= Half * 2; column++) {
				int x = (int)(Projectile.position.X + column * Width - Main.screenPosition.X);
				if (x > Main.screenWidth || x + Width < 0)
					continue;
				int kind = Kind(column);
				bool safe = Safe(column), stopped = Age >= StopTick(kind);
				Color paint = !stopped ? Fortuna.GoldLight : safe ? new Color(110, 255, 140) : Fortuna.FateRed;
				var area = new Rectangle(x + 4, top, (int)Width - 8, bottom - top);
				if (Age >= Blast) {
					if (safe)
						continue;
					float k = MathHelper.Clamp(1.2f - (Age - Blast) / BlastTicks, 0f, 1f);
					Texture2D erupt = PixelFx.Tex("ColumnErupt");
					int eh = erupt.Height / 3;
					PixelFx.Tile(sb, erupt, new Rectangle(0, (int)(t * 14f) % 3 * eh, erupt.Width, eh), area, Color.Lerp(paint, Color.White, 0.25f) * k, 0, (int)(t * 420f));
					continue;
				}
				float strong = stopped ? 0.3f + (safe ? 0.1f : 0.16f * (float)Math.Sin(t * 22f)) : 0.14f;
				PixelFx.Tile(sb, "Column", area, paint * (strong * alpha * 1.5f), 0, -(int)(t * (stopped ? 40f : 300f)));
				// Frame of the reel.
				foreach (int edge in new[] { x, x + (int)Width - 4 }) {
					sb.Draw(px, new Rectangle(edge / 2 * 2, top, 4, bottom - top), new Color(146, 88, 26) * (0.8f * alpha));
					sb.Draw(px, new Rectangle(edge / 2 * 2, top, 2, bottom - top), new Color(255, 225, 130) * (0.8f * alpha));
				}
				// Symbols roll down the strip until it stops.
				const int Step = 150;
				float roll = stopped ? 0f : (Age * 26f + kind * 60) % Step;
				int look = kind == (int)Projectile.ai[0] ? 3 : (int)(kind % 2 == 0 ? Projectile.ai[1] : Projectile.ai[2]);
				int baseY = (int)(Projectile.Center.Y - Main.screenPosition.Y);
				for (int y = baseY % Step - Step; y < Main.screenHeight + Step; y += Step) {
					int face = stopped ? look : (int)(Age / 3 + y / Step + kind + 100) % 4;
					Rectangle frame = symbols.Frame(1, 4, 0, face);
					float bounce = stopped ? Math.Max(0f, 1f - (Age - StopTick(kind)) / 8f) * 14f : 0f;
					sb.Draw(symbols, PixelFx.Snap(new Vector2(x + Width / 2f, y + roll + bounce)), frame, Color.White * ((stopped ? 0.9f : 0.5f) * alpha), 0f, frame.Size() / 2f, stopped ? 2f : 1.5f, SpriteEffects.None, 0f);
				}
			}
			return false;
		}
	}

	/// <summary>Gaze beam: a thin line with arrows first, then it fires and turns. ai[0] boss, ai[1] angle, ai[2] turn per tick.</summary>
	public class FateBeam : FateProjectile
	{
		public const int Warn = 52, Fire = 115;
		private const float Length = 2200f, Wide = 44f;
		public override string Texture => "CasinoWorld/Content/Projectiles/Boss/FateChip";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 30;
			Projectile.timeLeft = Warn + Fire;
		}

		public override bool CanHitPlayer(Player target) => Age > Warn;

		public override void OnKill(int timeLeft) { }

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			float hit = 0f;
			return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + Projectile.ai[1].ToRotationVector2() * Length, Wide - 12f, ref hit);
		}

		public override void AI()
		{
			Age++;
			NPC boss = Main.npc[(int)Projectile.ai[0]];
			if (!boss.active || boss.ModNPC is not Fortuna) {
				Projectile.Kill();
				return;
			}
			Projectile.Center = boss.Center;
			Projectile.velocity = Vector2.Zero;
			if (Age == Warn) {
				SoundEngine.PlaySound(CasinoSounds.FortunaBeam, Projectile.Center);
				CasinoFx.Hit(Projectile.Center, new Color(255, 225, 150), 1.2f, false);
				ScreenFx.ZoomKick(0.04f, 20);
				CasinoFx.Shake(Projectile.Center, Main.rand.NextVector2Unit(), 5f, 8f, 20, 2400f, "FateBeam");
				CasinoFx.Shake(Projectile.Center, Vector2.UnitX, 5f, 8f, 20, 2000f, "FateBeam");
			}
			if (Age > Warn) {
				// Starts slowly, so nobody is cut in the first instant.
				Projectile.ai[1] += Projectile.ai[2] * MathHelper.Clamp((Age - Warn) / 18f, 0f, 1f);
				Vector2 dir = Projectile.ai[1].ToRotationVector2();
				for (int k = 0; k < 4; k++)
					CasinoFx.Spark(Projectile.Center + dir * Main.rand.NextFloat(60f, 1200f) + Main.rand.NextVector2Circular(20, 20), dir.RotatedBy(MathHelper.PiOver2 * Math.Sign(Projectile.ai[2])) * -2f, Fortuna.GoldLight, 1f);
				for (int k = 0; k < 12; k++)
					Lighting.AddLight(Projectile.Center + dir * (k * 110f), 0.9f, 0.6f, 0.4f);
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			SpriteBatch sb = Main.spriteBatch;
			Vector2 from = Projectile.Center - Main.screenPosition, dir = Projectile.ai[1].ToRotationVector2();
			float t = Main.GlobalTimeWrappedHourly;
			if (Age <= Warn) {
				float k = Age / Warn;
				Color c = Color.Lerp(Fortuna.GoldLight, Fortuna.FateRed, k) * (0.45f + 0.55f * k);
				for (float d = 70f; d < 1700f; d += 36f)
					PixelFx.Line(sb, from + dir * d, from + dir * (d + 20f), c, 6f);
				// Arrows to the side it will turn.
				Vector2 side = dir.RotatedBy(MathHelper.PiOver2 * Math.Sign(Projectile.ai[2]));
				for (float d = 150f; d < 1200f; d += 120f) {
					float march = (t * 110f) % 60f;
					Vector2 tip = from + dir * d + side * (14f + march);
					Color arrow = new Color(255, 245, 190) * (1f - 0.7f * march / 60f);
					PixelFx.Line(sb, tip, tip - side * 18f + dir * 16f, arrow, 6f);
					PixelFx.Line(sb, tip, tip - side * 18f - dir * 16f, arrow, 6f);
				}
				return false;
			}
			// Beam: stacked stripes of square pixels, hot in the middle, with a ripple.
			float open = MathHelper.Clamp((Age - Warn) / 8f, 0f, 1f) * MathHelper.Clamp((Warn + Fire - Age) / 12f, 0f, 1f);
			float far = 1700f;
			Beam(sb, from, dir, far, Wide * 1.25f * open, new Color(150, 30, 90, 0) * 0.6f, t, 0f);
			Beam(sb, from, dir, far, Wide * 0.9f * open, new Color(255, 90, 80, 0) * 0.8f, t, 1f);
			Beam(sb, from, dir, far, Wide * 0.55f * open, new Color(255, 215, 110, 0), t, 2f);
			Beam(sb, from, dir, far, Wide * 0.25f * open, new Color(255, 255, 240, 0), t, 3f);
			PixelFx.Glow(sb, from + dir * 40f, 48f, Fortuna.GoldLight);
			return false;
		}

		private static void Beam(SpriteBatch sb, Vector2 from, Vector2 dir, float length, float width, Color color, float t, float seed)
		{
			if (width < 2f)
				return;
			Texture2D px = TextureAssets.MagicPixel.Value;
			const float Step = 12f;
			for (float d = 40f; d < length; d += Step) {
				Vector2 at = from + dir * d;
				if (at.X < -80 || at.Y < -80 || at.X > Main.screenWidth + 80 || at.Y > Main.screenHeight + 80)
					continue;
				float w = width + (float)Math.Round(Math.Sin(d * 0.05f - t * 22f + seed)) * 4f;
				sb.Draw(px, at, new Rectangle(0, 0, 1, 1), color, dir.ToRotation(), new Vector2(0f, 0.5f), new Vector2(Step + 0.5f, w), SpriteEffects.None, 0f);
			}
		}
	}

	/// <summary>Curtain card: speeds up to its lane speed and flies across. ai[0] suit.</summary>
	public class CurtainCard : FateProjectile
	{
		protected override Color TrailColor => Fortuna.GoldLight * 0.7f;
		protected override Color GlowColor => Fortuna.GoldLight * 0.35f;

		public override void SetStaticDefaults()
		{
			base.SetStaticDefaults();
			Main.projFrames[Type] = 4;
		}

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = 24;
			Projectile.height = 32;
			Projectile.timeLeft = 230;
		}

		public override void AI()
		{
			Age++;
			Projectile.frame = (int)Projectile.ai[0];
			Projectile.rotation = (float)Math.Sin(Age * 0.12f + Projectile.Center.Y * 0.02f) * 0.18f;
			Projectile.alpha = (int)Math.Max(0, 255 - Age * 20);
			Lighting.AddLight(Projectile.Center, 0.4f, 0.32f, 0.15f);
		}
	}

	/// <summary>Pot of Gold: pulls players in, swallows chips, then bursts into rings. ai[0] phase.</summary>
	public class PotHole : FateProjectile
	{
		public const int PullTicks = 190;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 84;
			Projectile.timeLeft = PullTicks + 50;
		}

		public override bool CanHitPlayer(Player target) => Age > 40 && Age < PullTicks + 4;

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
			=> Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2()) < 56f;

		public override void AI()
		{
			Age++;
			Projectile.velocity = Vector2.Zero;
			if (Age < PullTicks) {
				Player me = Main.LocalPlayer;
				float dist = Vector2.Distance(me.Center, Projectile.Center);
				if (!Main.dedServ && me.active && !me.dead && dist < 1500f && dist > 30f) {
					float pull = 0.34f * MathHelper.Clamp(Age / 50f, 0f, 1f);
					me.velocity += (Projectile.Center - me.Center) / dist * pull;
				}
				if (!Main.dedServ)
					for (int k = 0; k < 3; k++) {
						Vector2 off = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(160f, 520f);
						CasinoFx.Spark(Projectile.Center + off, -off.SafeNormalize(Vector2.Zero).RotatedBy(0.6f) * 9f, Main.rand.NextBool() ? Fortuna.GoldLight : Fortuna.FateViolet, 1f);
					}
				if (Main.netMode != NetmodeID.MultiplayerClient && Age > 30 && (int)Age % 9 == 0 && Age < PullTicks - 60) {
					Vector2 off = Main.rand.NextVector2CircularEdge(1f, 1f) * 820f;
					Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + off, -off / 820f * 7.5f, ModContent.ProjectileType<SwallowedChip>(), Projectile.damage * 4 / 5, 1f, Main.myPlayer, Projectile.Center.X, Projectile.Center.Y);
				}
				if ((int)Age == PullTicks - 34)
					SoundEngine.PlaySound(CasinoSounds.FortunaCharge, Projectile.Center);
				return;
			}
			for (int ring = 0; ring < 3; ring++)
				if ((int)Age == PullTicks + ring * 18) {
					SoundEngine.PlaySound(CasinoSounds.CoinsPayout, Projectile.Center);
					CasinoFx.Explosion(Projectile.Center, 150, Fortuna.GoldLight, Fortuna.FateViolet, 5f);
					if (Main.netMode != NetmodeID.MultiplayerClient) {
						const int Count = 22;
						for (int k = 0; k < Count; k++)
							Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, ((k + ring * 0.5f) * MathHelper.TwoPi / Count).ToRotationVector2() * (7f - ring * 0.7f), ModContent.ProjectileType<FateChip>(), Projectile.damage * 4 / 5, 1f, Main.myPlayer);
					}
				}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			float k = Age < PullTicks ? MathHelper.Clamp(Age / 30f, 0f, 1f) : MathHelper.Clamp(1f - (Age - PullTicks) / 40f, 0f, 1f);
			Rectangle frame = Tex.Frame(1, 4, 0, (int)(Main.GlobalTimeWrappedHourly * 12f) % 4);
			Vector2 at = Projectile.Center - Main.screenPosition;
			PixelFx.Glow(Main.spriteBatch, at, 110f, Fortuna.FateViolet * (0.7f * k));
			if (Age > PullTicks - 34 && Age < PullTicks)
				PixelFx.Burst(Main.spriteBatch, at, Fortuna.GoldLight * 0.8f, Main.GlobalTimeWrappedHourly, 4);
			Main.EntitySpriteDraw(Tex, at, frame, Color.White * k, 0f, frame.Size() / 2f, 1f, SpriteEffects.None);
			return false;
		}
	}

	/// <summary>Chip on its way into the pot; gone when it gets there. ai[0], ai[1] the pot.</summary>
	public class SwallowedChip : FateChip
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/Boss/FateChip";

		public override void AI()
		{
			base.AI();
			if (Vector2.Distance(Projectile.Center, new Vector2(Projectile.ai[0], Projectile.ai[1])) < 30f)
				Projectile.Kill();
		}
	}

	/// <summary>Steel ball in a neon frame: bounces off its edges, the next stretch of its path is drawn. ai[0], ai[1] middle of the frame.</summary>
	public class FateBall : FateProjectile
	{
		private const float HalfWidth = 760f, HalfHeight = 430f;
		private const int Delay = 45, Life = 250;

		protected override Color TrailColor => new Color(200, 210, 255) * 0.8f;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 52;
			Projectile.timeLeft = Life;
		}

		public override bool CanHitPlayer(Player target) => Age > Delay;

		public override bool ShouldUpdatePosition() => Age > Delay;

		private bool Bounce(ref Vector2 at, ref Vector2 velocity)
		{
			Vector2 middle = new(Projectile.ai[0], Projectile.ai[1]);
			bool hit = false;
			if (Math.Abs(at.X - middle.X) > HalfWidth && Math.Sign(at.X - middle.X) == Math.Sign(velocity.X)) {
				velocity.X = -velocity.X;
				hit = true;
			}
			if (Math.Abs(at.Y - middle.Y) > HalfHeight && Math.Sign(at.Y - middle.Y) == Math.Sign(velocity.Y)) {
				velocity.Y = -velocity.Y;
				hit = true;
			}
			return hit;
		}

		public override void AI()
		{
			Age++;
			if (Age <= Delay)
				return;
			Vector2 at = Projectile.Center, v = Projectile.velocity;
			if (Bounce(ref at, ref v)) {
				Projectile.velocity = v * 1.04f;
				SoundEngine.PlaySound(CasinoSounds.FortunaBall, Projectile.Center);
				CasinoFx.Ring(Projectile.Center, new Color(200, 220, 255), 20, 6f, 1.3f);
				CasinoFx.Shake(Projectile.Center, v.SafeNormalize(Vector2.UnitY), 4f, 8f, 10, 1400f, "FateBall");
			}
			CasinoFx.Spark(Projectile.Center + Main.rand.NextVector2Circular(22, 22), -Projectile.velocity * 0.1f, new Color(200, 220, 255), 1f);
			Lighting.AddLight(Projectile.Center, 0.6f, 0.65f, 0.9f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			SpriteBatch sb = Main.spriteBatch;
			float alpha = Math.Min(1f, Math.Min(Age / 20f, Projectile.timeLeft / 20f)), t = Main.GlobalTimeWrappedHourly;
			Vector2 middle = new Vector2(Projectile.ai[0], Projectile.ai[1]) - Main.screenPosition;
			Color neon = ((int)(t * 6f) % 2 == 0 ? Fortuna.GoldLight : Fortuna.FateRed) * (0.5f * alpha);
			Vector2 a = middle + new Vector2(-HalfWidth - 28, -HalfHeight - 28), b = middle + new Vector2(HalfWidth + 28, HalfHeight + 28);
			foreach ((Vector2 p, Vector2 q) in new[] { (a, new Vector2(b.X, a.Y)), (new Vector2(b.X, a.Y), b), (b, new Vector2(a.X, b.Y)), (new Vector2(a.X, b.Y), a) })
				PixelFx.Line(sb, p, q, neon, 6f);
			// Where it goes next.
			Vector2 at = Projectile.Center, v = Projectile.velocity;
			for (int k = 0; k < 80; k++) {
				at += v;
				Bounce(ref at, ref v);
				if (k % 2 == 0) {
					Vector2 p = PixelFx.Snap(at - Main.screenPosition);
					sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)p.X - 3, (int)p.Y - 3, 6, 6), Color.Lerp(Fortuna.FateRed, Fortuna.GoldLight, k / 80f) * (0.75f * alpha * (1f - k / 90f)));
				}
			}
			CasinoFx.Trail(Projectile, Tex, null, TrailColor, 1f);
			Main.EntitySpriteDraw(Tex, Projectile.Center - Main.screenPosition, null, Color.White * alpha, 0f, Tex.Size() / 2f, 1f, SpriteEffects.None);
			return false;
		}
	}
}
