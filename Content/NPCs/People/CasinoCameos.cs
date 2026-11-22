using System;
using CasinoWorld.Common;
using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.NPCs.People
{
	/// <summary>
	/// Easter egg: Togi, the gambling streamer, sometimes plays the upstairs slots of the surface casino.
	/// He pulls the lever non-stop, talks to his chat and, every so often, hits a "bonus" with confetti.
	/// </summary>
	public class Togi : CasinoNPC
	{
		private const int OverheadLines = 8;

		protected override int VanillaLook => NPCID.Guide;
		protected override int ChatLines => 6;

		// Black hoodie with a red lining, messy dark hair, gamer sunglasses.
		private static readonly PersonLook look = new() {
			Hair = 3, SkinVariant = 2, Skin = new(240, 195, 160), HairColor = new(35, 28, 25),
			Shirt = new(28, 28, 32), UnderShirt = new(200, 30, 45), Pants = new(45, 45, 60), Shoes = new(230, 230, 230),
			Accessory = ItemID.Sunglasses,
		};
		protected override PersonLook Look => look;

		/// <summary>Spin, spin, spin: the lever every two seconds, both arms up when the bonus hits.</summary>
		protected override int BodyPose(int tick)
		{
			if (tick % 1800 < 90)
				return (tick / 8) % 2 == 0 ? PersonDoll.ArmUp : PersonDoll.ArmUpForward;
			int cycle = tick % 120;
			return cycle < 8 ? PersonDoll.ArmUp : cycle < 16 ? PersonDoll.ArmForward : -1;
		}

		protected override float DrawLift(int tick) => tick % 1800 < 90 ? Math.Abs((float)Math.Sin(tick * 0.25f)) * 4f : 0f;

		public override void AI()
		{
			NPC.velocity.X = 0f;
			if (NPC.ai[0] == 0f)
				NPC.ai[0] = 1f;
			NPC.direction = NPC.spriteDirection = (int)NPC.ai[0];
			FaceTalkingPlayer();
			if (Main.dedServ)
				return;

			int tick = (int)Main.GameUpdateCount + NPC.whoAmI * 37;
			if (tick % 1800 == 0) {
				CombatText.NewText(NPC.Hitbox, new Color(255, 220, 80), Language.GetTextValue("Mods.CasinoWorld.NPCs.Togi.Bonus"), dramatic: true);
				SoundEngine.PlaySound(CasinoSounds.SmallWin, NPC.Center);
				for (int k = 0; k < 24; k++)
					Dust.NewDustPerfect(NPC.Top, DustID.Confetti + Main.rand.Next(4), new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-5f, -2f)));
			}
			else if (Main.rand.NextBool(500)) {
				string line = Language.GetTextValue($"Mods.CasinoWorld.NPCs.Togi.Overhead.{Main.rand.Next(OverheadLines)}");
				CombatText.NewText(NPC.Hitbox, new Color(180, 140, 255), line);
			}
		}
	}

	/// <summary>
	/// Guests who step out of the surface casino for some air: they leave through the door, stroll
	/// around in front of the building, chat, and walk back in. Spawned by <see cref="CasinoVisitorSystem"/>.
	///
	/// ai[0] phase (0 out, 1 linger, 2 back), ai[1] timer, ai[2] stroll target x, ai[3] door x.
	/// </summary>
	public class CasinoVisitor : CasinoNPC
	{
		private const float WalkSpeed = 1f;

		protected override int VanillaLook => NPCID.Guide;
		protected override int ChatLines => 6;

		private PersonLook personLook;
		protected override PersonLook Look => personLook ??= PersonLook.RandomGuest(Main.worldID * 31 + NPC.whoAmI * 7919 + (int)NPC.ai[3]);

		private ref float Phase => ref NPC.ai[0];
		private ref float Timer => ref NPC.ai[1];
		private ref float TargetX => ref NPC.ai[2];
		private ref float DoorX => ref NPC.ai[3];

		public override void SetDefaults()
		{
			base.SetDefaults();
			NPC.npcSlots = 0f;
		}

		public override void AI()
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				ServerAI();
			if (Math.Abs(NPC.velocity.X) > 0.05f)
				NPC.direction = NPC.spriteDirection = Math.Sign(NPC.velocity.X);
			FaceTalkingPlayer();
			if (Main.LocalPlayer.talkNPC == NPC.whoAmI)
				NPC.velocity.X = 0f;

			// Small step-ups, like town NPCs, so they do not get stuck on a one-tile ledge.
			if (NPC.collideX && NPC.velocity.Y == 0f)
				NPC.velocity.Y = -5f;

			if (!Main.dedServ && Phase == 1 && Main.rand.NextBool(700)) {
				string line = Language.GetTextValue($"Mods.CasinoWorld.NPCs.CasinoGuest.Overhead.{Main.rand.Next(10)}");
				CombatText.NewText(NPC.Hitbox, new Color(255, 230, 150), line);
			}
		}

		private void ServerAI()
		{
			switch (Phase) {
				case 0:
				case 2:
					float goal = Phase == 0 ? TargetX : DoorX;
					float dx = goal - NPC.Center.X;
					if (Math.Abs(dx) < 8f || ++Timer > 1800) {
						if (Phase == 2) {
							// Back inside: the door chimes and the visitor is gone.
							SoundEngine.PlaySound(CasinoSounds.DoorEnter with { Volume = 0.4f }, NPC.Center);
							NPC.active = false;
							NPC.netUpdate = true;
							return;
						}
						Phase = 1;
						Timer = Main.rand.Next(600, 1800);
						NPC.velocity.X = 0f;
						NPC.netUpdate = true;
					}
					else {
						NPC.velocity.X = Math.Sign(dx) * WalkSpeed;
					}
					break;

				case 1:
					NPC.velocity.X *= 0.8f;
					if (--Timer <= 0) {
						Phase = 2;
						Timer = 0;
						NPC.netUpdate = true;
					}
					break;
			}
		}

		public override string GetChat() => Language.GetTextValue($"Mods.CasinoWorld.NPCs.CasinoVisitor.Chat.{Main.rand.Next(ChatLines)}");
	}

	/// <summary>Lets up to three guests at a time step out of the surface casino while a player is nearby.</summary>
	public class CasinoVisitorSystem : ModSystem
	{
		private const int MaxVisitors = 3;
		private const int SpawnChance = 900;   // 1 in N ticks
		private const float NearDistance = 90 * 16;

		public override void PostUpdateWorld()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || CasinoTravel.InCasino || !CasinoWorldSystem.HasSurfaceCasino || !Main.rand.NextBool(SpawnChance))
				return;

			Point casino = CasinoWorldSystem.SurfaceCasinoPosition;
			Vector2 door = new Vector2(casino.X + CasinoFacade.Width / 2f, casino.Y + CasinoFacade.Height) * 16f;
			bool playerNear = false;
			foreach (Player player in Main.ActivePlayers) {
				if (player.Distance(door) < NearDistance)
					playerNear = true;
			}
			if (!playerNear || NPC.CountNPCS(ModContent.NPCType<CasinoVisitor>()) >= MaxVisitors)
				return;

			int side = Main.rand.NextBool() ? -1 : 1;
			float target = door.X + side * Main.rand.Next(12, 30) * 16f;
			int index = NPC.NewNPC(new Terraria.DataStructures.EntitySource_SpawnNPC(), (int)door.X, (int)door.Y - 2, ModContent.NPCType<CasinoVisitor>(),
				ai0: 0, ai1: 0, ai2: target, ai3: door.X);
			if (index < Main.maxNPCs)
				SoundEngine.PlaySound(CasinoSounds.DoorEnter with { Volume = 0.4f }, door);
		}
	}
}
