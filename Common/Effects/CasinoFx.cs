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
			if (!Main.dedServ && Systems.CasinoClientConfig.Instance.ScreenEffects)
				Main.instance.CameraModifiers.Add(new PunchCameraModifier(at, direction, strength, vibration, frames, falloff, id));
		}

		public static void Spark(Vector2 at, Vector2 velocity, Color color, float scale = 1f)
		{
			if (Main.dedServ)
				return;
			Dust d = Dust.NewDustPerfect(at, SparkType, velocity, 0, color, scale);
			d.noGravity = true;
		}

		public static void Sparks(Vector2 at, Color color, int count, float speed, float scale = 1f)
		{
			for (int k = 0; k < count; k++)
				Spark(at, Main.rand.NextVector2Circular(speed, speed), color, scale * Main.rand.NextFloat(0.7f, 1.2f));
		}

		/// <summary>A ring of sparks (explosions, ricochets, procs).</summary>
		public static void Ring(Vector2 at, Color color, int count, float speed, float scale = 1f)
		{
			for (int k = 0; k < count; k++)
				Spark(at, (k / (float)count * MathHelper.TwoPi).ToRotationVector2() * speed, color, scale);
		}

		/// <summary>Card hits a wall or an enemy: paper shards fly back, a few sparks and a soft slap.</summary>
		public static void CardImpact(Vector2 at, Vector2 velocity, Color tint, bool sound = true)
		{
			if (Main.dedServ)
				return;
			Vector2 back = -velocity.SafeNormalize(Vector2.UnitY);
			for (int k = 0; k < 7; k++) {
				Vector2 v = back.RotatedByRandom(1.1f) * Main.rand.NextFloat(1.5f, 4f) - Vector2.UnitY;
				Dust.NewDustPerfect(at, ShardType, v, 0, k % 3 == 0 ? tint : Color.White, Main.rand.NextFloat(0.8f, 1.2f));
			}
			Sparks(at, tint, 4, 2.5f, 0.8f);
			if (sound)
				SoundEngine.PlaySound(CasinoSounds.CardHit with { Volume = 0.45f }, at);
		}

		/// <summary>Chip clinks off something: metal-bright sparks in its colour and a ping.</summary>
		public static void ChipImpact(Vector2 at, Color color, bool sound = true)
		{
			if (Main.dedServ)
				return;
			Ring(at, color, 8, 2.6f, 0.9f);
			for (int k = 0; k < 4; k++)
				Dust.NewDustPerfect(at, DustID.GoldCoin, Main.rand.NextVector2Circular(2.5f, 2.5f), 0, default, 0.8f).noGravity = true;
			if (sound)
				SoundEngine.PlaySound(CasinoSounds.ChipFlick with { Volume = 0.5f, Pitch = 0.2f }, at);
		}

		/// <summary>Die lands or breaks: dust puff and pip sparks.</summary>
		public static void DieImpact(Vector2 at, Color color, bool sound = true)
		{
			if (Main.dedServ)
				return;
			for (int k = 0; k < 6; k++) {
				Dust d = Dust.NewDustPerfect(at, DustID.Smoke, Main.rand.NextVector2Circular(1.5f, 1.5f), 120, default, 0.9f);
				d.noGravity = true;
			}
			Sparks(at, color, 5, 2.5f, 0.8f);
			if (sound)
				SoundEngine.PlaySound(CasinoSounds.DiceBounce with { Volume = 0.5f }, at);
		}

		/// <summary>Explosion: flash light, fire and smoke, a spark ring, optional screen shake.</summary>
		public static void Explosion(Vector2 at, float radius, Color hot, Color cold, float shake = 0f)
		{
			if (Main.dedServ)
				return;
			Lighting.AddLight(at, hot.ToVector3() * 1.5f);
			int count = (int)(radius * 0.35f);
			for (int k = 0; k < count; k++) {
				Dust d = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(radius * 0.3f, radius * 0.3f), DustID.Torch,
					Main.rand.NextVector2Circular(radius / 14f, radius / 14f), 0, default, Main.rand.NextFloat(1.4f, 2.2f));
				d.noGravity = true;
			}
			for (int k = 0; k < count / 2; k++) {
				Dust d = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(radius * 0.4f, radius * 0.4f), DustID.Smoke,
					Main.rand.NextVector2Circular(radius / 24f, radius / 24f), 100, default, Main.rand.NextFloat(1.2f, 1.8f));
				d.noGravity = true;
			}
			Ring(at, hot, 16, radius / 12f, 1.3f);
			Ring(at, cold, 10, radius / 20f, 1f);
			if (shake > 0f)
				Shake(at, Main.rand.NextVector2Unit(), shake, 8f, 14, 1000f, "CasinoExplosion");
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
