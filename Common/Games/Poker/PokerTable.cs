using System;
using System.Collections.Generic;

namespace CasinoWorld.Common.Games.Poker
{
	public enum PokerStreet : byte { Waiting, Preflop, Flop, Turn, River, Showdown }

	public enum PokerAction : byte { Fold, Call, Raise }

	/// <summary>Hand ranking. Cards are suit * 13 + rank with rank 0 = ace, as everywhere in the casino.</summary>
	public static class PokerHands
	{
		/// <summary>Category 0 high card .. 8 straight flush, in the top bits, then the ranks that break ties.</summary>
		public static int Best(ReadOnlySpan<int> cards)
		{
			Span<int> counts = stackalloc int[15];
			Span<int> suits = stackalloc int[4];
			Span<int> suitMask = stackalloc int[4];
			int mask = 0;
			foreach (int card in cards) {
				int rank = card % 13 == 0 ? 14 : card % 13 + 1, suit = card / 13;
				counts[rank]++;
				suits[suit]++;
				suitMask[suit] |= 1 << rank;
				mask |= 1 << rank;
			}
			for (int s = 0; s < 4; s++)
				if (suits[s] >= 5) {
					int top = Straight(suitMask[s]);
					return top > 0 ? Pack(8, top) : Flush(suitMask[s]);
				}
			return Rest(counts, mask);
		}

		private static int Flush(int mask)
		{
			// Seven cards with a flush have at least five ranks, so a full house or four of a kind cannot hide behind it.
			Span<int> k = stackalloc int[5];
			Top(mask, k);
			return Pack(5, k[0], k[1], k[2], k[3], k[4]);
		}

		private static int Rest(Span<int> counts, int mask)
		{
			int four = 0, three = 0, three2 = 0, pair = 0, pair2 = 0;
			for (int r = 14; r >= 2; r--) {
				if (counts[r] == 4)
					four = Math.Max(four, r);
				else if (counts[r] == 3) {
					if (three == 0)
						three = r;
					else if (three2 == 0)
						three2 = r;
				}
				else if (counts[r] == 2) {
					if (pair == 0)
						pair = r;
					else if (pair2 == 0)
						pair2 = r;
				}
			}
			Span<int> k = stackalloc int[5];
			if (four > 0) {
				Top(mask & ~(1 << four), k);
				return Pack(7, four, k[0]);
			}
			if (three > 0 && (three2 > 0 || pair > 0))
				return Pack(6, three, Math.Max(three2, pair));
			int straight = Straight(mask);
			if (straight > 0)
				return Pack(4, straight);
			if (three > 0) {
				Top(mask & ~(1 << three), k);
				return Pack(3, three, k[0], k[1]);
			}
			if (pair2 > 0) {
				Top(mask & ~(1 << pair) & ~(1 << pair2), k);
				return Pack(2, pair, pair2, k[0]);
			}
			if (pair > 0) {
				Top(mask & ~(1 << pair), k);
				return Pack(1, pair, k[0], k[1], k[2]);
			}
			Top(mask, k);
			return Pack(0, k[0], k[1], k[2], k[3], k[4]);
		}

		private static void Top(int mask, Span<int> into)
		{
			int n = 0;
			for (int r = 14; r >= 2 && n < into.Length; r--)
				if ((mask & 1 << r) != 0)
					into[n++] = r;
			while (n < into.Length)
				into[n++] = 0;
		}

		private static int Straight(int mask)
		{
			if ((mask & 1 << 14) != 0)
				mask |= 1 << 1;
			for (int top = 14; top >= 5; top--) {
				int need = 0b11111 << (top - 4);
				if ((mask & need) == need)
					return top;
			}
			return 0;
		}

		private static int Pack(int category, int a = 0, int b = 0, int c = 0, int d = 0, int e = 0)
			=> category << 20 | a << 16 | b << 12 | c << 8 | d << 4 | e;

		public static int Category(int value) => value >> 20;
	}

	public class PokerSeat
	{
		/// <summary>Client index of the player, -1 for a bot.</summary>
		public int Player = -1;
		public string Name = "";
		public long Stack;
		public readonly int[] Hole = { -1, -1 };
		/// <summary>Put in on this street, and in the whole hand.</summary>
		public long Bet, Total;
		public bool Folded = true, AllIn, Acted;
		/// <summary>Result of the last finished hand: money won or lost, and the hand shown (0 = none).</summary>
		public long Net;
		public int Value;
		public string Last = "";
		public int Style;

		public bool Bot => Player < 0;
		public bool InHand => !Folded;
	}

	/// <summary>
	/// Fixed-limit Texas Hold'em for four seats. Empty seats are played by the house's regulars. Knows nothing of
	/// Terraria: the host (server or single player) ticks it and forwards the players' actions.
	/// The house takes 5 % of every pot that saw a flop, at most three big bets.
	/// </summary>
	public class PokerTable
	{
		public const int Seats = 4, MaxRaises = 4;
		public const int BuyInBigBets = 20;
		private const int BotTicks = 50, HumanTicks = 60 * 30, ShowTicks = 260;

		private static readonly string[] BotNames = { "Lou", "Duchess", "Slick", "Penny", "The Count", "Roxie" };

		public readonly PokerSeat[] Seat = new PokerSeat[Seats];
		public readonly int[] Board = { -1, -1, -1, -1, -1 };
		public PokerStreet Street = PokerStreet.Waiting;
		public long SmallBet = 1000;
		public int Button, Turn = -1, Raises, Timer, HandId;
		public long Rake;
		/// <summary>Set whenever something visible changed; the host sends the state and clears it.</summary>
		public bool Dirty = true;

		private readonly Random rand;
		private readonly int[] deck = new int[52];
		private int dealt;

		public PokerTable(Random random)
		{
			rand = random;
			for (int k = 0; k < Seats; k++) {
				Seat[k] = new PokerSeat();
				SeatBot(k);
			}
		}

		public long BigBet => SmallBet * 2;
		public long BuyIn => BigBet * BuyInBigBets;
		public long StreetBet => Street is PokerStreet.Turn or PokerStreet.River ? BigBet : SmallBet;

		public long Pot
		{
			get {
				long sum = 0;
				foreach (PokerSeat s in Seat)
					sum += s.Total;
				return sum;
			}
		}

		public long HighBet
		{
			get {
				long high = 0;
				foreach (PokerSeat s in Seat)
					high = Math.Max(high, s.Bet);
				return high;
			}
		}

		public bool HasHumans
		{
			get {
				foreach (PokerSeat s in Seat)
					if (!s.Bot)
						return true;
				return false;
			}
		}

		public long ToCall(PokerSeat s) => Math.Min(s.Stack, HighBet - s.Bet);

		public bool CanRaise(PokerSeat s) => Raises < MaxRaises && s.Stack > HighBet - s.Bet;

		private void SeatBot(int index)
		{
			PokerSeat s = Seat[index];
			s.Player = -1;
			s.Style = rand.Next(3);
			string name;
			do
				name = BotNames[rand.Next(BotNames.Length)];
			while (Array.Exists(Seat, o => o != null && o != s && o.Name == name));
			s.Name = name;
			s.Stack = BuyIn;
			s.Folded = true;
			s.Last = "";
		}

		/// <summary>Takes a bot's chair, or adds chips to the player's stack. False when the table is full.</summary>
		public bool Sit(int player, string name, long amount)
		{
			int index = IndexOf(player);
			if (index < 0) {
				index = Array.FindIndex(Seat, s => s.Bot && (Street is PokerStreet.Waiting or PokerStreet.Showdown || s.Folded));
				if (index < 0)
					index = Array.FindIndex(Seat, s => s.Bot);
				if (index < 0)
					return false;
				PokerSeat seat = Seat[index];
				if (Street is not (PokerStreet.Waiting or PokerStreet.Showdown) && seat.InHand)
					ForceFold(index);
				seat.Player = player;
				seat.Name = name;
				seat.Stack = 0;
				seat.Last = "";
				seat.Net = 0;
				seat.Value = 0;
				seat.Bet = seat.Total = Street is PokerStreet.Waiting or PokerStreet.Showdown ? 0 : seat.Total;
			}
			Seat[index].Stack += amount;
			Dirty = true;
			return true;
		}

		public int IndexOf(int player) => Array.FindIndex(Seat, s => s.Player == player && player >= 0);

		public void Leave(int player)
		{
			int index = IndexOf(player);
			if (index < 0)
				return;
			if (Seat[index].InHand && Street is not (PokerStreet.Waiting or PokerStreet.Showdown))
				ForceFold(index);
			long total = Seat[index].Total, bet = Seat[index].Bet;
			SeatBot(index);
			Seat[index].Total = total;   // the money in the pot stays there
			Seat[index].Bet = bet;
			Dirty = true;
		}

		private void ForceFold(int index)
		{
			if (Turn == index)
				Apply(index, PokerAction.Fold);
			else {
				Seat[index].Folded = true;
				Seat[index].Last = "Fold";
				if (Alive() == 1)
					Finish();
			}
			Dirty = true;
		}

		private int Alive()
		{
			int n = 0;
			foreach (PokerSeat s in Seat)
				if (s.InHand)
					n++;
			return n;
		}

		// ------------------------------------------------------------------ flow

		/// <summary>One tick of the host.</summary>
		public void Update()
		{
			if (Street is PokerStreet.Waiting or PokerStreet.Showdown) {
				if (Timer > 0) {
					Timer--;
					return;
				}
				if (HasHumans)
					StartHand();
				else if (Street != PokerStreet.Waiting) {
					Street = PokerStreet.Waiting;
					Array.Fill(Board, -1);
					Dirty = true;
				}
				return;
			}
			if (Turn < 0)
				return;
			Timer--;
			PokerSeat s = Seat[Turn];
			if (s.Bot && Timer <= 0)
				Apply(Turn, BotDecision(Turn));
			else if (!s.Bot && Timer <= 0)
				Apply(Turn, ToCall(s) == 0 ? PokerAction.Call : PokerAction.Fold);
		}

		public void StartHand()
		{
			for (int k = 0; k < 52; k++)
				deck[k] = k;
			for (int k = 51; k > 0; k--) {
				int j = rand.Next(k + 1);
				(deck[k], deck[j]) = (deck[j], deck[k]);
			}
			dealt = 0;
			HandId++;
			Array.Fill(Board, -1);
			foreach (PokerSeat s in Seat) {
				if (s.Bot && (s.Stack < BigBet * 6 || s.Stack > BuyIn * 4))
					s.Stack = BuyIn;
				s.Bet = s.Total = s.Net = 0;
				s.Value = 0;
				s.Last = "";
				s.AllIn = s.Acted = false;
				s.Folded = s.Stack <= 0;
				s.Hole[0] = s.Folded ? -1 : deck[dealt++];
				s.Hole[1] = s.Folded ? -1 : deck[dealt++];
			}
			if (Alive() < 2) {
				Timer = 60;
				Street = PokerStreet.Waiting;
				return;
			}
			Street = PokerStreet.Preflop;
			Button = Next(Button);
			int small = Next(Button), big = Next(small);
			Put(Seat[small], SmallBet / 2);
			Put(Seat[big], SmallBet);
			Seat[small].Last = "Small";
			Seat[big].Last = "Big";
			Raises = 1;
			SetTurn(Next(big));
			Dirty = true;
		}

		/// <summary>Next seat that is in the hand.</summary>
		private int Next(int from)
		{
			for (int k = 1; k <= Seats; k++)
				if (Seat[(from + k) % Seats].InHand)
					return (from + k) % Seats;
			return from;
		}

		private void Put(PokerSeat s, long amount)
		{
			amount = Math.Min(amount, s.Stack);
			s.Stack -= amount;
			s.Bet += amount;
			s.Total += amount;
			if (s.Stack == 0)
				s.AllIn = true;
		}

		private void SetTurn(int index)
		{
			// Skips players without chips; closes the street when nobody is left to act.
			for (int k = 0; k < Seats; k++) {
				int at = (index + k) % Seats;
				PokerSeat s = Seat[at];
				if (s.InHand && !s.AllIn && (!s.Acted || s.Bet < HighBet)) {
					Turn = at;
					Timer = s.Bot ? BotTicks + rand.Next(40) : HumanTicks;
					Dirty = true;
					return;
				}
			}
			NextStreet();
		}

		public void Act(int player, PokerAction action)
		{
			int index = IndexOf(player);
			if (index >= 0 && index == Turn)
				Apply(index, action);
		}

		private void Apply(int index, PokerAction action)
		{
			PokerSeat s = Seat[index];
			if (action == PokerAction.Raise && !CanRaise(s))
				action = PokerAction.Call;
			switch (action) {
				case PokerAction.Fold:
					s.Folded = true;
					s.Last = "Fold";
					break;
				case PokerAction.Call:
					long due = ToCall(s);
					s.Last = due == 0 ? "Check" : "Call";
					Put(s, due);
					break;
				case PokerAction.Raise:
					s.Last = HighBet == 0 ? "Bet" : "Raise";
					long before = HighBet;
					Put(s, HighBet - s.Bet + StreetBet);
					if (s.Bet > before) {
						Raises++;
						foreach (PokerSeat o in Seat)
							if (o != s)
								o.Acted = false;
					}
					break;
			}
			s.Acted = true;
			Dirty = true;
			if (Alive() == 1) {
				Finish();
				return;
			}
			SetTurn((index + 1) % Seats);
		}

		private void NextStreet()
		{
			Turn = -1;
			foreach (PokerSeat s in Seat) {
				s.Bet = 0;
				s.Acted = false;
			}
			Raises = 0;
			switch (Street) {
				case PokerStreet.Preflop:
					Street = PokerStreet.Flop;
					for (int k = 0; k < 3; k++)
						Board[k] = deck[dealt++];
					break;
				case PokerStreet.Flop:
					Street = PokerStreet.Turn;
					Board[3] = deck[dealt++];
					break;
				case PokerStreet.Turn:
					Street = PokerStreet.River;
					Board[4] = deck[dealt++];
					break;
				default:
					Finish();
					return;
			}
			Dirty = true;
			int able = 0;
			foreach (PokerSeat s in Seat)
				if (s.InHand && !s.AllIn)
					able++;
			if (able <= 1)
				NextStreet();      // everyone is all in: run the board out
			else
				SetTurn(Next(Button));
		}

		/// <summary>Pays the pots (side pots included), takes the rake and shows the hands.</summary>
		private void Finish()
		{
			bool showdown = Alive() > 1;
			long pot = Pot;
			Rake = Board[2] >= 0 ? Math.Min(pot / 20, BigBet * 3) : 0;
			Span<int> seven = stackalloc int[7];
			var won = new long[Seats];
			if (showdown)
				foreach (PokerSeat s in Seat)
					if (s.InHand) {
						for (int k = 0; k < 5; k++)
							seven[k] = Board[k];
						seven[5] = s.Hole[0];
						seven[6] = s.Hole[1];
						s.Value = PokerHands.Best(seven);
					}
			var left = new long[Seats];
			for (int k = 0; k < Seats; k++)
				left[k] = Seat[k].Total;
			while (true) {
				// Smallest stake among the players still contesting makes one pot.
				long level = long.MaxValue;
				for (int k = 0; k < Seats; k++)
					if (Seat[k].InHand && left[k] > 0)
						level = Math.Min(level, left[k]);
				if (level == long.MaxValue)
					break;
				long part = 0;
				int best = -1;
				var winners = new List<int>();
				for (int k = 0; k < Seats; k++) {
					if (Seat[k].InHand && left[k] > 0) {
						int value = showdown ? Seat[k].Value : 1;
						if (value > best) {
							best = value;
							winners.Clear();
						}
						if (value == best)
							winners.Add(k);
					}
				}
				for (int k = 0; k < Seats; k++) {
					long take = Math.Min(level, left[k]);
					left[k] -= take;
					part += take;
				}
				for (int k = 0; k < winners.Count; k++)
					won[winners[k]] += part / winners.Count + (k < part % winners.Count ? 1 : 0);
			}
			// Chips of players who folded beyond every pot go to the last winner.
			long rest = 0;
			for (int k = 0; k < Seats; k++)
				rest += left[k];
			int winner = Array.FindIndex(won, w => w > 0);
			if (winner >= 0)
				won[winner] += rest;
			// Rake, shared by the winners in proportion.
			long paid = 0;
			foreach (long w in won)
				paid += w;
			for (int k = 0; k < Seats; k++) {
				PokerSeat s = Seat[k];
				long gain = paid > 0 ? won[k] - (long)((double)Rake * won[k] / paid) : 0;
				s.Stack += gain;
				s.Net = gain - s.Total;
				if (gain > 0)
					s.Last = "Win";
			}
			Street = PokerStreet.Showdown;
			Turn = -1;
			Timer = ShowTicks;
			Dirty = true;
		}

		/// <summary>True if this seat's cards are open to everyone now.</summary>
		public bool Shown(PokerSeat s) => Street == PokerStreet.Showdown && s.InHand && s.Value > 0;

		// ------------------------------------------------------------------ bots

		/// <summary>Chance to win against that many random hands, by a short simulation.</summary>
		public float Equity(int[] hole, int opponents, int trials)
		{
			Span<bool> used = stackalloc bool[52];
			used[hole[0]] = used[hole[1]] = true;
			int known = 0;
			foreach (int card in Board)
				if (card >= 0) {
					used[card] = true;
					known++;
				}
			Span<int> rest = stackalloc int[52];
			int count = 0;
			for (int k = 0; k < 52; k++)
				if (!used[k])
					rest[count++] = k;
			Span<int> seven = stackalloc int[7];
			float score = 0f;
			for (int t = 0; t < trials; t++) {
				int need = 5 - known + opponents * 2;
				for (int k = 0; k < need; k++) {
					int j = k + rand.Next(count - k);
					(rest[k], rest[j]) = (rest[j], rest[k]);
				}
				for (int k = 0; k < 5; k++)
					seven[k] = k < known ? Board[k] : rest[k - known];
				seven[5] = hole[0];
				seven[6] = hole[1];
				int mine = PokerHands.Best(seven), ties = 0;
				bool lost = false;
				for (int o = 0; o < opponents && !lost; o++) {
					seven[5] = rest[5 - known + o * 2];
					seven[6] = rest[5 - known + o * 2 + 1];
					int theirs = PokerHands.Best(seven);
					lost = theirs > mine;
					if (theirs == mine)
						ties++;
				}
				if (!lost)
					score += 1f / (1 + ties);
			}
			return score / trials;
		}

		private PokerAction BotDecision(int index)
		{
			PokerSeat s = Seat[index];
			int opponents = Alive() - 1;
			float equity = Equity(s.Hole, opponents, 70);
			float fair = 1f / (opponents + 1);
			long due = ToCall(s);
			float odds = due / (float)Math.Max(1, Pot + due);
			// Raises from others mean better hands than random ones. Styles: 0 careful, 1 solid, 2 loose.
			float doubt = (Raises > 1 ? 0.035f * (Raises - 1) : 0f) + (s.Style == 0 ? 0.03f : s.Style == 2 ? -0.04f : 0f);
			float strength = equity - doubt;
			float mood = (float)rand.NextDouble();
			if (CanRaise(s)) {
				float need = fair * (s.Style == 2 ? 1.25f : 1.4f) + 0.05f * Raises;
				if (strength > need && mood < 0.85f)
					return PokerAction.Raise;
				if (due == 0 && mood < (s.Style == 2 ? 0.12f : 0.07f))
					return PokerAction.Raise;      // a bluff
			}
			if (due == 0)
				return PokerAction.Call;
			if (strength > odds + 0.02f || strength > fair * 0.95f && Street == PokerStreet.Preflop && Raises <= 2)
				return PokerAction.Call;
			return mood < 0.05f ? PokerAction.Call : PokerAction.Fold;
		}

		/// <summary>Used by the bots' own decision code in tests.</summary>
		public PokerAction Decide(int index) => BotDecision(index);
	}
}
