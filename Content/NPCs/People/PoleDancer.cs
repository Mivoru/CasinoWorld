using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace CasinoWorld.Content.NPCs.People
{
	/// <summary>
	/// Cabaret pole dancer on the pole stage. Her 16-second routine: circling the pole, climbing it in a
	/// fast spin, an upside-down hold, a spinning slide back down and a dance at the base.
	/// Drawn with the vanilla player renderer (rotation, facing and offsets per frame).
	/// </summary>
	public class PoleDancer : CasinoStaff
	{
		private const int Routine = 960;

		protected override int VanillaLook => NPCID.Stylist;
		protected override int ChatLines => 4;
		protected override string CustomTexture => "CasinoWorld/Content/NPCs/People/Showgirl";
		protected override int CustomFrames => 8;

		// Sequinned silver and hot-pink stage costume, platinum hair.
		private static readonly PersonLook look = new() {
			Hair = 26, SkinVariant = 7, Skin = new(245, 200, 170), HairColor = new(240, 230, 210),
			Shirt = new(230, 60, 160), UnderShirt = new(220, 220, 235), Pants = new(230, 60, 160), Shoes = new(240, 240, 250),
		};
		protected override PersonLook Look => look;

		public override void SetDefaults()
		{
			base.SetDefaults();
			NPC.noGravity = true;
		}

		public override void AI()
		{
			// Fixed on the stage platform: no gravity, no walking.
			NPC.velocity = Vector2.Zero;
			base.AI();
		}

		private static int Step(int tick) => tick % Routine;

		private static float Spin(int tick, float speed) => tick * speed;

		protected override float DrawShiftX(int tick)
		{
			int s = Step(tick);
			if (s < 180)
				return (float)Math.Sin(Spin(tick, 0.035f)) * 13f;          // circling the pole
			if (s < 660)
				return (float)Math.Sin(Spin(tick, s < 360 || s >= 540 ? 0.14f : 0.03f)) * (s >= 360 && s < 540 ? 5f : 8f);
			return 0f;
		}

		protected override float DrawLift(int tick)
		{
			int s = Step(tick);
			if (s < 180)
				return 0f;
			if (s < 360)
				return MathHelper.Lerp(0f, 40f, (s - 180) / 180f);         // climbing in a spin
			if (s < 540)
				return 42f + (float)Math.Sin(tick * 0.05f) * 2f;          // upside-down hold
			if (s < 660)
				return MathHelper.Lerp(42f, 0f, (s - 540) / 120f);        // spinning slide down
			int e = s - 660;
			return e % 60 < 12 ? (float)Math.Sin(e % 60 / 12f * Math.PI) * 4f : 0f; // little hops
		}

		protected override float DrawRotation(int tick)
		{
			int s = Step(tick);
			if (s >= 180 && s < 360)
				return (float)Math.Sin(Spin(tick, 0.14f)) * 0.35f;
			if (s >= 360 && s < 540)
				return MathHelper.Pi + (float)Math.Sin(tick * 0.04f) * 0.2f;
			if (s >= 540 && s < 660)
				return MathHelper.Lerp(MathHelper.Pi, 0f, (s - 540) / 120f);
			return 0f;
		}

		protected override int DrawFacing(int tick)
		{
			int s = Step(tick);
			if (s >= 660)
				return (s / 90) % 2 == 0 ? 1 : -1;
			float speed = s < 180 ? 0.035f : s >= 360 && s < 540 ? 0.03f : 0.14f;
			return Math.Cos(Spin(tick, speed)) >= 0 ? 1 : -1;
		}

		protected override int BodyPose(int tick)
		{
			int s = Step(tick);
			if (s < 180)
				return PersonDoll.ArmUpForward;                           // one hand on the pole
			if (s < 660)
				return PersonDoll.ArmUp;                                  // holding on above the head
			return (s / 15) % 3 == 0 ? PersonDoll.ArmUp : (s / 15) % 3 == 1 ? PersonDoll.ArmUpForward : PersonDoll.ArmForward;
		}

		protected override int LegPose(int tick)
		{
			int s = Step(tick);
			if (s < 180)
				return 6 + (tick / 5) % 14;                               // walking frames
			if (s < 660)
				return PersonDoll.Jump;                                   // legs wrapped / split
			return (s / 30) % 2 == 0 ? 0 : PersonDoll.Jump;
		}
	}
}
