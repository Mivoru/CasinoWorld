using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using CasinoWorld.Content.Projectiles.Boss;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.Crates;
using CasinoWorld.Content.Items.Weapons;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Utilities;

namespace CasinoWorld.Content.NPCs.Enemies
{
	// Casino-themed enemies. Stats are first-pass numbers placed next to vanilla enemies of the
	// same stage (Chip Slime ~ Spiked Slime, Card Soldier ~ Skeleton, Slot Mimic ~ Mimic).

	/// <summary>A hopping poker chip. Night surface and caves, any stage.</summary>
	public class ChipSlime : ModNPC
	{
		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 2;
		}

		public override void SetDefaults()
		{
			NPC.width = 28;
			NPC.height = 28;
			NPC.aiStyle = NPCAIStyleID.Slime;
			AIType = NPCID.BlueSlime;
			AnimationType = NPCID.BlueSlime;
			NPC.damage = 14;
			NPC.defense = 4;
			NPC.lifeMax = 45;
			NPC.knockBackResist = 0.8f;
			NPC.value = Item.buyPrice(silver: 2);
			NPC.HitSound = CasinoSounds.ChipSlimeHit;
			NPC.DeathSound = CasinoSounds.ChipSlimeDeath;
		}

		/// <summary>1 in 20 is a golden chip slime: twice the life, glows and sparkles, drops far more.</summary>
		public bool Golden;

		public override void SendExtraAI(System.IO.BinaryWriter writer) => writer.Write(Golden);

		public override void ReceiveExtraAI(System.IO.BinaryReader reader) => Golden = reader.ReadBoolean();

		public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient && Main.rand.NextBool(20)) {
				Golden = true;
				NPC.lifeMax *= 2;
				NPC.life = NPC.lifeMax;
				NPC.value *= 10;
				NPC.netUpdate = true;
			}
		}

		public override void AI()
		{
			if (Golden) {
				Lighting.AddLight(NPC.Center, 0.5f, 0.4f, 0.1f);
				if (Main.rand.NextBool(6))
					CasinoFx.Spark(NPC.Center + Main.rand.NextVector2Circular(14, 10), new Vector2(0, -0.5f), new Color(255, 220, 90), 0.8f);
			}
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (Golden)
				NPC.color = new Color(255, 215, 90);
			return true;
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			int jelly = Golden ? DustID.GoldCoin : DustID.t_Slime;
			for (int k = 0; k < (NPC.life <= 0 ? 16 : 4); k++) {
				Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, jelly, hit.HitDirection * 2f, -1.5f, 120, Golden ? default : new Color(230, 70, 90), 1.1f);
				d.noGravity = Main.rand.NextBool();
			}
			if (NPC.life <= 0) {
				CasinoFx.ChipImpact(NPC.Center, Golden ? new Color(255, 220, 90) : new Color(230, 80, 90));
				if (Golden)
					CasinoFx.Ring(NPC.Center, new Color(255, 220, 90), 16, 4f, 1.2f);
			}
		}

		public override float SpawnChance(NPCSpawnInfo spawnInfo)
		{
			float stage = Main.hardMode ? 0.5f : 1f;   // an early-game pest, rarer once harder enemies take over
			if (spawnInfo.Player.ZoneOverworldHeight && !Main.dayTime)
				return SpawnCondition.OverworldNightMonster.Chance * 0.12f * stage;
			if (spawnInfo.Player.ZoneRockLayerHeight || spawnInfo.Player.ZoneDirtLayerHeight)
				return SpawnCondition.Cavern.Chance * 0.06f * stage;
			return 0f;
		}

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			var golden = new LeadingConditionRule(new GoldenChipCondition());
			golden.OnSuccess(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 1, 8, 15));
			golden.OnSuccess(ItemDropRule.Common(ModContent.ItemType<LuckyCrate>(), 3));
			golden.OnFailedConditions(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 3, 1, 3));
			golden.OnFailedConditions(ItemDropRule.Common(ModContent.ItemType<LuckyCrate>(), 40));
			npcLoot.Add(golden);
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Times.NightTime,
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Caverns,
				new FlavorTextBestiaryInfoElement("Mods.CasinoWorld.Bestiary.ChipSlime"),
			});
		}
	}

	/// <summary>Loot condition: the chip slime that died was golden.</summary>
	public class GoldenChipCondition : IItemDropRuleCondition
	{
		public bool CanDrop(DropAttemptInfo info) => info.npc?.ModNPC is ChipSlime slime && slime.Golden;
		public bool CanShowItemDropInUI() => false;
		public string GetConditionDescription() => null;
	}

	/// <summary>A marching playing card with a spear. Caves and night surface.</summary>
	public class CardSoldier : ModNPC
	{
		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 5;
		}

		public override void SetDefaults()
		{
			NPC.width = 24;
			NPC.height = 44;
			NPC.aiStyle = NPCAIStyleID.Fighter;
			AIType = NPCID.Skeleton;
			NPC.damage = 20;
			NPC.defense = 8;
			NPC.lifeMax = 80;
			NPC.knockBackResist = 0.5f;
			NPC.value = Item.buyPrice(silver: 5);
			NPC.HitSound = CasinoSounds.CardSoldierHit;
			NPC.DeathSound = CasinoSounds.CardSoldierDeath;
		}

		public override void PostAI()
		{
			// Spear lunge: on the ground and close to the target, a quick hop forward every two seconds.
			if (!NPC.HasValidTarget || NPC.velocity.Y != 0f)
				return;
			Player target = Main.player[NPC.target];
			if (++NPC.localAI[0] >= 120 && NPC.Distance(target.Center) < 130f) {
				NPC.localAI[0] = 0;
				NPC.velocity.X = System.Math.Sign(target.Center.X - NPC.Center.X) * 6.5f;
				NPC.velocity.Y = -3f;
				SoundEngine.PlaySound(CasinoSounds.CardThrow with { Pitch = -0.3f }, NPC.Center);
			}
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			int count = NPC.life <= 0 ? 14 : 3;
			for (int k = 0; k < count; k++)
				Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(10, 20), CasinoFx.ShardType, new Vector2(hit.HitDirection * 2f, -2f) + Main.rand.NextVector2Circular(1.5f, 1.5f), 0, k % 3 == 0 ? new Color(220, 40, 50) : Color.White);
		}

		public override void FindFrame(int frameHeight)
		{
			// Frames 0-3 walk, 4 is the spear thrust (used while airborne or right next to the target).
			NPC.spriteDirection = -NPC.direction;   // the sprite faces right
			bool close = NPC.HasValidTarget && NPC.Distance(Main.player[NPC.target].Center) < 60f;
			if (NPC.velocity.Y != 0f || close) {
				NPC.frame.Y = frameHeight * 4;
				return;
			}
			NPC.frameCounter += System.Math.Abs(NPC.velocity.X) * 1.6f;
			if (NPC.frameCounter >= 10) {
				NPC.frameCounter = 0;
				NPC.frame.Y = (NPC.frame.Y / frameHeight + 1) % 4 * frameHeight;
			}
		}

		public override float SpawnChance(NPCSpawnInfo spawnInfo)
		{
			if (spawnInfo.Player.ZoneRockLayerHeight)
				return SpawnCondition.Cavern.Chance * 0.08f;
			if (spawnInfo.Player.ZoneOverworldHeight && !Main.dayTime)
				return SpawnCondition.OverworldNightMonster.Chance * 0.05f;
			return 0f;
		}

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 2, 2, 5));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LuckyCrate>(), 25));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<CardDeck>(), 80));
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Caverns,
				new FlavorTextBestiaryInfoElement("Mods.CasinoWorld.Bestiary.CardSoldier"),
			});
		}
	}

	/// <summary>Hardmode mimic disguised as a slot machine. Rare in the caverns; drops High Roller Crates.</summary>
	public class SlotMimic : ModNPC
	{
		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 6;
		}

		public override void SetDefaults()
		{
			NPC.width = 40;
			NPC.height = 44;
			NPC.aiStyle = NPCAIStyleID.Mimic;
			AIType = NPCID.Mimic;
			NPC.damage = 80;
			NPC.defense = 30;
			NPC.lifeMax = 700;
			NPC.knockBackResist = 0.1f;
			NPC.value = Item.buyPrice(gold: 2);
			NPC.HitSound = CasinoSounds.SlotMimicHit;
			NPC.DeathSound = CasinoSounds.SlotMimicDeath;
		}

		public override void PostAI()
		{
			// Once awake, every three seconds it pulls its own lever and sprays a fan of hot chips.
			if (NPC.ai[0] == 0f || !NPC.HasValidTarget)
				return;
			if (++NPC.localAI[0] == 160)
				SoundEngine.PlaySound(CasinoSounds.LeverPull, NPC.Center);
			if (NPC.localAI[0] >= 180) {
				NPC.localAI[0] = 0;
				SoundEngine.PlaySound(CasinoSounds.Jackpot with { Volume = 0.5f, Pitch = 0.3f }, NPC.Center);
				CasinoFx.Ring(NPC.Top, new Color(255, 220, 90), 10, 3f);
				if (Main.netMode != NetmodeID.MultiplayerClient) {
					Player target = Main.player[NPC.target];
					Vector2 aim = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
					int damage = Main.expertMode ? 24 : 32;
					for (int k = -2; k <= 2; k++)
						Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Top, aim.RotatedBy(k * 0.2f) * 6f - Vector2.UnitY * 2f, ModContent.ProjectileType<AllInChip>(), damage, 1f, Main.myPlayer, k % 2 == 0 ? 0 : 1);
				}
			}
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			for (int k = 0; k < (NPC.life <= 0 ? 30 : 4); k++)
				Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldCoin, hit.HitDirection * 2f, -2f);
			if (NPC.life <= 0)
				CasinoFx.Explosion(NPC.Center, 90, new Color(255, 210, 80), new Color(200, 120, 255), 2f);
		}

		public override void FindFrame(int frameHeight)
		{
			// Still and on the ground: keep the disguise. Otherwise chomp through frames 1..5.
			if (NPC.ai[0] == 0f && NPC.velocity == Microsoft.Xna.Framework.Vector2.Zero) {
				NPC.frame.Y = 0;
				return;
			}
			NPC.frameCounter++;
			if (NPC.frameCounter >= 6) {
				NPC.frameCounter = 0;
				int frame = NPC.frame.Y / frameHeight + 1;
				if (frame > 5)
					frame = 1;
				NPC.frame.Y = frame * frameHeight;
			}
			NPC.spriteDirection = NPC.direction;
		}

		public override float SpawnChance(NPCSpawnInfo spawnInfo)
			=> Main.hardMode && spawnInfo.Player.ZoneRockLayerHeight ? SpawnCondition.Cavern.Chance * 0.015f : 0f;

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<HighRollerCrate>(), 2));
			npcLoot.Add(ItemDropRule.OneFromOptions(6,
				ModContent.ItemType<SnakeEyes>(), ModContent.ItemType<RoyalFlush>(), ModContent.ItemType<JackpotCannon>()));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 1, 20, 40));
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Caverns,
				new FlavorTextBestiaryInfoElement("Mods.CasinoWorld.Bestiary.SlotMimic"),
			});
		}
	}
}
