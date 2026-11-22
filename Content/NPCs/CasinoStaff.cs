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
		protected override PersonLook Look => look;

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
		protected override PersonLook Look => look;

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
	/// Prices are a first pass; 1 token = 10 silver wagered.
	/// </summary>
	public class PrizeCashier : CasinoStaff
	{
		// Cashier: blonde, red uniform jacket with gold trim. Waves at passing players now and then.
		private static readonly PersonLook look = new() {
			Hair = 9, SkinVariant = 5, Skin = new(250, 210, 180), HairColor = new(230, 195, 110),
			Shirt = new(185, 35, 45), UnderShirt = new(230, 185, 60), Pants = new(40, 30, 35), Shoes = new(90, 20, 25),
		};
		protected override PersonLook Look => look;

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
		}

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (firstButton)
				shopName = "Shop";
		}

		private static Item Prize(int itemType, int tokens) => new(itemType) {
			shopSpecialCurrency = Items.LoyaltyToken.CurrencyId,
			shopCustomPrice = tokens,
		};

		public override void AddShops()
		{
			new NPCShop(Type)
				.Add(Prize(ModContent.ItemType<Items.Crates.LuckyCrate>(), 75))
				.Add(Prize(ModContent.ItemType<Items.Crates.HighRollerCrate>(), 150), Condition.Hardmode)
				.Add(Prize(ModContent.ItemType<Items.GoldenChip>(), 150), Condition.DownedSkeletron)
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

	/// <summary>Runs the bar and sells drinks.</summary>
	public class Bartender : CasinoStaff
	{
		// Bartender: moustache-friendly short hair, white shirt, black waistcoat. Polishes a glass.
		private static readonly PersonLook look = new() {
			Hair = 24, SkinVariant = 3, Skin = new(200, 140, 100), HairColor = new(70, 45, 30),
			Shirt = new(25, 25, 28), UnderShirt = new(240, 238, 230), Pants = new(30, 30, 35), Shoes = new(20, 15, 10),
		};
		protected override PersonLook Look => look;

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
		}

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (firstButton)
				shopName = "Shop";
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
