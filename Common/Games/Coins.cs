using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace CasinoWorld.Common.Games
{
	/// <summary>Helpers for paying with and paying out vanilla coins. Values are in copper.</summary>
	public static class Coins
	{
		public const long Silver = 100;
		public const long Gold = 10_000;
		public const long Platinum = 1_000_000;

		/// <summary>
		/// All coins the player can pay with: inventory, piggy bank, safe, Defender's Forge and Void Vault,
		/// the same places Player.BuyItem takes them from (mouse and ammo slots are not among them).
		/// </summary>
		public static long Count(Player player)
			=> Utils.CoinsCount(out _, player.inventory, 58, 57, 56, 55, 54)
				+ Utils.CoinsCount(out _, player.bank.item)
				+ Utils.CoinsCount(out _, player.bank2.item)
				+ Utils.CoinsCount(out _, player.bank3.item)
				+ Utils.CoinsCount(out _, player.bank4.item);

		/// <summary>Removes coins from the player (inventory, then piggy bank and the like). False if they cannot pay.</summary>
		public static bool TryTake(Player player, long amount) => amount <= 0 || player.BuyItem(amount);

		/// <summary>Gives coins to the local player, straight into the inventory.</summary>
		public static void Give(Player player, long amount)
		{
			if (amount <= 0)
				return;

			int[] split = Utils.CoinsSplit(amount); // copper, silver, gold, platinum
			int[] types = { ItemID.CopperCoin, ItemID.SilverCoin, ItemID.GoldCoin, ItemID.PlatinumCoin };
			for (int k = 3; k >= 0; k--) {
				int left = split[k];
				while (left > 0) {
					int stack = System.Math.Min(left, 999);
					GiveItem(player, new Item(types[k], stack));
					left -= stack;
				}
			}
		}

		/// <summary>
		/// Puts an item straight into the player's inventory (like picking it up); whatever does not
		/// fit is dropped at the player. Unlike spawning items on the ground, nothing is lost when the
		/// player saves and quits right after (items on the ground are not saved).
		/// </summary>
		public static void GiveItem(Player player, Item item)
		{
			if (item == null || item.IsAir)
				return;
			Item rest = player.GetItem(player.whoAmI, item.Clone(), GetItemSettings.InventoryEntityToPlayerInventorySettings);
			if (!rest.IsAir)
				player.QuickSpawnItem(player.GetSource_Misc("CasinoWorld"), rest, rest.stack);
		}

		/// <summary>Short text such as "1g 50s".</summary>
		public static string Format(long amount)
		{
			if (amount <= 0)
				return Language.GetTextValue("Mods.CasinoWorld.Coins.C", 0);

			int[] split = Utils.CoinsSplit(amount);
			string[] keys = { "C", "S", "G", "P" };
			var parts = new List<string>();
			for (int k = 3; k >= 0; k--) {
				if (split[k] > 0)
					parts.Add(Language.GetTextValue("Mods.CasinoWorld.Coins." + keys[k], split[k]));
			}
			return string.Join(" ", parts);
		}
	}
}
