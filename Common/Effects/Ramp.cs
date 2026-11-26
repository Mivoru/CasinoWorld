using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace CasinoWorld.Common.Effects
{
	/// <summary>
	/// Five-step colour ramp, lightest first, the way pixel artists shade: the light end shifts towards warm
	/// white, the dark end shifts in hue (towards red or violet) and loses saturation instead of only going darker.
	/// Effects step through a ramp in whole steps, never blend between them.
	/// </summary>
	public readonly struct Ramp
	{
		public readonly Color C0, C1, C2, C3, C4;

		public Ramp(Color c0, Color c1, Color c2, Color c3, Color c4)
		{
			C0 = c0; C1 = c1; C2 = c2; C3 = c3; C4 = c4;
		}

		public Color this[int step] => step switch {
			<= 0 => C0,
			1 => C1,
			2 => C2,
			3 => C3,
			_ => C4,
		};

		/// <summary>Step for a life fraction 0..1, starting at a given step.</summary>
		public Color At(float t, int from = 0, int to = 4)
			=> this[from + Math.Clamp((int)(t * (to - from + 1)), 0, to - from)];

		// ---- hand-picked ramps for the main effect families

		public static readonly Ramp Gold = new(new Color(255, 255, 236), new Color(255, 236, 140), new Color(250, 186, 56), new Color(214, 102, 34), new Color(128, 40, 36));
		public static readonly Ramp Violet = new(new Color(255, 240, 255), new Color(232, 176, 255), new Color(176, 96, 240), new Color(108, 46, 186), new Color(54, 22, 104));
		public static readonly Ramp Danger = new(new Color(255, 248, 230), new Color(255, 196, 140), new Color(244, 78, 58), new Color(168, 28, 54), new Color(84, 14, 48));
		public static readonly Ramp Celestial = new(new Color(255, 252, 240), new Color(255, 226, 168), new Color(246, 150, 150), new Color(178, 78, 156), new Color(88, 40, 120));
		public static readonly Ramp Cyan = new(new Color(240, 255, 255), new Color(160, 250, 255), new Color(70, 196, 236), new Color(40, 112, 190), new Color(30, 48, 110));
		public static readonly Ramp Green = new(new Color(244, 255, 230), new Color(176, 255, 160), new Color(86, 210, 110), new Color(34, 132, 96), new Color(22, 66, 70));
		public static readonly Ramp Smoke = new(new Color(236, 230, 236), new Color(178, 168, 184), new Color(118, 108, 130), new Color(70, 62, 86), new Color(40, 34, 52));
		public static readonly Ramp Paper = new(new Color(255, 255, 255), new Color(236, 232, 240), new Color(196, 188, 206), new Color(130, 118, 146), new Color(70, 58, 84));

		private static readonly Dictionary<uint, Ramp> cache = new();

		/// <summary>
		/// Ramp for any colour: the colour sits at step 2, step 1 is lighter and warmer, step 0 near white,
		/// steps 3 and 4 darker with the hue turned towards red-violet and less saturation.
		/// </summary>
		public static Ramp For(Color c)
		{
			if (cache.TryGetValue(c.PackedValue, out Ramp r))
				return r;
			Rgb2Hsv(c, out float h, out float s, out float v);
			Color Make(float dh, float ds, float dv, float sMul, float vMul)
			{
				float hh = h + dh;
				float ss = Math.Clamp(s * sMul + ds, 0f, 1f), vv = Math.Clamp(v * vMul + dv, 0f, 1f);
				return Hsv2Rgb(hh, ss, vv);
			}
			// Turning the dark end towards hue 300-340 (red-violet) reads warmer than plain darkening.
			float towards = Shortest(h, 0.9f);
			r = new Ramp(
				Make(Shortest(h, 0.14f) * 0.15f, 0f, 0.25f, 0.18f, 1f),
				Make(Shortest(h, 0.14f) * 0.08f, 0f, 0.12f, 0.6f, 1f),
				c,
				Make(towards * 0.08f, 0.08f, 0f, 1f, 0.66f),
				Make(towards * 0.16f, 0f, 0f, 0.75f, 0.36f));
			if (cache.Count < 512)
				cache[c.PackedValue] = r;
			return r;
		}

		private static float Shortest(float from, float to)
		{
			float d = (to - from) % 1f;
			if (d > 0.5f)
				d -= 1f;
			if (d < -0.5f)
				d += 1f;
			return d;
		}

		private static void Rgb2Hsv(Color c, out float h, out float s, out float v)
		{
			float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
			float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
			v = max;
			s = max <= 0f ? 0f : d / max;
			if (d <= 0f)
				h = 0f;
			else if (max == r)
				h = (g - b) / d / 6f;
			else if (max == g)
				h = ((b - r) / d + 2f) / 6f;
			else
				h = ((r - g) / d + 4f) / 6f;
			h = (h % 1f + 1f) % 1f;
		}

		private static Color Hsv2Rgb(float h, float s, float v)
		{
			h = (h % 1f + 1f) % 1f * 6f;
			int i = (int)h;
			float f = h - i, p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
			(float r, float g, float b) = i switch {
				0 => (v, t, p),
				1 => (q, v, p),
				2 => (p, v, t),
				3 => (p, q, v),
				4 => (t, p, v),
				_ => (v, p, q),
			};
			return new Color(r, g, b);
		}
	}
}
