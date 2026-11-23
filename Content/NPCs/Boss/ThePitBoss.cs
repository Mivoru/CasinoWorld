using System;
using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.Crates;
using CasinoWorld.Content.Items.PitBoss;
using CasinoWorld.Content.NPCs.Enemies;
using CasinoWorld.Content.Projectiles.Boss;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
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
		public enum State { Intro, Hover, ChipSpray, PlaceBets, GoldRush, Security, CashOut, WheelBarrage, DoubleOrNothing, Stunned, Leave }

		public const int BaseLife = 30000;
		private const int ChipDamage = 44, ZoneDamage = 56, BarDamage = 50, CoinDamage = 70;

		private State CurrentState { get => (State)NPC.ai[0]; set => NPC.ai[0] = (float)value; }
		private ref float Timer => ref NPC.ai[1];
		private ref float Counter => ref NPC.ai[2];
		private ref float Argument => ref NPC.ai[3];

		private float LifeRatio => NPC.life / (float)NPC.lifeMax;
		public int Phase => LifeRatio > 0.55f ? 1 : LifeRatio > 0.25f ? 2 : 3;
		private float Speed => Phase == 3 ? 1.25f : 1f;
		private int lastAttack = -1;

		public override string Texture => "CasinoWorld/Content/NPCs/Boss/PitBoss_Body";
		public override string BossHeadTexture => "CasinoWorld/Content/NPCs/Boss/PitBoss_Head_Boss";

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 2;
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
			NPC.damage = 70;
			NPC.defense = 26;
			NPC.lifeMax = BaseLife;
			NPC.HitSound = SoundID.NPCHit4;
			NPC.DeathSound = CasinoSounds.BossBreak;
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
			NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
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

		private int Damage(int baseDamage) => Main.expertMode ? baseDamage / 2 * (Main.masterMode ? 3 : 2) / 2 : baseDamage;

		private void Shoot(Vector2 position, Vector2 velocity, int type, int damage, float ai0 = 0, float ai1 = 0, float ai2 = 0)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				Projectile.NewProjectile(NPC.GetSource_FromAI(), position, velocity, type, Damage(damage), 1f, Main.myPlayer, ai0, ai1, ai2);
		}

		private void FlyTo(Vector2 goal, float speed, float inertia)
		{
			Vector2 desired = goal - NPC.Center;
			float dist = desired.Length();
			if (dist > 1f)
				desired = desired / dist * Math.Min(speed, dist * 0.1f + 1f);
			NPC.velocity = (NPC.velocity * (inertia - 1) + desired) / inertia;
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
			if (leaving && CurrentState != State.Leave) {
				Say("Mods.CasinoWorld.Boss.PitBoss.Leave", new Color(255, 200, 90));
				SetState(State.Leave);
			}
			if (!Main.dedServ && Phase >= 2)
				Music = CasinoMusic.PitBossTwo;
			NPC.dontTakeDamage = CurrentState is State.Intro or State.Leave;
			Lighting.AddLight(NPC.Center, Phase == 3 ? new Vector3(0.9f, 0.3f, 0.2f) : new Vector3(0.8f, 0.6f, 0.3f));
			if (Main.rand.NextBool(5))   // cigar smoke
				Dust.NewDustPerfect(NPC.Center + new Vector2(NPC.spriteDirection * 26, -46), DustID.Smoke, new Vector2(NPC.spriteDirection * 0.3f, -1.2f), 150, default, 0.9f).noGravity = true;

			Timer += Speed;
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
			}
			if (CurrentState != State.CashOut)
				NPC.spriteDirection = NPC.direction = Target.Center.X < NPC.Center.X ? -1 : 1;
		}

		private void Intro()
		{
			NPC.velocity *= 0.9f;
			if (Timer <= Speed) {
				SoundEngine.PlaySound(CasinoSounds.VaultDoor, NPC.Center);
				Say("Mods.CasinoWorld.Boss.PitBoss.Intro", new Color(255, 200, 90));
				if (!Main.dedServ)
					BossTitle.Show("Mods.CasinoWorld.Boss.PitBoss.Title", "Mods.CasinoWorld.Boss.PitBoss.Subtitle", new Color(255, 200, 90));
			}
			if ((int)Timer == 60) {
				SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f }, NPC.Center);
				CasinoFx.Ring(NPC.Center, new Color(255, 210, 90), 30, 9f, 1.6f);
				if (!Main.dedServ)
					Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Vector2.UnitY, 7f, 8f, 25, 1800f, "PitBossIntro"));
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
			State next;
			do
				next = pool[Main.rand.Next(pool.Length)];
			while ((int)next == lastAttack || next == State.Security && CountGuards() >= 3);
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
			if (Timer >= 20 + Counter * 26 && Counter < volleys) {
				Counter++;
				int side = (int)Counter % 2 == 0 ? 1 : -1;
				Vector2 hand = HandPosition(side);
				SoundEngine.PlaySound(CasinoSounds.ChipStack with { Pitch = 0.2f }, hand);
				CasinoFx.Ring(hand, new Color(255, 210, 90), 8, 2.5f);
				Vector2 to = Target.Center - hand;
				for (int k = 0; k < (Phase == 1 ? 5 : 7); k++) {
					float t = 40f + k * 6f;   // flight time in ticks for each chip
					Vector2 v = new(to.X / t + Main.rand.NextFloat(-1f, 1f), to.Y / t - 0.5f * 0.3f * t);
					Shoot(hand, v, ModContent.ProjectileType<BouncingChip>(), ChipDamage, k % 3);
				}
			}
			if (Timer > 20 + volleys * 26 + 40)
				SetState(State.Hover);
		}

		// ---- Place Your Bets: red/black floor zones, one colour erupts.
		public const int BetTelegraph = 110, BetReveal = 45;

		private void PlaceBets()
		{
			FlyTo(Target.Center + new Vector2(0, -200f), 9f, 16f);
			int rounds = Phase == 3 ? 2 : 1;
			int cycle = BetTelegraph + 50;
			if ((int)(Timer - Counter * cycle) == 1 && Counter < rounds) {
				Counter++;
				SoundEngine.PlaySound(CasinoSounds.RouletteSpin, NPC.Center);
				Say("Mods.CasinoWorld.Boss.PitBoss.Bets", new Color(255, 220, 120));
				float floor = FloorBelow(Target.Center);
				int winning = Main.rand.Next(2);
				const int ZoneWidth = 6 * 16;
				float offset = Main.rand.Next(2) * ZoneWidth;
				for (int k = -7; k <= 7; k++) {
					float x = Target.Center.X + k * ZoneWidth + offset - ZoneWidth / 2f;
					int color = (k + 100) % 2;
					Shoot(new Vector2(x, floor), Vector2.Zero, ModContent.ProjectileType<BetZone>(), ZoneDamage, color, winning, BetTelegraph);
				}
			}
			if ((int)(Timer % cycle) == BetTelegraph - BetReveal)
				SoundEngine.PlaySound(CasinoSounds.RouletteBallDrop, NPC.Center);
			if (Timer > rounds * cycle + 20)
				SetState(State.Hover);
		}

		// ---- Gold Rush: bars fall from the ceiling.
		private void GoldRush()
		{
			FlyTo(Target.Center + new Vector2(Math.Sign(NPC.Center.X - Target.Center.X) * 320f, -120f), 8f, 20f);
			int bars = Phase == 3 ? 18 : 12;
			if (Timer >= 10 && Counter < bars && (int)Timer % 6 == 0) {
				Counter++;
				float x = Target.Center.X + Main.rand.NextFloat(-520f, 520f);
				if (Counter % 4 == 0)
					x = Target.Center.X + Target.velocity.X * 30f;   // every fourth bar aims ahead of the player
				float ceiling = CeilingAbove(new Vector2(x, Target.Center.Y));
				Shoot(new Vector2(x, ceiling + 8), Vector2.Zero, ModContent.ProjectileType<GoldBarDrop>(), BarDamage, 45);
			}
			if (Timer == 12)
				SoundEngine.PlaySound(SoundID.CoinPickup with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
			if (Timer > 10 + bars * 6 + 70)
				SetState(State.Hover);
		}

		// ---- Security: two Royal Guards join the fight.
		private void Security()
		{
			NPC.velocity *= 0.9f;
			if (Timer == 20) {
				SoundEngine.PlaySound(CasinoSounds.BossBell, NPC.Center);
				Say("Mods.CasinoWorld.Boss.PitBoss.Security", new Color(255, 200, 90));
				if (Main.netMode != NetmodeID.MultiplayerClient)
					for (int side = -1; side <= 1; side += 2) {
						Vector2 at = new(Target.Center.X + side * 420f, FloorBelow(Target.Center + new Vector2(side * 420f, 0)) - 30);
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

		// ---- Cash Out: telegraphed charges across the hall.
		private void CashOut()
		{
			int dashes = Phase == 1 ? 2 : 3;
			float cycle = Timer - Counter * 70;
			if (cycle < 34) {
				// Line up beside the player at their height, then flash.
				float side = Counter % 2 == 0 ? -1 : 1;
				FlyTo(new Vector2(Target.Center.X + side * 520f, Target.Center.Y - 10), 16f, 8f);
				NPC.spriteDirection = NPC.direction = -(int)side;
				Argument = Target.Center.Y - 10;
			}
			else if (cycle < 35) {
				SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f }, NPC.Center);
				SoundEngine.PlaySound(CasinoSounds.BossBell with { Pitch = 0.4f }, NPC.Center);
				NPC.velocity = new Vector2(NPC.spriteDirection * (Phase == 3 ? 26f : 22f), 0);
			}
			else if (cycle < 70) {
				NPC.velocity *= 0.985f;
				CasinoFx.Spark(NPC.Center + Main.rand.NextVector2Circular(40, 50), -NPC.velocity * 0.1f, new Color(255, 210, 90), 1f);
				if ((int)cycle % 6 == 0)
					Dust.NewDustPerfect(NPC.Bottom, DustID.GoldCoin, new Vector2(0, -2), 0, default, 1.2f);
			}
			else {
				Counter++;
				if (Counter >= dashes)
					SetState(State.Hover);
			}
		}

		public bool CashOutTelegraph(out float y) { y = Argument; return CurrentState == State.CashOut && Timer - Counter * 70 is > 12 and < 34; }

		// ---- Wheel Barrage (phase 2+): rings of red and black chips with gaps.
		private void WheelBarrage()
		{
			NPC.velocity *= 0.92f;
			if (Timer == 10)
				SoundEngine.PlaySound(CasinoSounds.RouletteSpin, NPC.Center);
			int waves = Phase == 3 ? 5 : 4;
			if (Timer >= 30 && Counter < waves && (int)(Timer - 30) % 28 == 0) {
				Counter++;
				float start = Main.rand.NextFloat(MathHelper.TwoPi);
				int count = 22;
				int gap = Main.rand.Next(count);
				for (int k = 0; k < count; k++) {
					if (Math.Abs(k - gap) <= 1)
						continue;   // a safe gap in every ring
					Vector2 v = (start + k * MathHelper.TwoPi / count).ToRotationVector2() * 5f;
					Shoot(NPC.Center + new Vector2(0, 50), v, ModContent.ProjectileType<AllInChip>(), ChipDamage, k % 2);
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
			if (Timer == 10) {
				SoundEngine.PlaySound(SoundID.Coins with { Pitch = -0.4f, Volume = 1.3f }, NPC.Center);
				Say("Mods.CasinoWorld.Boss.PitBoss.Double", new Color(255, 220, 120));
				bool tails = Main.rand.NextBool(2);
				Shoot(Target.Center + new Vector2(0, -360f), Vector2.Zero, ModContent.ProjectileType<GiantCoin>(), CoinDamage, NPC.whoAmI, tails ? 1 : 0, Target.whoAmI);
			}
			if (Timer > 150)
				SetState(State.Hover);
		}

		/// <summary>Called by the giant coin when it lands tails: the boss is stunned and vulnerable.</summary>
		public void CoinTails()
		{
			if (CurrentState is State.Leave or State.Intro)
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
			if (NPC.life <= 0) {
				Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Main.rand.NextVector2Unit(), 14f, 6f, 40, 1500f, "PitBoss"));
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
			NPC.frame.Y = laugh ? frameHeight : 0;
			if (Phase != lastPhase && !NPC.IsABestiaryIconDummy) {
				lastPhase = Phase;
				transitionFlash = 60;
				if (!Main.dedServ) {
					SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f }, NPC.Center);
					SoundEngine.PlaySound(CasinoSounds.BossBell, NPC.Center);
					CasinoFx.Ring(NPC.Center, Phase == 3 ? new Color(255, 70, 60) : new Color(255, 210, 90), 36, 11f, 1.8f);
					BossTitle.Flash(Phase == 3 ? new Color(255, 70, 60) : new Color(255, 210, 90));
					Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Vector2.UnitY, 8f, 8f, 30, 1500f, "PitBossPhase"));
					CombatText.NewText(NPC.Hitbox, new Color(255, 210, 90), Language.GetTextValue(Phase == 3 ? "Mods.CasinoWorld.Boss.PitBoss.Phase3" : "Mods.CasinoWorld.Boss.PitBoss.Phase2"), true);
				}
			}
			if (transitionFlash > 0)
				transitionFlash--;
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
			bool stunned = CurrentState == State.Stunned;
			float tilt = stunned ? (float)Math.Sin(t * 6f) * 0.15f : NPC.velocity.X * 0.015f;
			Vector2 center = NPC.Center - screenPos + new Vector2(0, bob);

			// Telegraph for Cash Out: a gold line across the hall at the charge height.
			if (!NPC.IsABestiaryIconDummy && CashOutTelegraph(out float lineY)) {
				Color line = new Color(255, 200, 80) * (0.35f + 0.25f * (float)Math.Sin(t * 30));
				line.A = 0;
				spriteBatch.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value, new Rectangle(0, (int)(lineY - screenPos.Y) - 3, Main.screenWidth, 6), line);
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
			int wf = (int)(t * (WheelSpinning ? 24 : 8)) % 4;
			var wFrame = new Rectangle(0, wf * wheel.Height / 4, wheel.Width, wheel.Height / 4);
			if (WheelSpinning) {
				Color glow = new Color(255, 80, 80) * 0.35f;
				glow.A = 0;
				spriteBatch.Draw(wheel, center + new Vector2(0, 58), wFrame, glow, 0f, wFrame.Size() / 2f, 1.15f, SpriteEffects.None, 0f);
			}
			spriteBatch.Draw(wheel, center + new Vector2(0, 58), wFrame, light, 0f, wFrame.Size() / 2f, 1f, SpriteEffects.None, 0f);

			// Body.
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
				if (stunned)
					lift = (float)Math.Sin(t * 8 + side) * 12f;
				Vector2 at = center + new Vector2(side * 58f, 8f + lift + (float)Math.Sin(t * 3f + side) * 4f);
				var hFrame = new Rectangle(fist ? hand.Width / 2 : 0, 0, hand.Width / 2, hand.Height);
				spriteBatch.Draw(hand, at, hFrame, light, side * 0.2f, hFrame.Size() / 2f, 1f, side < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
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
