using System;
using System.IO;
using CasinoWorld.Common;
using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.Accessories;
using CasinoWorld.Content.Items.Crates;
using CasinoWorld.Content.Items.Weapons;
using CasinoWorld.Content.Projectiles.Boss;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace CasinoWorld.Content.NPCs.Boss
{
	/// <summary>
	/// The Wild Card (design/CasinoWorld/boss.md): a flying jester made of playing cards, with a mask,
	/// a three-point hat, gloved hands and a tail of cards instead of legs. Post-Skeletron, pre-WoF.
	///
	///   Phase 1 (100-60 %) Card Tricks: Deal (fans of cards), Shell Game (hides in one of three big
	///     cards that shuffle; the fakes burst, the real one is stunned and vulnerable), 52 Pickup
	///     (telegraphed columns of falling cards).
	///   Phase 2 (60-25 %) Roll the Dice: two dice land and their sum picks the attack (snake eyes,
	///     bouncing dice, lucky sevens, dashes, boxcars); doubles repeat the next attack faster.
	///   Phase 3 (below 25 %) All In: spiral chip barrages between faster tricks; 25 % faster overall.
	///
	/// The AI runs on the server; ai[0] state, ai[1] timer, ai[2] counter, ai[3] seed/argument are synced
	/// by vanilla, plus the shell-game data in SendExtraAI. Body parts are animated when drawing.
	/// </summary>
	[AutoloadBossHead]
	public class TheWildCard : ModNPC
	{
		public enum State { Intro, Hover, Deal, ShellGame, Pickup, DiceRoll, DiceAttack, Dash, Spiral, Stunned, Leave }

		public const int BaseLife = 9500;
		private const int CardDamage = 30, DiceDamage = 34, ChipDamage = 26, SpadeDamage = 28;

		private State CurrentState { get => (State)NPC.ai[0]; set => NPC.ai[0] = (float)value; }
		private ref float Timer => ref NPC.ai[1];
		private ref float Counter => ref NPC.ai[2];
		private ref float Argument => ref NPC.ai[3];

		// Shell game: shuffle seed, number of cards, which slot holds the real card, centre.
		public int ShellSeed;
		public int ShellCards = 3;
		public int RealCard;
		public Vector2 ShellCenter;
		public int ShellSwaps = 6;
		public int SwapTicks = 22;

		// Dice: the faces of the last roll and whether the next attack repeats (doubles).
		private int dieA, dieB;
		private bool repeatNext;
		private int attacksSinceShell;

		private float LifeRatio => NPC.life / (float)NPC.lifeMax;
		public int Phase => LifeRatio > 0.6f ? 1 : LifeRatio > 0.25f ? 2 : 3;
		private float Speed => Phase == 3 ? 1.25f : 1f;

		public override string Texture => "CasinoWorld/Content/NPCs/Boss/WildCard_Head";
		public override string BossHeadTexture => "CasinoWorld/Content/NPCs/Boss/WildCard_Head_Boss";

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 1;
			NPCID.Sets.MPAllowedEnemies[Type] = true;
			NPCID.Sets.BossBestiaryPriority.Add(Type);
			NPCID.Sets.TrailCacheLength[Type] = 8;
			NPCID.Sets.TrailingMode[Type] = 3;
			NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
		}

		public override void SetDefaults()
		{
			NPC.width = 70;
			NPC.height = 110;
			NPC.damage = 40;
			NPC.defense = 14;
			NPC.lifeMax = BaseLife;
			NPC.HitSound = CasinoSounds.CardSoldierHit;
			NPC.DeathSound = CasinoSounds.BossBreak;
			NPC.knockBackResist = 0f;
			NPC.noGravity = true;
			NPC.noTileCollide = true;
			NPC.boss = true;
			NPC.npcSlots = 10f;
			NPC.value = Item.buyPrice(gold: 8);
			NPC.aiStyle = -1;
			if (!Main.dedServ)
				Music = CasinoMusic.TheHouse;
		}

		public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
		{
			NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
			NPC.damage = (int)(NPC.damage * 0.8f);
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Times.NightTime,
				new FlavorTextBestiaryInfoElement("Mods.CasinoWorld.Bestiary.TheWildCard"),
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
				ChatHelper.BroadcastChatMessage(NetworkText.FromKey(key), color);
			else if (Main.netMode == NetmodeID.SinglePlayer)
				Main.NewText(Language.GetTextValue(key), color);
		}

		private int Damage(int baseDamage) => Main.expertMode ? baseDamage / 2 * (Main.masterMode ? 3 : 2) / 2 : baseDamage;

		private void Shoot(Vector2 position, Vector2 velocity, int type, int damage, float ai0 = 0, float ai1 = 0, float ai2 = 0)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				Projectile.NewProjectile(NPC.GetSource_FromAI(), position, velocity, type, Damage(damage), 1f, Main.myPlayer, ai0, ai1, ai2);
		}

		/// <summary>Glides towards a point with smooth acceleration.</summary>
		private void FlyTo(Vector2 goal, float speed, float inertia)
		{
			Vector2 desired = goal - NPC.Center;
			float dist = desired.Length();
			if (dist > 1f)
				desired = desired / dist * Math.Min(speed, dist * 0.12f + 1f);
			NPC.velocity = (NPC.velocity * (inertia - 1) + desired) / inertia;
		}

		// ------------------------------------------------------------------ AI

		public override void AI()
		{
			if (NPC.target < 0 || NPC.target == 255 || Target.dead || !Target.active)
				NPC.TargetClosest();

			bool leaving = Main.dayTime || Target.dead || !Target.active || NPC.Distance(Target.Center) > 6000f;
			if (leaving && CurrentState != State.Leave) {
				if (CurrentState == State.ShellGame)
					EndShellGameEarly();
				Say("Mods.CasinoWorld.Boss.WildCard.Leave", new Color(200, 120, 255));
				SetState(State.Leave);
			}

			if (!Main.dedServ && Phase == 3)
				Music = CasinoMusic.TheHouseWheel;

			NPC.dontTakeDamage = CurrentState is State.Intro or State.ShellGame or State.Leave;
			Lighting.AddLight(NPC.Center, Phase == 3 ? new Vector3(0.9f, 0.2f, 0.25f) : new Vector3(0.6f, 0.3f, 0.8f));

			Timer += Speed;
			switch (CurrentState) {
				case State.Intro: Intro(); break;
				case State.Hover: Hover(); break;
				case State.Deal: Deal(); break;
				case State.ShellGame: ShellGame(); break;
				case State.Pickup: Pickup(); break;
				case State.DiceRoll: DiceRoll(); break;
				case State.DiceAttack: DiceAttack(); break;
				case State.Dash: Dash(); break;
				case State.Spiral: Spiral(); break;
				case State.Stunned: Stunned(); break;
				case State.Leave: Leave(); break;
			}
			NPC.spriteDirection = NPC.direction = Target.Center.X < NPC.Center.X ? -1 : 1;
		}

		private void Intro()
		{
			NPC.velocity *= 0.9f;
			if (Timer < 2) {
				SoundEngine.PlaySound(CasinoSounds.HouseLaugh, NPC.Center);
				Say("Mods.CasinoWorld.Boss.WildCard.Intro", new Color(255, 200, 80));
			}
			if (Timer > 90)
				SetState(State.Hover);
		}

		private void Hover()
		{
			FlyTo(Target.Center + new Vector2(Math.Sign(NPC.Center.X - Target.Center.X) * 260f, -230f), 10f, 18f);
			if (Timer < (Phase == 1 ? 70 : 50) || Main.netMode == NetmodeID.MultiplayerClient)
				return;

			// Pick the next trick. Repeated doubles keep the last attack.
			var rand = Main.rand;
			State next;
			if (Phase == 3 && rand.NextBool(3))
				next = State.Spiral;
			else if (attacksSinceShell >= (Phase == 1 ? 3 : 2) && rand.NextBool(2))
				next = State.ShellGame;
			else if (Phase >= 2 && rand.NextBool(2))
				next = State.DiceRoll;
			else
				next = rand.NextBool() ? State.Deal : State.Pickup;

			attacksSinceShell = next == State.ShellGame ? 0 : attacksSinceShell + 1;
			if (next == State.ShellGame)
				BeginShellGame();
			else
				SetState(next);
		}

		// ---- Deal: three fans of five cards at the player.
		private void Deal()
		{
			FlyTo(Target.Center + new Vector2(Math.Sign(NPC.Center.X - Target.Center.X) * 300f, -200f), 8f, 20f);
			int volleys = Phase == 3 ? 4 : 3;
			if (Timer >= 30 + Counter * 34 && Counter < volleys) {
				Counter++;
				SoundEngine.PlaySound(CasinoSounds.CardThrow with { Pitch = -0.2f }, NPC.Center);
				Vector2 hand = HandPosition(Target.Center.X > NPC.Center.X ? 1 : -1);
				Vector2 aim = (Target.Center - hand).SafeNormalize(Vector2.UnitY);
				int cards = Phase == 1 ? 5 : 7;
				float spread = MathHelper.ToRadians(Phase == 1 ? 40f : 56f);
				for (int k = 0; k < cards; k++) {
					float a = MathHelper.Lerp(-spread / 2, spread / 2, k / (float)(cards - 1));
					Shoot(hand, aim.RotatedBy(a) * 9f, ModContent.ProjectileType<DealtCard>(), CardDamage);
				}
			}
			if (Timer > 30 + volleys * 34 + 40)
				SetState(State.Hover);
		}

		// ---- Shell game
		private void BeginShellGame()
		{
			ShellCards = Phase == 3 ? 4 : 3;
			ShellSwaps = Phase == 1 ? 6 : 8;
			SwapTicks = Phase == 1 ? 22 : Phase == 2 ? 17 : 14;
			ShellSeed = Main.rand.Next(1, int.MaxValue);
			RealCard = Main.rand.Next(ShellCards);
			ShellCenter = Target.Center + new Vector2(0, -170);
			SetState(State.ShellGame);
			SoundEngine.PlaySound(CasinoSounds.CardShuffle, NPC.Center);
			for (int k = 0; k < ShellCards; k++)
				Shoot(ShellCenter, Vector2.Zero, ModContent.ProjectileType<ShellCard>(), SpadeDamage, NPC.whoAmI, k, k == RealCard ? 1 : 0);
			for (int k = 0; k < 30; k++)
				Dust.NewDustPerfect(NPC.Center, DustID.PurpleTorch, Main.rand.NextVector2Circular(6, 6), 0, default, 1.5f).noGravity = true;
		}

		public const int ShellShowTicks = 70, ShellRevealTicks = 50;
		public int ShellShuffleTicks => ShellSwaps * SwapTicks;
		public int ShellTotalTicks => ShellShowTicks + ShellShuffleTicks + ShellRevealTicks;

		/// <summary>Position of the card that started in slot <paramref name="start"/> at shell-game time t (same on every client).</summary>
		public Vector2 ShellCardPosition(int start, float t)
		{
			float spacing = 120f;
			Vector2 SlotPos(int slot) => ShellCenter + new Vector2((slot - (ShellCards - 1) / 2f) * spacing, 0f);
			int slot = start;
			float shuffleT = t - ShellShowTicks;
			if (shuffleT <= 0)
				return SlotPos(slot);
			var rand = new UnifiedRandom(ShellSeed);
			int done = (int)(shuffleT / SwapTicks);
			for (int k = 0; k < ShellSwaps; k++) {
				int a = rand.Next(ShellCards), b = (a + 1 + rand.Next(ShellCards - 1)) % ShellCards;
				if (k < done || k >= ShellSwaps) {
					if (slot == a)
						slot = b;
					else if (slot == b)
						slot = a;
					continue;
				}
				if (k == done) {
					// Mid-swap: the two cards trade places along opposite arcs.
					float p = (shuffleT - done * SwapTicks) / SwapTicks;
					p = p * p * (3 - 2 * p);
					if (slot == a || slot == b) {
						int other = slot == a ? b : a;
						Vector2 from = SlotPos(slot), to = SlotPos(other);
						float lift = (slot == a ? -1 : 1) * 50f * (float)Math.Sin(p * Math.PI);
						return Vector2.Lerp(from, to, p) + new Vector2(0, lift);
					}
					return SlotPos(slot);
				}
			}
			return SlotPos(slot);
		}

		private void ShellGame()
		{
			// The boss hides at the shell centre; the cards (projectiles) read its timer.
			NPC.velocity = Vector2.Zero;
			NPC.Center = ShellCenter;
			NPC.alpha = 255;
			if (Timer >= ShellShowTicks + ShellShuffleTicks && Counter == 0) {
				Counter = 1;
				SoundEngine.PlaySound(CasinoSounds.CardFlip, ShellCenter);
			}
			if (Timer >= ShellTotalTicks) {
				// Appear where the real card ended up, dazed.
				NPC.Center = ShellCardPosition(RealCard, ShellTotalTicks);
				NPC.alpha = 0;
				SoundEngine.PlaySound(CasinoSounds.BossBust, NPC.Center);
				SetState(State.Stunned);
			}
		}

		private void EndShellGameEarly()
		{
			NPC.alpha = 0;
			foreach (Projectile p in Main.ActiveProjectiles) {
				if (p.type == ModContent.ProjectileType<ShellCard>() && (int)p.ai[0] == NPC.whoAmI)
					p.Kill();
			}
		}

		private void Stunned()
		{
			NPC.velocity *= 0.9f;
			if (Main.rand.NextBool(4))
				Dust.NewDustPerfect(NPC.Top + new Vector2(Main.rand.NextFloat(-20, 20), -10), DustID.GoldFlame, new Vector2(0, -1), 0, default, 1.2f).noGravity = true;
			if (Timer > (Phase == 3 ? 120 : 170))
				SetState(State.Hover);
		}

		public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
		{
			if (CurrentState == State.Stunned)
				modifiers.FinalDamage *= 1.5f;
		}

		// ---- 52 Pickup: telegraphed columns of falling cards around the player.
		private void Pickup()
		{
			FlyTo(Target.Center + new Vector2(0, -320f), 9f, 18f);
			if (Timer == 20 || (Phase == 3 && Timer == 80)) {
				SoundEngine.PlaySound(CasinoSounds.CardBridge, NPC.Center);
				int columns = Phase == 1 ? 9 : 12;
				float offset = Main.rand.NextFloat(0, 70f);
				for (int k = 0; k < columns; k++) {
					float x = Target.Center.X + (k - columns / 2f) * 140f + offset;
					Shoot(new Vector2(x, Target.Center.Y - 520f), Vector2.Zero, ModContent.ProjectileType<PickupCard>(), CardDamage, 40 + k % 3 * 12);
				}
			}
			if (Timer > (Phase == 3 ? 200 : 150))
				SetState(State.Hover);
		}

		// ---- Dice roll and dice attacks
		private void DiceRoll()
		{
			FlyTo(Target.Center + new Vector2(Math.Sign(NPC.Center.X - Target.Center.X) * 220f, -220f), 8f, 18f);
			if (Timer == 20 && Main.netMode != NetmodeID.MultiplayerClient) {
				dieA = Main.rand.Next(1, 7);
				dieB = Main.rand.Next(1, 7);
				repeatNext = dieA == dieB;
				SoundEngine.PlaySound(CasinoSounds.DiceThrow, NPC.Center);
				Vector2 hand = HandPosition(Target.Center.X > NPC.Center.X ? 1 : -1);
				Shoot(hand, new Vector2(-3f, -6f), ModContent.ProjectileType<RollingDie>(), 0, dieA, 0);
				Shoot(hand, new Vector2(3f, -6f), ModContent.ProjectileType<RollingDie>(), 0, dieB, 0);
				NPC.netUpdate = true;
			}
			if (Timer == 95) {
				int sum = dieA + dieB;
				if (!Main.dedServ)
					CombatText.NewText(NPC.Hitbox, sum == 7 ? Color.Gold : Color.White, Language.GetTextValue("Mods.CasinoWorld.Boss.WildCard.Roll", dieA, dieB, sum), true);
				SetState(State.DiceAttack, sum);
			}
		}

		private void DiceAttack()
		{
			int sum = (int)Argument;
			if (sum == 2)
				SnakeEyes();
			else if (sum <= 6)
				BouncingDice(sum);
			else if (sum == 7)
				LuckySevens();
			else if (sum <= 11)
				SetState(State.Dash, sum - 7);
			else
				Boxcars();
		}

		private void FinishAttack()
		{
			if (repeatNext && Argument >= 2) {
				repeatNext = false;
				int sum = (int)Argument;
				SetState(State.DiceAttack, sum);
				Timer = 20;   // a faster second go
				return;
			}
			SetState(State.Hover);
		}

		private void SnakeEyes()
		{
			FlyTo(Target.Center + new Vector2(0, -260f), 7f, 20f);
			if (Timer == 10) {
				SoundEngine.PlaySound(CasinoSounds.ChipStack with { Pitch = -0.4f }, NPC.Center);
				Shoot(HandPosition(-1), new Vector2(-6, -2), ModContent.ProjectileType<ChipSnake>(), ChipDamage);
				Shoot(HandPosition(1), new Vector2(6, -2), ModContent.ProjectileType<ChipSnake>(), ChipDamage);
			}
			if (Timer > 200)
				FinishAttack();
		}

		private void BouncingDice(int count)
		{
			FlyTo(Target.Center + new Vector2(Math.Sign(NPC.Center.X - Target.Center.X) * 240f, -240f), 8f, 18f);
			if (Timer >= 10 && Counter < count && Timer >= 10 + Counter * 14) {
				Counter++;
				SoundEngine.PlaySound(CasinoSounds.DiceThrow, NPC.Center);
				Vector2 aim = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
				Shoot(NPC.Center, aim.RotatedByRandom(0.5f) * 8f + new Vector2(0, -4), ModContent.ProjectileType<BouncingDie>(), DiceDamage, Main.rand.Next(1, 7));
			}
			if (Timer > 10 + count * 14 + 60)
				FinishAttack();
		}

		private void LuckySevens()
		{
			NPC.velocity *= 0.92f;
			if (Timer == 20) {
				SoundEngine.PlaySound(CasinoSounds.Jackpot with { Volume = 0.6f }, NPC.Center);
				float start = Main.rand.NextFloat(MathHelper.TwoPi);
				for (int k = 0; k < 7; k++)
					Shoot(NPC.Center, Vector2.UnitX.RotatedBy(start + k * MathHelper.TwoPi / 7) * 6f, ModContent.ProjectileType<LuckySeven>(), CardDamage);
			}
			if (Timer > 110)
				FinishAttack();
		}

		private void Dash()
		{
			int dashes = (int)Argument;
			float cycle = Timer - Counter * 46;
			if (cycle < 22) {
				// Wind up: back off from the player, eyes flare.
				Vector2 away = (NPC.Center - Target.Center).SafeNormalize(Vector2.UnitY);
				FlyTo(Target.Center + away * 280f, 9f, 10f);
			}
			else if (cycle < 23) {
				SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.4f }, NPC.Center);
				NPC.velocity = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * (Phase == 3 ? 21f : 18f);
			}
			else if (cycle < 46) {
				NPC.velocity *= 0.97f;
				if (Main.rand.NextBool(2))
					Dust.NewDustPerfect(NPC.Center, DustID.PurpleTorch, -NPC.velocity * 0.2f, 0, default, 1.6f).noGravity = true;
			}
			else {
				Counter++;
				if (Counter >= dashes)
					FinishAttack();
			}
		}

		private void Boxcars()
		{
			NPC.velocity *= 0.9f;
			if (Timer == 20) {
				SoundEngine.PlaySound(CasinoSounds.BossSpin, NPC.Center);
				foreach (int side in new[] { -1, 1 }) {
					Vector2 start = new(Target.Center.X + side * 700f, Target.Center.Y - 200f);
					Shoot(start, new Vector2(-side * 7.5f, 0f), ModContent.ProjectileType<BoxcarDie>(), DiceDamage + 6, 6);
				}
			}
			if (Timer > 220)
				FinishAttack();
		}

		// ---- All In: spiral chip barrage.
		private void Spiral()
		{
			NPC.velocity *= 0.9f;
			if (Timer == 10) {
				SoundEngine.PlaySound(CasinoSounds.HouseLaugh with { Pitch = 0.2f }, NPC.Center);
				Say("Mods.CasinoWorld.Boss.WildCard.AllIn", new Color(255, 80, 80));
			}
			if (Timer > 30 && Timer < 210 && (int)Timer % 5 == 0) {
				float a = Timer * 0.11f;
				for (int k = 0; k < 2; k++)
					Shoot(NPC.Center, Vector2.UnitX.RotatedBy(a + k * MathHelper.Pi) * 5.5f, ModContent.ProjectileType<AllInChip>(), ChipDamage, k);
				if ((int)Timer % 10 == 0)
					SoundEngine.PlaySound(CasinoSounds.ChipPlace with { Volume = 0.4f }, NPC.Center);
			}
			if (Timer > 240)
				SetState(State.Hover);
		}

		private void Leave()
		{
			NPC.velocity.Y -= 0.4f;
			NPC.alpha = Math.Min(255, NPC.alpha + 4);
			if (Timer > 120)
				NPC.active = false;
		}

		public override bool CheckActive() => false;

		public override void OnKill()
		{
			NPC.SetEventFlagCleared(ref CasinoDowned.DownedWildCard, -1);
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			if (Main.dedServ)
				return;
			for (int k = 0; k < 3; k++)
				Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Confetti + Main.rand.Next(4), hit.HitDirection * 2f, -2f);
			if (NPC.life <= 0) {
				Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Main.rand.NextVector2Unit(), 12f, 6f, 30, 1000f, "WildCard"));
				for (int k = 0; k < 60; k++)
					Dust.NewDustPerfect(NPC.Center, DustID.Confetti + Main.rand.Next(4), Main.rand.NextVector2Circular(10, 10), 0, default, 1.4f);
				for (int k = 0; k < 20; k++)
					Dust.NewDustPerfect(NPC.Center, DustID.GoldCoin, Main.rand.NextVector2Circular(7, 7), 0, default, 1.2f);
			}
		}

		public override void BossLoot(ref int potionType) => potionType = ItemID.HealingPotion;

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<WildCardBag>()));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.Vanity.WildCardTrophy>(), 10));
			npcLoot.Add(ItemDropRule.MasterModeCommonDrop(ModContent.ItemType<Items.Vanity.WildCardRelicItem>()));
			npcLoot.Add(ItemDropRule.MasterModeDropOnAllPlayers(ModContent.ItemType<Items.Vanity.MiniSlotItem>(), 4));
			var normal = new LeadingConditionRule(new Conditions.NotExpert());
			normal.OnSuccess(ItemDropRule.OneFromOptions(1, ModContent.ItemType<JackpotLever>(), ModContent.ItemType<DealersHand>(), ModContent.ItemType<WheelOfFortune>()));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 1, 80, 120));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<LuckyCrate>(), 1, 2, 3));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<GoldenChip>(), 3));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<Items.Vanity.WildCardMask>(), 7));
			npcLoot.Add(normal);
		}

		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.Write(ShellSeed);
			writer.Write((byte)ShellCards);
			writer.Write((byte)RealCard);
			writer.Write((byte)ShellSwaps);
			writer.Write((byte)SwapTicks);
			writer.WriteVector2(ShellCenter);
			writer.Write((byte)dieA);
			writer.Write((byte)dieB);
			writer.Write(repeatNext);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			ShellSeed = reader.ReadInt32();
			ShellCards = reader.ReadByte();
			RealCard = reader.ReadByte();
			ShellSwaps = reader.ReadByte();
			SwapTicks = reader.ReadByte();
			ShellCenter = reader.ReadVector2();
			dieA = reader.ReadByte();
			dieB = reader.ReadByte();
			repeatNext = reader.ReadBoolean();
		}

		// ------------------------------------------------------------------ drawing

		private static Asset<Texture2D> headTex, headGlow, torsoTex, armTex, cardTex, pickupCard;

		public override void Unload()
		{
			headTex = headGlow = torsoTex = armTex = cardTex = pickupCard = null;
		}

		private float Bob => (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + NPC.whoAmI) * 4f;

		/// <summary>World position of a hand (side -1 left, 1 right), used for throws.</summary>
		public Vector2 HandPosition(int side) => NPC.Center + new Vector2(side * 34f, 18f + Bob);

		private Color PhaseColor => Phase == 1 ? new Color(255, 200, 70) : Phase == 2 ? new Color(200, 90, 255) : new Color(255, 60, 60);

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (NPC.alpha >= 255)
				return false;
			headTex ??= ModContent.Request<Texture2D>("CasinoWorld/Content/NPCs/Boss/WildCard_Head");
			headGlow ??= ModContent.Request<Texture2D>("CasinoWorld/Content/NPCs/Boss/WildCard_Head_Glow");
			torsoTex ??= ModContent.Request<Texture2D>("CasinoWorld/Content/NPCs/Boss/WildCard_Torso");
			armTex ??= ModContent.Request<Texture2D>("CasinoWorld/Content/NPCs/Boss/WildCard_Arm");
			cardTex ??= ModContent.Request<Texture2D>("CasinoWorld/Content/NPCs/Boss/WildCard_Card");
			pickupCard ??= ModContent.Request<Texture2D>("CasinoWorld/Content/Projectiles/Boss/PickupCard");

			float fade = 1f - NPC.alpha / 255f;
			Color light = NPC.IsABestiaryIconDummy ? Color.White : Color.Lerp(drawColor, Color.White, 0.35f) * fade;
			float t = Main.GlobalTimeWrappedHourly;

			// Afterimages while dashing.
			if (CurrentState == State.Dash) {
				for (int k = NPC.oldPos.Length - 1; k > 0; k--) {
					Vector2 old = NPC.oldPos[k] + NPC.Size / 2f;
					if (old == NPC.Size / 2f)
						continue;
					DrawBody(spriteBatch, old - screenPos, PhaseColor * (0.35f * (1f - k / (float)NPC.oldPos.Length)) * fade, t, true);
				}
			}
			DrawBody(spriteBatch, NPC.Center - screenPos, light, t, false);
			return false;
		}

		private void DrawBody(SpriteBatch sb, Vector2 center, Color color, float t, bool ghost)
		{
			center.Y += Bob;
			bool stunned = CurrentState == State.Stunned;
			float spin = CurrentState == State.Spiral ? t * 9f : 0f;
			float tilt = stunned ? (float)Math.Sin(t * 5f) * 0.25f : NPC.velocity.X * 0.02f;

			// Tail: nine cards waving behind the body, each a little smaller.
			Texture2D card = cardTex.Value;
			for (int k = 8; k >= 0; k--) {
				float wave = (float)Math.Sin(t * 4f - k * 0.6f) * (6f + k * 1.6f) - NPC.velocity.X * k * 0.9f;
				Vector2 p = center + new Vector2(wave, 34f + k * 11f);
				float rot = (float)Math.Sin(t * 4f - k * 0.6f + 0.8f) * 0.35f + spin * 0.1f;
				float scale = 1f - k * 0.055f;
				sb.Draw(card, p, null, color, rot, card.Size() / 2f, scale, SpriteEffects.None, 0f);
			}

			// Arms: swing with the attacks (raised when dealing/throwing, flailing when stunned).
			Texture2D arm = armTex.Value;
			for (int side = -1; side <= 1; side += 2) {
				float raise = CurrentState switch {
					State.Deal or State.DiceRoll or State.DiceAttack => -1.6f,
					State.Spiral => -2.4f,
					State.Pickup => -2.8f,
					_ => -0.25f,
				};
				if (stunned)
					raise = (float)Math.Sin(t * 8f + side) * 0.6f;
				float swing = side * (raise + (float)Math.Sin(t * 2.5f + side) * 0.12f) + spin;
				Vector2 shoulder = center + new Vector2(side * 22f, -14f).RotatedBy(tilt + spin);
				sb.Draw(arm, shoulder, null, color, swing, new Vector2(arm.Width / 2f, 4f), 1f, side < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
				// A fan of cards in the throwing hand.
				if (!ghost && CurrentState is State.Deal && side == NPC.direction) {
					Vector2 hand = shoulder + new Vector2(0, arm.Height - 8f).RotatedBy(swing);
					Texture2D pc = pickupCard.Value;
					for (int c = -1; c <= 1; c++)
						sb.Draw(pc, hand, null, color, swing + c * 0.3f, new Vector2(pc.Width / 2f, pc.Height), 0.9f, SpriteEffects.None, 0f);
				}
			}

			// Torso and head.
			Texture2D torso = torsoTex.Value, head = headTex.Value;
			sb.Draw(torso, center + new Vector2(0, 4f), null, color, tilt + spin, torso.Size() / 2f, 1f, SpriteEffects.None, 0f);
			Vector2 headPos = center + new Vector2(0, -44f).RotatedBy(tilt + spin);
			SpriteEffects flip = NPC.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			float headTilt = tilt * 1.4f + (float)Math.Sin(t * 1.7f) * 0.05f;
			sb.Draw(head, headPos, null, color, headTilt, head.Size() / 2f, 1f, flip, 0f);
			if (!ghost) {
				sb.Draw(headGlow.Value, headPos, null, Color.White * (1f - NPC.alpha / 255f), headTilt, head.Size() / 2f, 1f, flip, 0f);
				// Glowing eyes in the phase colour (gold, purple, red), drawn over the mask.
				Texture2D pixel = TextureAssets.MagicPixel.Value;
				for (int side = -1; side <= 1; side += 2) {
					Vector2 eye = headPos + new Vector2(side * 12f, 6f).RotatedBy(headTilt);
					float pulse = 0.75f + 0.25f * (float)Math.Sin(t * 6f + side);
					Color c = PhaseColor * pulse * (1f - NPC.alpha / 255f);
					c.A = 0;
					sb.Draw(pixel, new Rectangle((int)eye.X - 5, (int)eye.Y - 2, 10, 4), c);
					sb.Draw(pixel, new Rectangle((int)eye.X - 3, (int)eye.Y - 3, 6, 6), c * 0.6f);
				}
			}
		}
	}

	/// <summary>Remembers whether The Wild Card has been defeated in this world.</summary>
	public class CasinoDowned : ModSystem
	{
		public static bool DownedWildCard;

		public override void ClearWorld() => DownedWildCard = false;

		public override void SaveWorldData(Terraria.ModLoader.IO.TagCompound tag)
		{
			if (DownedWildCard)
				tag["downedWildCard"] = true;
		}

		// Worlds that beat the old boss (The House) keep their progress.
		public override void LoadWorldData(Terraria.ModLoader.IO.TagCompound tag)
			=> DownedWildCard = tag.ContainsKey("downedWildCard") || tag.ContainsKey("downedTheHouse");

		public override void NetSend(BinaryWriter writer) => writer.Write(DownedWildCard);

		public override void NetReceive(BinaryReader reader) => DownedWildCard = reader.ReadBoolean();

		public override void PostSetupContent()
		{
			// Boss Checklist entry (between Skeletron 5.0 and the Wall of Flesh 6.0).
			if (!ModLoader.TryGetMod("BossChecklist", out Mod checklist))
				return;
			checklist.Call("LogBoss", Mod, nameof(TheWildCard), 5.5f, () => DownedWildCard, ModContent.NPCType<TheWildCard>(),
				new System.Collections.Generic.Dictionary<string, object> {
					["spawnItems"] = ModContent.ItemType<GoldenChip>(),
				});
		}
	}
}
