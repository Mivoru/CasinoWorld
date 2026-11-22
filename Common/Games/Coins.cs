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

		/// <summary>Coins in the main inventory.</summary>
		public static long Count(Player player) => Utils.CoinsCount(out _, player.inventory);

		/// <summary>Removes coins from the player (inventory, then piggy bank and the like). False if they cannot pay.</summary>
		public static bool TryTake(Player player, long amount) => amount <= 0 || player.BuyItem(amount);

		/// <summary>Gives coins to the local player as items.</summary>
		public static void Give(Player player, long amount)
		{
			if (amount <= 0)
				return;

			int[] split = Utils.CoinsSplit(amount); // copper, silver, gold, platinum
			var source = player.GetSource_Misc("CasinoWorld");
			int[] types = { ItemID.CopperCoin, ItemID.SilverCoin, ItemID.GoldCoin, ItemID.PlatinumCoin };
			for (int k = 3; k >= 0; k--) {
				int left = split[k];
				while (left > 0) {
					int stack = System.Math.Min(left, 999);
					player.QuickSpawnItem(source, types[k], stack);
					left -= stack;
				}
			}
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
