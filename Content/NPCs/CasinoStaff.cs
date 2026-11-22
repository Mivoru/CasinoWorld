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
		protected override int VanillaLook => NPCID.Clothier;
		protected override int ChatLines => 6;
		protected override string HelpKey => "Mods.CasinoWorld.NPCs.RouletteCroupier.Help";
	}

	public class BlackjackDealer : CasinoStaff
	{
		protected override int VanillaLook => NPCID.TaxCollector;
		protected override int ChatLines => 6;
		protected override string HelpKey => "Mods.CasinoWorld.NPCs.BlackjackDealer.Help";
	}

	/// <summary>Runs the bar and sells drinks.</summary>
	public class Bartender : CasinoStaff
	{
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
