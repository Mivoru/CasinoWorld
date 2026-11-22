using CasinoWorld.Common;
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

		public override float SpawnChance(NPCSpawnInfo spawnInfo)
		{
			if (spawnInfo.Player.ZoneOverworldHeight && !Main.dayTime)
				return SpawnCondition.OverworldNightMonster.Chance * 0.12f;
			if (spawnInfo.Player.ZoneRockLayerHeight || spawnInfo.Player.ZoneDirtLayerHeight)
				return SpawnCondition.Cavern.Chance * 0.06f;
			return 0f;
		}

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 3, 1, 3));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LuckyCrate>(), 40));
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

	/// <summary>A marching playing card with a spear. Caves and night surface.</summary>
	public class CardSoldier : ModNPC
	{
		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 4;
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

		public override void FindFrame(int frameHeight)
		{
			if (NPC.velocity.Y != 0f) {
				NPC.frame.Y = frameHeight;
				return;
			}
			NPC.frameCounter += System.Math.Abs(NPC.velocity.X) * 1.6f;
			if (NPC.frameCounter >= 10) {
				NPC.frameCounter = 0;
				NPC.frame.Y = (NPC.frame.Y + frameHeight) % (frameHeight * Main.npcFrameCount[Type]);
			}
			NPC.spriteDirection = NPC.direction;
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
			NPC.damage = 70;
			NPC.defense = 30;
			NPC.lifeMax = 1200;
			NPC.knockBackResist = 0.1f;
			NPC.value = Item.buyPrice(gold: 2);
			NPC.HitSound = CasinoSounds.SlotMimicHit;
			NPC.DeathSound = CasinoSounds.SlotMimicDeath;
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
