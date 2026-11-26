using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CasinoWorld.Common.Effects
{
	/// <summary>
	/// How a boss's own sprite reacts: a white flash when hit hard or changing phase, squash and stretch that
	/// springs back (dash start, landing, phase change), and a tremble while winding up or stunned. The boss draws
	/// itself through <see cref="Draw"/>, which wraps its normal drawing in a transform and adds an additive pass for
	/// the flash. Visual only and client side; the bestiary icon is drawn untouched.
	/// </summary>
	public class BossReaction
	{
		private float flash, squashX = 1f, squashY = 1f, velX, velY;
		private int shakeTicks, hitCooldown;
		private float shakeStrength;

		/// <summary>White flash, 0..1. Hits use about 0.4, phase changes 1.</summary>
		public void Flash(float amount) => flash = Math.Max(flash, Math.Min(1f, amount));

		/// <summary>A light flash for an ordinary hit, at most every few ticks so fast weapons do not strobe.</summary>
		public void Hit(float amount = 0.35f)
		{
			if (hitCooldown > 0)
				return;
			hitCooldown = 7;
			Flash(amount);
		}

		/// <summary>Squash (x &gt; 1, y &lt; 1) or stretch; springs back with a little overshoot.</summary>
		public void Squash(float x, float y)
		{
			squashX = x;
			squashY = y;
			velX = velY = 0f;
		}

		/// <summary>Trembles the sprite by up to the strength in pixels.</summary>
		public void Shake(float strength, int ticks)
		{
			shakeStrength = Math.Max(shakeTicks > 0 ? shakeStrength : 0f, strength);
			shakeTicks = Math.Max(shakeTicks, ticks);
		}

		/// <summary>Call once per tick (client).</summary>
		public void Update()
		{
			flash *= 0.78f;
			if (flash < 0.03f)
				flash = 0f;
			if (hitCooldown > 0)
				hitCooldown--;
			if (shakeTicks > 0)
				shakeTicks--;
			// Damped spring back to 1.
			velX = (velX + (1f - squashX) * 0.3f) * 0.72f;
			velY = (velY + (1f - squashY) * 0.3f) * 0.72f;
			squashX += velX;
			squashY += velY;
			if (Math.Abs(squashX - 1f) < 0.005f && Math.Abs(velX) < 0.005f)
				squashX = 1f;
			if (Math.Abs(squashY - 1f) < 0.005f && Math.Abs(velY) < 0.005f)
				squashY = 1f;
		}

		private static void Restart(SpriteBatch sb, BlendState blend, Matrix matrix)
		{
			sb.End();
			sb.Begin(SpriteSortMode.Deferred, blend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, matrix);
		}

		/// <summary>Draws the boss through its normal draw method with the reaction applied.</summary>
		public bool Draw(NPC npc, SpriteBatch sb, Vector2 screenPos, Color drawColor, Func<SpriteBatch, Vector2, Color, bool> body)
		{
			if (npc.IsABestiaryIconDummy || Main.gameMenu)
				return body(sb, screenPos, drawColor);
			Vector2 offset = Vector2.Zero;
			if (shakeTicks > 0) {
				// Whole art pixels only, so the sprite does not shimmer.
				offset = new Vector2(Main.rand.Next(-1, 2), Main.rand.Next(-1, 2)) * 2f * (float)Math.Ceiling(shakeStrength / 2f);
			}
			bool squash = squashX != 1f || squashY != 1f;
			if (!squash && flash <= 0f)
				return body(sb, screenPos - offset, drawColor);

			Matrix view = Main.GameViewMatrix.TransformationMatrix;
			Vector2 pivot = npc.Center - screenPos;
			Matrix m = squash
				? Matrix.CreateTranslation(-pivot.X, -pivot.Y, 0f) * Matrix.CreateScale(squashX, squashY, 1f) * Matrix.CreateTranslation(pivot.X, pivot.Y, 0f) * view
				: view;
			bool ok;
			try {
				if (squash)
					Restart(sb, BlendState.AlphaBlend, m);
				ok = body(sb, screenPos - offset, drawColor);
				if (flash > 0f) {
					// Additive copies of the sprite: bright colours burn towards white, the outline stays readable.
					Restart(sb, BlendState.Additive, m);
					Color white = Color.White * flash;
					body(sb, screenPos - offset, white);
					if (flash > 0.5f)
						body(sb, screenPos - offset, white);
				}
			}
			finally {
				Restart(sb, BlendState.AlphaBlend, view);
			}
			return ok;
		}
	}
}
