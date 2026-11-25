using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.NPCs
{
	/// <summary>
	/// Staff that stand at their post (a table end or the bar) and talk to players.
	/// The first chat button explains their job, for example the rules of the game they run.
	/// </summary>
	public abstract class CasinoStaff : CasinoNPC
	{
		/// <summary>Localization key of the help text shown by the first chat button, or null for none.</summary>
		protected virtual string HelpKey => null;

		public override void AI()
		{
			NPC.velocity.X = 0f;
			if (NPC.ai[0] == 0f) {
				// ai[0] holds the side the NPC faces at its post: -1 or 1. 0 means not set yet.
				NPC.ai[0] = NPC.direction == 0 ? 1 : NPC.direction;
			}
			NPC.direction = NPC.spriteDirection = (int)NPC.ai[0];
			FaceTalkingPlayer();
		}

		public override void FindFrame(int frameHeight)
		{
			NPC.frame.Y = 0;
		}

		public override void SetChatButtons(ref string button, ref string button2)
		{
			if (HelpKey != null)
				button = Language.GetTextValue("Mods.CasinoWorld.NPCs.HowToPlay");
		}

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (firstButton && HelpKey != null)
				Main.npcChatText = Language.GetTextValue(HelpKey);
		}
	}

	public class RouletteCroupier : CasinoStaff
	{
		// Croupier: slicked black hair, white shirt under a burgundy waistcoat. Spins the wheel every few seconds.
		private static readonly PersonLook look = new() {
			Hair = 15, SkinVariant = 1, Skin = new(240, 195, 160), HairColor = new(25, 22, 22),
			Shirt = new(120, 25, 40), UnderShirt = new(235, 232, 225), Pants = new(25, 25, 30), Shoes = new(15, 15, 15),
		};
		private static readonly PersonLook highRollerLook = new() {
			Hair = 15, SkinVariant = 1, Skin = new(240, 195, 160), HairColor = new(25, 22, 22),
			Shirt = new(18, 18, 22), UnderShirt = new(230, 190, 80), Pants = new(25, 25, 30), Shoes = new(15, 15, 15),
		};
		protected override PersonLook Look => HighRollers ? highRollerLook : look;

		protected override int BodyPose(int tick)
		{
			int cycle = tick % 300;
			return cycle < 10 ? PersonDoll.ArmUpForward : cycle < 22 ? PersonDoll.ArmForward : -1;
		}

		protected override string CustomTexture => "CasinoWorld/Content/NPCs/People/RouletteCroupier";
		protected override int VanillaLook => NPCID.Clothier;
		protected override int ChatLines => 6;
		protected override string HelpKey => "Mods.CasinoWorld.NPCs.RouletteCroupier.Help";
	}

	public class BlackjackDealer : CasinoStaff
	{
		// Dealer: dark bob, black waistcoat, white shirt. Deals in quick bursts of cards.
		private static readonly PersonLook look = new() {
			Hair = 7, SkinVariant = 4, Skin = new(225, 170, 130), HairColor = new(45, 30, 25),
			Shirt = new(30, 30, 35), UnderShirt = new(240, 238, 230), Pants = new(25, 25, 30), Shoes = new(15, 15, 15),
		};
		private static readonly PersonLook highRollerLook = new() {
			Hair = 7, SkinVariant = 4, Skin = new(225, 170, 130), HairColor = new(45, 30, 25),
			Shirt = new(230, 190, 80), UnderShirt = new(20, 20, 24), Pants = new(25, 25, 30), Shoes = new(15, 15, 15),
		};
		protected override PersonLook Look => HighRollers ? highRollerLook : look;

		protected override int BodyPose(int tick)
		{
			int cycle = tick % 360;
			if (cycle < 48)
				return (cycle / 6) % 2 == 0 ? PersonDoll.ArmForward : PersonDoll.ArmDown;
			return -1;
		}

		protected override string CustomTexture => "CasinoWorld/Content/NPCs/People/BlackjackDealer";
		protected override int VanillaLook => NPCID.TaxCollector;
		protected override int ChatLines => 6;
		protected override string HelpKey => "Mods.CasinoWorld.NPCs.BlackjackDealer.Help";
	}

	/// <summary>
	/// Runs the prize counter: cosmetics and other customisable items for loyalty tokens.
	/// Prices are a first pass; for tokens per bet see CasinoRewards.TokensFor.
	/// </summary>
	public class PrizeCashier : CasinoStaff
	{
		// Cashier: blonde, red uniform jacket with gold trim. Waves at passing players now and then.
		private static readonly PersonLook look = new() {
			Hair = 9, SkinVariant = 5, Skin = new(250, 210, 180), HairColor = new(230, 195, 110),
			Shirt = new(185, 35, 45), UnderShirt = new(230, 185, 60), Pants = new(40, 30, 35), Shoes = new(90, 20, 25),
		};
		private static readonly PersonLook highRollerLook = new() {
			Hair = 9, SkinVariant = 5, Skin = new(250, 210, 180), HairColor = new(230, 195, 110),
			Shirt = new(20, 20, 24), UnderShirt = new(230, 190, 80), Pants = new(40, 30, 35), Shoes = new(90, 20, 25),
		};
		protected override PersonLook Look => HighRollers ? highRollerLook : look;

		protected override int BodyPose(int tick)
		{
			int cycle = tick % 540;
			return cycle < 40 ? ((cycle / 8) % 2 == 0 ? PersonDoll.ArmUp : PersonDoll.ArmUpForward) : -1;
		}

		protected override string CustomTexture => "CasinoWorld/Content/NPCs/People/PrizeCashier";
		protected override int VanillaLook => NPCID.Steampunker;
		protected override int ChatLines => 4;

		public override void SetChatButtons(ref string button, ref string button2)
		{
			button = Language.GetTextValue("Mods.CasinoWorld.NPCs.PrizeCashier.Prizes");
			button2 = Language.GetTextValue("Mods.CasinoWorld.NPCs.PrizeCashier.ChallengesButton");
		}

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (firstButton)
				shopName = "Shop";
			else
				Main.npcChatText = Main.LocalPlayer.GetModPlayer<Common.Games.DailyChallenges>().Describe();
		}

		private static Item Prize(int itemType, int tokens) => new(itemType) {
			shopSpecialCurrency = Items.LoyaltyToken.CurrencyId,
			shopCustomPrice = tokens,
		};

		public override void AddShops()
		{
			new NPCShop(Type)
				.Add(new Item(ModContent.ItemType<Items.GamblersHandbook>()) { shopCustomPrice = 10 * 100 })
				.Add(new Item(ModContent.ItemType<Items.GamblersLedger>()) { shopCustomPrice = 10 * 100 })
				.Add(new Item(ModContent.ItemType<Items.ScratchCard>()) { shopCustomPrice = 50 * 100 })
				.Add(new Item(ModContent.ItemType<Items.PlatinumScratchCard>()) { shopCustomPrice = 5 * 10000 }, Condition.Hardmode)
				.Add(Prize(ModContent.ItemType<Items.Crates.LuckyCrate>(), 75))
				.Add(Prize(ModContent.ItemType<Items.Crates.HighRollerCrate>(), 150), Condition.Hardmode)
				.Add(Prize(ModContent.ItemType<Items.GoldenChip>(), 150), Condition.DownedSkeletron)
				.Add(Prize(ModContent.ItemType<Items.Armor.GamblerFedora>(), 60))
				.Add(Prize(ModContent.ItemType<Items.Armor.GamblerVest>(), 80))
				.Add(Prize(ModContent.ItemType<Items.Armor.GamblerShoes>(), 60))
				.Add(Prize(ModContent.ItemType<Items.MarkedInvitation>(), 60), Condition.DownedMechBossAny)
				.Add(Prize(ModContent.ItemType<Items.Mounts.Gamblorghini>(), 200))
				.Add(Prize(ModContent.ItemType<Items.Accessories.GolexWatch>(), 150))
				.Add(Prize(ModContent.ItemType<Items.Tools.ChipChainHook>(), 120))
				.Add(Prize(ModContent.ItemType<Items.Accessories.DealersVisor>(), 250), Condition.Hardmode)
				.Add(Prize(ModContent.ItemType<Items.Accessories.HighRollersRing>(), 400), Condition.Hardmode)
				.Add(Prize(ModContent.ItemType<Items.Armor.WhiteFedora>(), 60))
				.Add(Prize(ModContent.ItemType<Items.Armor.WhiteTuxedo>(), 80))
				.Add(Prize(ModContent.ItemType<Items.Armor.WhiteSpats>(), 60))
				.Add(Prize(ItemID.TopHat, 25))
				.Add(Prize(ItemID.TuxedoShirt, 40))
				.Add(Prize(ItemID.TuxedoPants, 40))
				.Add(Prize(ItemID.Sunglasses, 30))
				.Add(Prize(ItemID.GoldChandelier, 20))
				.Add(Prize(ItemID.DiscoBall, 30))
				.Register();
		}
	}

	/// <summary>Runs the Hold'em and Coin Duel tables.</summary>
	public class PokerDealer : CasinoStaff
	{
		private static readonly PersonLook look = new() {
			Hair = 26, SkinVariant = 1, Skin = new(225, 175, 140), HairColor = new(120, 70, 35),
			Shirt = new(120, 24, 48), UnderShirt = new(20, 18, 24), Pants = new(25, 25, 30), Shoes = new(15, 15, 15), HeadVanity = ItemID.GreenCap,
		};
		protected override PersonLook Look => look;

		protected override int BodyPose(int tick)
		{
			int cycle = tick % 420;
			if (cycle < 60)
				return (cycle / 7) % 2 == 0 ? PersonDoll.ArmForward : PersonDoll.ArmDown;
			return cycle is > 200 and < 220 ? PersonDoll.ArmUpForward : -1;
		}

		protected override string CustomTexture => "CasinoWorld/Content/NPCs/People/BlackjackDealer";
		protected override int VanillaLook => NPCID.TaxCollector;
		protected override int ChatLines => 5;
		protected override string HelpKey => "Mods.CasinoWorld.NPCs.PokerDealer.Help";
	}

	/// <summary>Sells VIP levels, the penthouse and the casino's own furniture.</summary>
	public class Concierge : CasinoStaff
	{
		// Concierge: silver hair, long midnight coat with gold, white gloves.
		private static readonly PersonLook look = new() {
			Hair = 18, SkinVariant = 3, Skin = new(245, 205, 175), HairColor = new(210, 212, 220),
			Shirt = new(24, 30, 70), UnderShirt = new(235, 195, 90), Pants = new(18, 20, 40), Shoes = new(15, 15, 15),
		};
		protected override PersonLook Look => look;

		protected override int BodyPose(int tick)
		{
			int cycle = tick % 600;
			return cycle < 30 ? PersonDoll.ArmForward : cycle is > 300 and < 330 ? PersonDoll.ArmUpForward : -1;
		}

		protected override string CustomTexture => "CasinoWorld/Content/NPCs/People/PrizeCashier";
		protected override int VanillaLook => NPCID.Steampunker;
		protected override int ChatLines => 5;

		private static readonly string[] shops = { "Furniture", "Walls", "Materials" };
		private static int nextShop;

		public override void SetChatButtons(ref string button, ref string button2)
		{
			button = Language.GetTextValue("Mods.CasinoWorld.NPCs.Concierge.Shop" + shops[nextShop]);
			button2 = Language.GetTextValue("Mods.CasinoWorld.NPCs.Concierge.Club");
		}

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (firstButton) {
				shopName = shops[nextShop];
				nextShop = (nextShop + 1) % shops.Length;   // the next visit to the counter shows the next catalogue
				return;
			}
			Main.npcChatText = "";
			Main.LocalPlayer.SetTalkNPC(-1);
			ModContent.GetInstance<Common.Club.LedgerSystem>().Open(Common.Club.LedgerTab.Club, true);
		}

		public override void AddShops()
		{
			var penthouse = new Condition("Mods.CasinoWorld.UI.Vip.PenthouseCondition", () => Common.Club.VipClub.LocalLevel >= Common.Club.VipClub.PenthouseLevel);
			var member = new Condition("Mods.CasinoWorld.UI.Vip.MemberCondition", () => Common.Club.VipClub.LocalLevel >= 1);
			var furniture = new NPCShop(Type, "Furniture");
			furniture.Add(new Item(ModContent.ItemType<Items.PenthouseKey>()) { shopCustomPrice = (int)Common.Club.VipClub.PenthousePrice }, penthouse);
			var walls = new NPCShop(Type, "Walls");
			var materials = new NPCShop(Type, "Materials");
			foreach (Items.Decor.DecorItem decor in Items.Decor.DecorCatalog.All) {
				NPCShop shop = decor.Kind switch {
					Items.Decor.DecorKind.Furniture => furniture,
					Items.Decor.DecorKind.Wall => walls,
					_ => materials,
				};
				shop.Add(new Item(decor.Type) { shopCustomPrice = decor.Price }, member);
			}
			furniture.Register();
			walls.Register();
			materials.Register();
		}
	}

	/// <summary>Runs the bar and sells drinks.</summary>
	public class Bartender : CasinoStaff
	{
		// Bartender: moustache-friendly short hair, white shirt, black waistcoat. Polishes a glass.
		private static readonly PersonLook look = new() {
			Hair = 24, SkinVariant = 3, Skin = new(200, 140, 100), HairColor = new(70, 45, 30),
			Shirt = new(25, 25, 28), UnderShirt = new(240, 238, 230), Pants = new(30, 30, 35), Shoes = new(20, 15, 10),
		};
		private static readonly PersonLook highRollerLook = new() {
			Hair = 24, SkinVariant = 3, Skin = new(200, 140, 100), HairColor = new(70, 45, 30),
			Shirt = new(240, 238, 230), UnderShirt = new(20, 20, 24), Pants = new(30, 30, 35), Shoes = new(20, 15, 10),
		};
		protected override PersonLook Look => HighRollers ? highRollerLook : look;

		protected override int BodyPose(int tick)
		{
			int cycle = tick % 480;
			return cycle < 120 ? ((cycle / 15) % 2 == 0 ? PersonDoll.ArmForward : PersonDoll.ArmDown) : -1;
		}

		protected override string CustomTexture => "CasinoWorld/Content/NPCs/People/Bartender";
		protected override int VanillaLook => NPCID.Demolitionist;
		protected override int ChatLines => 6;

		public override void SetChatButtons(ref string button, ref string button2)
		{
			button = Language.GetTextValue("LegacyInterface.28"); // "Shop"
			button2 = Language.GetTextValue("Mods.CasinoWorld.NPCs.Bartender.DiceButton");
		}

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (firstButton)
				shopName = "Shop";
			else
				ModContent.GetInstance<Common.Games.SicBoSystem>().Open();
		}

		public override void AddShops()
		{
			new NPCShop(Type)
				.Add(new Item(ItemID.Ale) { shopCustomPrice = Item.buyPrice(silver: 5) })
				.Add(new Item(ItemID.Sake) { shopCustomPrice = Item.buyPrice(silver: 8) })
				.Add(new Item(ItemID.FruitJuice) { shopCustomPrice = Item.buyPrice(silver: 10) })
				.Add(new Item(ItemID.Lemonade) { shopCustomPrice = Item.buyPrice(silver: 10) })
				.Add(new Item(ItemID.CreamSoda) { shopCustomPrice = Item.buyPrice(silver: 15) })
				.Register();
		}
	}
}
