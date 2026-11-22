using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace CasinoWorld.Content.NPCs.People
{
	/// <summary>
	/// Cabaret showgirl on the stage: dances an 8-frame high-kick loop and sways from side to side
	/// across the stage. Talks like the other staff.
	/// </summary>
	public class Showgirl : CasinoStaff
	{
		private const int FrameTicks = 7;
		private const float SwayTiles = 3f;

		protected override int VanillaLook => NPCID.PartyGirl;
		protected override int ChatLines => 4;
		protected override string CustomTexture => "CasinoWorld/Content/NPCs/People/Showgirl";
		protected override int CustomFrames => 8;

		private ref float HomeX => ref NPC.localAI[1];

		// Cabaret look: red curls, pink sequinned dress, bunny ears.
		private static readonly PersonLook look = new() {
			Hair = 25, SkinVariant = 6, Skin = new(250, 205, 175), HairColor = new(190, 50, 40),
			Shirt = new(235, 90, 160), UnderShirt = new(255, 200, 230), Pants = new(235, 90, 160), Shoes = new(200, 30, 90),
			HeadVanity = ItemID.BunnyEars,
		};
		protected override PersonLook Look => look;

		// Dance routine, 8 beats of 10 ticks: arms up and down, high kicks (jump legs) and little hops.
		private static readonly int[] DanceArms = { PersonDoll.ArmUp, PersonDoll.ArmUpForward, PersonDoll.ArmUp, PersonDoll.ArmForward,
			PersonDoll.ArmUpForward, PersonDoll.ArmUp, PersonDoll.ArmDown, PersonDoll.ArmUp };
		private static readonly int[] DanceLegs = { 0, PersonDoll.Jump, 0, 9, 0, PersonDoll.Jump, 13, 0 };

		protected override int BodyPose(int tick) => DanceArms[tick / 10 % 8];

		protected override int LegPose(int tick) => Math.Abs(NPC.velocity.X) > 0.3f ? -1 : DanceLegs[tick / 10 % 8];

		protected override float DrawLift(int tick) => DanceLegs[tick / 10 % 8] == PersonDoll.Jump ? 4f * (float)Math.Sin(tick % 10 / 10f * Math.PI) : 0f;

		public override void AI()
		{
			if (HomeX == 0f)
				HomeX = NPC.Center.X;
			// Slow sway across the stage, facing the way she moves.
			float t = (float)Main.timeForVisualEffects * 0.01f + NPC.whoAmI;
			float target = HomeX + (float)Math.Sin(t) * SwayTiles * 16f;
			NPC.velocity.X = MathHelper.Clamp((target - NPC.Center.X) * 0.05f, -1.2f, 1.2f);
			if (Math.Abs(NPC.velocity.X) > 0.05f)
				NPC.direction = NPC.spriteDirection = Math.Sign(NPC.velocity.X);
			FaceTalkingPlayer();
			if (Main.LocalPlayer.talkNPC == NPC.whoAmI)
				NPC.velocity.X = 0f;
		}

		public override void FindFrame(int frameHeight)
		{
			NPC.frameCounter++;
			if (NPC.frameCounter >= FrameTicks) {
				NPC.frameCounter = 0;
				NPC.frame.Y = (NPC.frame.Y + frameHeight) % (frameHeight * CustomFrames);
			}
		}
	}
}
