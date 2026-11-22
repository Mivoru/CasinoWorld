using System;
using CasinoWorld.Common;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.Crates;
using CasinoWorld.Content.Items.Weapons;
using CasinoWorld.Content.Projectiles.Boss;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Utilities;

namespace CasinoWorld.Content.NPCs.Enemies
{
	/// <summary>
	/// Pre-hardmode night zombie: a gambler who lost everything, in a torn tuxedo. Drawn with the vanilla
	/// player renderer (green skin, ragged suit), so it animates exactly like vanilla zombies.
	/// </summary>
	public class BrokeGambler : ModNPC
	{
		public override string Texture => "Terraria/Images/NPC_" + NPCID.Zombie;

		private PersonDoll doll;
		private PersonLook look;

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Zombie];
		}

		public override void SetDefaults()
		{
			NPC.CloneDefaults(NPCID.Zombie);
			NPC.lifeMax = 55;
			NPC.damage = 16;
			NPC.defense = 6;
			NPC.value = Item.buyPrice(silver: 3);
			AIType = NPCID.Zombie;
			AnimationType = NPCID.Zombie;
			Banner = 0;
		}

		public override float SpawnChance(NPCSpawnInfo spawnInfo)
			=> spawnInfo.Player.ZoneOverworldHeight && !Main.dayTime && !spawnInfo.Player.ZoneTowerSolar ? SpawnCondition.OverworldNightMonster.Chance * 0.18f : 0f;

		public override void AI()
		{
			if (!Main.dedServ && Main.rand.NextBool(1400))
				CombatText.NewText(NPC.Hitbox, new Color(160, 200, 150), Language.GetTextValue($"Mods.CasinoWorld.NPCs.BrokeGambler.Moan.{Main.rand.Next(5)}"));
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (NPC.IsABestiaryIconDummy)
				return true;
			look ??= new PersonLook {
				Hair = 2 + NPC.whoAmI % 12, SkinVariant = NPC.whoAmI % 4, Skin = new Color(120, 170, 110), HairColor = new Color(60, 60, 50),
				Eyes = new Color(200, 30, 30), Shirt = new Color(30, 30, 35), UnderShirt = new Color(170, 165, 140),
				Pants = new Color(35, 35, 40), Shoes = new Color(60, 40, 30),
			};
			try {
				doll ??= new PersonDoll();
				doll.BodyPose = PersonDoll.ArmForward;   // zombie arms
				doll.LegPose = -1;
				doll.Draw(NPC, look, NPC.Bottom + new Vector2(0, NPC.gfxOffY));
				return false;
			}
			catch {
				return true;
			}
		}

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 2, 1, 4));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LuckyCrate>(), 40));
			npcLoot.Add(ItemDropRule.Common(ItemID.Sunglasses, 60));
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Times.NightTime,
				new FlavorTextBestiaryInfoElement("Mods.CasinoWorld.Bestiary.BrokeGambler"),
			});
		}
	}

	/// <summary>Pre-hardmode cave bat carrying a gold coin; drops extra money.</summary>
	public class LuckyBat : ModNPC
	{
		public override void SetStaticDefaults() => Main.npcFrameCount[Type] = 4;

		public override void SetDefaults()
		{
			NPC.width = 26;
			NPC.height = 18;
			NPC.aiStyle = NPCAIStyleID.Bat;
			AIType = NPCID.CaveBat;
			NPC.lifeMax = 30;
			NPC.damage = 14;
			NPC.defense = 4;
			NPC.knockBackResist = 0.8f;
			NPC.value = Item.buyPrice(silver: 25);
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath4;
		}

		public override void FindFrame(int frameHeight)
		{
			NPC.spriteDirection = NPC.direction;
			if (++NPC.frameCounter >= 5) {
				NPC.frameCounter = 0;
				NPC.frame.Y = (NPC.frame.Y + frameHeight) % (frameHeight * 4);
			}
		}

		public override float SpawnChance(NPCSpawnInfo spawnInfo)
			=> spawnInfo.Player.ZoneRockLayerHeight && !spawnInfo.Player.ZoneDungeon ? SpawnCondition.Cavern.Chance * 0.07f : 0f;

		public override void HitEffect(NPC.HitInfo hit)
		{
			if (NPC.life <= 0)
				for (int k = 0; k < 6; k++)
					Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldCoin);
		}

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			npcLoot.Add(ItemDropRule.Common(ItemID.GoldCoin, 3, 1, 2));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LuckyCrate>(), 60));
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Caverns,
				new FlavorTextBestiaryInfoElement("Mods.CasinoWorld.Bestiary.LuckyBat"),
			});
		}
	}

	/// <summary>Hardmode underground knight of spades: card shield, throws spade blades.</summary>
	public class RoyalGuard : ModNPC
	{
		public override void SetStaticDefaults() => Main.npcFrameCount[Type] = 5;

		public override void SetDefaults()
		{
			NPC.width = 30;
			NPC.height = 52;
			NPC.aiStyle = NPCAIStyleID.Fighter;
			AIType = NPCID.ArmoredSkeleton;
			NPC.lifeMax = 420;
			NPC.damage = 60;
			NPC.defense = 30;
			NPC.knockBackResist = 0.25f;
			NPC.value = Item.buyPrice(silver: 60);
			NPC.HitSound = SoundID.NPCHit4;
			NPC.DeathSound = CasinoSounds.CardSoldierDeath;
		}

		public override void PostAI()
		{
			// Every few seconds, a volley of three spade blades at the target.
			if (Main.netMode == NetmodeID.MultiplayerClient || !NPC.HasValidTarget)
				return;
			Player target = Main.player[NPC.target];
			if (++NPC.localAI[0] >= 150 && NPC.Distance(target.Center) < 500f && Collision.CanHitLine(NPC.Center, 1, 1, target.Center, 1, 1)) {
				NPC.localAI[0] = 0;
				Vector2 aim = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 7f;
				int damage = Main.expertMode ? 22 : 30;
				for (int k = -1; k <= 1; k++)
					Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, aim.RotatedBy(k * 0.18f), ModContent.ProjectileType<SpadeShot>(), damage, 1f, Main.myPlayer);
				NPC.netUpdate = true;
			}
		}

		public override void FindFrame(int frameHeight)
		{
			NPC.spriteDirection = -NPC.direction;
			if (NPC.localAI[0] > 135 || NPC.velocity.Y != 0f) {
				NPC.frame.Y = frameHeight * 4;
				return;
			}
			NPC.frameCounter += Math.Abs(NPC.velocity.X) * 1.4f;
			if (NPC.frameCounter >= 10) {
				NPC.frameCounter = 0;
				NPC.frame.Y = (NPC.frame.Y / frameHeight + 1) % 4 * frameHeight;
			}
		}

		public override float SpawnChance(NPCSpawnInfo spawnInfo)
			=> Main.hardMode && spawnInfo.Player.ZoneRockLayerHeight ? SpawnCondition.Cavern.Chance * 0.05f : 0f;

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 1, 4, 9));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<HighRollerCrate>(), 30));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<RoyalFlush>(), 60));
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Caverns,
				new FlavorTextBestiaryInfoElement("Mods.CasinoWorld.Bestiary.RoyalGuard"),
			});
		}
	}

	/// <summary>Hardmode spectre with a roulette wheel for a body; drifts through walls and fires red and black chips.</summary>
	public class RouletteWraith : ModNPC
	{
		public override void SetStaticDefaults() => Main.npcFrameCount[Type] = 4;

		public override void SetDefaults()
		{
			NPC.width = 36;
			NPC.height = 44;
			NPC.aiStyle = NPCAIStyleID.HoveringFighter;
			AIType = NPCID.Wraith;
			NPC.noGravity = true;
			NPC.noTileCollide = true;
			NPC.lifeMax = 320;
			NPC.damage = 55;
			NPC.defense = 20;
			NPC.knockBackResist = 0.4f;
			NPC.value = Item.buyPrice(silver: 50);
			NPC.HitSound = SoundID.NPCHit54;
			NPC.DeathSound = SoundID.NPCDeath52;
			NPC.alpha = 40;
		}

		public override void PostAI()
		{
			Lighting.AddLight(NPC.Center, 0.4f, 0.1f, 0.15f);
			if (Main.netMode == NetmodeID.MultiplayerClient || !NPC.HasValidTarget)
				return;
			Player target = Main.player[NPC.target];
			if (++NPC.localAI[0] >= 120 && NPC.Distance(target.Center) < 450f) {
				NPC.localAI[0] = 0;
				int damage = Main.expertMode ? 20 : 28;
				for (int k = 0; k < 6; k++)
					Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.UnitX.RotatedBy(k * MathHelper.TwoPi / 6 + NPC.localAI[1]) * 4f, ModContent.ProjectileType<AllInChip>(), damage, 1f, Main.myPlayer, k % 2);
				NPC.localAI[1] += 0.5f;
				NPC.netUpdate = true;
			}
		}

		public override void FindFrame(int frameHeight)
		{
			NPC.spriteDirection = NPC.direction;
			if (++NPC.frameCounter >= 6) {
				NPC.frameCounter = 0;
				NPC.frame.Y = (NPC.frame.Y + frameHeight) % (frameHeight * 4);
			}
		}

		public override float SpawnChance(NPCSpawnInfo spawnInfo)
		{
			if (!Main.hardMode)
				return 0f;
			if (spawnInfo.Player.ZoneOverworldHeight && !Main.dayTime)
				return SpawnCondition.OverworldNightMonster.Chance * 0.04f;
			return spawnInfo.Player.ZoneRockLayerHeight ? SpawnCondition.Cavern.Chance * 0.03f : 0f;
		}

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 1, 3, 7));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<HighRollerCrate>(), 40));
			npcLoot.Add(ItemDropRule.Common(ItemID.Ectoplasm, 4));
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Times.NightTime,
				new FlavorTextBestiaryInfoElement("Mods.CasinoWorld.Bestiary.RouletteWraith"),
			});
		}
	}

	/// <summary>Hardmode ocean shark in a pinstripe vest with gold teeth. Carries a lot of cash.</summary>
	public class LoanShark : ModNPC
	{
		public override void SetStaticDefaults() => Main.npcFrameCount[Type] = 4;

		public override void SetDefaults()
		{
			NPC.width = 56;
			NPC.height = 24;
			NPC.aiStyle = NPCAIStyleID.Piranha;
			AIType = NPCID.Shark;
			NPC.noGravity = true;
			NPC.lifeMax = 520;
			NPC.damage = 70;
			NPC.defense = 18;
			NPC.knockBackResist = 0.2f;
			NPC.value = Item.buyPrice(gold: 2);
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath1;
		}

		public override void FindFrame(int frameHeight)
		{
			NPC.spriteDirection = -NPC.direction;
			if (++NPC.frameCounter >= 7) {
				NPC.frameCounter = 0;
				NPC.frame.Y = (NPC.frame.Y + frameHeight) % (frameHeight * 4);
			}
		}

		public override float SpawnChance(NPCSpawnInfo spawnInfo)
			=> Main.hardMode && spawnInfo.Player.ZoneBeach && spawnInfo.Water ? 0.08f : 0f;

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<HighRollerCrate>(), 20));
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 1, 6, 12));
			npcLoot.Add(ItemDropRule.Common(ItemID.SharkFin, 2));
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Ocean,
				new FlavorTextBestiaryInfoElement("Mods.CasinoWorld.Bestiary.LoanShark"),
			});
		}
	}
}
