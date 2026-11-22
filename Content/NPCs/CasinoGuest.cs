using System;
using CasinoWorld.Content.Subworlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.NPCs
{
	/// <summary>
	/// Guests who stroll between the tables and the bar, linger to "play", and mutter gambler lines.
	/// Each guest picks one of several looks so the hall does not look cloned.
	///
	/// AI slots (synced by vanilla): ai[0] state (0 walking, 1 lingering), ai[1] timer,
	/// ai[2] target x in pixels, ai[3] look index.
	/// </summary>
	public class CasinoGuest : CasinoNPC
	{
		private const float WalkSpeed = 1.1f;
		private const int OverheadLines = 10;

		private static readonly int[] Looks = {
			NPCID.Guide, NPCID.Merchant, NPCID.ArmsDealer, NPCID.Painter, NPCID.Stylist,
			NPCID.Golfer, NPCID.Nurse, NPCID.PartyGirl, NPCID.Mechanic, NPCID.DD2Bartender,
		};

		protected override int VanillaLook => NPCID.Guide;
		protected override int ChatLines => 12;

		private int Look => Looks[Math.Clamp((int)NPC.ai[3], 0, Looks.Length - 1)];

		private ref float State => ref NPC.ai[0];
		private ref float Timer => ref NPC.ai[1];
		private ref float TargetX => ref NPC.ai[2];

		public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;

			NPC.ai[3] = Main.rand.Next(Looks.Length);
			State = 1;
			Timer = Main.rand.Next(60, 400);
			NPC.netUpdate = true;
		}

		public override void AI()
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				ServerAI();

			if (NPC.velocity.X != 0f)
				NPC.direction = NPC.spriteDirection = Math.Sign(NPC.velocity.X);
			FaceTalkingPlayer();

			// Stand still while someone is talking to this guest.
			if (Main.LocalPlayer.talkNPC == NPC.whoAmI)
				NPC.velocity.X = 0f;

			// Cosmetic overhead lines, rolled on each client separately.
			if (Main.netMode != NetmodeID.Server && State == 1 && Main.rand.NextBool(900)) {
				string line = Language.GetTextValue($"Mods.CasinoWorld.NPCs.CasinoGuest.Overhead.{Main.rand.Next(OverheadLines)}");
				CombatText.NewText(NPC.Hitbox, new Color(255, 230, 150), line);
			}
		}

		private void ServerAI()
		{
			if (State == 0) {
				float dx = TargetX - NPC.Center.X;
				if (Math.Abs(dx) < 10f || NPC.collideX) {
					State = 1;
					Timer = Main.rand.Next(300, 1000);
					NPC.velocity.X = 0f;
					NPC.netUpdate = true;
				}
				else {
					NPC.velocity.X = Math.Sign(dx) * WalkSpeed;
				}
				return;
			}

			NPC.velocity.X *= 0.8f;
			if (--Timer <= 0) {
				TargetX = PickDestination();
				State = 0;
				NPC.netUpdate = true;
			}
		}

		private float PickDestination()
		{
			int[] spots = (SubworldLibrary.SubworldSystem.Current as CasinoSubworld)?.GuestSpots;
			if (spots == null || spots.Length == 0)
				return NPC.Center.X;
			int tileX = spots[Main.rand.Next(spots.Length)] + Main.rand.Next(-2, 3);
			return tileX * 16 + 8;
		}

		public override void FindFrame(int frameHeight)
		{
			// Frame index only; PreDraw computes the rectangle for the chosen look.
			if (Math.Abs(NPC.velocity.X) > 0.1f) {
				NPC.frameCounter += Math.Abs(NPC.velocity.X) * 2f;
				if (NPC.frameCounter >= 8) {
					NPC.frameCounter = 0;
					NPC.localAI[0] = NPC.localAI[0] < 2 || NPC.localAI[0] >= 15 ? 2 : NPC.localAI[0] + 1;
				}
			}
			else {
				NPC.localAI[0] = 0;
				NPC.frameCounter = 0;
			}
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			int look = Look;
			Main.instance.LoadNPC(look);
			Texture2D texture = TextureAssets.Npc[look].Value;
			int frames = Main.npcFrameCount[look];
			int frameHeight = texture.Height / frames;
			int frame = Math.Min((int)NPC.localAI[0], frames - 1);
			var source = new Rectangle(0, frame * frameHeight, texture.Width, frameHeight);

			Vector2 position = NPC.Bottom - screenPos + new Vector2(0f, NPC.gfxOffY + 2f);
			var origin = new Vector2(texture.Width / 2f, frameHeight);
			SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			spriteBatch.Draw(texture, position, source, NPC.GetAlpha(drawColor), 0f, origin, 1f, effects, 0f);
			return false;
		}
	}
}
