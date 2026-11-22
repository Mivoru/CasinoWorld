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
