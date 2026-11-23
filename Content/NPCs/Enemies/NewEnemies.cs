using System;
using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using Terraria.Audio;
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
			Banner = Type;
			BannerItem = ModContent.ItemType<Items.Banners.BrokeGamblerBanner>();
			NPC.lifeMax = 55;
			NPC.damage = 16;
			NPC.defense = 6;
			NPC.value = Item.buyPrice(silver: 3);
			AIType = NPCID.Zombie;
			AnimationType = NPCID.Zombie;
		}

		public override float SpawnChance(NPCSpawnInfo spawnInfo)
			=> spawnInfo.Player.ZoneOverworldHeight && !Main.dayTime && !spawnInfo.Player.ZoneTowerSolar ? SpawnCondition.OverworldNightMonster.Chance * 0.18f : 0f;

		public override void AI()
		{
			if (!Main.dedServ && Main.rand.NextBool(1400))
				CombatText.NewText(NPC.Hitbox, new Color(160, 200, 150), Language.GetTextValue($"Mods.CasinoWorld.NPCs.BrokeGambler.Moan.{Main.rand.Next(5)}"));
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			for (int k = 0; k < (NPC.life <= 0 ? 10 : 2); k++)
				Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(8, 16), CasinoFx.ShardType, new Vector2(hit.HitDirection * 1.5f, -2f) + Main.rand.NextVector2Circular(1, 1), 0, new Color(240, 230, 170));
			for (int k = 0; k < (NPC.life <= 0 ? 12 : 3); k++)
				Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection * 2f, -1.5f);
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
			Banner = Type;
			BannerItem = ModContent.ItemType<Items.Banners.LuckyBatBanner>();
			NPC.width = 26;
			NPC.height = 18;
			NPC.aiStyle = NPCAIStyleID.Bat;
			AIType = NPCID.CaveBat;
			NPC.lifeMax = 30;
			NPC.damage = 14;
			NPC.defense = 4;
			NPC.knockBackResist = 0.8f;
			NPC.value = Item.buyPrice(silver: 10);
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

		public override void AI()
		{
			Lighting.AddLight(NPC.Bottom, 0.25f, 0.2f, 0.05f);
			if (Main.rand.NextBool(10))
				CasinoFx.Spark(NPC.Bottom, new Vector2(0, 0.5f), new Color(255, 220, 90), 0.6f);
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			for (int k = 0; k < (NPC.life <= 0 ? 8 : 2); k++)
				Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection * 2f, -1f);
			if (NPC.life <= 0) {
				for (int k = 0; k < 8; k++)
					Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldCoin, 0, -2f);
				SoundEngine.PlaySound(SoundID.CoinPickup, NPC.Center);
			}
		}

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			npcLoot.Add(ItemDropRule.Common(ItemID.GoldCoin, 4, 1, 1));
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
			Banner = Type;
			BannerItem = ModContent.ItemType<Items.Banners.RoyalGuardBanner>();
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

		private bool Facing(Vector2 from) => Math.Sign(from.X - NPC.Center.X) == NPC.direction;

		public override void ModifyHitByItem(Player player, Item item, ref NPC.HitModifiers modifiers) => ShieldBlock(player.Center, ref modifiers);

		public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers) => ShieldBlock(projectile.Center, ref modifiers);

		/// <summary>Hits from the front land on the card shield: 25 % less damage, a clink and sparks.</summary>
		private void ShieldBlock(Vector2 from, ref NPC.HitModifiers modifiers)
		{
			if (!Facing(from))
				return;
			modifiers.FinalDamage *= 0.75f;
			SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = 0.5f, Volume = 0.6f }, NPC.Center);
			CasinoFx.Sparks(NPC.Center + new Vector2(NPC.direction * 14, 0), new Color(230, 230, 255), 4, 2.5f, 0.8f);
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			for (int k = 0; k < (NPC.life <= 0 ? 16 : 2); k++)
				Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(12, 24), CasinoFx.ShardType, new Vector2(hit.HitDirection * 2f, -2f) + Main.rand.NextVector2Circular(1.5f, 1.5f), 0, k % 3 == 0 ? new Color(40, 40, 50) : Color.White);
			if (NPC.life <= 0)
				CasinoFx.Ring(NPC.Center, new Color(200, 200, 230), 14, 4f);
		}

		public override void PostAI()
		{
			// Telegraph: the blade gleams for a moment before the volley.
			if (NPC.localAI[0] > 125 && Main.rand.NextBool(2))
				CasinoFx.Spark(NPC.Center + new Vector2(NPC.direction * 16, -14), Vector2.Zero, new Color(220, 220, 255), 0.9f);
			if (NPC.localAI[0] == 126)
				SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.6f }, NPC.Center);
			// Every few seconds, a volley of three spade blades at the target.
			if (Main.netMode == NetmodeID.MultiplayerClient || !NPC.HasValidTarget) {
				NPC.localAI[0]++;
				return;
			}
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
		public override void SetDefaults()
		{
			Banner = Type;
			BannerItem = ModContent.ItemType<Items.Banners.RouletteWraithBanner>();
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

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 4;
			NPCID.Sets.TrailCacheLength[Type] = 6;
			NPCID.Sets.TrailingMode[Type] = 0;
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			// Ghostly afterimages, red and black.
			Texture2D tex = Terraria.GameContent.TextureAssets.Npc[Type].Value;
			SpriteEffects fx = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			for (int k = NPC.oldPos.Length - 1; k >= 1; k--) {
				Color c = (k % 2 == 0 ? new Color(255, 60, 70) : new Color(60, 40, 90)) * (0.35f * (1f - k / (float)NPC.oldPos.Length));
				c.A = 0;
				spriteBatch.Draw(tex, NPC.oldPos[k] + NPC.Size / 2f - screenPos, NPC.frame, c, NPC.rotation, NPC.frame.Size() / 2f, NPC.scale, fx, 0f);
			}
			return true;
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			for (int k = 0; k < (NPC.life <= 0 ? 20 : 3); k++)
				Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(14, 18), DustID.Shadowflame, Main.rand.NextVector2Circular(2, 2), 100, default, 1.2f).noGravity = true;
			if (NPC.life <= 0) {
				CasinoFx.Ring(NPC.Center, new Color(255, 60, 70), 12, 4f, 1.2f);
				CasinoFx.Ring(NPC.Center, new Color(120, 120, 160), 12, 2.5f, 1f);
				SoundEngine.PlaySound(CasinoSounds.RouletteBallDrop, NPC.Center);
			}
		}

		public override void PostAI()
		{
			Lighting.AddLight(NPC.Center, 0.4f, 0.1f, 0.15f);
			if (NPC.localAI[0] > 90 && (int)NPC.localAI[0] % 6 == 0) {
				SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f, Pitch = -0.3f + (NPC.localAI[0] - 90) / 40f }, NPC.Center);
				CasinoFx.Spark(NPC.Center + Main.rand.NextVector2CircularEdge(12, 12), Vector2.Zero, (int)NPC.localAI[0] % 12 == 0 ? new Color(255, 60, 70) : new Color(150, 150, 190), 0.8f);
			}
			if (Main.netMode == NetmodeID.MultiplayerClient || !NPC.HasValidTarget) {
				NPC.localAI[0] = (NPC.localAI[0] + 1) % 120;
				return;
			}
			Player target = Main.player[NPC.target];
			if (++NPC.localAI[0] >= 120 && NPC.Distance(target.Center) < 450f) {
				NPC.localAI[0] = 0;
				int damage = Main.expertMode ? 20 : 28;
				for (int k = 0; k < 6; k++)
					Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.UnitX.RotatedBy(k * MathHelper.TwoPi / 6 + NPC.localAI[1]) * 4f, ModContent.ProjectileType<AllInChip>(), damage, 1f, Main.myPlayer, k % 2);
				NPC.localAI[1] += 0.5f;
				NPC.netUpdate = true;
				SoundEngine.PlaySound(CasinoSounds.ChipStack, NPC.Center);
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
			Banner = Type;
			BannerItem = ModContent.ItemType<Items.Banners.LoanSharkBanner>();
			NPC.width = 56;
			NPC.height = 24;
			NPC.aiStyle = NPCAIStyleID.Piranha;
			AIType = NPCID.Shark;
			NPC.noGravity = true;
			NPC.lifeMax = 420;
			NPC.damage = 62;
			NPC.defense = 16;
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

		public override void AI()
		{
			if (NPC.wet && Main.rand.NextBool(8))
				Dust.NewDustPerfect(NPC.Center + new Vector2(-NPC.direction * 26, 0), DustID.BreatheBubble, new Vector2(0, -1f), 0, default, 1f);
		}

		public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
		{
			// The gold teeth flash on a bite.
			CasinoFx.Sparks(NPC.Center + new Vector2(NPC.direction * 26, 2), new Color(255, 220, 90), 5, 2f);
			SoundEngine.PlaySound(SoundID.CoinPickup with { Pitch = -0.4f }, NPC.Center);
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			for (int k = 0; k < (NPC.life <= 0 ? 20 : 4); k++)
				Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection * 2f, -1f);
			if (NPC.life <= 0)
				for (int k = 0; k < 16; k++)
					Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldCoin, Main.rand.NextFloat(-3, 3), -3f);
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
