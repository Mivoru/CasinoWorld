using System;
using CasinoWorld.Content.NPCs.Boss;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace CasinoWorld.Common.Effects
{
	/// <summary>
	/// Sky of the Fortuna fight: velvet night in steps, a huge dim roulette wheel turning behind the world and
	/// falling card suits. Violet and gold at first, blood red once her eye is open.
	/// </summary>
	public class FortunaSky : CustomSky
	{
		public const string Key = "CasinoWorld:Fortuna";

		private struct Suit
		{
			public Vector2 Position;
			public float Speed, Sway;
			public int Kind;
			public bool Big;
		}

		private bool active;
		private float intensity, red;
		private Suit[] suits;
		private static Asset<Texture2D> pocketTex, suitTex;

		public override void Activate(Vector2 position, params object[] args)
		{
			active = true;
			if (suits != null)
				return;
			var rand = new UnifiedRandom();
			suits = new Suit[70];
			for (int k = 0; k < suits.Length; k++)
				suits[k] = new Suit { Position = new Vector2(rand.NextFloat(), rand.NextFloat()), Speed = rand.NextFloat(0.0008f, 0.0028f), Sway = rand.NextFloat(6f), Kind = rand.Next(4), Big = rand.NextBool(4) };
		}

		public override void Deactivate(params object[] args) => active = false;

		public override void Reset()
		{
			active = false;
			intensity = 0f;
		}

		public override bool IsActive() => active || intensity > 0.001f;

		public override void Update(GameTime gameTime)
		{
			intensity = MathHelper.Clamp(intensity + (active ? 0.015f : -0.02f), 0f, 1f);
			red = MathHelper.Clamp(red + (FortunaSkySystem.Awake ? 0.02f : -0.02f), 0f, 1f);
			if (suits == null)
				return;
			for (int k = 0; k < suits.Length; k++) {
				suits[k].Position.Y += suits[k].Speed * (1f + red);
				suits[k].Position.X += (float)Math.Sin(Main.GlobalTimeWrappedHourly + suits[k].Sway) * 0.0003f;
				if (suits[k].Position.Y > 1.05f)
					suits[k].Position.Y = -0.05f;
			}
		}

		public override Color OnTileColor(Color inColor)
			=> Color.Lerp(inColor, Color.Lerp(new Color(150, 120, 190), new Color(200, 105, 115), red), intensity * 0.6f);

		public override float GetCloudAlpha() => 1f - intensity;

		public override void Draw(SpriteBatch sb, float minDepth, float maxDepth)
		{
			if (maxDepth < 0f || minDepth >= 0f)
				return;
			Texture2D px = TextureAssets.MagicPixel.Value;
			Color top = Color.Lerp(new Color(16, 6, 38), new Color(34, 2, 12), red), low = Color.Lerp(new Color(92, 34, 110), new Color(128, 18, 34), red);
			const int Bands = 18;
			int h = Main.screenHeight / Bands + 1;
			for (int k = 0; k < Bands; k++)
				sb.Draw(px, new Rectangle(0, k * h, Main.screenWidth, h), Color.Lerp(top, low, k / (float)Bands) * intensity);

			pocketTex ??= ModContent.Request<Texture2D>("CasinoWorld/Content/NPCs/Boss/Fortuna_Pocket", AssetRequestMode.ImmediateLoad);
			suitTex ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/Suits", AssetRequestMode.ImmediateLoad);
			Vector2 middle = new(Main.screenWidth / 2f, Main.screenHeight * 0.42f);
			float spin = Main.GlobalTimeWrappedHourly * (0.06f + 0.08f * red);
			for (int ring = 0; ring < 2; ring++) {
				int count = ring == 0 ? 37 : 24;
				float radius = Main.screenHeight * (ring == 0 ? 0.62f : 0.4f);
				for (int k = 0; k < count; k++) {
					float a = (ring == 0 ? spin : -spin * 1.5f) + k * MathHelper.TwoPi / count;
					Rectangle frame = pocketTex.Value.Frame(1, 3, 0, k == 0 ? 2 : k % 2);
					Color c = Color.Lerp(new Color(150, 110, 200), new Color(230, 90, 90), red) * (0.22f * intensity);
					sb.Draw(pocketTex.Value, middle + a.ToRotationVector2() * radius, frame, c, a + MathHelper.PiOver2, frame.Size() / 2f, ring == 0 ? 3f : 2f, SpriteEffects.None, 0f);
				}
			}
			if (suits == null)
				return;
			foreach (Suit s in suits) {
				Color c = Color.Lerp(new Color(255, 215, 120), new Color(255, 110, 100), red) * ((s.Big ? 0.1f : 0.2f) * intensity);
				sb.Draw(suitTex.Value, PixelFx.Snap(s.Position * new Vector2(Main.screenWidth, Main.screenHeight)), new Rectangle(s.Kind * 12, 0, 12, 12), c, 0f, new Vector2(6), s.Big ? 4f : 2f, SpriteEffects.None, 0f);
			}
		}
	}

	/// <summary>Switches the sky on while Fortuna is alive.</summary>
	public class FortunaSkySystem : ModSystem
	{
		public static bool Awake { get; private set; }

		public override void Load()
		{
			if (!Main.dedServ)
				SkyManager.Instance[FortunaSky.Key] = new FortunaSky();
		}

		public override void PostUpdateEverything()
		{
			if (Main.dedServ)
				return;
			int index = NPC.FindFirstNPC(ModContent.NPCType<Fortuna>());
			bool alive = index >= 0;
			Awake = alive && Main.npc[index].ModNPC is Fortuna { Awake: true };
			if (alive && !skyOn) {
				skyOn = true;
				SkyManager.Instance.Activate(FortunaSky.Key);
			}
			else if (!alive && skyOn) {
				skyOn = false;
				SkyManager.Instance.Deactivate(FortunaSky.Key);
			}
		}

		private static bool skyOn;

		public override void OnWorldUnload()
		{
			if (Main.dedServ || !skyOn)
				return;
			skyOn = false;
			SkyManager.Instance.Deactivate(FortunaSky.Key);
		}
	}
}
