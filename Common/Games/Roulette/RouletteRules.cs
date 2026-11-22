using System.Collections.Generic;

namespace CasinoWorld.Common.Games.Roulette
{
	public enum RouletteBetType : byte
	{
		Straight, // one number, pays 35:1
		Red,
		Black,
		Odd,
		Even,
		Low,      // 1-18
		High,     // 19-36
		Dozen,    // Value 0..2: 1-12, 13-24, 25-36, pays 2:1
		Column,   // Value 0..2: numbers with (n - 1) % 3 == Value, pays 2:1
		Split,    // Value = a * 100 + b: two neighbouring numbers, pays 17:1
		Corner,   // Value = v: the four numbers v, v+1, v+3, v+4, pays 8:1
	}

	public readonly record struct RouletteBet(RouletteBetType Type, int Value = 0);

	/// <summary>
	/// European single-zero roulette. Every bet has the same house edge of 1/37 (2.70 %):
	/// for example a straight bet wins 1 time in 37 and returns 36 times the stake.
	/// </summary>
	public static class RouletteRules
	{
		public const int Pockets = 37;

		/// <summary>Numbers in wheel order, clockwise, starting at 0.</summary>
		public static readonly int[] WheelOrder = {
			0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10,
			5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26,
		};

		private static readonly HashSet<int> Reds = new() {
			1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36,
		};

		public static bool IsRed(int n) => Reds.Contains(n);

		public static int WheelIndexOf(int n) => System.Array.IndexOf(WheelOrder, n);

		public static bool Wins(RouletteBet bet, int n) => bet.Type switch {
			RouletteBetType.Straight => n == bet.Value,
			RouletteBetType.Red => n != 0 && IsRed(n),
			RouletteBetType.Black => n != 0 && !IsRed(n),
			RouletteBetType.Odd => n != 0 && n % 2 == 1,
			RouletteBetType.Even => n != 0 && n % 2 == 0,
			RouletteBetType.Low => n is >= 1 and <= 18,
			RouletteBetType.High => n is >= 19 and <= 36,
			RouletteBetType.Dozen => n != 0 && (n - 1) / 12 == bet.Value,
			RouletteBetType.Column => n != 0 && (n - 1) % 3 == bet.Value,
			RouletteBetType.Split => n != 0 && (n == bet.Value / 100 || n == bet.Value % 100),
			RouletteBetType.Corner => n != 0 && (n == bet.Value || n == bet.Value + 1 || n == bet.Value + 3 || n == bet.Value + 4),
			_ => false,
		};

		/// <summary>Winnings per unit staked, not counting the returned stake.</summary>
		public static int PayoutMultiplier(RouletteBetType type) => type switch {
			RouletteBetType.Straight => 35,
			RouletteBetType.Split => 17,
			RouletteBetType.Corner => 8,
			RouletteBetType.Dozen or RouletteBetType.Column => 2,
			_ => 1,
		};

		/// <summary>Total coins returned to the player for the result (stakes of winning bets plus winnings).</summary>
		public static long Resolve(IReadOnlyDictionary<RouletteBet, long> bets, int n)
		{
			long total = 0;
			foreach ((RouletteBet bet, long stake) in bets) {
				if (Wins(bet, n))
					total += stake * (PayoutMultiplier(bet.Type) + 1);
			}
			return total;
		}
	}
}
