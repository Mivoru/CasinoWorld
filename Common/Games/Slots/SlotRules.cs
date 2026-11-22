using Terraria.Utilities;

namespace CasinoWorld.Common.Games.Slots
{
	public enum SlotSymbol : byte { Cherry, Lemon, Bell, Clover, Bar, Seven, Diamond }

	/// <summary>
	/// Three independent reels with weighted symbols (32 stops each).
	/// Return to player is 95.72 % (house edge 4.28 %), hit rate 27.2 %, computed exactly over all
	/// 32^3 outcomes by tools/slots.py. Keep both files in sync when changing any number.
	/// </summary>
	public static class SlotRules
	{
		public const int Reels = 3;
		public const int SymbolCount = 7;

		/// <summary>Stops per reel for each symbol (sum 32).</summary>
		public static readonly int[] Weights = { 8, 7, 6, 5, 3, 2, 1 };

		/// <summary>Total return for three of a kind, in multiples of the bet.</summary>
		public static readonly int[] TriplePays = { 8, 11, 18, 30, 60, 150, 750 };

		public const int TwoCherriesPay = 4;   // cherries on reels 1 and 2
		public const int OneCherryPay = 1;     // cherry on reel 1 (bet back)

		/// <summary>
		/// Chance at full stake that a three of a kind also drops a jackpot item. With these numbers
		/// one item drops about every 150 spins at full stake, which costs about 6.5 full stakes
		/// on average (house edge 4.28 %), in line with roulette (about 4) and blackjack.
		/// </summary>
		public static float JackpotChance(SlotSymbol a, SlotSymbol b, SlotSymbol c)
		{
			if (a != b || b != c)
				return 0f;
			return a switch {
				SlotSymbol.Diamond or SlotSymbol.Seven => 1f,
				SlotSymbol.Bar => 1f,
				_ => 0.15f,
			};
		}

		public static SlotSymbol Roll(UnifiedRandom rand)
		{
			int stop = rand.Next(32);
			for (int k = 0; k < SymbolCount; k++) {
				stop -= Weights[k];
				if (stop < 0)
					return (SlotSymbol)k;
			}
			return SlotSymbol.Cherry;
		}

		/// <summary>Total return in multiples of the bet (0 = loss).</summary>
		public static int Payout(SlotSymbol a, SlotSymbol b, SlotSymbol c)
		{
			if (a == b && b == c)
				return TriplePays[(int)a];
			if (a == SlotSymbol.Cherry && b == SlotSymbol.Cherry)
				return TwoCherriesPay;
			if (a == SlotSymbol.Cherry)
				return OneCherryPay;
			return 0;
		}
	}
}
