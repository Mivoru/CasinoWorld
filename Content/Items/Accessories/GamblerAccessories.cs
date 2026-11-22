using CasinoWorld.Common.Gambler;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items.Accessories
{
	/// <summary>+2 Luck. Dropped by casino jackpots.</summary>
	public class FourLeafClover : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 22;
			Item.height = 22;
			Item.accessory = true;
			Item.value = Item.sellPrice(gold: 1);
			Item.rare = ItemRarityID.Green;
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 2;
		}
	}

	/// <summary>+3 % jackpot chance on top of Luck. Dropped by casino jackpots.</summary>
	public class AceUpTheSleeve : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 20;
			Item.height = 24;
			Item.accessory = true;
			Item.value = Item.sellPrice(gold: 1, silver: 50);
			Item.rare = ItemRarityID.Orange;
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetModPlayer<GamblerPlayer>().BonusJackpotChance += 0.03f;
		}
	}

	/// <summary>
	/// Luxury reskin of the Stopwatch: shows movement speed from the inventory, like the original.
	/// Bought with loyalty tokens at the prize counter.
	/// </summary>
	public class GolexWatch : ModItem
	{
		public override void SetDefaults()
		{
			Item.CloneDefaults(ItemID.Stopwatch);
			Item.width = 20;
			Item.height = 26;
			Item.rare = ItemRarityID.Pink;
		}

		public override void UpdateInfoAccessory(Player player)
		{
			player.accStopwatch = true;
		}
	}
}
