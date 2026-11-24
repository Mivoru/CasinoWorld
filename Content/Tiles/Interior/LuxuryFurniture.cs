using System;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Games;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Tiles.Interior
{
	// Luxury pieces of both casinos (tools/review_art2.py).

	public class VelvetRope : InteriorFurniture { public override int Width => 4; public override int Height => 2; protected override Color MapColor => new(190, 40, 50); }

	public class GoldenAce : InteriorFurniture { public override int Width => 3; public override int Height => 6; protected override Color MapColor => new(226, 172, 62); }

	public class PalmPlanter : InteriorFurniture { public override int Width => 3; public override int Height => 6; protected override Color MapColor => new(50, 130, 70); }

	public class ChipDisplay : InteriorFurniture { public override int Width => 3; public override int Height => 3; protected override Color MapColor => new(120, 70, 40); }

	public class VelvetDrape : InteriorFurniture { public override int Width => 2; public override int Height => 6; protected override Color MapColor => new(140, 24, 48); }

	public class CashMachine : InteriorFurniture
	{
		public override int Width => 2;
		public override int Height => 4;
		protected override Color MapColor => new(50, 90, 140);

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) => (r, g, b) = (0.1f, 0.25f, 0.18f);
	}

	/// <summary>Furniture with an animated part, drawn once from its top-left tile.</summary>
	public abstract class AnimatedFurniture : InteriorFurniture
	{
		public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
		{
			if (IsTopLeft(Main.tile[i, j], Width, Height))
				Main.instance.TilesRenderer.AddSpecialLegacyPoint(i, j);
		}

		public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			DrawLive(spriteBatch, new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen, Main.GlobalTimeWrappedHourly, i * 7 + j * 3);
		}

		protected abstract void DrawLive(SpriteBatch sb, Vector2 origin, float t, int seed);

		protected static void Px(SpriteBatch sb, Vector2 at, int w, int h, Color c) => sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)at.X / 2 * 2, (int)at.Y / 2 * 2, w, h), c);
	}

	/// <summary>Jackpot totem: the prize on its counter climbs all day and bulbs run up its sides.</summary>
	public class JackpotTotem : AnimatedFurniture
	{
		public override int Width => 3;
		public override int Height => 7;
		protected override Color MapColor => new(220, 60, 100);

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) => (r, g, b) = (0.6f, 0.35f, 0.25f);

		protected override void DrawLive(SpriteBatch sb, Vector2 o, float t, int seed)
		{
			for (int side = 0; side < 2; side++)
				for (int k = 0; k < 13; k++)
					Px(sb, o + new Vector2(side == 0 ? 8 : 38, 92 - k * 6), 2, 2, (k + (int)(t * 12f)) % 4 == 0 ? Color.White : new Color(120, 60, 30));
			int value = (int)(t * 7f + seed * 31) % 100000;
			Utils.DrawBorderString(sb, value.ToString("00000"), o + new Vector2(24, 43), Color.Lerp(new Color(255, 190, 60), Color.White, 0.3f + 0.3f * (float)Math.Sin(t * 5f)), 0.5f, 0.5f, 0.5f);
			if ((int)(t * 2f) % 5 == 0)
				PixelFx.Twinkle(sb, o + new Vector2(8 + (int)(t * 2f) * 11 % 34, 10), new Color(255, 230, 150), t * 2f % 1f);
		}
	}

	/// <summary>Four neon suits that light up one after another.</summary>
	public class NeonSuits : AnimatedFurniture
	{
		public override int Width => 8;
		public override int Height => 3;
		protected override Color MapColor => new(200, 90, 220);

		private static readonly Color[] Lights = { new(90, 220, 255), new(255, 80, 110), new(255, 200, 80), new(120, 255, 150) };

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			Vector3 c = Lights[Math.Clamp(Main.tile[i, j].TileFrameX / 36, 0, 3)].ToVector3() * 0.55f;
			(r, g, b) = (c.X, c.Y, c.Z);
		}

		protected override void DrawLive(SpriteBatch sb, Vector2 o, float t, int seed)
		{
			int lit = (int)(t * 2f + seed) % 5;
			for (int k = 0; k < 4; k++)
				if (k == lit || lit == 4)
					PixelFx.Glow(sb, o + new Vector2(19 + k * 30, 23), 32f, Lights[k] * (lit == 4 ? 0.5f + 0.4f * (float)Math.Sin(t * 14f) : 0.8f));
		}
	}

	/// <summary>Window over the night skyline with fireworks.</summary>
	public class NightWindow : AnimatedFurniture
	{
		public override int Width => 6;
		public override int Height => 5;
		protected override Color MapColor => new(60, 40, 100);

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) => (r, g, b) = (0.22f, 0.16f, 0.32f);

		protected override void DrawLive(SpriteBatch sb, Vector2 o, float t, int seed)
		{
			for (int rocket = 0; rocket < 2; rocket++) {
				float cycle = t * 0.45f + rocket * 0.5f + seed * 0.13f;
				int shot = (int)Math.Floor(cycle);
				float f = cycle - shot;
				var at = new Vector2(16 + (shot * 37 + rocket * 23) % 64, 14 + (shot * 17 + rocket * 5) % 16);
				Color c = Main.hslToRgb((shot * 0.37f + rocket * 0.2f) % 1f, 0.9f, 0.65f);
				if (f < 0.35f)   // climbs
					Px(sb, o + new Vector2(at.X, MathHelper.Lerp(70, at.Y, f / 0.35f)), 2, 4, new Color(255, 230, 170));
				else if (f < 0.8f) {   // bursts
					float p = (f - 0.35f) / 0.45f;
					for (int k = 0; k < 10; k++) {
						Vector2 d = (k * MathHelper.TwoPi / 10f).ToRotationVector2() * p * 12f + new Vector2(0, p * p * 5f);
						Vector2 q = at + d;
						if (q.X > 8 && q.X < 86 && q.Y > 8 && q.Y < 70)
							Px(sb, o + q, 2, 2, c * (1f - p));
					}
				}
			}
		}
	}
}
