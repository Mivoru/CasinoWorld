using System.Collections.Generic;
using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Games;
using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.PitBoss;
using CasinoWorld.Content.Items.Vanity;
using CasinoWorld.Content.NPCs.Boss;
using CasinoWorld.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CasinoWorld.Content.NPCs.People
{
	/// <summary>
	/// Shared base of the casino's town NPCs: they need a house like any vanilla town NPC, have a shop, a service
	/// on the second button and throw something at enemies. Drawn like vanilla people (PersonDoll).
	/// </summary>
	[AutoloadHead]
	public abstract class CasinoTownNPC : ModNPC
	{
		private PersonDoll doll;
		private bool dollFailed;

		protected abstract PersonLook Look { get; }
		protected abstract int ChatLines { get; }
		protected abstract int AttackProjectile { get; }
		protected abstract int AttackDamage { get; }
		protected virtual float AttackSpeed => 11f;

		/// <summary>Service on the second button; returns what the NPC says.</summary>
		protected abstract string Service(Player player);

		protected string Text(string key, params object[] args) => Language.GetTextValue($"Mods.CasinoWorld.NPCs.{Name}.{key}", args);

		public override string Texture => "CasinoWorld/Content/NPCs/People/" + Name;

		public override string HeadTexture => Texture + "_Head";

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 25;
			NPCID.Sets.ExtraFramesCount[Type] = 9;
			NPCID.Sets.AttackFrameCount[Type] = 4;
			NPCID.Sets.DangerDetectRange[Type] = 600;
			NPCID.Sets.AttackType[Type] = 0;
			NPCID.Sets.AttackTime[Type] = 50;
			NPCID.Sets.AttackAverageChance[Type] = 20;
			NPCID.Sets.HatOffsetY[Type] = 4;
			Moods();
		}

		protected abstract void Moods();

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
			=> bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
				new FlavorTextBestiaryInfoElement("Mods.CasinoWorld.Bestiary." + Name),
			});

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			bool sitting = NPC.ai[0] == 5f;
			if (dollFailed || sitting || NPC.IsABestiaryIconDummy)
				return true;
			try {
				doll ??= new PersonDoll();
				bool attacking = NPC.ai[0] == 10f;
				doll.BodyPose = attacking ? (NPC.ai[1] > 30 ? PersonDoll.ArmUp : NPC.ai[1] > 15 ? PersonDoll.ArmForward : PersonDoll.ArmDown) : -1;
				doll.LegPose = -1;
				doll.Draw(NPC, Look, NPC.Bottom + new Vector2(0f, NPC.gfxOffY));
			}
			catch (System.Exception e) {
				dollFailed = true;
				Mod.Logger.Warn(Name + " renderer failed, using sprite: " + e.Message);
				return true;
			}
			return false;
		}

		public override string GetChat() => Text("Chat." + Main.rand.Next(ChatLines));

		public override void SetChatButtons(ref string button, ref string button2)
		{
			button = Language.GetTextValue("LegacyInterface.28");
			button2 = Text("Service");
		}

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (firstButton)
				shopName = "Shop";
			else
				Main.npcChatText = Service(Main.LocalPlayer);
		}

		public override void TownNPCAttackStrength(ref int damage, ref float knockback)
		{
			damage = AttackDamage * (Main.hardMode ? 2 : 1) * (NPC.downedPlantBoss ? 3 : 2) / 2;
			knockback = 4f;
		}

		public override void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown)
		{
			cooldown = 25;
			randExtraCooldown = 20;
		}

		public override void TownNPCAttackProj(ref int projType, ref int attackDelay)
		{
			projType = AttackProjectile;
			attackDelay = 1;
		}

		public override void TownNPCAttackProjSpeed(ref float multiplier, ref float gravityCorrection, ref float randomOffset)
		{
			multiplier = AttackSpeed;
			randomOffset = 1f;
		}
	}

	/// <summary>Bookie: after any boss, with a casino in the world. Scratch cards and luck potions; takes one bet a day on your next boss fight.</summary>
	public class Bookie : CasinoTownNPC
	{
		private static PersonLook look;

		protected override PersonLook Look => look ??= new PersonLook {
			Hair = 23, SkinVariant = 2, Skin = new(235, 190, 150), HairColor = new(90, 60, 35), Shirt = new(40, 110, 70), UnderShirt = new(240, 220, 160),
			Pants = new(50, 60, 50), Shoes = new(60, 40, 25), HeadVanity = ItemID.Fedora, Accessory = ItemID.Sunglasses,
		};

		protected override int ChatLines => 6;
		protected override int AttackProjectile => ModContent.ProjectileType<HighRollerChip>();
		protected override int AttackDamage => 18;

		protected override void Moods() => NPC.Happiness
			.SetBiomeAffection<ForestBiome>(AffectionLevel.Like).SetBiomeAffection<DesertBiome>(AffectionLevel.Dislike)
			.SetNPCAffection<TownGambler>(AffectionLevel.Love).SetNPCAffection(NPCID.TaxCollector, AffectionLevel.Like).SetNPCAffection(NPCID.Clothier, AffectionLevel.Dislike);

		public override bool CanTownNPCSpawn(int numTownNPCs)
			=> CasinoWorldSystem.HasSurfaceCasino && (NPC.downedBoss1 || NPC.downedBoss2 || NPC.downedBoss3 || NPC.downedSlimeKing || NPC.downedQueenBee || Main.hardMode);

		public override List<string> SetNPCNameList() => new() { "Odds Ollie", "Parlay Pete", "Spread Eddie", "Five-to-One Finn", "Ledger Lew" };

		protected override string Service(Player player) => player.GetModPlayer<BossBet>().Talk(this);

		public string Say(string key, params object[] args) => Text(key, args);

		public override void AddShops()
		{
			new NPCShop(Type)
				.Add(new Item(ModContent.ItemType<ScratchCard>()) { shopCustomPrice = 50 * 100 })
				.Add(new Item(ModContent.ItemType<PlatinumScratchCard>()) { shopCustomPrice = 5 * 10000 }, Condition.Hardmode)
				.Add(new Item(ModContent.ItemType<GamblersHandbook>()) { shopCustomPrice = 10 * 100 })
				.Add(new Item(ItemID.LuckPotionLesser) { shopCustomPrice = Item.buyPrice(gold: 1) })
				.Add(new Item(ItemID.LuckPotion) { shopCustomPrice = Item.buyPrice(gold: 3) }, Condition.DownedSkeletron)
				.Add(new Item(ItemID.LuckPotionGreater) { shopCustomPrice = Item.buyPrice(gold: 8) }, Condition.Hardmode)
				.Add(new Item(ItemID.LifeformAnalyzer) { shopCustomPrice = Item.buyPrice(gold: 8) }, Condition.DownedSkeletron)
				.Add(new Item(ItemID.Binoculars) { shopCustomPrice = Item.buyPrice(gold: 5) })
				.Register();
		}
	}

	/// <summary>
	/// One bet per in-game day with the Bookie: beat a boss before the next dawn without dying and get 1.6 times the
	/// stake back (5 gold before hardmode, 50 gold in it).
	/// </summary>
	public class BossBet : ModPlayer
	{
		private const double Pays = 1.6;
		private const float FightRange = 4000f;

		private long stake;
		private bool usedToday, wasDay = true;
		private readonly HashSet<int> watched = new();

		private static long Price => Main.hardMode ? 50 * Coins.Gold : 5 * Coins.Gold;

		public string Talk(Bookie bookie)
		{
			if (!CasinoConfig.Instance.GamblingEnabled)
				return Language.GetTextValue("Mods.CasinoWorld.UI.GamblingDisabled");
			if (stake > 0)
				return bookie.Say("BetRunning", Coins.Format((long)(stake * Pays)));
			if (usedToday)
				return bookie.Say("BetTomorrow");
			foreach (NPC npc in Main.ActiveNPCs)
				if (npc.boss)
					return bookie.Say("BetTooLate");
			long price = Price;
			if (!Coins.TryTake(Player, price))
				return bookie.Say("BetPoor", Coins.Format(price));
			stake = price;
			usedToday = true;
			SoundEngine.PlaySound(CasinoSounds.CashRegister);
			return bookie.Say("BetTaken", Coins.Format(price), Coins.Format((long)(price * Pays)));
		}

		public override void PostUpdate()
		{
			if (Player.whoAmI != Main.myPlayer || CasinoTravel.InCasino && stake == 0)
				return;
			if (!wasDay && Main.dayTime && !CasinoTravel.InCasino) {   // dawn
				usedToday = false;
				if (stake > 0)
					Lose("Mods.CasinoWorld.NPCs.Bookie.BetExpired");
			}
			if (!CasinoTravel.InCasino)
				wasDay = Main.dayTime;
			if (stake <= 0) {
				watched.Clear();
				return;
			}
			foreach (NPC npc in Main.ActiveNPCs)
				if (npc.boss && npc.Distance(Player.Center) < FightRange)   // another player's fight elsewhere does not count
					watched.Add(npc.whoAmI);
			foreach (int id in watched)
				if (!Main.npc[id].active && Main.npc[id].life <= 0) {
					watched.Clear();
					long win = (long)(stake * Pays);
					stake = 0;
					Coins.Give(Player, win);
					CombatText.NewText(Player.Hitbox, Color.Gold, "+" + Coins.Format(win), true);
					Main.NewText(Language.GetTextValue("Mods.CasinoWorld.NPCs.Bookie.BetWon", Coins.Format(win)), new Color(255, 215, 90));
					SoundEngine.PlaySound(CasinoSounds.Jackpot);
					CasinoFx.Ring(Player.Center, new Color(255, 215, 90), 20, 5f);
					return;
				}
			watched.RemoveWhere(id => !Main.npc[id].active);
		}

		public override void Kill(double damage, int hitDirection, bool pvp, Terraria.DataStructures.PlayerDeathReason damageSource)
		{
			if (Player.whoAmI == Main.myPlayer && stake > 0)
				Lose("Mods.CasinoWorld.NPCs.Bookie.BetLost");
		}

		private void Lose(string key)
		{
			stake = 0;
			watched.Clear();
			Main.NewText(Language.GetTextValue(key), new Color(220, 150, 150));
		}

		public override void SaveData(TagCompound tag)
		{
			tag["bossBet"] = stake;
			tag["bossBetUsed"] = usedToday;
		}

		public override void LoadData(TagCompound tag)
		{
			stake = tag.ContainsKey("bossBet") ? tag.GetLong("bossBet") : 0;
			usedToday = tag.GetBool("bossBetUsed");
		}
	}

	/// <summary>Magician, The Wild Card's former assistant: moves in after it is beaten. Tricks and vanity; one card trick a day gives a random boon.</summary>
	public class Magician : CasinoTownNPC
	{
		private static PersonLook look;

		protected override PersonLook Look => look ??= new PersonLook {
			Hair = 21, SkinVariant = 4, Skin = new(245, 205, 175), HairColor = new(170, 50, 60), Shirt = new(150, 25, 45), UnderShirt = new(245, 240, 230),
			Pants = new(30, 24, 36), Shoes = new(20, 18, 24), HeadVanity = ItemID.TopHat,
		};

		protected override int ChatLines => 6;
		protected override int AttackProjectile => ModContent.ProjectileType<RoyalCard>();
		protected override int AttackDamage => 16;
		protected override float AttackSpeed => 10f;

		protected override void Moods() => NPC.Happiness
			.SetBiomeAffection<HallowBiome>(AffectionLevel.Love).SetBiomeAffection<ForestBiome>(AffectionLevel.Like).SetBiomeAffection<UndergroundBiome>(AffectionLevel.Dislike)
			.SetNPCAffection(NPCID.Wizard, AffectionLevel.Love).SetNPCAffection(NPCID.PartyGirl, AffectionLevel.Like).SetNPCAffection<Bookie>(AffectionLevel.Dislike);

		public override bool CanTownNPCSpawn(int numTownNPCs) => CasinoDowned.DownedWildCard;

		public override List<string> SetNPCNameList() => new() { "Mirabelle", "Presta", "Vanessa Vanish", "Abra", "Seraphine" };

		protected override string Service(Player player) => player.GetModPlayer<CardTrick>().Draw(this);

		public string Say(string key, params object[] args) => Text(key, args);

		public override void AddShops()
		{
			new NPCShop(Type)
				.Add(new Item(ItemID.SmokeBomb) { shopCustomPrice = Item.buyPrice(silver: 1) })
				.Add(new Item(ItemID.ConfettiGun) { shopCustomPrice = Item.buyPrice(silver: 50) })
				.Add(new Item(ItemID.InvisibilityPotion) { shopCustomPrice = Item.buyPrice(silver: 40) })
				.Add(new Item(ItemID.TeleportationPotion) { shopCustomPrice = Item.buyPrice(silver: 60) })
				.Add(new Item(ItemID.MagicHat) { shopCustomPrice = Item.buyPrice(gold: 3) })
				.Add(new Item(ItemID.TopHat) { shopCustomPrice = Item.buyPrice(gold: 1) })
				.Add(new Item(ItemID.MagicMirror) { shopCustomPrice = Item.buyPrice(gold: 10) })
				.Add(new Item(ModContent.ItemType<WildCardMask>()) { shopCustomPrice = Item.buyPrice(gold: 10) })
				.Add(new Item(ModContent.ItemType<GoldenChip>()) { shopCustomPrice = Item.buyPrice(gold: 6) })
				.Register();
		}
	}

	/// <summary>The Magician's daily card: one random potion effect for ten minutes, once per in-game day.</summary>
	public class CardTrick : ModPlayer
	{
		private static readonly int[] Boons = {
			BuffID.Regeneration, BuffID.Swiftness, BuffID.Ironskin, BuffID.MagicPower, BuffID.Archery, BuffID.Wrath, BuffID.Rage, BuffID.Endurance, BuffID.Lifeforce, BuffID.Mining, BuffID.Heartreach, BuffID.Titan,
		};

		private bool used, wasDay = true;

		public string Draw(Magician magician)
		{
			if (used)
				return magician.Say("TrickTomorrow");
			used = true;
			int buff = Main.rand.Next(Boons);
			Player.AddBuff(buff, 60 * 600);
			SoundEngine.PlaySound(CasinoSounds.CardFlip);
			SoundEngine.PlaySound(CasinoSounds.RareReveal with { Volume = 0.5f });
			CasinoFx.Ring(Player.Center, new Color(220, 140, 255), 24, 5f, 1.2f);
			return magician.Say("TrickDone", Lang.GetBuffName(buff));
		}

		public override void PostUpdate()
		{
			if (CasinoTravel.InCasino)
				return;
			if (!wasDay && Main.dayTime)
				used = false;
			wasDay = Main.dayTime;
		}

		public override void SaveData(TagCompound tag) => tag["cardTrickUsed"] = used;

		public override void LoadData(TagCompound tag) => used = tag.GetBool("cardTrickUsed");
	}

	/// <summary>Bouncer: hardmode, with a casino in the world. Sells protection: potions, summons and ten minutes of cover.</summary>
	public class Bouncer : CasinoTownNPC
	{
		private static PersonLook look;

		protected override PersonLook Look => look ??= new PersonLook {
			Hair = 15, SkinVariant = 3, Skin = new(150, 100, 70), HairColor = new(25, 20, 20), Shirt = new(26, 26, 32), UnderShirt = new(235, 235, 235),
			Pants = new(22, 22, 28), Shoes = new(12, 12, 14), Accessory = ItemID.Sunglasses,
		};

		protected override int ChatLines => 6;
		protected override int AttackProjectile => ModContent.ProjectileType<LoadedDie>();
		protected override int AttackDamage => 26;

		public override void SetDefaults()
		{
			base.SetDefaults();
			NPC.lifeMax = 400;
			NPC.defense = 30;
		}

		protected override void Moods() => NPC.Happiness
			.SetBiomeAffection<UndergroundBiome>(AffectionLevel.Like).SetBiomeAffection<OceanBiome>(AffectionLevel.Dislike)
			.SetNPCAffection(NPCID.ArmsDealer, AffectionLevel.Like).SetNPCAffection(NPCID.Nurse, AffectionLevel.Love).SetNPCAffection(NPCID.Pirate, AffectionLevel.Hate);

		public override bool CanTownNPCSpawn(int numTownNPCs) => Main.hardMode && CasinoWorldSystem.HasSurfaceCasino;

		public override List<string> SetNPCNameList() => new() { "Big Sal", "Tiny", "Knuckles", "Brick", "The Wall" };

		private static long Fee => NPC.downedPlantBoss ? 25 * Coins.Gold : 10 * Coins.Gold;

		protected override string Service(Player player)
		{
			long fee = Fee;
			if (!Coins.TryTake(player, fee))
				return Text("GuardPoor", Coins.Format(fee));
			player.AddBuff(ModContent.BuffType<Buffs.Protected>(), 60 * 600);
			SoundEngine.PlaySound(CasinoSounds.CashRegister);
			return Text("GuardHired", Coins.Format(fee));
		}

		public override void AddShops()
		{
			new NPCShop(Type)
				.Add(new Item(ItemID.IronskinPotion) { shopCustomPrice = Item.buyPrice(silver: 40) })
				.Add(new Item(ItemID.EndurancePotion) { shopCustomPrice = Item.buyPrice(silver: 80) })
				.Add(new Item(ItemID.ThornsPotion) { shopCustomPrice = Item.buyPrice(silver: 40) })
				.Add(new Item(ItemID.GreaterHealingPotion) { shopCustomPrice = Item.buyPrice(gold: 1) })
				.Add(new Item(ItemID.Sunglasses) { shopCustomPrice = Item.buyPrice(gold: 1) })
				.Add(new Item(ItemID.Shackle) { shopCustomPrice = Item.buyPrice(gold: 2) })
				.Add(new Item(ModContent.ItemType<VaultKey>()) { shopCustomPrice = Item.buyPrice(gold: 50) }, PitBossDown)
				.Register();
		}

		private static readonly Condition PitBossDown = new("Mods.CasinoWorld.NPCs.Bouncer.PitBossCondition", () => CasinoDowned.DownedPitBoss);
	}
}

namespace CasinoWorld.Content.Buffs
{
	/// <summary>Hired bouncer: +6 defence and 5 % less damage for ten minutes.</summary>
	public class Protected : ModBuff
	{
		public override void Update(Player player, ref int buffIndex)
		{
			player.statDefense += 6;
			player.endurance += 0.05f;
		}
	}
}
