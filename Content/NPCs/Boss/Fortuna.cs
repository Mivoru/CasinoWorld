using System;
using System.Collections.Generic;
using System.IO;
using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.Crates;
using CasinoWorld.Content.Items.Fortuna;
using CasinoWorld.Content.Projectiles.Boss;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.NPCs.Boss
{
	/// <summary>
	/// Fortuna, the Last Wheel (design/CasinoWorld/endgame.md): the boss after the Moon Lord. A porcelain mask
	/// inside three turning casino rings. The wheel is drawn before every attack and shows what comes next.
	/// AI runs on the server; ai[0] state, ai[1] timer, ai[2] counter, ai[3] argument (synced by vanilla).
	/// </summary>
	[AutoloadBossHead]
	public class Fortuna : ModNPC
	{
		public enum State { Intro, Draw, RingToss, FateDice, ThreeReels, Gaze, CardCurtain, PotOfGold, RunawayBall, LastBet, HouseWins, Stunned, Awaken, Leave, Dying }

		public const int BaseLife = 320000;
		// Damage taken in normal mode before defense (see Damage).
		private const int ChipDamage = 96, DieDamage = 120, BeamDamage = 130, ReelDamage = 125, BallDamage = 125;

		public const float PhaseTwo = 0.66f, PhaseThree = 0.33f, BetLife = 0.08f;
		private const int DrawTicks = 58, BetTicks = 300, AwakenTicks = 150, DyingTicks = 230, StunTicks = 240;
		private const float BetBase = 0.10f, BetPerPercent = 0.12f, BetCap = 0.90f;

		private State CurrentState { get => (State)NPC.ai[0]; set => NPC.ai[0] = (float)value; }
		private ref float Timer => ref NPC.ai[1];
		private ref float Counter => ref NPC.ai[2];
		private ref float Argument => ref NPC.ai[3];

		private float LifeRatio => NPC.life / (float)NPC.lifeMax;
		public int Phase => LifeRatio > PhaseTwo ? 1 : LifeRatio > PhaseThree ? 2 : 3;
		private float Speed => Phase == 3 ? 1.2f : 1f;

		/// <summary>The blindfold is torn off (phase 3). Stays so even if she somehow heals.</summary>
		public bool Awake { get; private set; }
		private bool betMade;
		private int betStartLife;
		private State lastAttack;
		private readonly List<State> deck = new();
		private int deckPhase;
		private float previousTimer;

		/// <summary>True on the one tick in which the timer passes a mark (the timer steps by 1.2 in phase 3).</summary>
		private bool At(float mark) => previousTimer < mark && Timer >= mark;

		public override string Texture => "CasinoWorld/Content/NPCs/Boss/Fortuna_Core";
		public override string BossHeadTexture => "CasinoWorld/Content/NPCs/Boss/Fortuna_Head_Boss";

		public static readonly Color GoldLight = new(255, 220, 110), FateViolet = new(190, 120, 255), FateRed = new(255, 70, 90);

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 4;   // calm, speaking, stunned, open eye
			NPCID.Sets.MPAllowedEnemies[Type] = true;
			NPCID.Sets.BossBestiaryPriority.Add(Type);
			NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
			NPCID.Sets.NPCBestiaryDrawOffset[Type] = new NPCID.Sets.NPCBestiaryDrawModifiers { Scale = 0.5f, PortraitScale = 0.65f };
		}

		public override void SetDefaults()
		{
			NPC.width = 92;
			NPC.height = 104;
			NPC.damage = 110;
			NPC.defense = 50;
			NPC.lifeMax = BaseLife;
			NPC.HitSound = SoundID.NPCHit4 with { Pitch = 0.4f };
			NPC.DeathSound = null;
			NPC.knockBackResist = 0f;
			NPC.noGravity = true;
			NPC.noTileCollide = true;
			NPC.boss = true;
			NPC.npcSlots = 20f;
			NPC.value = Item.buyPrice(platinum: 1);
			NPC.aiStyle = -1;
			if (!Main.dedServ)
				Music = CasinoMusic.Fortuna;
			SceneEffectPriority = SceneEffectPriority.BossHigh;
		}

		public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
		{
			NPC.lifeMax = (int)(NPC.lifeMax * 0.7f * balance * bossAdjustment);
			NPC.damage = (int)(NPC.damage * 0.75f);
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Sky,
				new FlavorTextBestiaryInfoElement("Mods.CasinoWorld.Bestiary.Fortuna"),
			});
		}

		// ------------------------------------------------------------------ helpers

		private Player Target => Main.player[NPC.target];

		private void SetState(State state, float argument = 0f)
		{
			CurrentState = state;
			Timer = 0;
			previousTimer = 0;
			Counter = 0;
			Argument = argument;
			NPC.netUpdate = true;
		}

		private static void Say(string key, Color color)
		{
			key = "Mods.CasinoWorld.Boss.Fortuna." + key;
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

		private void FlyTo(Vector2 goal, float speed, float inertia)
		{
			Vector2 desired = goal - NPC.Center;
			float dist = desired.Length();
			speed += MathHelper.Clamp((dist - 450f) / 30f, 0f, 26f);
			if (dist > 1f)
				desired = desired / dist * Math.Min(speed, dist * 0.12f + 1f);
			NPC.velocity = (NPC.velocity * (inertia - 1) + desired) / inertia;
		}

		private void HoverBeside(float x, float y, float speed = 14f)
			=> FlyTo(Target.Center + new Vector2(Math.Sign(NPC.Center.X - Target.Center.X) * x, y), speed, 18f);

		private static void Shake(Vector2 at, float strength, int ticks, string id) => CasinoFx.Shake(at, Main.rand.NextVector2Unit(), strength, 7f, ticks, 2400f, id);

		// ------------------------------------------------------------------ AI

		public override void AI()
		{
			if (NPC.target < 0 || NPC.target == 255 || Target.dead || !Target.active)
				NPC.TargetClosest();
			bool leaving = Target.dead || !Target.active || NPC.Distance(Target.Center) > 7000f;
			if (leaving && CurrentState is not (State.Leave or State.Dying)) {
				Say("Leave", GoldLight);
				SetState(State.Leave);
			}
			if (!Main.dedServ && Awake)
				Music = CasinoMusic.FortunaTwo;
			NPC.dontTakeDamage = CurrentState is State.Intro or State.Awaken or State.Leave or State.Dying;
			Lighting.AddLight(NPC.Center, Awake ? new Vector3(1.4f, 0.5f, 0.5f) : new Vector3(1.3f, 1.05f, 0.6f));
			NPC.direction = NPC.spriteDirection = 1;

			previousTimer = Timer;
			Timer += CurrentState is State.LastBet or State.Dying or State.Awaken or State.Intro ? 1f : Speed;
			if (Timer > 1000 && CurrentState is not (State.Leave or State.Dying))   // safety net: nothing takes this long
				SetState(State.Draw);

			// Phase 3 and the Last Bet interrupt whatever is going on, but not each other.
			if (Main.netMode != NetmodeID.MultiplayerClient && CurrentState is not (State.Intro or State.Leave or State.Dying or State.Awaken or State.LastBet or State.Stunned or State.HouseWins)) {
				if (Phase == 3 && !Awake)
					SetState(State.Awaken);
				else if (Awake && !betMade && LifeRatio <= BetLife)
					SetState(State.LastBet);
			}

			switch (CurrentState) {
				case State.Intro: Intro(); break;
				case State.Draw: Draw(); break;
				case State.RingToss: RingToss(); break;
				case State.FateDice: FateDice(); break;
				case State.ThreeReels: ThreeReels(); break;
				case State.Gaze: Gaze(); break;
				case State.CardCurtain: CardCurtain(); break;
				case State.PotOfGold: PotOfGold(); break;
				case State.RunawayBall: RunawayBall(); break;
				case State.LastBet: LastBet(); break;
				case State.HouseWins: HouseWins(); break;
				case State.Stunned: Stunned(); break;
				case State.Awaken: Awaken(); break;
				case State.Leave: Leave(); break;
				case State.Dying: Dying(); break;
			}
			if (!Main.dedServ)
				UpdateVisuals();
		}

		private void Intro()
		{
			NPC.velocity *= 0.94f;
			NPC.alpha = (int)MathHelper.Clamp(255 - Timer * 4f, 0, 255);
			if (At(1)) {
				SoundEngine.PlaySound(CasinoSounds.FortunaCharge, NPC.Center);
				if (Main.netMode != NetmodeID.MultiplayerClient) {
					NPC.Center = Target.Center + new Vector2(0, -320f);
					NPC.netUpdate = true;
				}
			}
			if (At(70)) {
				Say("Intro", GoldLight);
				SoundEngine.PlaySound(CasinoSounds.FortunaVoice, NPC.Center);
				if (!Main.dedServ) {
					BossTitle.Show("Mods.CasinoWorld.Boss.Fortuna.Title", "Mods.CasinoWorld.Boss.Fortuna.Subtitle", GoldLight);
					BossTitle.Flash(GoldLight);
				}
				CasinoFx.Ring(NPC.Center, GoldLight, 48, 14f, 1.8f);
				Shake(NPC.Center, 9f, 30, "FortunaIntro");
			}
			if (Timer > 130)
				SetState(State.Draw);
		}

		/// <summary>The rings line up, the icons roll and stop on the next attack.</summary>
		private void Draw()
		{
			HoverBeside(300f, -220f);
			if (At(2)) {
				SoundEngine.PlaySound(CasinoSounds.FortunaSpin with { Volume = 0.75f, Pitch = Awake ? 0.25f : 0f }, NPC.Center);
				if (Main.netMode != NetmodeID.MultiplayerClient) {
					Argument = (float)NextAttack();
					NPC.netUpdate = true;
				}
			}
			if (At(DrawTicks - 20)) {
				SoundEngine.PlaySound(CasinoSounds.FortunaReel with { Volume = 0.6f }, NPC.Center);
				CasinoFx.Ring(NPC.Center + new Vector2(0, -150), GoldLight, 14, 3.5f);
			}
			if (Timer > DrawTicks && Main.netMode != NetmodeID.MultiplayerClient && Argument != 0f)
				SetState((State)(int)Argument);
		}

		private State NextAttack()
		{
			// A shuffled deck: each attack once, then it is shuffled again; never the same one twice in a row.
			int phase = Math.Min(Phase, 2);
			if (deckPhase != phase) {
				deckPhase = phase;
				deck.Clear();
			}
			if (deck.Count == 0) {
				deck.AddRange(new[] { State.RingToss, State.FateDice, State.ThreeReels, State.Gaze, State.CardCurtain });
				if (phase == 2)
					deck.AddRange(new[] { State.PotOfGold, State.RunawayBall });
				for (int k = deck.Count - 1; k > 0; k--) {
					int swap = Main.rand.Next(k + 1);
					(deck[k], deck[swap]) = (deck[swap], deck[k]);
				}
			}
			int pick = Math.Max(0, deck.FindIndex(a => a != lastAttack));
			lastAttack = deck[pick];
			deck.RemoveAt(pick);
			return lastAttack;
		}

		// ---- Ring Toss: the outer ring flies apart, turns, and comes home. Two rings turn against each other.
		private void RingToss()
		{
			if (Timer < 18)
				HoverBeside(260f, -120f);
			else
				NPC.velocity *= 0.86f;
			int rings = Phase == 1 ? 2 : 3;
			if (Timer >= 24 + Counter * 46 && Counter < rings) {
				int spin = (int)Counter % 2 == 0 ? 1 : -1;
				Counter++;
				const int Count = 26;
				float offset = Main.rand.NextFloat(MathHelper.TwoPi);
				SoundEngine.PlaySound(CasinoSounds.OphanimSpin with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
				CasinoFx.Ring(NPC.Center, GoldLight, 26, 8f, 1.2f);
				if (Main.netMode != NetmodeID.MultiplayerClient)
					for (int k = 0; k < Count; k++)
						Shoot(NPC.Center, Vector2.Zero, ModContent.ProjectileType<FatePocket>(), ChipDamage, offset + k * MathHelper.TwoPi / Count, spin, k % 13 == 0 ? 2 : k % 2);
			}
			if (Timer > 24 + rings * 46 + 110)
				SetState(State.Draw);
		}

		/// <summary>True while the pockets of the outer ring are out flying.</summary>
		private bool RingAway => CurrentState == State.RingToss && Timer > 24 && Timer < 24 + (Phase == 1 ? 2 : 3) * 46 + 90;

		// ---- Fate Dice: star dice fall on marked spots; the number is how many pairs of chips fly out.
		private void FateDice()
		{
			HoverBeside(380f, -180f);
			int dice = Phase == 1 ? 4 : 6;
			if (Timer >= 14 + Counter * 30 && Counter < dice) {
				Counter++;
				DropDie(Counter % 2 == 0 ? 26f : 4f);
			}
			if (Timer > 14 + dice * 30 + 70)
				SetState(State.Draw);
		}

		private void DropDie(float lead)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			Vector2 spot = Target.Center + Target.velocity * lead;
			Shoot(spot + new Vector2(0, -FateDie.DropHeight), Vector2.Zero, ModContent.ProjectileType<FateDie>(), DieDamage, spot.Y, Main.rand.Next(1, 7));
		}

		// ---- Three Reels: columns stop one after another; the clover columns are safe.
		private void ThreeReels()
		{
			HoverBeside(120f, -330f);
			int rounds = Phase == 3 ? 2 : 1;
			if (Timer >= 8 + Counter * 150 && Counter < rounds) {
				Counter++;
				if (Main.netMode != NetmodeID.MultiplayerClient)
					Shoot(Target.Center, Vector2.Zero, ModContent.ProjectileType<FateReels>(), ReelDamage, Main.rand.Next(3), Main.rand.Next(3), Main.rand.Next(3));
			}
			if (Timer > 8 + rounds * 150 + 20)
				SetState(State.Draw);
		}

		// ---- Gaze: beams sweep around; the way they turn is shown before they fire.
		private void Gaze()
		{
			if (Timer < 26)
				HoverBeside(380f, 0f, 18f);
			else
				NPC.velocity *= 0.8f;
			if (At(28)) {
				SoundEngine.PlaySound(CasinoSounds.FortunaCharge, NPC.Center);
				int dir = Main.rand.NextBool() ? 1 : -1;
				float start = (Target.Center - NPC.Center).ToRotation() - dir * 0.6f;
				int beams = Phase;
				for (int k = 0; k < beams; k++)
					Shoot(NPC.Center, Vector2.Zero, ModContent.ProjectileType<FateBeam>(), BeamDamage, NPC.whoAmI, start + k * MathHelper.TwoPi / beams, dir * (Phase == 1 ? 0.0165f : 0.0145f));
			}
			if (Phase == 3 && At(110))
				DropDie(10f);
			if (Timer > 28 + (FateBeam.Warn + FateBeam.Fire) * Speed + 15)
				SetState(State.Draw);
		}

		// ---- Card Curtain: walls of cards with a moving gap.
		private void CardCurtain()
		{
			HoverBeside(60f, -340f);
			int waves = Phase == 1 ? 4 : 6;
			if (Timer >= 16 + Counter * 44 && Counter < waves) {
				Counter++;
				if (Main.netMode != NetmodeID.MultiplayerClient) {
					if (Counter == 1)
						Argument = 0f;
					Argument = MathHelper.Clamp(Argument + Main.rand.NextFloat(-190f, 190f), -230f, 230f);
					NPC.netUpdate = true;
					int side = (int)Counter % 2 == 0 ? 1 : -1;
					const float Spacing = 50f;
					float gap = Phase == 3 ? 92f : 104f;
					for (int k = -13; k <= 13; k++) {
						if (Math.Abs(k * Spacing - Argument) < gap)
							continue;
						Shoot(Target.Center + new Vector2(side * 950f, k * Spacing), new Vector2(-side * 10f, 0), ModContent.ProjectileType<CurtainCard>(), ChipDamage, (k + 13) % 4);
					}
				}
				SoundEngine.PlaySound(CasinoSounds.CardBridge with { Pitch = -0.2f, Volume = 1f }, Target.Center);
			}
			if (Phase == 3 && At(16 + 3 * 44))
				DropDie(0f);
			if (Timer > 16 + waves * 44 + 60)
				SetState(State.Draw);
		}

		// ---- Pot of Gold (phase 2+): a hole drags everyone in, swallows chips and then pays them out.
		private void PotOfGold()
		{
			if (Timer < 22)
				HoverBeside(240f, -60f, 18f);
			else
				NPC.velocity *= 0.8f;
			if (At(24)) {
				SoundEngine.PlaySound(CasinoSounds.FortunaHole, NPC.Center);
				Shoot(NPC.Center + new Vector2(0, 10), Vector2.Zero, ModContent.ProjectileType<PotHole>(), DieDamage, Phase);
			}
			if (Timer > 24 + (PotHole.PullTicks + 70) * Speed)
				SetState(State.Draw);
		}

		// ---- Runaway Ball (phase 2+): steel balls bounce around a neon frame; the path is drawn ahead.
		private void RunawayBall()
		{
			HoverBeside(100f, -360f);
			int balls = Phase == 3 ? 2 : 1;
			if (Timer >= 12 + Counter * 60 && Counter < balls) {
				Counter++;
				if (Main.netMode != NetmodeID.MultiplayerClient) {
					float angle = Main.rand.NextFromList(0.6f, 0.75f, 0.5f) + Main.rand.Next(4) * MathHelper.PiOver2;
					Vector2 box = Target.Center;
					Shoot(NPC.Center, angle.ToRotationVector2() * 17f, ModContent.ProjectileType<FateBall>(), BallDamage, box.X, box.Y);
				}
			}
			if (Phase == 2 && At(150))
				DropDie(12f);
			if (Timer > 12 + 270 * Speed)
				SetState(State.Draw);
		}

		// ---- Last Bet (once, at 8 %): hurt her while the wheel turns to raise the odds of a jackpot.
		private float BetOdds => MathHelper.Clamp(BetBase + BetPerPercent * 100f * (betStartLife - NPC.life) / NPC.lifeMax, BetBase, BetCap);

		private void LastBet()
		{
			if (NPC.Distance(Target.Center) > 520f)
				HoverBeside(200f, -240f);
			else
				NPC.velocity *= 0.9f;
			if (At(1)) {
				betStartLife = NPC.life;
				betMade = true;
				Say("LastBet", GoldLight);
				SoundEngine.PlaySound(CasinoSounds.FortunaVoice, NPC.Center);
				SoundEngine.PlaySound(CasinoSounds.HeartBeat);
				ClearShots();
				if (!Main.dedServ)
					BossTitle.Flash(GoldLight);
			}
			for (int beat = 60; beat < BetTicks; beat += beat < 200 ? 60 : 30)
				if (At(beat))
					SoundEngine.PlaySound(CasinoSounds.HeartBeat with { Pitch = beat / 600f });
			if (At(BetTicks - 120))
				SoundEngine.PlaySound(CasinoSounds.FortunaSpin, NPC.Center);
			if (Timer > BetTicks && Main.netMode != NetmodeID.MultiplayerClient) {
				if (Main.rand.NextFloat() < BetOdds) {
					Say("Jackpot", GoldLight);
					SetState(State.Stunned);
				}
				else {
					Say("HouseWins", FateRed);
					SetState(State.HouseWins);
				}
			}
		}

		private void HouseWins()
		{
			FlyTo(Target.Center + new Vector2(0, -300f), 10f, 24f);
			if (At(1)) {
				SoundEngine.PlaySound(CasinoSounds.HouseLaugh with { Pitch = 0.4f }, NPC.Center);
				SoundEngine.PlaySound(CasinoSounds.BossBust, NPC.Center);
			}
			if (Timer > 40 && Timer >= 40 + Counter * 13 && Timer < 400) {
				Counter++;
				const int Arms = 9;
				for (int k = 0; k < Arms; k++)
					Shoot(NPC.Center, (Counter * 0.21f + k * MathHelper.TwoPi / Arms).ToRotationVector2() * 6.2f, ModContent.ProjectileType<FateChip>(), ChipDamage);
				if (Counter % 5 == 0)
					DropDie(14f);
				SoundEngine.PlaySound(CasinoSounds.ChipStack with { Volume = 0.6f }, NPC.Center);
			}
			if (Timer > 430)
				SetState(State.Draw);
		}

		private void Stunned()
		{
			NPC.velocity *= 0.9f;
			NPC.velocity.Y += 0.04f;
			if (At(1)) {
				SoundEngine.PlaySound(CasinoSounds.Jackpot, NPC.Center);
				CasinoFx.Ring(NPC.Center, GoldLight, 50, 13f, 1.8f);
				if (!Main.dedServ) {
					BossTitle.Flash(GoldLight);
					CombatText.NewText(NPC.Hitbox, GoldLight, Language.GetTextValue("Mods.CasinoWorld.Boss.Fortuna.JackpotText"), true);
				}
			}
			if (!Main.dedServ && Main.rand.NextBool(2))
				Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(60, 60), DustID.GoldCoin, new Vector2(Main.rand.NextFloat(-2, 2), -5), 0, default, 1.2f);
			if (Timer > StunTicks * Speed)
				SetState(State.Draw);
		}

		public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
		{
			if (CurrentState == State.Stunned)
				modifiers.FinalDamage *= 1.5f;
		}

		/// <summary>Phase 3: the blindfold tears and the eye opens.</summary>
		private void Awaken()
		{
			NPC.velocity = NPC.velocity * 0.9f + Main.rand.NextVector2Circular(0.6f, 0.6f) * Math.Min(1f, Timer / 90f);
			if (At(1)) {
				ClearShots();
				Say("Awaken", FateRed);
				SoundEngine.PlaySound(CasinoSounds.FortunaCharge with { Pitch = -0.4f }, NPC.Center);
			}
			if (At(90)) {
				Awake = true;
				SoundEngine.PlaySound(CasinoSounds.FortunaTear, NPC.Center);
				SoundEngine.PlaySound(CasinoSounds.FortunaVoice with { Pitch = -0.3f }, NPC.Center);
				CasinoFx.Ring(NPC.Center, FateRed, 60, 16f, 2f);
				Shake(NPC.Center, 14f, 45, "FortunaAwaken");
				if (!Main.dedServ) {
					BossTitle.Flash(FateRed);
					for (int k = 0; k < 24; k++)
						Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(40, 10), CasinoFx.ShardType, Main.rand.NextVector2Circular(7, 6), 0, GoldLight, 1.3f);
				}
				NPC.netUpdate = true;
			}
			if (Timer > AwakenTicks)
				SetState(State.Draw);
		}

		/// <summary>Removes her shots (phase change, Last Bet, death).</summary>
		private static void ClearShots()
		{
			foreach (Projectile p in Main.ActiveProjectiles)
				if (p.hostile && p.ModProjectile is FateProjectile)
					p.Kill();
		}

		private void Leave()
		{
			NPC.velocity.Y -= 0.5f;
			NPC.alpha = Math.Min(255, NPC.alpha + 3);
			if (Timer > 130)
				NPC.active = false;
		}

		/// <summary>Contact hurts only while she moves around, not while she draws, aims or is helpless.</summary>
		public override bool CanHitPlayer(Player target, ref int cooldownSlot)
			=> CurrentState is State.FateDice or State.ThreeReels or State.CardCurtain or State.RunawayBall or State.HouseWins or State.RingToss or State.Gaze && NPC.alpha == 0;

		// ---- Death: the rings spin apart, cracks of light, a rain of coins.
		public override bool CheckDead()
		{
			if (CurrentState == State.Dying && (Timer >= DyingTicks || Main.netMode == NetmodeID.MultiplayerClient))
				return true;
			NPC.life = 1;
			NPC.dontTakeDamage = true;
			if (CurrentState != State.Dying) {
				SetState(State.Dying);
				ClearShots();
			}
			return false;
		}

		private void Dying()
		{
			float t = Math.Min(1f, Timer / DyingTicks);
			NPC.velocity = NPC.velocity * 0.9f + Main.rand.NextVector2Circular(1.2f, 1.2f) * t;
			if (At(1)) {
				Say("Death", GoldLight);
				SoundEngine.PlaySound(CasinoSounds.FortunaVoice with { Pitch = -0.5f }, NPC.Center);
			}
			for (int k = 1; k <= 9; k++)
				if (At(20 + k * 18)) {
					Vector2 at = NPC.Center + Main.rand.NextVector2Circular(150, 150);
					CasinoFx.Explosion(at, 70 + k * 12, GoldLight, FateViolet, 1f + k * 0.6f);
					SoundEngine.PlaySound(CasinoSounds.ChipExplode with { Pitch = k * 0.07f }, at);
				}
			if (!Main.dedServ)
				for (int k = 0; k < 1 + (int)(t * 7f); k++)
					Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(80, 80), DustID.GoldCoin, new Vector2(Main.rand.NextFloat(-5, 5), Main.rand.NextFloat(-12, -3)) * (0.4f + t), 0, default, 1.3f);
			if (At(DyingTicks - 45)) {
				SoundEngine.PlaySound(CasinoSounds.FortunaDeath, NPC.Center);
				CasinoFx.Ring(NPC.Center, Color.White, 60, 18f, 2f);
				if (!Main.dedServ)
					BossTitle.Flash(Color.White);
			}
			if (Timer >= DyingTicks && Main.netMode != NetmodeID.MultiplayerClient)
				NPC.StrikeInstantKill();
		}

		public override bool CheckActive() => false;

		public override void OnKill()
		{
			NPC.SetEventFlagCleared(ref CasinoDowned.DownedFortuna, -1);
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		public override void BossLoot(ref int potionType) => potionType = ItemID.SuperHealingPotion;

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<FortunaBag>()));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<FortunaTrophy>(), 10));
			npcLoot.Add(ItemDropRule.MasterModeCommonDrop(ModContent.ItemType<FortunaRelicItem>()));
			var normal = new LeadingConditionRule(new Conditions.NotExpert());
			normal.OnSuccess(ItemDropRule.OneFromOptions(1, ModContent.ItemType<Ophanim>(), ModContent.ItemType<DiceOfDestiny>(), ModContent.ItemType<EyeOfFortune>()));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<FateShard>(), 1, 8, 12));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 1, 300, 400));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<HighRollerCrate>(), 1, 4, 6));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<FortunaMask>(), 7));
			npcLoot.Add(normal);
		}

		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.Write(Awake);
			writer.Write(betMade);
			writer.Write(betStartLife);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			Awake = reader.ReadBoolean();
			betMade = reader.ReadBoolean();
			betStartLife = reader.ReadInt32();
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			if (Main.dedServ)
				return;
			for (int k = 0; k < 2; k++)
				Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldCoin, hit.HitDirection * 2f, -2f);
			if (NPC.life <= 0 && CurrentState == State.Dying) {
				Shake(NPC.Center, 18f, 50, "FortunaDeath");
				CasinoFx.Explosion(NPC.Center, 320, GoldLight, FateViolet, 0f);
				for (int k = 0; k < 110; k++)
					Dust.NewDustPerfect(NPC.Center, DustID.GoldCoin, Main.rand.NextVector2Circular(15, 15), 0, default, 1.5f);
				for (int k = 0; k < 40; k++)
					Dust.NewDustPerfect(NPC.Center, CasinoFx.ShardType, Main.rand.NextVector2Circular(11, 11), 0, k % 2 == 0 ? Color.White : FateRed, 1.4f);
			}
		}

		// ------------------------------------------------------------------ visuals

		private static Asset<Texture2D> pocketTex, cardTex, studTex, wingTex, iconTex;

		public override void Unload() => pocketTex = cardTex = studTex = wingTex = iconTex = null;

		private static Texture2D Tex(ref Asset<Texture2D> asset, string name)
			=> (asset ??= ModContent.Request<Texture2D>("CasinoWorld/Content/NPCs/Boss/Fortuna_" + name, AssetRequestMode.ImmediateLoad)).Value;

		private float align, spread = 1f, ringSpin, wingBeat;
		private int lastPhase = 1, flash;
		private readonly Vector2[][] ribbons = new Vector2[2][];

		private void UpdateVisuals()
		{
			bool aligned = CurrentState is State.Draw or State.LastBet;
			align = MathHelper.Lerp(align, aligned ? 1f : 0f, 0.07f);
			float wantSpread = CurrentState switch {
				State.Dying => 1f + 1.2f * Timer / DyingTicks,
				State.Awaken => Timer < 90 ? 0.55f : 1.25f,
				State.Stunned => 0.75f,
				State.PotOfGold => 1.15f,
				_ => 1f,
			};
			spread = MathHelper.Lerp(spread, wantSpread, 0.08f);
			float rate = CurrentState switch {
				State.Draw => 0.12f * Math.Max(0.1f, 1f - Timer / DrawTicks),
				State.LastBet => Timer > BetTicks - 120 ? 0.2f : 0.05f,
				State.Dying => 0.05f + 0.25f * Timer / DyingTicks,
				State.Stunned => 0.004f,
				_ => Awake ? 0.04f : 0.022f,
			};
			ringSpin += rate;
			wingBeat += 0.05f + NPC.velocity.Length() * 0.004f;
			if (Phase != lastPhase) {
				lastPhase = Phase;
				flash = 60;
				SoundEngine.PlaySound(CasinoSounds.BossBell, NPC.Center);
				CasinoFx.Ring(NPC.Center, GoldLight, 44, 12f, 1.8f);
				BossTitle.Flash(GoldLight);
			}
			if (flash > 0)
				flash--;

			// The loose ends of the blindfold trail behind her.
			for (int r = 0; r < 2; r++) {
				Vector2 anchor = NPC.Center + new Vector2(r == 0 ? -42 : 42, -8);
				ribbons[r] ??= new Vector2[12];
				Vector2[] chain = ribbons[r];
				if (Vector2.DistanceSquared(chain[0], anchor) > 400f * 400f)
					Array.Fill(chain, anchor);
				chain[0] = anchor;
				for (int k = 1; k < chain.Length; k++) {
					float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + k * 0.6f + r * 2f);
					Vector2 p = chain[k] + new Vector2((r == 0 ? -1.1f : 1.1f) + wave * 0.8f, 1.6f);
					Vector2 d = p - chain[k - 1];
					chain[k] = chain[k - 1] + d.SafeNormalize(Vector2.UnitY) * Math.Min(d.Length(), 11f);
				}
			}
		}

		public override void FindFrame(int frameHeight)
		{
			int pose = CurrentState switch {
				State.Stunned => 2,
				State.Intro or State.LastBet or State.Awaken when !Awake => 1,
				State.Dying => Awake ? 3 : 2,
				_ => Awake ? 3 : 0,
			};
			if (NPC.IsABestiaryIconDummy)
				pose = 0;
			NPC.frame.Y = pose * frameHeight;
		}

		private readonly struct Piece
		{
			public readonly Vector2 At;
			public readonly float Z, Angle;
			public readonly int Kind, Frame;

			public Piece(Vector2 at, float z, float angle, int kind, int frame)
			{
				At = at;
				Z = z;
				Angle = angle;
				Kind = kind;
				Frame = frame;
			}
		}

		private readonly List<Piece> pieces = new();

		/// <summary>Adds one ring of the gyroscope: points on a circle, turned in space, flattened to the screen.</summary>
		private void AddRing(int kind, int count, float radius, float spin, float tiltX, float tiltY)
		{
			tiltX *= 1f - align;
			tiltY *= 1f - align;
			float cx = (float)Math.Cos(tiltX), sx = (float)Math.Sin(tiltX), cy = (float)Math.Cos(tiltY), sy = (float)Math.Sin(tiltY);
			for (int k = 0; k < count; k++) {
				float a = spin + k * MathHelper.TwoPi / count;
				float x = (float)Math.Cos(a) * radius, y = (float)Math.Sin(a) * radius, z = 0f;
				(y, z) = (y * cx - z * sx, y * sx + z * cx);
				(x, z) = (x * cy + z * sy, -x * sy + z * cy);
				int frame = kind switch {
					0 => k == 0 ? 2 : k % 2,
					1 => Awake || (int)(Main.GlobalTimeWrappedHourly * 6f + k * 5) % 40 > 2 ? 0 : (int)(Main.GlobalTimeWrappedHourly * 6f + k * 5) % 40 == 1 ? 2 : 1,
					_ => 0,
				};
				pieces.Add(new Piece(new Vector2(x, y), z, new Vector2(x, y).ToRotation() + MathHelper.PiOver2, kind, frame));
			}
		}

		private void DrawPieces(SpriteBatch sb, Vector2 center, bool front, float fade)
		{
			Texture2D pocket = Tex(ref pocketTex, "Pocket"), card = Tex(ref cardTex, "EyeCard"), stud = Tex(ref studTex, "Stud");
			foreach (Piece p in pieces) {
				if (p.Z >= 0f != front)
					continue;
				if (front && p.At.LengthSquared() < 50f * 50f)
					continue;   // nothing passes in front of her face
				Texture2D tex = p.Kind == 0 ? pocket : p.Kind == 1 ? card : stud;
				int frames = p.Kind == 2 ? 1 : 3;
				Rectangle frame = tex.Frame(1, frames, 0, p.Frame);
				float depth = MathHelper.Clamp(0.72f + p.Z / 420f, 0.4f, 1f);
				Color c = new Color(depth, depth * 0.97f, MathHelper.Clamp(depth + 0.08f, 0f, 1f)) * fade;
				sb.Draw(tex, PixelFx.Snap(center + p.At), frame, c, p.Kind == 2 ? 0f : p.Angle, frame.Size() / 2f, 1f, SpriteEffects.None, 0f);
			}
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			Texture2D body = TextureAssets.Npc[Type].Value;
			float fade = 1f - NPC.alpha / 255f;
			float t = Main.GlobalTimeWrappedHourly;
			bool dummy = NPC.IsABestiaryIconDummy;
			Vector2 center = NPC.Center - screenPos + new Vector2(0, (float)Math.Sin(t * 1.8f) * 6f);
			if (dummy)
				ringSpin = t * 0.8f;
			Color tint = Awake ? FateRed : GoldLight;

			if (!dummy) {
				PixelFx.Glow(spriteBatch, center, 110f, tint * (0.22f * fade));
				if (CurrentState == State.Dying || flash > 0)
					PixelFx.Burst(spriteBatch, center, Color.Lerp(tint, Color.White, 0.4f) * (CurrentState == State.Dying ? Math.Min(1f, Timer / 90f) : flash / 60f), t, CurrentState == State.Dying ? 2 + (int)(Timer / DyingTicks * 2f) : 3);
			}

			// Wings: two fans of cards.
			Texture2D wing = Tex(ref wingTex, "WingCard");
			float beat = (float)Math.Sin(wingBeat) * 0.22f;
			for (int side = -1; side <= 1; side += 2)
				for (int k = 0; k < 7; k++) {
					Rectangle wf = wing.Frame(1, 2, 0, k % 2);
					float angle = side * (0.35f + k * 0.27f + beat * (1f + k * 0.15f));
					Vector2 root = center + new Vector2(side * (34 + k * 3), -4 + k * 5);
					float shade = 0.95f - k * 0.05f;
					spriteBatch.Draw(wing, PixelFx.Snap(root), wf, new Color(shade, shade, shade + 0.03f) * fade, angle, new Vector2(wf.Width / 2f, wf.Height + 26 - k * 2), 1f + (3 - Math.Abs(k - 3)) * 0.12f, SpriteEffects.None, 0f);
				}

			pieces.Clear();
			float dieJitter = CurrentState == State.Dying ? Main.rand.NextFloat(-0.06f, 0.06f) : 0f;
			if (!RingAway)
				AddRing(0, 24, 152f * spread, ringSpin, 1.05f + (float)Math.Sin(t * 0.5f) * 0.25f + dieJitter, t * 0.35f);
			AddRing(1, 12, 112f * spread, -ringSpin * 1.4f, -0.8f + (float)Math.Sin(t * 0.4f) * 0.3f, -t * 0.45f + 1f);
			AddRing(2, 20, 78f * spread, ringSpin * 2f, 1.2f + t * 0.6f, 0.5f);
			DrawPieces(spriteBatch, center, false, fade);

			// Ribbons, then the mask.
			if (!dummy)
				foreach (Vector2[] chain in ribbons)
					if (chain != null && (!Awake || chain == ribbons[0]))
						for (int k = 1; k < chain.Length; k++) {
							PixelFx.Line(spriteBatch, chain[k - 1] - screenPos, chain[k] - screenPos, new Color(120, 70, 20) * fade, 10f - k * 0.4f);
							PixelFx.Line(spriteBatch, chain[k - 1] - screenPos, chain[k] - screenPos, (k % 3 == 0 ? new Color(200, 50, 60) : new Color(236, 180, 70)) * fade, 6f - k * 0.3f);
						}
			float tilt = CurrentState is State.Stunned or State.Dying ? (float)Math.Sin(t * 7f) * 0.14f : NPC.velocity.X * 0.008f;
			Color light = dummy ? Color.White : Color.Lerp(drawColor, Color.White, 0.75f);
			spriteBatch.Draw(body, PixelFx.Snap(center), NPC.frame, light * fade, tilt, NPC.frame.Size() / 2f, 1f, SpriteEffects.None, 0f);
			if (Awake && !dummy)
				PixelFx.Glow(spriteBatch, center + new Vector2(0, -8), 32f, FateRed * (0.5f + 0.3f * (float)Math.Sin(t * 8f)));
			DrawPieces(spriteBatch, center, true, fade);
			if (!dummy)
				DrawOracle(spriteBatch, center, fade, t);
			return false;
		}

		/// <summary>The icon over her crown, and the odds during the Last Bet.</summary>
		private void DrawOracle(SpriteBatch sb, Vector2 center, float fade, float t)
		{
			Texture2D icons = Tex(ref iconTex, "Icons");
			Vector2 at = center + new Vector2(0, CurrentState == State.LastBet ? -272 : -196);
			// Always on screen, even when she hovers high above the player.
			at.Y = Math.Max(at.Y, 60f);
			at.X = MathHelper.Clamp(at.X, 130f, Main.screenWidth - 130f);
			int icon = -1;
			float alpha = fade;
			if (CurrentState == State.Draw && Timer > 4) {
				bool landed = Timer > DrawTicks - 20;
				icon = landed && Argument > 0 ? (int)Argument - (int)State.RingToss : (int)(Timer / 3f) % 7;
				if (landed)
					PixelFx.Burst(sb, at, GoldLight * 0.7f, t, 1);
			}
			else if (CurrentState is >= State.RingToss and <= State.RunawayBall && Timer < 90) {
				icon = (int)CurrentState - (int)State.RingToss;
				alpha *= MathHelper.Clamp((90 - Timer) / 30f, 0f, 1f);
			}
			else if (CurrentState == State.LastBet)
				icon = 7;
			if (icon < 0)
				return;
			Rectangle frame = icons.Frame(1, 8, 0, icon);
			float pop = CurrentState == State.Draw && Timer > DrawTicks - 20 && Timer < DrawTicks - 12 ? 2f : 1f;
			sb.Draw(icons, PixelFx.Snap(at), frame, Color.White * alpha, 0f, frame.Size() / 2f, pop, SpriteEffects.None, 0f);
			if (CurrentState != State.LastBet)
				return;
			Texture2D px = TextureAssets.MagicPixel.Value;
			const int W = 220, H = 16;
			var bar = new Rectangle((int)at.X - W / 2, (int)at.Y + 26, W, H);
			sb.Draw(px, new Rectangle(bar.X - 4, bar.Y - 4, W + 8, H + 8), new Color(146, 88, 26));
			sb.Draw(px, new Rectangle(bar.X - 2, bar.Y - 2, W + 4, H + 4), new Color(236, 180, 70));
			sb.Draw(px, bar, new Color(90, 14, 30));
			float odds = BetOdds;
			sb.Draw(px, new Rectangle(bar.X, bar.Y, (int)(W * odds) / 2 * 2, H), new Color(60, 190, 90));
			sb.Draw(px, new Rectangle(bar.X, bar.Y, (int)(W * odds) / 2 * 2, 4), new Color(150, 250, 160));
			// The pointer wanders while the wheel turns, faster and faster.
			float p = 0.5f + 0.5f * (float)Math.Sin(Timer * Timer * 0.00045f);
			sb.Draw(px, new Rectangle(bar.X + (int)(W * p) / 2 * 2 - 2, bar.Y - 6, 4, H + 12), Color.White);
			Utils.DrawBorderString(sb, Language.GetTextValue("Mods.CasinoWorld.Boss.Fortuna.Odds", (int)(odds * 100)), new Vector2(at.X, bar.Bottom + 8), GoldLight, 0.95f, 0.5f);
		}
	}
}
