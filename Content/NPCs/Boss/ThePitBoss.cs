using System;
using System.Collections.Generic;
using System.IO;
using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.Crates;
using CasinoWorld.Content.Items.PitBoss;
using CasinoWorld.Content.NPCs.Enemies;
using CasinoWorld.Content.Projectiles.Boss;
using CasinoWorld.Content.Subworlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using SubworldLibrary;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.NPCs.Boss
{
	/// <summary>
	/// The Pit Boss (design/CasinoWorld/pit_boss.md): hardmode boss of the High Rollers casino. A mob-boss
	/// croupier riding a flying roulette wheel. AI runs on the server; ai[0] state, ai[1] timer, ai[2]
	/// counter, ai[3] argument (synced by vanilla). Drawn in parts: wheel, body, hands, smoke.
	/// </summary>
	[AutoloadBossHead]
	public class ThePitBoss : ModNPC
	{
		public enum State { Intro, Hover, ChipSpray, PlaceBets, GoldRush, Security, CashOut, WheelBarrage, DoubleOrNothing, Stunned, Leave, Dying }

		public const int BaseLife = 30000;
		// Damage taken in normal mode before defense (see Damage).
		private const int ChipDamage = 56, ZoneDamage = 72, BarDamage = 64, CoinDamage = 90;

		private State CurrentState { get => (State)NPC.ai[0]; set => NPC.ai[0] = (float)value; }
		private ref float Timer => ref NPC.ai[1];
		private ref float Counter => ref NPC.ai[2];
		private ref float Argument => ref NPC.ai[3];

		private float LifeRatio => NPC.life / (float)NPC.lifeMax;
		public int Phase => LifeRatio > 0.55f ? 1 : LifeRatio > 0.25f ? 2 : 3;
		private float Speed => Phase == 3 ? 1.25f : 1f;
		private int lastAttack = -1;
		private readonly List<State> deck = new();
		private int deckPhase;
		private float previousTimer;

		/// <summary>True on the one tick in which the timer passes a mark (the timer steps by 1.25 in phase 3).</summary>
		private bool At(float mark) => previousTimer < mark && Timer >= mark;

		public override string Texture => "CasinoWorld/Content/NPCs/Boss/PitBoss_Body";
		public override string BossHeadTexture => "CasinoWorld/Content/NPCs/Boss/PitBoss_Head_Boss";

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 5;   // idle, laugh, throw, charge, stunned
			NPCID.Sets.MPAllowedEnemies[Type] = true;
			NPCID.Sets.BossBestiaryPriority.Add(Type);
			NPCID.Sets.TrailCacheLength[Type] = 8;
			NPCID.Sets.TrailingMode[Type] = 3;
			NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
		}

		public override void SetDefaults()
		{
			NPC.width = 90;
			NPC.height = 130;
			NPC.damage = 64;
			NPC.defense = 26;
			NPC.lifeMax = BaseLife;
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath1;
			NPC.knockBackResist = 0f;
			NPC.noGravity = true;
			NPC.noTileCollide = true;
			NPC.boss = true;
			NPC.npcSlots = 15f;
			NPC.value = Item.buyPrice(gold: 15);
			NPC.aiStyle = -1;
			if (!Main.dedServ)
				Music = CasinoMusic.PitBoss;
		}

		public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
		{
			NPC.lifeMax = (int)(NPC.lifeMax * 0.7f * balance * bossAdjustment);
			NPC.damage = (int)(NPC.damage * 0.8f);
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				new FlavorTextBestiaryInfoElement("Mods.CasinoWorld.Bestiary.ThePitBoss"),
			});
		}

		// ------------------------------------------------------------------ helpers

		private Player Target => Main.player[NPC.target];

		private void SetState(State state, float argument = 0f)
		{
			CurrentState = state;
			Timer = 0;
			Counter = 0;
			Argument = argument;
			NPC.netUpdate = true;
		}

		private static void Say(string key, Color color)
		{
			if (Main.netMode == NetmodeID.Server)
				Terraria.Chat.ChatHelper.BroadcastChatMessage(NetworkText.FromKey(key), color);
			else if (Main.netMode == NetmodeID.SinglePlayer)
				Main.NewText(Language.GetTextValue(key), color);
		}

		/// <summary>
		/// Projectile damage for a hit of the given normal-mode size. Vanilla multiplies hostile projectiles
		/// by 2 / 4 / 6 (normal / expert / master); the hits end up at x1 / x1.55 / x2.2.
		/// </summary>
		private static int Damage(int hit) => Main.masterMode ? (int)(hit * 2.2f / 6f) : Main.expertMode ? (int)(hit * 1.55f / 4f) : hit / 2;

		private void Shoot(Vector2 position, Vector2 velocity, int type, int damage, float ai0 = 0, float ai1 = 0, float ai2 = 0)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				Projectile.NewProjectile(NPC.GetSource_FromAI(), position, velocity, type, Damage(damage), 1f, Main.myPlayer, ai0, ai1, ai2);
		}

		/// <summary>Flies towards a point, kept inside the target's room. Speeds up when the target runs far away.</summary>
		private void FlyTo(Vector2 goal, float speed, float inertia)
		{
			goal = ClampToRoom(goal);
			Vector2 desired = goal - NPC.Center;
			float dist = desired.Length();
			speed += MathHelper.Clamp((dist - 450f) / 40f, 0f, 14f);
			if (dist > 1f)
				desired = desired / dist * Math.Min(speed, dist * 0.1f + 1f);
			NPC.velocity = (NPC.velocity * (inertia - 1) + desired) / inertia;
		}

		public static bool InHall => SubworldSystem.Current is UndergroundCasino;

		/// <summary>World x of the inner sides of the hall walls; outside the casino, a wide area around the given x.</summary>
		public static float HallLeft(float around) => InHall ? (CasinoInterior.Left + 1) * 16 : around - BetZone.BoardWidth / 2f;
		public static float HallRight(float around) => InHall ? CasinoInterior.Right * 16 : around + BetZone.BoardWidth / 2f;

		/// <summary>Keeps the boss between the walls, floor and ceiling of the storey the target is on.</summary>
		private Vector2 ClampToRoom(Vector2 at)
		{
			Vector2 room = Target.Center;
			float top = CeilingAbove(room) + 70f, bottom = Math.Max(top, FloorBelow(room) - NPC.height / 2f);
			return new Vector2(MathHelper.Clamp(at.X, HallLeft(room.X) + 60f, HallRight(room.X) - 60f), MathHelper.Clamp(at.Y, top, bottom));
		}

		/// <summary>Floor (world y of the first solid tile top) under a point, and the ceiling above it.</summary>
		public static float FloorBelow(Vector2 at)
		{
			int x = (int)(at.X / 16), y = (int)(at.Y / 16);
			for (int k = 0; k < 60 && y < Main.maxTilesY - 1; k++, y++)
				if (WorldGen.SolidTile(x, y))
					return y * 16;
			return at.Y + 200;
		}

		public static float CeilingAbove(Vector2 at)
		{
			int x = (int)(at.X / 16), y = (int)(at.Y / 16);
			for (int k = 0; k < 60 && y > 1; k++, y--)
				if (WorldGen.SolidTile(x, y))
					return y * 16 + 16;
			return at.Y - 400;
		}

		public Vector2 HandPosition(int side) => NPC.Center + new Vector2(side * 56f, 6f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + side) * 4f);

		// ------------------------------------------------------------------ AI

		public override void AI()
		{
			if (NPC.target < 0 || NPC.target == 255 || Target.dead || !Target.active)
				NPC.TargetClosest();
			bool leaving = Target.dead || !Target.active || NPC.Distance(Target.Center) > 4000f;
			if (leaving && CurrentState is not (State.Leave or State.Dying)) {
				Say("Mods.CasinoWorld.Boss.PitBoss.Leave", new Color(255, 200, 90));
				SetState(State.Leave);
			}
			if (!Main.dedServ && Phase >= 2)
				Music = CasinoMusic.PitBossTwo;
			NPC.dontTakeDamage = CurrentState is State.Intro or State.Leave or State.Dying;
			Lighting.AddLight(NPC.Center, Phase == 3 ? new Vector3(0.9f, 0.3f, 0.2f) : new Vector3(0.8f, 0.6f, 0.3f));
			if (Main.rand.NextBool(5))   // cigar smoke
				Dust.NewDustPerfect(NPC.Center + new Vector2(NPC.spriteDirection * 26, -46), DustID.Smoke, new Vector2(NPC.spriteDirection * 0.3f, -1.2f), 150, default, 0.9f).noGravity = true;

			previousTimer = Timer;
			Timer += Speed;
			if (Timer > 900 && CurrentState is not (State.Intro or State.Leave or State.Dying))   // safety net: no attack takes this long
				SetState(State.Hover);
			switch (CurrentState) {
				case State.Intro: Intro(); break;
				case State.Hover: Hover(); break;
				case State.ChipSpray: ChipSpray(); break;
				case State.PlaceBets: PlaceBets(); break;
				case State.GoldRush: GoldRush(); break;
				case State.Security: Security(); break;
				case State.CashOut: CashOut(); break;
				case State.WheelBarrage: WheelBarrage(); break;
				case State.DoubleOrNothing: DoubleOrNothing(); break;
				case State.Stunned: Stunned(); break;
				case State.Leave: Leave(); break;
				case State.Dying: Dying(); break;
			}
			if (CurrentState != State.CashOut)
				NPC.spriteDirection = NPC.direction = Target.Center.X < NPC.Center.X ? -1 : 1;
		}

		private void Intro()
		{
			NPC.velocity *= 0.9f;
			if (At(1)) {
				SoundEngine.PlaySound(CasinoSounds.VaultDoor, NPC.Center);
				Say("Mods.CasinoWorld.Boss.PitBoss.Intro", new Color(255, 200, 90));
				if (!Main.dedServ)
					BossTitle.Show("Mods.CasinoWorld.Boss.PitBoss.Title", "Mods.CasinoWorld.Boss.PitBoss.Subtitle", new Color(255, 200, 90));
			}
			if (At(60)) {
				SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f }, NPC.Center);
				CasinoFx.Ring(NPC.Center, new Color(255, 210, 90), 30, 9f, 1.6f);
				if (!Main.dedServ)
					global::CasinoWorld.Common.Effects.CasinoFx.Shake(NPC.Center, Vector2.UnitY, 7f, 8f, 25, 1800f, "PitBossIntro");
			}
			if (Timer > 100)
				SetState(State.Hover);
		}

		private void Hover()
		{
			FlyTo(Target.Center + new Vector2(Math.Sign(NPC.Center.X - Target.Center.X) * 300f, -150f), 9f, 18f);
			if (Timer < (Phase == 1 ? 60 : 42) || Main.netMode == NetmodeID.MultiplayerClient)
				return;
			State[] pool = Phase == 1
				? new[] { State.ChipSpray, State.PlaceBets, State.GoldRush, State.Security, State.CashOut }
				: new[] { State.ChipSpray, State.PlaceBets, State.GoldRush, State.CashOut, State.WheelBarrage, State.DoubleOrNothing, State.Security };
			// Attacks come from a shuffled deck: each one once, then it is shuffled again (never the same twice in a row).
			if (deckPhase != Math.Min(Phase, 2)) {
				deckPhase = Math.Min(Phase, 2);
				deck.Clear();
			}
			if (deck.Count == 0) {
				deck.AddRange(pool);
				for (int k = deck.Count - 1; k > 0; k--) {
					int swap = Main.rand.Next(k + 1);
					(deck[k], deck[swap]) = (deck[swap], deck[k]);
				}
			}
			int pick = deck.FindIndex(a => (int)a != lastAttack && !(a == State.Security && CountGuards() >= 2));
			if (pick < 0) {
				deck.Clear();
				return;
			}
			State next = deck[pick];
			deck.RemoveAt(pick);
			lastAttack = (int)next;
			SetState(next);
		}

		private int CountGuards()
		{
			int n = 0;
			foreach (NPC npc in Main.ActiveNPCs)
				if (npc.type == ModContent.NPCType<RoyalGuard>())
					n++;
			return n;
		}

		// ---- Chip Spray: arcs of bouncing chips from both hands.
		private void ChipSpray()
		{
			FlyTo(Target.Center + new Vector2(Math.Sign(NPC.Center.X - Target.Center.X) * 260f, -170f), 8f, 20f);
			int volleys = Phase == 3 ? 5 : 4;
			if (Counter < volleys)
				CasinoFx.Spark(HandPosition((int)(Counter + 1) % 2 == 0 ? 1 : -1) + Main.rand.NextVector2Circular(18, 18), new Vector2(0, -1f), new Color(255, 220, 90), 0.8f);
			if (Timer >= 20 + Counter * 26 && Counter < volleys) {
				Counter++;
				int side = (int)Counter % 2 == 0 ? 1 : -1;
				Vector2 hand = HandPosition(side);
				SoundEngine.PlaySound(CasinoSounds.ChipStack with { Pitch = 0.2f }, hand);
				CasinoFx.Ring(hand, new Color(255, 210, 90), 8, 2.5f);
				Vector2 to = Target.Center - hand;
				for (int k = 0; k < (Phase == 1 ? 5 : 7); k++) {
					float t = 32f + k * 5f;   // flight time in ticks for each chip
					// Every second chip leads a moving target, so flying on in a straight line is not safe.
					Vector2 aim = k % 2 == 1 ? to + new Vector2(Target.velocity.X * t * 0.8f, 0) : to;
					Vector2 v = new(aim.X / t + Main.rand.NextFloat(-1f, 1f), aim.Y / t - 0.5f * 0.3f * t);
					Shoot(hand, v, ModContent.ProjectileType<BouncingChip>(), ChipDamage, k % 3);
				}
			}
			if (Timer > 20 + volleys * 26 + 40)
				SetState(State.Hover);
		}

		// ---- Place Your Bets: the whole hall becomes a red/black table, one colour erupts floor to ceiling.
		public const int BetTelegraph = 110, BetReveal = 45;

		private void PlaceBets()
		{
			FlyTo(Target.Center + new Vector2(0, -200f), 9f, 16f);
			int rounds = Phase == 3 ? 2 : 1;
			const int Cycle = BetTelegraph + 50;
			for (int round = 0; round < rounds; round++) {
				if (At(round * Cycle + 1)) {
					SoundEngine.PlaySound(CasinoSounds.RouletteSpin, NPC.Center);
					Say("Mods.CasinoWorld.Boss.PitBoss.Bets", new Color(255, 220, 120));
					if (Main.netMode != NetmodeID.MultiplayerClient) {
						Argument = Main.rand.Next(2);
						NPC.netUpdate = true;
						Vector2 corner = InHall
							? new Vector2(HallLeft(0), (CasinoInterior.Ceiling + 1) * 16)
							: new Vector2(HallLeft(Target.Center.X), FloorBelow(Target.Center) - BetZone.BoardHeight);
						Shoot(corner + new Vector2(BetZone.BoardWidth, BetZone.BoardHeight) / 2f, Vector2.Zero, ModContent.ProjectileType<BetZone>(), ZoneDamage,
							Argument, (int)(BetTelegraph / Speed));
					}
				}
				if (At(round * Cycle + BetTelegraph - BetReveal * Speed)) {
					// The ball lands: call the colour out loud.
					SoundEngine.PlaySound(CasinoSounds.RouletteBallDrop, NPC.Center);
					bool red = Argument == 0;
					if (!Main.dedServ)
						CombatText.NewText(NPC.Hitbox, red ? new Color(255, 80, 80) : new Color(200, 200, 220), Language.GetTextValue(red ? "Mods.CasinoWorld.Boss.PitBoss.Red" : "Mods.CasinoWorld.Boss.PitBoss.Black"), true);
					CasinoFx.Ring(NPC.Center + new Vector2(0, 58), red ? new Color(255, 70, 70) : new Color(150, 150, 180), 16, 5f, 1.2f);
				}
			}
			if (Timer > rounds * Cycle + 20)
				SetState(State.Hover);
		}

		// ---- Gold Rush: bars fall from the ceiling.
		private void GoldRush()
		{
			FlyTo(Target.Center + new Vector2(Math.Sign(NPC.Center.X - Target.Center.X) * 320f, -120f), 8f, 20f);
			int bars = Phase == 3 ? 18 : 12;
			if (Timer >= 10 + Counter * 6 && Counter < bars) {
				Counter++;
				// A bar lands about 65 ticks after it appears. Every third bar falls on the player, every third
				// where the player will be, the rest at random around them.
				float ahead = Target.Center.X + MathHelper.Clamp(Target.velocity.X, -10f, 10f) * 60f;
				float x = ((int)Counter % 3) switch {
					0 => Target.Center.X,
					1 => ahead + Main.rand.NextFloat(-60f, 60f),
					_ => (Target.Center.X + ahead) / 2f + Main.rand.NextFloat(-480f, 480f),
				};
				x = MathHelper.Clamp(x, HallLeft(Target.Center.X) + 20f, HallRight(Target.Center.X) - 20f);
				float ceiling = CeilingAbove(new Vector2(x, Target.Center.Y));
				Shoot(new Vector2(x, ceiling + 8), Vector2.Zero, ModContent.ProjectileType<GoldBarDrop>(), BarDamage, 45);
			}
			if (At(12))
				SoundEngine.PlaySound(SoundID.CoinPickup with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
			if (Timer > 10 + bars * 6 + 70)
				SetState(State.Hover);
		}

		// ---- Security: two Royal Guards join the fight.
		private void Security()
		{
			NPC.velocity *= 0.9f;
			if (At(20)) {
				SoundEngine.PlaySound(CasinoSounds.BossBell, NPC.Center);
				Say("Mods.CasinoWorld.Boss.PitBoss.Security", new Color(255, 200, 90));
				if (Main.netMode != NetmodeID.MultiplayerClient)
					for (int side = -1; side <= 1; side += 2) {
						if (CountGuards() >= 2)
							break;
						float x = MathHelper.Clamp(Target.Center.X + side * 420f, HallLeft(Target.Center.X) + 40f, HallRight(Target.Center.X) - 40f);
						Vector2 at = new(x, FloorBelow(new Vector2(x, Target.Center.Y)) - 30);
						CasinoFx.Ring(at, new Color(220, 220, 255), 20, 5f, 1.2f);
						int id = NPC.NewNPC(NPC.GetSource_FromAI(), (int)at.X, (int)at.Y, ModContent.NPCType<RoyalGuard>());
						if (id < Main.maxNPCs) {
							Main.npc[id].value = 0;
							Main.npc[id].SpawnedFromStatue = true;   // hired help: no loot
						}
					}
				CasinoFx.Ring(NPC.Center, new Color(200, 200, 230), 16, 5f);
			}
			if (Timer > 70)
				SetState(State.Hover);
		}

		// ---- Cash Out: charges along a marked lane. The lane is locked before the charge and the boss
		// always travels exactly from its start to its end.
		private const float LaneLead = 520f, LaneOvershoot = 900f;
		private Vector2 laneStart, laneEnd;

		private enum Dash { Align, Locked, Charge, Recover }

		private Dash DashStep => (Dash)(int)Argument;

		private void SetDash(Dash step)
		{
			Argument = (float)step;
			Timer = 0;
			NPC.netUpdate = true;
		}

		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.WriteVector2(laneStart);
			writer.WriteVector2(laneEnd);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			laneStart = reader.ReadVector2();
			laneEnd = reader.ReadVector2();
		}

		/// <summary>Puts the lane beside the target: from the boss's side, unless the wall leaves no run-up there.</summary>
		private void PlaceLane()
		{
			float left = HallLeft(Target.Center.X) + 60f, right = HallRight(Target.Center.X) - 60f;
			int side = NPC.Center.X < Target.Center.X ? -1 : 1;
			if ((side < 0 ? Target.Center.X - left : right - Target.Center.X) < 320f)
				side = -side;
			laneStart = ClampToRoom(Target.Center + new Vector2(side * LaneLead, 0));
			laneEnd = ClampToRoom(Target.Center - new Vector2(side * LaneOvershoot, 0));
		}

		private void CashOut()
		{
			int dashes = Phase == 1 ? 2 : 3;
			switch (DashStep) {
				case Dash.Align:
					PlaceLane();
					FlyTo(laneStart, 18f, 6f);
					if (Timer > 30) {
						SetDash(Dash.Locked);
						SoundEngine.PlaySound(CasinoSounds.BossBell with { Pitch = 0.4f }, NPC.Center);
					}
					break;
				case Dash.Locked:
					// Settle exactly on the start of the lane while it flashes.
					NPC.velocity = (laneStart - NPC.Center) * 0.25f;
					if (Timer > 34) {
						NPC.Center = laneStart;
						NPC.velocity = Vector2.Zero;
						SetDash(Dash.Charge);
						SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f }, NPC.Center);
					}
					break;
				case Dash.Charge:
					float left = Vector2.Distance(NPC.Center, laneEnd);
					float top = Phase == 3 ? 34f : 29f;
					float speed = Math.Min(Math.Min(top, 8f + Timer * 4f), Math.Min(left, 6f + left * 0.2f));
					NPC.velocity = (laneEnd - NPC.Center).SafeNormalize(Vector2.Zero) * speed;
					CasinoFx.Spark(NPC.Center + Main.rand.NextVector2Circular(40, 50), -NPC.velocity * 0.1f, new Color(255, 210, 90), 1f);
					if (Main.rand.NextBool(6))
						Dust.NewDustPerfect(NPC.Bottom, DustID.GoldCoin, new Vector2(0, -2), 0, default, 1.2f);
					if (left < 2f || Timer > 160) {
						SetDash(Dash.Recover);
						SoundEngine.PlaySound(CasinoSounds.ChipExplode with { Volume = 0.6f }, NPC.Center);
						CasinoFx.Ring(NPC.Center, new Color(255, 210, 90), 24, 7f, 1.3f);
						if (!Main.dedServ)
							for (int k = 0; k < 18; k++)   // the stack of notes he was holding
								Dust.NewDustPerfect(NPC.Center, CasinoFx.ShardType, Main.rand.NextVector2Circular(7, 5), 0, new Color(120, 200, 110), 1.1f);
					}
					break;
				case Dash.Recover:
					NPC.velocity *= 0.85f;
					if (Timer > 14) {
						if (++Counter >= dashes)
							SetState(State.Hover);
						else
							SetDash(Dash.Align);
					}
					break;
			}
			if (CurrentState == State.CashOut && DashStep <= Dash.Charge && laneEnd.X != laneStart.X)
				NPC.spriteDirection = NPC.direction = Math.Sign(laneEnd.X - laneStart.X);
		}

		/// <summary>Contact hurts while it hovers or charges, not while it repositions past the player.</summary>
		public override bool CanHitPlayer(Player target, ref int cooldownSlot) => CurrentState switch {
			State.Intro or State.Leave or State.Stunned or State.Dying => false,
			State.CashOut => DashStep == Dash.Charge,
			_ => true,
		};

		// ---- Wheel Barrage (phase 2+): rings of red and black chips with gaps. Against a distant player the
		// same chips close up into a fan, so running away does not thin the ring out.
		private void WheelBarrage()
		{
			if (NPC.Distance(Target.Center) > 380f)
				FlyTo(Target.Center + new Vector2(0, -120f), 7f, 20f);
			else
				NPC.velocity *= 0.92f;
			if (At(10))
				SoundEngine.PlaySound(CasinoSounds.RouletteSpin, NPC.Center);
			int waves = Phase == 3 ? 5 : 4;
			for (int wave = 0; wave < waves; wave++)
				if (At(30 + wave * 28 - 12)) {
					// The chips gather on the rim of the wheel before they fly.
					for (int k = 0; k < 22; k++) {
						Vector2 rim = (k * MathHelper.TwoPi / 22f).ToRotationVector2() * new Vector2(70, 26);
						CasinoFx.Spark(NPC.Center + new Vector2(0, 58) + rim, rim.SafeNormalize(Vector2.Zero) * -1.5f, k % 2 == 0 ? new Color(255, 80, 80) : new Color(190, 190, 230), 1.1f);
					}
					SoundEngine.PlaySound(CasinoSounds.RouletteBallBounce, NPC.Center);
				}
			if (Timer >= 30 + Counter * 28 && Counter < waves) {
				Counter++;
				const int Count = 22;
				Vector2 from = NPC.Center + new Vector2(0, 50);
				float dist = Vector2.Distance(from, Target.Center);
				float spacing = Math.Min(MathHelper.TwoPi / Count, 105f / Math.Max(dist, 1f));
				float aim = (Target.Center - from).ToRotation() + Main.rand.NextFloat(-0.5f, 0.5f) * spacing;
				// The gap is always near the player, never out of reach.
				int gap = Count / 2 + (Main.rand.NextBool() ? 1 : -2);
				for (int k = 0; k < Count; k++) {
					if (k == gap || k == gap + 1)
						continue;
					Vector2 v = (aim + (k - Count / 2) * spacing).ToRotationVector2() * 5.5f;
					Shoot(from, v, ModContent.ProjectileType<AllInChip>(), ChipDamage, k % 2);
				}
				SoundEngine.PlaySound(CasinoSounds.ChipStack, NPC.Center);
			}
			if (Timer > 30 + waves * 28 + 40)
				SetState(State.Hover);
		}

		public bool WheelSpinning => CurrentState is State.WheelBarrage or State.PlaceBets;

		// ---- Double or Nothing (phase 2+): a giant coin slams down on the player.
		private void DoubleOrNothing()
		{
			FlyTo(Target.Center + new Vector2(Math.Sign(NPC.Center.X - Target.Center.X) * 360f, -160f), 8f, 20f);
			if (At(10)) {
				SoundEngine.PlaySound(SoundID.Coins with { Pitch = -0.4f, Volume = 1.3f }, NPC.Center);
				Say("Mods.CasinoWorld.Boss.PitBoss.Double", new Color(255, 220, 120));
				bool tails = Main.rand.NextBool(2);
				Shoot(GiantCoin.HoverSpot(Target), Vector2.Zero, ModContent.ProjectileType<GiantCoin>(), CoinDamage, NPC.whoAmI, tails ? 1 : 0, Target.whoAmI);
			}
			if (Timer > 150)
				SetState(State.Hover);
		}

		/// <summary>Called by the giant coin when it lands tails: the boss is stunned and vulnerable.</summary>
		public void CoinTails()
		{
			if (CurrentState is State.Leave or State.Intro or State.Dying)
				return;
			SoundEngine.PlaySound(CasinoSounds.BossBust, NPC.Center);
			CasinoFx.Ring(NPC.Center, new Color(255, 230, 120), 18, 5f, 1.3f);
			if (!Main.dedServ)
				CombatText.NewText(NPC.Hitbox, new Color(255, 230, 120), Language.GetTextValue("Mods.CasinoWorld.Boss.PitBoss.Tails"), true);
			SetState(State.Stunned);
		}

		private void Stunned()
		{
			NPC.velocity *= 0.9f;
			NPC.velocity.Y += 0.05f;
			if (Main.rand.NextBool(3))
				Dust.NewDustPerfect(NPC.Top + new Vector2(Main.rand.NextFloat(-30, 30), 0), DustID.GoldFlame, new Vector2(0, -1), 0, default, 1.2f).noGravity = true;
			if (Timer > 120)
				SetState(State.Hover);
		}

		public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
		{
			if (CurrentState == State.Stunned)
				modifiers.FinalDamage *= 1.25f;
		}

		private void Leave()
		{
			NPC.velocity.Y -= 0.4f;
			NPC.alpha = Math.Min(255, NPC.alpha + 4);
			if (Timer > 120)
				NPC.active = false;
		}

		// ---- Death: the wheel spins out of control and cracks, the vault bursts open, then he is gone.
		private const int DyingTicks = 190;

		public override bool CheckDead()
		{
			if (CurrentState == State.Dying && (Timer >= DyingTicks || Main.netMode == NetmodeID.MultiplayerClient))
				return true;
			NPC.life = 1;
			NPC.dontTakeDamage = true;
			if (CurrentState != State.Dying) {
				SetState(State.Dying);
				foreach (Projectile p in Main.ActiveProjectiles)   // the table is closed
					if (p.hostile && p.ModProjectile is WildCardProjectile)
						p.Kill();
			}
			return false;
		}

		private void Dying()
		{
			float t = Math.Min(1f, Timer / DyingTicks);
			NPC.velocity = NPC.velocity * 0.9f + Main.rand.NextVector2Circular(1f, 1f) * t;
			if (At(1)) {
				SoundEngine.PlaySound(CasinoSounds.BossBust, NPC.Center);
				SoundEngine.PlaySound(CasinoSounds.RouletteSpin with { Pitch = 0.3f }, NPC.Center);
			}
			for (int k = 1; k <= 7; k++)
				if (At(20 + k * 20)) {   // faster and bigger blasts around him
					Vector2 at = NPC.Center + Main.rand.NextVector2Circular(60, 70);
					CasinoFx.Explosion(at, 60 + k * 10, new Color(255, 210, 90), new Color(255, 90, 80), 1f + k * 0.6f);
					SoundEngine.PlaySound(CasinoSounds.ChipExplode with { Pitch = k * 0.08f }, at);
				}
			if (!Main.dedServ)
				for (int k = 0; k < 1 + (int)(t * 6f); k++)
					Dust.NewDustPerfect(NPC.Center + new Vector2(Main.rand.NextFloat(-60, 60), 50), DustID.GoldCoin, new Vector2(Main.rand.NextFloat(-4, 4), Main.rand.NextFloat(-12, -4)) * (0.4f + t), 0, default, 1.3f);
			if (At(150)) {
				SoundEngine.PlaySound(CasinoSounds.VaultDoor, NPC.Center);
				CasinoFx.Ring(NPC.Center, Color.White, 40, 14f, 1.8f);
				BossTitle.Flash(new Color(255, 225, 130));
			}
			if (Timer >= DyingTicks && Main.netMode != NetmodeID.MultiplayerClient)
				NPC.StrikeInstantKill();
		}

		public override bool CheckActive() => false;

		public override void OnKill()
		{
			NPC.SetEventFlagCleared(ref CasinoDowned.DownedPitBoss, -1);
			// Security goes home with the boss.
			foreach (NPC npc in Main.ActiveNPCs)
				if (npc.type == ModContent.NPCType<RoyalGuard>() && npc.SpawnedFromStatue) {
					npc.active = false;
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);
				}
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		public override void BossLoot(ref int potionType) => potionType = ItemID.GreaterHealingPotion;

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<PitBossBag>()));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<PitBossTrophy>(), 10));
			npcLoot.Add(ItemDropRule.MasterModeCommonDrop(ModContent.ItemType<PitBossRelicItem>()));
			var normal = new LeadingConditionRule(new Conditions.NotExpert());
			normal.OnSuccess(ItemDropRule.OneFromOptions(1, ModContent.ItemType<CroupiersRake>(), ModContent.ItemType<BankBreaker>(), ModContent.ItemType<VaultBuster>()));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 1, 150, 220));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<VaultBullion>(), 1, 10, 14));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<HighRollerCrate>(), 1, 2, 3));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<PitBossMask>(), 7));
			npcLoot.Add(normal);
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			if (Main.dedServ)
				return;
			for (int k = 0; k < 3; k++)
				Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldCoin, hit.HitDirection * 2f, -2f);
			if (NPC.life <= 0 && CurrentState == State.Dying) {   // not on the hit that starts the death scene
				global::CasinoWorld.Common.Effects.CasinoFx.Shake(NPC.Center, Main.rand.NextVector2Unit(), 14f, 6f, 40, 1500f, "PitBoss");
				CasinoFx.Explosion(NPC.Center, 260, new Color(255, 210, 90), new Color(255, 80, 80), 0f);
				CasinoFx.Ring(NPC.Center, Color.White, 36, 12f, 1.7f);
				SoundEngine.PlaySound(CasinoSounds.Jackpot, NPC.Center);
				for (int k = 0; k < 80; k++)
					Dust.NewDustPerfect(NPC.Center, DustID.GoldCoin, Main.rand.NextVector2Circular(12, 12), 0, default, 1.4f);
			}
		}

		// ------------------------------------------------------------------ drawing

		private static Asset<Texture2D> handTex, wheelTex;

		public override void Unload() => handTex = wheelTex = null;

		private int lastPhase = 1;
		private int transitionFlash;

		public override void FindFrame(int frameHeight)
		{
			bool laugh = CurrentState is State.Intro or State.Security or State.DoubleOrNothing || transitionFlash > 0;
			int pose = CurrentState switch {
				State.Stunned or State.Dying => 4,
				State.CashOut => DashStep is Dash.Locked or Dash.Charge ? 3 : 0,
				State.ChipSpray or State.WheelBarrage or State.GoldRush => 2,
				_ => laugh ? 1 : 0,
			};
			NPC.frame.Y = pose * frameHeight;
			if (Phase != lastPhase && !NPC.IsABestiaryIconDummy) {
				lastPhase = Phase;
				transitionFlash = 60;
				if (!Main.dedServ) {
					SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f }, NPC.Center);
					SoundEngine.PlaySound(CasinoSounds.BossBell, NPC.Center);
					CasinoFx.Ring(NPC.Center, Phase == 3 ? new Color(255, 70, 60) : new Color(255, 210, 90), 36, 11f, 1.8f);
					BossTitle.Flash(Phase == 3 ? new Color(255, 70, 60) : new Color(255, 210, 90));
					global::CasinoWorld.Common.Effects.CasinoFx.Shake(NPC.Center, Vector2.UnitY, 8f, 8f, 30, 1500f, "PitBossPhase");
					CombatText.NewText(NPC.Hitbox, new Color(255, 210, 90), Language.GetTextValue(Phase == 3 ? "Mods.CasinoWorld.Boss.PitBoss.Phase3" : "Mods.CasinoWorld.Boss.PitBoss.Phase2"), true);
				}
			}
			if (transitionFlash > 0)
				transitionFlash--;
		}

		private static void Sleeve(SpriteBatch sb, Vector2 a, Vector2 b, Color c, float width)
		{
			PixelFx.Line(sb, a, b, c, width);
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			handTex ??= ModContent.Request<Texture2D>("CasinoWorld/Content/NPCs/Boss/PitBoss_Hand");
			wheelTex ??= ModContent.Request<Texture2D>("CasinoWorld/Content/NPCs/Boss/PitBoss_Wheel");
			Texture2D body = Terraria.GameContent.TextureAssets.Npc[Type].Value, hand = handTex.Value, wheel = wheelTex.Value;
			float fade = 1f - NPC.alpha / 255f;
			Color light = (NPC.IsABestiaryIconDummy ? Color.White : Color.Lerp(drawColor, Color.White, 0.45f)) * fade;
			if (Phase == 3 && !NPC.IsABestiaryIconDummy)
				light = light.MultiplyRGB(new Color(255, 200, 190));
			float t = Main.GlobalTimeWrappedHourly;
			float bob = (float)Math.Sin(t * 2.2f) * 4f;
			bool dying = CurrentState == State.Dying;
			bool stunned = CurrentState == State.Stunned || dying;
			float tilt = stunned ? (float)Math.Sin(t * 6f) * 0.15f : NPC.velocity.X * 0.015f;
			Vector2 center = NPC.Center - screenPos + new Vector2(0, bob);

			// Telegraph for Cash Out: the lane the charge will cover, as high as the boss's hitbox. Faint while it
			// follows the player, bright with arrows once locked.
			if (!NPC.IsABestiaryIconDummy && CurrentState == State.CashOut && DashStep <= Dash.Charge && laneStart.X != laneEnd.X) {
				int x0 = (int)(Math.Min(laneStart.X, laneEnd.X) - NPC.width / 2f - screenPos.X), x1 = (int)(Math.Max(laneStart.X, laneEnd.X) + NPC.width / 2f - screenPos.X);
				PixelFx.ChargeLane(spriteBatch, x0, x1, (int)(laneStart.Y - screenPos.Y), Math.Sign(laneEnd.X - laneStart.X), DashStep != Dash.Align, DashStep == Dash.Charge ? 0.55f : 0.9f, t);
			}
			// Afterimages while charging.
			if (CurrentState == State.CashOut && NPC.velocity.Length() > 8f) {
				for (int k = NPC.oldPos.Length - 1; k > 0; k--) {
					Vector2 old = NPC.oldPos[k] + NPC.Size / 2f - screenPos;
					Color ghost = new Color(255, 200, 80) * (0.3f * (1f - k / (float)NPC.oldPos.Length));
					ghost.A = 0;
					spriteBatch.Draw(body, old, NPC.frame, ghost, tilt, NPC.frame.Size() / 2f, 1f, NPC.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
				}
			}

			// Roulette wheel platform (4 rotation frames), spinning faster during wheel attacks.
			int wf = (int)(t * (dying ? 40 : WheelSpinning ? 24 : 8)) % 4;
			var wFrame = new Rectangle(0, wf * wheel.Height / 4, wheel.Width, wheel.Height / 4);
			if (WheelSpinning) {
				Color glow = new Color(255, 80, 80) * 0.35f;
				glow.A = 0;
				spriteBatch.Draw(wheel, center + new Vector2(0, 58), wFrame, glow, 0f, wFrame.Size() / 2f, 1.15f, SpriteEffects.None, 0f);
			}
			spriteBatch.Draw(wheel, center + new Vector2(0, 58), wFrame, light, 0f, wFrame.Size() / 2f, 1f, SpriteEffects.None, 0f);

			// Body (drawn after the sleeves start so the shoulders cover them).
			SpriteEffects flip = NPC.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			spriteBatch.Draw(body, center, NPC.frame, light, tilt, NPC.frame.Size() / 2f, 1f, flip, 0f);

			// Hands: fists while attacking, open otherwise; they rise for chip sprays and bets.
			for (int side = -1; side <= 1; side += 2) {
				bool fist = CurrentState is State.ChipSpray or State.CashOut or State.WheelBarrage;
				float lift = CurrentState switch {
					State.ChipSpray => (int)Counter % 2 == (side > 0 ? 0 : 1) ? -30f : 0f,
					State.PlaceBets or State.DoubleOrNothing or State.Intro => -40f,
					_ => 0f,
				};
				float reach = CurrentState == State.CashOut && DashStep is (Dash.Locked or Dash.Charge) ? (side == NPC.spriteDirection ? 18f : -10f) * side : 0f;
				if (stunned)
					lift = (float)Math.Sin(t * 8 + side) * 12f;
				Vector2 at = center + new Vector2(side * 62f + reach, 8f + lift + (float)Math.Sin(t * 3f + side) * 4f);
				// Pinstripe sleeve from the shoulder to the cuff.
				Vector2 shoulder = center + new Vector2(side * 30f, 6f);
				Vector2 elbow = (shoulder + at) / 2f + new Vector2(side * 8f, 10f);
				Color dark = new Color(24, 26, 46).MultiplyRGB(light), cloth = new Color(52, 58, 96).MultiplyRGB(light), hi = new Color(84, 92, 140).MultiplyRGB(light);
				Sleeve(spriteBatch, shoulder, elbow, dark, 20f);
				Sleeve(spriteBatch, elbow, at, dark, 17f);
				Sleeve(spriteBatch, shoulder, elbow, cloth, 15f);
				Sleeve(spriteBatch, elbow, at, cloth, 12f);
				Sleeve(spriteBatch, shoulder + new Vector2(0, -4), elbow + new Vector2(0, -4), hi, 3f);
				var hFrame = new Rectangle(fist ? hand.Width / 2 : 0, 0, hand.Width / 2, hand.Height);
				spriteBatch.Draw(hand, at, hFrame, light, side * 0.2f, hFrame.Size() / 2f, 1.3f, side < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
			}

			// Stunned: a ring of chips circles the hat.
			if (stunned) {
				Texture2D chip = ModContent.Request<Texture2D>("CasinoWorld/Content/Projectiles/HighRollerChip").Value;
				for (int k = 0; k < 6; k++) {
					float a = t * 5f + k * MathHelper.TwoPi / 6f;
					sbChip(new Vector2((float)Math.Cos(a) * 46f, (float)Math.Sin(a) * 12f - 70f), 0.9f + 0.2f * (float)Math.Sin(a));
				}
				void sbChip(Vector2 at, float scale) => spriteBatch.Draw(chip, center + at, null, Color.White, t * 4f, chip.Size() / 2f, scale, SpriteEffects.None, 0f);
			}

			if (dying) {
				float d = Math.Min(1f, Timer / DyingTicks);
				// Vault light breaks out: a turning sunburst that grows in steps, over a hot core.
				PixelFx.Burst(spriteBatch, center, Color.Lerp(new Color(255, 140, 60), new Color(255, 240, 180), d) * (0.4f + 0.6f * d), t, 2 + (int)(d * 4f));
				PixelFx.Glow(spriteBatch, center, 48f + d * 62f, new Color(255, 220, 130) * (0.3f + 0.6f * d));
			}

			// Phase aura.
			if (!NPC.IsABestiaryIconDummy && (Phase >= 2 || transitionFlash > 0)) {
				Color aura = (Phase == 3 ? new Color(255, 70, 60) : new Color(255, 200, 80)) * (0.15f + transitionFlash / 150f);
				aura.A = 0;
				spriteBatch.Draw(body, center, NPC.frame, aura, tilt, NPC.frame.Size() / 2f, 1.12f, flip, 0f);
			}
			return false;
		}
	}
}
