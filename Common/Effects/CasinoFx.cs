using System;
using CasinoWorld.Content.Dusts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Effects
{
	/// <summary>
	/// Shared hit, impact and trail effects for Casino World projectiles (weapons and the boss), so every
	/// card, chip and die has a readable trail and a proper impact instead of just vanishing.
	/// All methods are visual only and safe to call on every client.
	/// </summary>
	public static class CasinoFx
	{
		public static int ShardType => ModContent.DustType<CardShard>();
		public static int SparkType => ModContent.DustType<Spark>();

		/// <summary>Screen shake, unless switched off in the client settings.</summary>
		public static void Shake(Vector2 at, Vector2 direction, float strength, float vibration, int frames, float falloff, string id)
		{
			if (!Main.dedServ && Systems.CasinoClientConfig.Shaking)
				Main.instance.CameraModifiers.Add(new PunchCameraModifier(at, direction, strength, vibration, frames, falloff, id));
		}

		/// <summary>One pixel streak spark.</summary>
		public static void Spark(Vector2 at, Vector2 velocity, Color color, float scale = 1f)
			=> Particles.Spark(at, velocity * 1.3f, color, (int)(14 + 8 * scale), scale);

		public static void Sparks(Vector2 at, Color color, int count, float speed, float scale = 1f)
		{
			for (int k = 0, n = Particles.N(count); k < n; k++)
				Spark(at, Main.rand.NextVector2Circular(speed, speed), color, scale * Main.rand.NextFloat(0.7f, 1.2f));
		}

		/// <summary>A ring of sparks (explosions, ricochets, procs); big ones also leave a small shockwave.</summary>
		public static void Ring(Vector2 at, Color color, int count, float speed, float scale = 1f)
		{
			int n = Particles.N(Math.Max(4, count * 2 / 3));
			float turn = Main.rand.NextFloat(MathHelper.TwoPi);
			for (int k = 0; k < n; k++)
				Spark(at, (turn + k / (float)n * MathHelper.TwoPi).ToRotationVector2() * speed * Main.rand.NextFloat(0.8f, 1.15f), color, scale);
			if (count >= 16)
				Particles.Ring(at, color, speed * 8f, 16, 2f);
		}

		/// <summary>Card hits a wall or an enemy: paper slivers fly back, a small impact star and a soft slap.</summary>
		public static void CardImpact(Vector2 at, Vector2 velocity, Color tint, bool sound = true)
		{
			if (Main.dedServ)
				return;
			Vector2 back = -velocity.SafeNormalize(Vector2.UnitY);
			for (int k = 0, n = Particles.N(4); k < n; k++)
				Particles.Shard(at, back.RotatedByRandom(1.1f) * Main.rand.NextFloat(1.5f, 4f) - Vector2.UnitY, k % 2 == 0 ? tint : Color.White);
			Particles.Impact(at, tint, 1, 10);
			if (sound)
				SoundEngine.PlaySound(CasinoSounds.CardHit with { Volume = 0.45f }, at);
		}

		/// <summary>Chip clinks off something: sparks in its colour, a coin bit and a ping.</summary>
		public static void ChipImpact(Vector2 at, Color color, bool sound = true)
		{
			if (Main.dedServ)
				return;
			Ring(at, color, 8, 2.6f, 0.9f);
			Particles.Impact(at, color, 1, 10);
			if (Main.rand.NextBool(2))
				Particles.Coin(at, Main.rand.NextVector2Circular(2.5f, 2.5f) - Vector2.UnitY * 2f, 40);
			if (sound)
				SoundEngine.PlaySound(CasinoSounds.ChipFlick with { Volume = 0.5f, Pitch = 0.2f }, at);
		}

		/// <summary>Die lands or breaks: a puff and pip sparks.</summary>
		public static void DieImpact(Vector2 at, Color color, bool sound = true)
		{
			if (Main.dedServ)
				return;
			Particles.Smoke(at, new Vector2(0, -0.5f), Color.Gray, 24);
			Particles.Impact(at, color, 1, 10);
			Sparks(at, color, 4, 2.5f, 0.8f);
			if (sound)
				SoundEngine.PlaySound(CasinoSounds.DiceBounce with { Volume = 0.5f }, at);
		}

		/// <summary>
		/// Explosion. The hand-drawn fireball is the main shape; a few streaks, a ring for big blasts and smoke that
		/// rolls out of the edge after it support it without covering it.
		/// </summary>
		public static void Explosion(Vector2 at, float radius, Color hot, Color cold, float shake = 0f)
		{
			if (Main.dedServ)
				return;
			Lighting.AddLight(at, hot.ToVector3() * 1.5f);
			Particles.Explosion(at, hot, radius, radius > 90 ? 34 : 26);
			if (radius > 110)
				Particles.Ring(at, hot, radius * 1.2f, 22, 4f);
			for (int k = 0, n = Particles.N((int)(radius * 0.08f) + 3); k < n; k++)
				Particles.Spark(at, Main.rand.NextVector2Unit() * Main.rand.NextFloat(radius / 18f, radius / 8f), Main.rand.NextBool(3) ? cold : hot,
					Main.rand.Next(16, 26), Main.rand.NextFloat(1f, 1.6f), 0.1f);
			for (int k = 0, n = Particles.N((int)(radius * 0.06f) + 1); k < n; k++)
				Particles.Ember(at + Main.rand.NextVector2Circular(radius * 0.4f, radius * 0.4f), Main.rand.NextVector2Circular(1.5f, 1.5f) - Vector2.UnitY,
					hot, Main.rand.Next(40, 70), 1.5f);
			int puffs = Particles.N(radius > 90 ? 4 : 2);
			for (int k = 0; k < puffs; k++) {
				Vector2 dir = Main.rand.NextVector2Unit();
				Vector2 spot = at + dir * radius * 0.45f;
				Vector2 vel = dir * 1.2f - Vector2.UnitY * 0.4f;
				int big = radius > 90 ? 2 : 1;
				Color smoke = Color.Lerp(cold, new Color(60, 40, 50), 0.75f);
				ScreenFx.After(8 + k * 2, () => Particles.Smoke(spot, vel, smoke, 36, big));
			}
			if (shake > 0f)
				Shake(at, Main.rand.NextVector2Unit(), shake, 8f, 14, 1000f, "CasinoExplosion");
		}

		// ---- epic combat pass: hits, shockwaves, telegraphs, cinematics

		/// <summary>Heavy hit: the impact star holds for a beat, streaks fly out; big ones stop time for a moment.</summary>
		public static void Hit(Vector2 at, Color color, float power = 1f, bool sound = true)
		{
			if (Main.dedServ)
				return;
			Particles.Impact(at, color, 2, (int)(14 + 4 * power));
			for (int k = 0, n = Particles.N((int)(6 * power) + 2); k < n; k++)
				Particles.Spark(at, Main.rand.NextVector2Unit() * Main.rand.NextFloat(5f, 11f) * Math.Min(power, 1.6f), color, Main.rand.Next(10, 18), power >= 1.5f ? 1.6f : 1f);
			if (power >= 1.5f)
				Particles.Ring(at, color, 70f * power, 14, 2f);
			if (sound)
				SoundEngine.PlaySound(CasinoSounds.ImpactHeavy with { Volume = 0.45f + 0.2f * Math.Min(power, 2f) }, at);
			if (power >= 1.5f) {
				Shake(at, Main.rand.NextVector2Unit(), 4f * power, 8f, 12, 1600f, "CasinoHit");
				ScreenFx.HitStop((int)(2 * power));
			}
		}

		/// <summary>Big shockwave: one thick ring leads, a thinner one follows, dust rolls along the front.</summary>
		public static void Shockwave(Vector2 at, Color color, float radius, float shake = 6f, bool sound = true)
		{
			if (Main.dedServ)
				return;
			Particles.Ring(at, color, radius, 26, 6f);
			ScreenFx.After(4, () => Particles.Ring(at, color, radius * 0.7f, 20, 2f));
			for (int k = 0, n = Particles.N(18); k < n; k++)
				Particles.Spark(at, (k / (float)n * MathHelper.TwoPi + Main.rand.NextFloat(-0.1f, 0.1f)).ToRotationVector2() * Main.rand.NextFloat(10f, 15f),
					color, Main.rand.Next(16, 24), 1.6f);
			if (sound)
				SoundEngine.PlaySound(CasinoSounds.Shockwave, at);
			if (shake > 0f)
				Shake(at, Vector2.UnitY, shake, 6f, 24, 2400f, "CasinoShockwave");
			ScreenFx.ZoomKick(0.04f, 20);
		}

		/// <summary>Telegraph, call every tick while an attack winds up: streaks and arcs rush into the point.</summary>
		public static void ChargeIn(Vector2 at, Color color, float radius = 140f, float progress = 0.5f)
		{
			if (Main.dedServ)
				return;
			int n = Particles.N(progress > 0.6f ? 2 : 1);
			for (int k = 0; k < n; k++) {
				Vector2 dir = Main.rand.NextVector2Unit();
				float r = radius * Main.rand.NextFloat(0.7f, 1.1f);
				// With 0.9 drag the streak covers about 6.2 times its start speed before it dies.
				Particles.Spark(at + dir * r, -dir * r / 6.2f, color, 16, 1f + progress * 0.6f);
			}
			if (progress > 0.5f && Main.rand.NextBool(6)) {
				Vector2 from = at + Main.rand.NextVector2CircularEdge(radius * 0.6f, radius * 0.6f);
				Particles.Arc(from, at, color, 6);
			}
			Lighting.AddLight(at, color.ToVector3() * (0.4f + progress));
		}

		/// <summary>Telegraph: an attack is about to fire. A gleam with a sharp shing.</summary>
		public static void Glint(Vector2 at, Color color, bool sound = true)
		{
			if (Main.dedServ)
				return;
			Particles.Star(at, color, 22, default, 3);
			if (sound)
				SoundEngine.PlaySound(CasinoSounds.Glint, at);
		}

		/// <summary>
		/// Danger telegraph for an attack that can really hurt: a red "!" over the boss, a lock-on ring closing on it,
		/// and an arrow at the screen edge while it is off screen.
		/// </summary>
		public static void Warn(NPC boss, int ticks = 40)
		{
			if (Main.dedServ)
				return;
			Particles.Alert(boss, ticks);
			Particles.LockOn(boss.Center, Ramp.Danger.C2, Math.Max(boss.width, boss.height) * 1.6f + 60f, Math.Min(ticks, 30), boss);
			ScreenFx.EdgeWarning(boss, ticks + 30);
			SoundEngine.PlaySound(CasinoSounds.Glint with { Pitch = -0.2f, Volume = 0.6f }, boss.Center);
		}

		/// <summary>Shower of coins and gold sparks.</summary>
		public static void CoinBurst(Vector2 at, int coins, float speed = 7f, bool sound = true)
		{
			if (Main.dedServ)
				return;
			for (int k = 0, n = Particles.N(coins); k < n; k++)
				Particles.Coin(at, Main.rand.NextVector2Circular(speed, speed) - Vector2.UnitY * speed * 0.6f, Main.rand.Next(50, 90));
			Sparks(at, new Color(255, 214, 90), coins / 3, speed);
			if (sound)
				SoundEngine.PlaySound(CasinoSounds.CoinBlast with { Volume = 0.8f }, at);
		}

		/// <summary>
		/// Boss phase change, staged so one shape leads at a time: time stops on the impact star, then three
		/// shockwaves roll out one after another under slow motion, the camera leans in, the music dips under the
		/// stinger and the boss's voice. The boss sprite itself flashes and squashes (<see cref="BossReaction"/>).
		/// </summary>
		public static void PhaseCinematic(Vector2 at, Color color, Color accent, SoundStyle voice)
		{
			if (Main.dedServ)
				return;
			ScreenFx.HitStop(6);
			ScreenFx.SlowMotion(50);
			ScreenFx.Focus(at, 80, 0.45f);
			ScreenFx.ZoomKick(0.1f, 80);
			ScreenFx.Vignette(110, 0.8f);
			ScreenFx.SpeedLines(at, color, 36);
			ScreenFx.Flash(Color.White, 0.6f, 10);
			ScreenFx.DuckMusic(150, 0.65f);
			SoundEngine.PlaySound(CasinoSounds.SlowMo, at);
			SoundEngine.PlaySound(CasinoSounds.PhaseStinger, at);
			SoundEngine.PlaySound(voice, at);
			SoundEngine.PlaySound(CasinoSounds.ImpactBass, at);
			Shake(at, Vector2.UnitY, 10f, 7f, 50, 3000f, "CasinoPhase");
			Particles.Impact(at, color, 2, 22);
			ScreenFx.After(6, () => Shockwave(at, color, 300f, 0f, false));
			ScreenFx.After(16, () => Shockwave(at, accent, 440f, 0f, false));
			ScreenFx.After(28, () => {
				Shockwave(at, color, 600f, 0f, false);
				SoundEngine.PlaySound(CasinoSounds.EnergyBurst, at);
				for (int k = 0, n = Particles.N(16); k < n; k++)
					Particles.Ember(at + Main.rand.NextVector2CircularEdge(120, 120), Main.rand.NextVector2Circular(2f, 2f), Main.rand.NextBool() ? color : accent, Main.rand.Next(50, 90), 1.5f);
			});
		}

		/// <summary>Boss death: a longer, bigger version of the phase cinematic with coins everywhere.</summary>
		public static void DeathCinematic(Vector2 at, Color color, Color accent)
		{
			if (Main.dedServ)
				return;
			ScreenFx.HitStop(8);
			ScreenFx.SlowMotion(80);
			ScreenFx.Focus(at, 120, 0.6f);
			ScreenFx.ZoomKick(0.14f, 120);
			ScreenFx.Vignette(150, 0.85f);
			ScreenFx.SpeedLines(at, color, 50);
			ScreenFx.Flash(Color.White, 0.6f, 10);
			ScreenFx.DuckMusic(220, 0.75f);
			SoundEngine.PlaySound(CasinoSounds.SlowMo, at);
			SoundEngine.PlaySound(CasinoSounds.ImpactBass, at);
			SoundEngine.PlaySound(CasinoSounds.Shockwave, at);
			Shake(at, Vector2.UnitY, 16f, 7f, 70, 3500f, "CasinoDeath");
			Explosion(at, 160f, color, accent);
			CoinBurst(at, 36, 10f);
			for (int k = 1; k <= 3; k++) {
				int wave = k;
				ScreenFx.After(wave * 12, () => {
					Shockwave(at, wave % 2 == 0 ? accent : color, 300f + wave * 160f, 0f, false);
					Explosion(at + Main.rand.NextVector2Circular(90, 90), 90f + wave * 20f, color, accent);
					SoundEngine.PlaySound(CasinoSounds.EnergyBurst with { Pitch = -0.1f * wave }, at);
				});
			}
		}

		/// <summary>Afterimage trail from the projectile's old positions (needs TrailCacheLength).</summary>
		public static void Trail(Projectile p, Texture2D tex, Rectangle? frame, Color color, float scale, bool additive = true)
		{
			Vector2 origin = frame?.Size() / 2f ?? tex.Size() / 2f;
			int len = p.oldPos.Length;
			for (int k = len - 1; k >= 1; k--) {
				if (p.oldPos[k] == Vector2.Zero)
					continue;
				float fade = 1f - k / (float)len;
				Color c = color * (fade * 0.6f);
				if (additive)
					c.A = 0;
				float rot = p.oldRot.Length > k ? p.oldRot[k] : p.rotation;
				Main.EntitySpriteDraw(tex, p.oldPos[k] + p.Size / 2f - Main.screenPosition, frame, c, rot, origin, scale * (0.6f + 0.4f * fade), SpriteEffects.None);
			}
		}

		/// <summary>Soft additive glow behind a sprite.</summary>
		public static void Glow(Projectile p, Texture2D tex, Rectangle? frame, Color color, float scale)
		{
			Vector2 origin = frame?.Size() / 2f ?? tex.Size() / 2f;
			Color c = color;
			c.A = 0;
			float pulse = 1f + 0.08f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f + p.whoAmI);
			for (int k = 0; k < 4; k++) {
				Vector2 off = (k * MathHelper.PiOver2 + Main.GlobalTimeWrappedHourly * 2f).ToRotationVector2() * 2f;
				Main.EntitySpriteDraw(tex, p.Center + off - Main.screenPosition, frame, c * 0.35f, p.rotation, origin, scale * pulse, SpriteEffects.None);
			}
		}

		/// <summary>Standard draw: trail, glow, then the sprite itself.</summary>
		public static void Draw(Projectile p, Color trail, Color glow, float scale = 1f, Color? tint = null)
		{
			Texture2D tex = TextureAssets.Projectile[p.type].Value;
			int frames = Main.projFrames[p.type];
			Rectangle? frame = frames > 1 ? tex.Frame(1, frames, 0, p.frame) : null;
			Trail(p, tex, frame, trail, scale);
			if (glow != Color.Transparent)
				Glow(p, tex, frame, glow, scale);
			Vector2 origin = frame?.Size() / 2f ?? tex.Size() / 2f;
			Color light = Lighting.GetColor((int)(p.Center.X / 16), (int)(p.Center.Y / 16));
			Color body = (tint ?? Color.White).MultiplyRGBA(Color.Lerp(light, Color.White, 0.5f)) * p.Opacity;
			Main.EntitySpriteDraw(tex, p.Center - Main.screenPosition, frame, body, p.rotation, origin, scale, SpriteEffects.None);
		}

		public static void EnableTrail(int type, int length = 8)
		{
			ProjectileID.Sets.TrailCacheLength[type] = length;
			ProjectileID.Sets.TrailingMode[type] = 2;
		}
	}
}
