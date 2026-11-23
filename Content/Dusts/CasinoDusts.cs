using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Dusts
{
	/// <summary>Paper fragment of a playing card: tumbles, flutters down and fades. Tinted by dust.color.</summary>
	public class CardShard : ModDust
	{
		public override void OnSpawn(Dust dust)
		{
			dust.frame = new Rectangle(0, Main.rand.Next(3) * 10, 8, 8);
			dust.noGravity = false;
			dust.noLight = true;
			dust.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
			if (dust.color == default)
				dust.color = Color.White;
		}

		public override bool Update(Dust dust)
		{
			dust.position += dust.velocity;
			dust.velocity.Y = System.Math.Min(dust.velocity.Y + 0.08f, 2.5f);
			dust.velocity.X *= 0.95f;
			dust.velocity.X += (float)System.Math.Sin(dust.alpha * 0.2f + dust.rotation) * 0.05f;   // flutter
			dust.rotation += dust.velocity.X * 0.15f + 0.05f;
			dust.alpha += 4;
			dust.scale *= 0.995f;
			if (dust.alpha >= 255 || Collision.SolidCollision(dust.position, 2, 2) && dust.velocity.Y > 0) {
				dust.velocity *= 0.5f;
				dust.alpha += 10;
			}
			if (dust.alpha >= 255)
				dust.active = false;
			return false;
		}

		public override Color? GetAlpha(Dust dust, Color lightColor)
			=> dust.color.MultiplyRGB(lightColor) * ((255 - dust.alpha) / 255f);
	}

	/// <summary>Glowing four-point spark, drawn additively. Tinted by dust.color, gives light.</summary>
	public class Spark : ModDust
	{
		public override void OnSpawn(Dust dust)
		{
			dust.frame = new Rectangle(0, Main.rand.Next(3) * 12, 10, 10);
			dust.noGravity = true;
			if (dust.color == default)
				dust.color = new Color(255, 220, 120);
		}

		public override bool Update(Dust dust)
		{
			dust.position += dust.velocity;
			dust.velocity *= 0.9f;
			dust.rotation += 0.1f;
			dust.scale -= 0.03f;
			if (!dust.noLight)
				Lighting.AddLight(dust.position, dust.color.ToVector3() * dust.scale * 0.4f);
			if (dust.scale < 0.2f)
				dust.active = false;
			return false;
		}

		public override Color? GetAlpha(Dust dust, Color lightColor)
		{
			Color c = dust.color;
			c.A = 0;
			return c;
		}
	}
}
