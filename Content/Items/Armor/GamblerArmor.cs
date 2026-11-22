using CasinoWorld.Common.Gambler;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items.Armor
{
	/// <summary>
	/// Pre-hardmode Gambler set, dropped by casino jackpots. First-pass numbers:
	/// total defence 12 (between Gold and Platinum armour) and Luck 4 from the pieces
	/// plus 3 from the set bonus. Luck 7 raises average gambler damage by 21 % (see GamblerPlayer).
	/// </summary>
	[AutoloadEquip(EquipType.Head)]
	public class GamblerFedora : ModItem
	{
		public override void SetStaticDefaults()
		{
			ArmorIDs.Head.Sets.DrawHatHair[Item.headSlot] = true;
		}

		public override void SetDefaults()
		{
			Item.width = 26;
			Item.height = 18;
			Item.defense = 4;
			Item.value = Item.sellPrice(gold: 1);
			Item.rare = ItemRarityID.Green;
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 1;
			player.GetDamage<GamblerDamageClass>() += 0.05f;
		}

		public override bool IsArmorSet(Item head, Item body, Item legs)
			=> body.type == ModContent.ItemType<GamblerVest>() && legs.type == ModContent.ItemType<GamblerShoes>();

		public override void UpdateArmorSet(Player player)
		{
			player.setBonus = Language.GetTextValue("Mods.CasinoWorld.Items.GamblerFedora.SetBonus");
			GamblerPlayer gambler = player.GetModPlayer<GamblerPlayer>();
			gambler.Luck += 3;
			gambler.HotStreakSet = true;
		}
	}

	[AutoloadEquip(EquipType.Body)]
	public class GamblerVest : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 26;
			Item.height = 24;
			Item.defense = 5;
			Item.value = Item.sellPrice(gold: 1, silver: 50);
			Item.rare = ItemRarityID.Green;
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 2;
		}
	}

	[AutoloadEquip(EquipType.Legs)]
	public class GamblerShoes : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 24;
			Item.height = 16;
			Item.defense = 3;
			Item.value = Item.sellPrice(gold: 1);
			Item.rare = ItemRarityID.Green;
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 1;
			player.moveSpeed += 0.05f;
		}
	}
}
