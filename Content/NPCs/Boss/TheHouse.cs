using System;
using System.IO;
using CasinoWorld.Common.Games.Slots;
using CasinoWorld.Common.Net;
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
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.NPCs.Boss
{
	/// <summary>
	/// The House: a giant living slot machine with croupier hands (design/CasinoWorld/boss.md).
	///
	/// Phase 1: every few seconds the reels spin and the result picks the next attack, so the player
	/// can read what is coming. Hitting the lever while the reels spin forces a BUST: the machine is
	/// stunned and takes 50 % more damage. Phase 2 (below 50 % HP): the casing breaks open and reveals
	/// the Wheel of Fate, with Red or Black floor strikes, an orbiting croupier ball, chip rings and dice.
	/// Below 15 % HP it goes All In: everything is 30 % faster. Leaves at dawn.
	///
	/// AI runs on the server; state, timers and reel results are synced through ai[] and extra AI.
	/// </summary>
	[AutoloadBossHead]
	public class TheHouse : ModNPC
	{
		private enum State { Intro, Hover, Spin, Attack, Stunned, Transition, Leave }

		private enum Attack { CherryBombs, SourSpray, BellToll, GoldRush, LuckySevens, Shatter, Hands, RedOrBlack, CroupierBall, ChipOrbit, DiceStorm }

		public const int SpinTicks = 110;
		private const int StunTicks = 180;
		private const int TransitionTicks = 180;
		private const int HandRespawnTicks = 60 * 20;

		private State CurrentState { get => (State)NPC.ai[0]; set => NPC.ai[0] = (float)value; }
		private ref float Timer => ref NPC.ai[1];
		private Attack CurrentAttack { get => (Attack)NPC.ai[2]; set => NPC.ai[2] = (float)value; }
		public bool PhaseTwo { get => NPC.ai[3] >= 1f; private set => NPC.ai[3] = value ? 1f : 0f; }

		/// <summary>Reel result shown on the display (SlotSymbol values, 255 = bust).</summary>
		public readonly byte[] Reels = { 5, 5, 5 };
		private int nextSpinIn = 240;
		private int handRespawn;
		private int chosenColor;
		private float wheelAngle;

		public bool IsSpinning => CurrentState == State.Spin;
		public bool AllIn => NPC.life < NPC.lifeMax * 0.15f;
		private float Speed => AllIn ? 1.3f : 1f;

		private static Asset<Texture2D> glow, wheel, mask, maskGlow, symbols;

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 2;
			NPCID.Sets.MPAllowedEnemies[Type] = true;
			NPCID.Sets.BossBestiaryPriority.Add(Type);
			NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
			NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
		}

		public override void SetDefaults()
		{
			NPC.width = 124;
			NPC.height = 140;
			NPC.damage = 35;
			NPC.defense = 18;
			NPC.lifeMax = 7000;
			NPC.knockBackResist = 0f;
			NPC.boss = true;
			NPC.noGravity = true;
			NPC.noTileCollide = true;
			NPC.lavaImmune = true;
			NPC.npcSlots = 10f;
			NPC.aiStyle = -1;
			NPC.value = Item.buyPrice(gold: 8);
			NPC.HitSound = SoundID.NPCHit4;
			NPC.DeathSound = SoundID.NPCDeath14;
			Music = MusicID.Boss2;
		}

		public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
		{
			NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
			NPC.damage = (int)(NPC.damage * 0.8f);
		}

		public override void Unload()
		{
			glow = wheel = mask = maskGlow = symbols = null;
		}

		// ---- AI -------------------------------------------------------------------------------

		public override void AI()
		{
			if (NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead || !Main.player[NPC.target].active)
				NPC.TargetClosest();
			Player target = Main.player[NPC.target];

			wheelAngle += PhaseTwo ? 0.03f + (AllIn ? 0.03f : 0f) : 0f;
			Lighting.AddLight(NPC.Center, 0.6f, 0.35f, 0.5f);

			if ((Main.dayTime || target.dead) && CurrentState != State.Leave) {
				CurrentState = State.Leave;
				Timer = 0;
				if (Main.netMode != NetmodeID.MultiplayerClient)
					Broadcast("Mods.CasinoWorld.Boss.Leave", new Color(255, 120, 200));
			}

			if (Main.netMode != NetmodeID.MultiplayerClient) {
				MaintainHands();
				if (CurrentState is State.Hover or State.Attack or State.Stunned)
					CheckPhase();
			}

			switch (CurrentState) {
				case State.Intro: Intro(target); break;
				case State.Hover: Hover(target); break;
				case State.Spin: Spin(target); break;
				case State.Attack: DoAttack(target); break;
				case State.Stunned: Stunned(); break;
				case State.Transition: Transition(); break;
				case State.Leave: Leave(); break;
			}
		}

		private void Intro(Player target)
		{
			NPC.dontTakeDamage = true;
			Vector2 goal = target.Center + new Vector2(0, -260);
			NPC.velocity = (goal - NPC.Center) * 0.05f;
			if (++Timer == 1 && Main.netMode != NetmodeID.MultiplayerClient)
				Broadcast("Mods.CasinoWorld.Boss.Intro", new Color(255, 200, 80));
			if (Timer >= 120) {
				NPC.dontTakeDamage = false;
				SetState(State.Hover);
			}
		}

		private void Hover(Player target)
		{
			Vector2 goal;
			if (!PhaseTwo) {
				goal = target.Center + new Vector2((float)Math.Sin(Main.GlobalTimeWrappedHourly * 0.8f) * 220f, -280);
			}
			else {
				float a = Main.GlobalTimeWrappedHourly * 0.6f;
				goal = target.Center + new Vector2((float)Math.Cos(a) * 340f, -220 + (float)Math.Sin(a * 2) * 60f);
			}
			Vector2 toGoal = goal - NPC.Center;
			NPC.velocity = Vector2.Lerp(NPC.velocity, toGoal.SafeNormalize(Vector2.Zero) * Math.Min(10f, toGoal.Length() * 0.06f), 0.08f);

			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			if (++Timer >= nextSpinIn / Speed) {
				if (!PhaseTwo)
					StartSpin();
				else
					StartAttack(PickPhaseTwoAttack());
			}
		}

		private void StartSpin()
		{
			// 70 %: three of a kind (weighted like the casino reels), 30 %: a mix that sends the hands in.
			if (Main.rand.NextFloat() < 0.7f) {
				byte s = (byte)SlotRules.Roll(Main.rand);
				Reels[0] = Reels[1] = Reels[2] = s;
			}
			else {
				do {
					for (int k = 0; k < 3; k++)
						Reels[k] = (byte)SlotRules.Roll(Main.rand);
				} while (Reels[0] == Reels[1] && Reels[1] == Reels[2]);
			}
			SetState(State.Spin);
			SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.4f }, NPC.Center);
		}

		private void Spin(Player target)
		{
			NPC.velocity *= 0.9f;
			Timer++;
			if (Timer % 8 == 0 && Timer < SpinTicks - 10)
				SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f, Pitch = -0.3f }, NPC.Center);
			if (Timer == 60 || Timer == 85 || Timer == SpinTicks)
				SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.2f }, NPC.Center);

			if (Main.netMode == NetmodeID.MultiplayerClient || Timer < SpinTicks)
				return;

			if (Reels[0] == Reels[1] && Reels[1] == Reels[2])
				StartAttack((Attack)Reels[0]);   // symbol order matches the first six attacks
			else
				StartAttack(Attack.Hands);
		}

		/// <summary>Lever hit during a spin: the reels jam on BUST.</summary>
		public void TriggerBust()
		{
			if (CurrentState != State.Spin)
				return;
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				CasinoNet.SendHouseLeverHit(Mod, NPC.whoAmI);
				return;
			}
			Reels[0] = Reels[1] = Reels[2] = 255;
			SetState(State.Stunned);
			CombatText.NewText(NPC.Hitbox, Color.OrangeRed, Language.GetTextValue("Mods.CasinoWorld.Boss.Bust"), dramatic: true);
			SoundEngine.PlaySound(SoundID.Item62, NPC.Center);
		}

		private void Stunned()
		{
			NPC.velocity *= 0.92f;
			NPC.velocity.Y += 0.05f;
			if (Main.rand.NextBool(3))
				Dust.NewDust(NPC.position, NPC.width, NPC.height / 2, DustID.Electric, Scale: 0.8f);
			if (++Timer >= StunTicks && Main.netMode != NetmodeID.MultiplayerClient)
				SetState(State.Hover);
		}

		private void Transition()
		{
			NPC.dontTakeDamage = true;
			NPC.velocity *= 0.9f;
			Timer++;
			if (Timer % 10 == 0)
				SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.5f, Pitch = Timer / 180f }, NPC.Center);
			for (int k = 0; k < 3; k++)
				Dust.NewDust(NPC.position, NPC.width, NPC.height, k == 0 ? DustID.GoldCoin : DustID.Electric, Main.rand.NextFloat(-6, 6), Main.rand.NextFloat(-6, 2));
			if (Timer == TransitionTicks / 2) {
				PhaseTwo = true;
				Music = MusicID.Boss3;
				SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
				Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Vector2.UnitY, 14f, 8f, 40, 2000f, "TheHouse"));
				for (int k = 0; k < 60; k++)
					Dust.NewDust(NPC.Center, 0, 0, DustID.GoldCoin, Main.rand.NextFloat(-10, 10), Main.rand.NextFloat(-10, 10), Scale: 1.6f);
				if (Main.netMode != NetmodeID.MultiplayerClient)
					Broadcast("Mods.CasinoWorld.Boss.PhaseTwo", new Color(255, 80, 120));
			}
			if (Timer >= TransitionTicks && Main.netMode != NetmodeID.MultiplayerClient) {
				NPC.dontTakeDamage = false;
				SetState(State.Hover);
			}
		}

		private void Leave()
		{
			NPC.dontTakeDamage = true;
			NPC.velocity.Y -= 0.4f;
			NPC.EncourageDespawn(10);
		}

		private Attack PickPhaseTwoAttack()
		{
			Attack[] pool = { Attack.RedOrBlack, Attack.CroupierBall, Attack.ChipOrbit, Attack.DiceStorm, Attack.Hands, Attack.Shatter };
			Attack pick;
			do {
				pick = pool[Main.rand.Next(pool.Length)];
			} while (pick == CurrentAttack && Main.rand.NextBool(3, 4));
			return pick;
		}

		private void StartAttack(Attack attack)
		{
			CurrentAttack = attack;
			if (attack == Attack.RedOrBlack)
				chosenColor = Main.rand.Next(2);
			SetState(State.Attack);
		}

		private void DoAttack(Player target)
		{
			NPC.velocity *= 0.94f;
			Timer++;
			int t = (int)Timer;
			bool server = Main.netMode != NetmodeID.MultiplayerClient;
			var source = NPC.GetSource_FromAI();
			Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
			int duration = 120;

			switch (CurrentAttack) {
				case Attack.CherryBombs:
					duration = 150;
					if (server && t % 20 == 0 && t <= 120)
						Projectile.NewProjectile(source, NPC.Center + new Vector2(0, 40), new Vector2(Main.rand.NextFloat(-7, 7), Main.rand.NextFloat(-9, -5)),
							ModContent.ProjectileType<CherryBomb>(), Damage(30, 50), 2f);
					break;

				case Attack.SourSpray:
					duration = 100;
					if (server && t % 4 == 0 && t <= 72) {
						Vector2 v = toPlayer.RotatedByRandom(0.35f) * Main.rand.NextFloat(9f, 13f) * Speed;
						Projectile.NewProjectile(source, NPC.Center + new Vector2(0, 30), v, ModContent.ProjectileType<SourDrop>(), Damage(22, 38), 1f);
					}
					break;

				case Attack.BellToll:
					duration = 200;
					if (server && (t == 20 || t == 75 || t == 130)) {
						float gap = toPlayer.ToRotation() + Main.rand.NextFloat(-1.2f, 1.2f);
						Projectile.NewProjectile(source, NPC.Center, Vector2.Zero, ModContent.ProjectileType<BellWave>(), Damage(28, 46), 0f, -1, gap);
						SoundEngine.PlaySound(SoundID.Item35 with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);
					}
					break;

				case Attack.GoldRush:
					duration = 170;
					if (server && (t == 10 || t == 70)) {
						int offset = Main.rand.Next(2);
						for (int k = -5; k <= 5; k++) {
							if ((k + offset) % 2 != 0)
								continue;
							float x = target.Center.X + k * 80 + Main.rand.Next(-10, 10);
							Projectile.NewProjectile(source, new Vector2(x, target.Center.Y - 640), Vector2.Zero, ModContent.ProjectileType<GoldRushMarker>(), Damage(32, 52), 3f);
						}
					}
					break;

				case Attack.LuckySevens:
					duration = 90;
					if (server && t == 20) {
						for (int k = 0; k < 3; k++)
							Projectile.NewProjectile(source, NPC.Center, toPlayer.RotatedBy((k - 1) * 0.8f) * 6f, ModContent.ProjectileType<LuckySeven>(), Damage(30, 48), 2f);
					}
					break;

				case Attack.Shatter:
					duration = 110;
					if (server && (t == 20 || t == 60)) {
						int count = PhaseTwo ? 20 : 16;
						float start = t == 60 ? MathHelper.Pi / count : 0f;
						for (int k = 0; k < count; k++)
							Projectile.NewProjectile(source, NPC.Center, Vector2.UnitX.RotatedBy(start + MathHelper.TwoPi * k / count) * 7f * Speed,
								ModContent.ProjectileType<DiamondShard>(), Damage(24, 40), 1f);
						SoundEngine.PlaySound(SoundID.Item27, NPC.Center);
					}
					break;

				case Attack.Hands:
					duration = 200;
					if (server && t == 1)
						CommandHands(HouseHand.Barrage);
					break;

				case Attack.RedOrBlack:
					duration = 170;
					if (server && t == 1) {
						for (int k = -4; k < 4; k++) {
							float x = target.Center.X + k * RouletteStripe.StripeWidth + RouletteStripe.StripeWidth / 2f;
							Projectile.NewProjectile(source, new Vector2(x, target.Center.Y), Vector2.Zero, ModContent.ProjectileType<RouletteStripe>(),
								Damage(45, 70), 0f, -1, (k + 4) % 2, chosenColor);
						}
						SoundEngine.PlaySound(SoundID.Item35 with { Pitch = 0.3f }, NPC.Center);
					}
					if (AllIn && server && t == 85) {
						for (int k = -4; k < 4; k++) {
							float x = target.Center.X + k * RouletteStripe.StripeWidth + RouletteStripe.StripeWidth / 2f;
							Projectile.NewProjectile(source, new Vector2(x, target.Center.Y), Vector2.Zero, ModContent.ProjectileType<RouletteStripe>(),
								Damage(45, 70), 0f, -1, (k + 4) % 2, 1 - chosenColor);
						}
					}
					break;

				case Attack.CroupierBall:
					duration = 200;
					if (server && t == 1)
						Projectile.NewProjectile(source, NPC.Center, Vector2.Zero, ModContent.ProjectileType<CroupierBall>(), Damage(36, 58), 4f, -1, NPC.whoAmI);
					break;

				case Attack.ChipOrbit:
					duration = 220;
					if (server && t == 1) {
						int count = 12;
						for (int k = 0; k < count; k++)
							Projectile.NewProjectile(source, NPC.Center, Vector2.Zero, ModContent.ProjectileType<OrbitChip>(), Damage(26, 42), 1f, -1,
								MathHelper.TwoPi * k / count, NPC.whoAmI);
					}
					break;

				case Attack.DiceStorm:
					duration = 160;
					if (server && t % 25 == 0 && t <= 100) {
						Vector2 pos = target.Center + new Vector2(Main.rand.Next(-300, 300), -520);
						Projectile.NewProjectile(source, pos, new Vector2(0, 2), ModContent.ProjectileType<BossDie>(), Damage(26, 42), 2f, -1, Main.rand.Next(1, 7));
					}
					break;
			}

			if (Timer >= duration / Speed && server)
				SetState(State.Hover);
		}

		private int Damage(int normal, int expert) => NPC.GetAttackDamage_ForProjectiles(normal, expert);

		// ---- Hands ----------------------------------------------------------------------------

		private int FindHand(int side)
		{
			foreach (NPC npc in Main.ActiveNPCs) {
				if (npc.type == ModContent.NPCType<HouseHand>() && (int)npc.ai[0] == NPC.whoAmI && (int)npc.ai[1] == side)
					return npc.whoAmI;
			}
			return -1;
		}

		public bool HandsAlive => FindHand(-1) >= 0 || FindHand(1) >= 0;

		private void MaintainHands()
		{
			if (CurrentState == State.Intro || CurrentState == State.Leave)
				return;
			bool missing = FindHand(-1) < 0 || FindHand(1) < 0;
			if (!missing) {
				handRespawn = 0;
				return;
			}
			bool firstTime = Timer < 2 && CurrentState == State.Hover && handRespawn == 0 && NPC.life == NPC.lifeMax;
			if (firstTime || ++handRespawn >= HandRespawnTicks) {
				handRespawn = 0;
				foreach (int side in new[] { -1, 1 }) {
					if (FindHand(side) < 0)
						NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X + side * 170, (int)NPC.Center.Y + 40, ModContent.NPCType<HouseHand>(), NPC.whoAmI, NPC.whoAmI, side);
				}
			}
		}

		private void CommandHands(int command)
		{
			foreach (int side in new[] { -1, 1 }) {
				int index = FindHand(side);
				if (index < 0)
					continue;
				NPC hand = Main.npc[index];
				hand.ai[2] = command;
				hand.ai[3] = side == 1 ? -40 : 0;   // stagger the two hands
				hand.netUpdate = true;
			}
		}

		// ---- Damage, phases, loot ---------------------------------------------------------------

		public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
		{
			if (CurrentState == State.Stunned)
				modifiers.FinalDamage *= 1.5f;
			if (!HandsAlive)
				modifiers.FinalDamage *= 1.25f;
		}

		public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			if (IsSpinning && projectile.Hitbox.Intersects(LeverRect))
				TriggerBust();
			CheckPhase();
		}

		public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
		{
			Rectangle reach = LeverRect;
			reach.Inflate(48, 48);
			if (IsSpinning && reach.Contains(player.Center.ToPoint()))
				TriggerBust();
			CheckPhase();
		}

		/// <summary>The lever on the machine's right side, in world pixels.</summary>
		public Rectangle LeverRect => new((int)NPC.Center.X + 44, (int)NPC.Center.Y - 62, 22, 70);

		private void CheckPhase()
		{
			if (!PhaseTwo && CurrentState != State.Transition && NPC.life < NPC.lifeMax / 2 && Main.netMode != NetmodeID.MultiplayerClient)
				SetState(State.Transition);
		}

		private void SetState(State state)
		{
			CurrentState = state;
			Timer = 0;
			if (state == State.Hover)
				nextSpinIn = PhaseTwo ? Main.rand.Next(80, 140) : Main.rand.Next(150, 230);
			NPC.netUpdate = true;
		}

		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.Write(Reels);
			writer.Write((byte)chosenColor);
			writer.Write((short)nextSpinIn);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			reader.Read(Reels, 0, 3);
			chosenColor = reader.ReadByte();
			nextSpinIn = reader.ReadInt16();
		}

		public override void OnKill()
		{
			NPC.SetEventFlagCleared(ref CasinoDowned.DownedTheHouse, -1);
			if (Main.netMode != NetmodeID.MultiplayerClient)
				Broadcast("Mods.CasinoWorld.Boss.Defeat", new Color(255, 200, 80));
		}

		public override void BossLoot(ref int potionType)
		{
			potionType = ItemID.HealingPotion;
		}

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<TheHouseBag>()));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.Vanity.TheHouseTrophy>(), 10));
			npcLoot.Add(ItemDropRule.MasterModeCommonDrop(ModContent.ItemType<Items.Vanity.TheHouseRelicItem>()));
			npcLoot.Add(ItemDropRule.MasterModeDropOnAllPlayers(ModContent.ItemType<Items.Vanity.MiniSlotItem>(), 4));
			var normal = new LeadingConditionRule(new Conditions.NotExpert());
			normal.OnSuccess(ItemDropRule.OneFromOptions(1, ModContent.ItemType<JackpotLever>(), ModContent.ItemType<DealersHand>(), ModContent.ItemType<WheelOfFortune>()));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 1, 80, 120));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<LuckyCrate>(), 1, 2, 3));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<GoldenChip>(), 3));
			normal.OnSuccess(ItemDropRule.Common(ModContent.ItemType<Items.Vanity.TheHouseMask>(), 7));
			npcLoot.Add(normal);
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Times.NightTime,
				new FlavorTextBestiaryInfoElement("Mods.CasinoWorld.Bestiary.TheHouse"),
			});
		}

		private static void Broadcast(string key, Color color) => ChatHelper.BroadcastChatMessage(NetworkText.FromKey(key), color);

		// ---- Drawing ------------------------------------------------------------------------------

		public override void FindFrame(int frameHeight)
		{
			NPC.frame.Y = CurrentState == State.Stunned ? frameHeight : 0;
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			Vector2 center = NPC.Center - screenPos + new Vector2(0, NPC.gfxOffY);
			Color lit = NPC.GetAlpha(Color.Lerp(drawColor, Color.White, 0.35f));
			float shake = CurrentState == State.Transition ? Main.rand.NextFloat(-3, 3) : 0f;
			center.X += shake;

			if (!PhaseTwo) {
				Texture2D tex = Terraria.GameContent.TextureAssets.Npc[Type].Value;
				Rectangle frame = NPC.frame;
				Vector2 origin = frame.Size() / 2f;
				spriteBatch.Draw(tex, center, frame, lit, 0f, origin, 1f, SpriteEffects.None, 0f);
				glow ??= ModContent.Request<Texture2D>(Texture + "_Glow");
				spriteBatch.Draw(glow.Value, center, frame, Color.White, 0f, origin, 1f, SpriteEffects.None, 0f);
				DrawReels(spriteBatch, center);
			}
			else {
				wheel ??= ModContent.Request<Texture2D>("CasinoWorld/Content/NPCs/Boss/TheHouseWheel");
				mask ??= ModContent.Request<Texture2D>("CasinoWorld/Content/NPCs/Boss/TheHouseMask");
				maskGlow ??= ModContent.Request<Texture2D>("CasinoWorld/Content/NPCs/Boss/TheHouseMask_Glow");
				Texture2D w = wheel.Value, m = mask.Value;
				Color wheelColor = AllIn ? Color.Lerp(lit, Color.Red, 0.35f) : lit;
				spriteBatch.Draw(w, center, null, wheelColor, wheelAngle, w.Size() / 2f, 1f, SpriteEffects.None, 0f);
				spriteBatch.Draw(m, center, null, lit, 0f, m.Size() / 2f, 1f, SpriteEffects.None, 0f);
				spriteBatch.Draw(maskGlow.Value, center, null, Color.White, 0f, m.Size() / 2f, 1f, SpriteEffects.None, 0f);
			}
			return false;
		}

		/// <summary>Draws the three reel symbols on the display (spinning, stopped or BUST).</summary>
		private void DrawReels(SpriteBatch spriteBatch, Vector2 center)
		{
			symbols ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/SlotSymbols");
			Texture2D tex = symbols.Value;
			const float scale = 0.34f;
			int[] stops = { 60, 85, SpinTicks };
			for (int k = 0; k < 3; k++) {
				Vector2 pos = center + new Vector2((k - 1) * 23, -11);
				bool spinning = CurrentState == State.Spin && Timer < stops[k];
				if (Reels[k] == 255) {
					Utils.DrawBorderString(spriteBatch, "X", pos, Color.OrangeRed, 1f, 0.5f, 0.5f);
					continue;
				}
				int symbol = spinning ? (int)(Main.GlobalTimeWrappedHourly * 20f + k * 3) % SlotRules.SymbolCount : Reels[k];
				float jitter = spinning ? (float)Math.Sin(Main.GlobalTimeWrappedHourly * 60f + k) * 3f : 0f;
				spriteBatch.Draw(tex, pos + new Vector2(0, jitter), new Rectangle(symbol * 64, 0, 64, 64), Color.White, 0f, new Vector2(32), scale, SpriteEffects.None, 0f);
			}
		}
	}

	/// <summary>Remembers whether The House has been defeated in this world.</summary>
	public class CasinoDowned : ModSystem
	{
		public static bool DownedTheHouse;

		public override void ClearWorld() => DownedTheHouse = false;

		public override void SaveWorldData(Terraria.ModLoader.IO.TagCompound tag)
		{
			if (DownedTheHouse)
				tag["downedTheHouse"] = true;
		}

		public override void LoadWorldData(Terraria.ModLoader.IO.TagCompound tag) => DownedTheHouse = tag.ContainsKey("downedTheHouse");

		public override void NetSend(BinaryWriter writer) => writer.Write(DownedTheHouse);

		public override void NetReceive(BinaryReader reader) => DownedTheHouse = reader.ReadBoolean();

		public override void PostSetupContent()
		{
			// Boss Checklist entry (between Skeletron 5.0 and the Wall of Flesh 6.0).
			if (!ModLoader.TryGetMod("BossChecklist", out Mod checklist))
				return;
			checklist.Call("LogBoss", Mod, nameof(TheHouse), 5.5f, () => DownedTheHouse, ModContent.NPCType<TheHouse>(),
				new System.Collections.Generic.Dictionary<string, object> {
					["spawnItems"] = ModContent.ItemType<GoldenChip>(),
				});
		}
	}
}
