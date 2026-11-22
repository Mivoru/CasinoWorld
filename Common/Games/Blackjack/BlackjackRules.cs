using System.Collections.Generic;

namespace CasinoWorld.Common.Games.Blackjack
{
	/// <summary>
	/// Cards are ints 0..51: rank = card % 13 (0 = ace, 1..8 = 2..9, 9 = 10, 10 = jack, 11 = queen, 12 = king),
	/// suit = card / 13 (spades, hearts, diamonds, clubs).
	///
	/// Table rules: 6-deck shoe, dealer stands on all 17s, blackjack pays 3:2, double on any first two cards,
	/// double after split allowed, one split per round, split aces get one card each.
	/// These rules give the house an edge of roughly 0.5 % against perfect basic strategy,
	/// and more against players who guess.
	/// </summary>
	public static class BlackjackRules
	{
		public const int Decks = 6;
		public const int DealerStandsOn = 17;

		public static int Rank(int card) => card % 13;

		public static int Suit(int card) => card / 13;

		/// <summary>Points of one card with aces counted as 1.</summary>
		public static int Points(int card) => Rank(card) switch {
			0 => 1,
			>= 9 => 10,
			int r => r + 1,
		};

		public static string RankName(int card) => Rank(card) switch {
			0 => "A",
			10 => "J",
			11 => "Q",
			12 => "K",
			int r => (r + 1).ToString(),
		};

		/// <summary>Best total of a hand; soft means an ace currently counts as 11.</summary>
		public static int Value(IReadOnlyList<int> cards, out bool soft)
		{
			int total = 0;
			bool hasAce = false;
			foreach (int card in cards) {
				total += Points(card);
				hasAce |= Rank(card) == 0;
			}

			soft = hasAce && total + 10 <= 21;
			return soft ? total + 10 : total;
		}

		public static int Value(IReadOnlyList<int> cards) => Value(cards, out _);

		public static bool IsBlackjack(IReadOnlyList<int> cards) => cards.Count == 2 && Value(cards) == 21;
	}
}
