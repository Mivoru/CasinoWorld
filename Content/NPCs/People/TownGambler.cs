using System.Collections.Generic;
using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.Crates;
using CasinoWorld.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.NPCs.People
{
	/// <summary>
	/// Town NPC who moves in once the casino is built (or the player owns a loyalty token).
	/// Sells crates, the blueprint and, after Skeletron, the Golden Chip; fights by throwing cards.
	/// Sprite rendered in Blender (render_people.py), 25 frames like vanilla town NPCs.
	/// </summary>
	[AutoloadHead]
	public class TownGambler : ModNPC
	{
		private const int ChatLines = 10;

		public override string Texture => "CasinoWorld/Content/NPCs/People/Gambler";

		public override string HeadTexture => "CasinoWorld/Content/NPCs/People/Gambler_Head";

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 25;
			NPCID.Sets.ExtraFramesCount[Type] = 9;
			NPCID.Sets.AttackFrameCount[Type] = 4;
			NPCID.Sets.DangerDetectRange[Type] = 600;
			NPCID.Sets.AttackType[Type] = 0;          // throws
			NPCID.Sets.AttackTime[Type] = 50;
			NPCID.Sets.AttackAverageChance[Type] = 20;
			NPCID.Sets.HatOffsetY[Type] = 4;

			NPC.Happiness
				.SetBiomeAffection<ForestBiome>(AffectionLevel.Like)
				.SetBiomeAffection<UndergroundBiome>(AffectionLevel.Love)
				.SetBiomeAffection<SnowBiome>(AffectionLevel.Dislike)
				.SetNPCAffection(NPCID.Merchant, AffectionLevel.Like)
				.SetNPCAffection(NPCID.TaxCollector, AffectionLevel.Love)
				.SetNPCAffection(NPCID.Nurse, AffectionLevel.Dislike);
		}

		public override void SetDefaults()
		{
			NPC.townNPC = true;
			NPC.friendly = true;
			NPC.width = 18;
			NPC.height = 40;
			NPC.aiStyle = NPCAIStyleID.Passive;
			NPC.damage = 10;
			NPC.defense = 15;
			NPC.lifeMax = 250;
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath1;
			NPC.knockBackResist = 0.5f;
			AnimationType = NPCID.Guide;
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
				new FlavorTextBestiaryInfoElement("Mods.CasinoWorld.Bestiary.TownGambler"),
			});
		}

		public override bool CanTownNPCSpawn(int numTownNPCs)
		{
			if (CasinoWorldSystem.HasSurfaceCasino)
				return true;
			foreach (Player player in Main.ActivePlayers) {
				if (player.HasItem(ModContent.ItemType<LoyaltyToken>()))
					return true;
			}
			return false;
		}

		public override List<string> SetNPCNameList() => new() { "Lucky Lou", "Vegas Vinnie", "Ace", "Dice Dan", "Chip", "Big Tony", "Roulette Ray", "Jackpot Jim" };

		public override string GetChat() => Language.GetTextValue($"Mods.CasinoWorld.NPCs.TownGambler.Chat.{Main.rand.Next(ChatLines)}");

		public override void SetChatButtons(ref string button, ref string button2)
		{
			button = Language.GetTextValue("LegacyInterface.28");
		}

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (firstButton)
				shopName = "Shop";
		}

		private static Item Crate<T>(int silver) where T : ModItem
			=> new(ModContent.ItemType<T>()) { shopCustomPrice = Item.buyPrice(silver: silver) };

		private static Condition StageIs(global::CasinoWorld.Common.Crates.CrateStage stage)
			=> new("Mods.CasinoWorld.Crates.StageCondition", () => global::CasinoWorld.Common.Crates.CrateLoot.CurrentStage == stage);

		public override void AddShops()
		{
			new NPCShop(Type)
				.Add<CasinoBlueprint>()
				// Crate prices follow the game stage (expected value of the contents x 1.25, see tools/crate_ev.py).
				.Add(Crate<LuckyCrate>(silver: 125), StageIs(global::CasinoWorld.Common.Crates.CrateStage.Early))
				.Add(Crate<LuckyCrate>(silver: 200), StageIs(global::CasinoWorld.Common.Crates.CrateStage.PostSkeletron))
				.Add(Crate<LuckyCrate>(silver: 600), StageIs(global::CasinoWorld.Common.Crates.CrateStage.Hardmode))
				.Add(Crate<LuckyCrate>(silver: 1400), StageIs(global::CasinoWorld.Common.Crates.CrateStage.PostPlantera))
				.Add(Crate<LuckyCrate>(silver: 4500), StageIs(global::CasinoWorld.Common.Crates.CrateStage.PostMoonLord))
				.Add(Crate<HighRollerCrate>(silver: 750), StageIs(global::CasinoWorld.Common.Crates.CrateStage.Hardmode))
				.Add(Crate<HighRollerCrate>(silver: 1600), StageIs(global::CasinoWorld.Common.Crates.CrateStage.PostPlantera))
				.Add(Crate<HighRollerCrate>(silver: 5000), StageIs(global::CasinoWorld.Common.Crates.CrateStage.PostMoonLord))
				.Add(new Item(ModContent.ItemType<GoldenChip>()) { shopCustomPrice = Item.buyPrice(gold: 8) }, Condition.DownedSkeletron)
				.Add(new Item(ItemID.Sunglasses) { shopCustomPrice = Item.buyPrice(gold: 1) })
				.Add(new Item(ItemID.TopHat) { shopCustomPrice = Item.buyPrice(gold: 1) })
				.Register();
		}

		public override void TownNPCAttackStrength(ref int damage, ref float knockback)
		{
			damage = 16;
			knockback = 3f;
		}

		public override void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown)
		{
			cooldown = 25;
			randExtraCooldown = 20;
		}

		public override void TownNPCAttackProj(ref int projType, ref int attackDelay)
		{
			projType = ModContent.ProjectileType<ThrownCard>();
			attackDelay = 1;
		}

		public override void TownNPCAttackProjSpeed(ref float multiplier, ref float gravityCorrection, ref float randomOffset)
		{
			multiplier = 11f;
			randomOffset = 1.5f;
		}
	}
}
