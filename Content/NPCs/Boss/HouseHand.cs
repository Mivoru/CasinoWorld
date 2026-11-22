using System;
using CasinoWorld.Content.Projectiles.Boss;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.NPCs.Boss
{
	/// <summary>
	/// One of The House's croupier hands. Floats beside the machine and deals fans of cards;
	/// on command (mixed reels) it does a barrage: card fans plus a slam down at the player.
	/// Destroyed hands return after 20 seconds; while both are gone the machine takes 25 % more damage.
	/// ai[0] = body index, ai[1] = side (-1 left, 1 right), ai[2] = command, ai[3] = timer.
	/// </summary>
	public class HouseHand : ModNPC
	{
		public const int Idle = 0;
		public const int Barrage = 1;

		private NPC Body => Main.npc[(int)NPC.ai[0]];
		private int Side => (int)NPC.ai[1];
		private ref float Command => ref NPC.ai[2];
		private ref float Timer => ref NPC.ai[3];

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 3;
			NPCID.Sets.NPCBestiaryDrawOffset[Type] = new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true };
		}

		public override void SetDefaults()
		{
			NPC.width = 40;
			NPC.height = 40;
			NPC.damage = 28;
			NPC.defense = 10;
			NPC.lifeMax = 1200;
			NPC.knockBackResist = 0f;
			NPC.noGravity = true;
			NPC.noTileCollide = true;
			NPC.aiStyle = -1;
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath3;
		}

		public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
		{
			NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
		}

		public override bool CheckActive() => false;

		public override void AI()
		{
			if (!Body.active || Body.type != ModContent.NPCType<TheHouse>()) {
				NPC.active = false;
				return;
			}

			NPC.target = Body.target;
			Player target = Main.player[NPC.target];
			Timer++;
			Vector2 home = Body.Center + new Vector2(Side * 175, 40 + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + Side) * 12f);
			bool server = Main.netMode != NetmodeID.MultiplayerClient;
			var source = NPC.GetSource_FromAI();
			int damage = NPC.GetAttackDamage_ForProjectiles(22, 36);

			if (Command == Barrage) {
				// Card fan, slam, card fan, slam; then back to idle.
				int t = (int)Timer;
				if (t < 0) {
					MoveTo(home, 0.12f);
				}
				else if (t < 50 || (t >= 120 && t < 160)) {
					MoveTo(target.Center + new Vector2(Side * 220, -160), 0.1f);
					if (server && (t == 30 || t == 140))
						CardFan(source, target, damage, 7, 0.14f);
				}
				else if (t < 70) {
					MoveTo(target.Center + new Vector2(0, -220), 0.15f);
				}
				else if (t < 100) {
					NPC.velocity = new Vector2(0, 18f);
					if (t == 99) {
						SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
						Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Vector2.UnitY, 6f, 6f, 20, 1000f, "HouseHand"));
					}
				}
				else if (t < 120) {
					NPC.velocity *= 0.8f;
				}
				else if (t >= 160) {
					Command = Idle;
					Timer = 0;
					NPC.netUpdate = true;
				}
			}
			else {
				MoveTo(home, 0.1f);
				if (server && (int)Timer % 170 == (Side == 1 ? 85 : 0))
					CardFan(source, target, damage, 5, 0.18f);
			}

			NPC.spriteDirection = Side;
		}

		private void MoveTo(Vector2 goal, float lerp)
		{
			Vector2 wanted = (goal - NPC.Center) * lerp;
			if (wanted.Length() > 16f)
				wanted = wanted.SafeNormalize(Vector2.Zero) * 16f;
			NPC.velocity = Vector2.Lerp(NPC.velocity, wanted, 0.3f);
		}

		private void CardFan(Terraria.DataStructures.IEntitySource source, Player target, int damage, int cards, float spread)
		{
			Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
			for (int k = 0; k < cards; k++)
				Projectile.NewProjectile(source, NPC.Center, dir.RotatedBy((k - (cards - 1) / 2f) * spread) * 9f, ModContent.ProjectileType<BossCard>(), damage, 1f);
			SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
		}

		public override void FindFrame(int frameHeight)
		{
			int frame = 0;
			if (Command == Barrage && Timer >= 50 && Timer < 120)
				frame = 1;   // fist for the slam
			else if ((int)Timer % 170 is > 150 or < 10 || (Command == Barrage && Timer is > 20 and < 40))
				frame = 2;   // pointing while dealing
			NPC.frame.Y = frame * frameHeight;
		}
	}
}
