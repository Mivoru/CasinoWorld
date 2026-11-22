using System;
using System.Collections.Generic;

namespace CasinoWorld.Common.Games.VideoSlots
{
	/// <summary>Bonus mechanic of a video slot (played during free spins).</summary>
	public enum SlotFeature
	{
		/// <summary>Winning symbols vanish and new ones tumble in; multiplier orbs add up (Gates of the Lunar Gods).</summary>
		CascadeOrbs,
		/// <summary>Fish carry coin values; each wild (the angler) collects them all (Angler's Bonanza).</summary>
		MoneyCollect,
		/// <summary>Wilds stick in place for the rest of the free spins (Boreal Wilds).</summary>
		StickyWilds,
		/// <summary>One symbol is chosen and expands over whole reels, paying on every line (Blood Moon Hunter).</summary>
		ExpandingSymbol,
	}

	/// <summary>Static description of a video slot: symbols, weights, pay table and bonus.</summary>
	public sealed class SlotDefinition
	{
		public string Key;
		public SlotFeature Feature;
		public int[] Weights;          // reel stop weight per symbol (same on all reels)
		public int[][] Pays;           // Pays[symbol] = { pay for 3, 4, 5 in a row } in line-bet multiples
		public int Wild;               // substitutes for everything except scatter and special symbols
		public int Scatter;            // 3+ anywhere trigger free spins
		public int Special = -1;       // orb (CascadeOrbs) or -1
		public int[] FishSymbols = Array.Empty<int>();   // MoneyCollect: symbols that carry values
		public int[] OrbValues = { 2, 3, 5, 10 };
		public int[] OrbWeights = { 50, 30, 15, 5 };
		public int[] FishValues = { 2, 5, 10, 25, 50 };  // in multiples of the total bet / 10
		public int[] FishWeights = { 50, 28, 14, 6, 2 };
		public int FreeSpins = 10;
		public int[] ScatterPays = { 2, 10, 50 };        // 3, 4, 5 scatters, in total-bet multiples
		public int SymbolCount => Weights.Length;
	}

	/// <summary>One cell of the grid: symbol plus an optional value (orb multiplier or fish value).</summary>
	public struct SlotCell
	{
		public int Symbol;
		public int Value;
		public bool Sticky;
	}

	/// <summary>A winning payline: which line, which symbol, how many in a row, the win in line-bet multiples.</summary>
	public readonly record struct LineWin(int Line, int Symbol, int Count, long Win);

	/// <summary>Everything that happened in one spin (including tumbles), for the UI to animate.</summary>
	public sealed class SpinOutcome
	{
		public readonly List<SlotCell[,]> Steps = new();   // grid after the spin and after each tumble
		public readonly List<List<LineWin>> StepWins = new();
		public long Win;                                     // total, in multiples of the line bet
		public int Scatters;
		public int FreeSpinsAwarded;
		public int Multiplier = 1;
		public int ExpandingSymbolChosen = -1;
	}

	/// <summary>
	/// Engine for 5x3 video slots with 10 paylines (no Terraria types, so tools/slotsim can run it).
	/// Wins are counted in multiples of the line bet (total bet / 10).
	/// </summary>
	public sealed class VideoSlotEngine
	{
		public const int Reels = 5, Rows = 3, LineCount = 10;

		public static readonly int[][] Lines = {
			new[] { 1, 1, 1, 1, 1 }, new[] { 0, 0, 0, 0, 0 }, new[] { 2, 2, 2, 2, 2 }, new[] { 0, 1, 2, 1, 0 }, new[] { 2, 1, 0, 1, 2 },
			new[] { 0, 0, 1, 2, 2 }, new[] { 2, 2, 1, 0, 0 }, new[] { 1, 0, 0, 0, 1 }, new[] { 1, 2, 2, 2, 1 }, new[] { 1, 0, 1, 2, 1 },
		};

		private readonly SlotDefinition def;
		private readonly Func<int, int> next;   // random integer in [0, n)
		private readonly int totalWeight;

		// Free-spin state.
		public int FreeSpinsLeft;
		public int BonusMultiplier = 1;
		public int ExpandingSymbol = -1;
		public int WildsCollected;
		private readonly bool[,] sticky = new bool[Reels, Rows];

		public bool InBonus => FreeSpinsLeft > 0;

		public VideoSlotEngine(SlotDefinition definition, Func<int, int> random)
		{
			def = definition;
			next = random;
			foreach (int w in def.Weights)
				totalWeight += w;
		}

		private int Pick(int[] weights, int total)
		{
			int roll = next(total);
			for (int k = 0; k < weights.Length; k++) {
				roll -= weights[k];
				if (roll < 0)
					return k;
			}
			return 0;
		}

		private static int Sum(int[] a)
		{
			int s = 0;
			foreach (int v in a)
				s += v;
			return s;
		}

		private SlotCell RollCell()
		{
			var cell = new SlotCell { Symbol = Pick(def.Weights, totalWeight) };
			if (cell.Symbol == def.Special && def.Feature == SlotFeature.CascadeOrbs)
				cell.Value = def.OrbValues[Pick(def.OrbWeights, Sum(def.OrbWeights))];
			if (Array.IndexOf(def.FishSymbols, cell.Symbol) >= 0)
				cell.Value = def.FishValues[Pick(def.FishWeights, Sum(def.FishWeights))];
			return cell;
		}

		private bool Substitutes(int symbol) => symbol != def.Scatter && symbol != def.Special;

		/// <summary>Evaluates all paylines (left to right, 3+ in a row, wilds substitute).</summary>
		private List<LineWin> Evaluate(SlotCell[,] grid, int expanding = -1)
		{
			var wins = new List<LineWin>();
			for (int l = 0; l < LineCount; l++) {
				int[] line = Lines[l];
				int symbol = -1, count = 0;
				for (int reel = 0; reel < Reels; reel++) {
					int s = grid[reel, line[reel]].Symbol;
					if (!Substitutes(s))
						break;
					if (s == def.Wild) {
						count++;
						continue;
					}
					if (symbol < 0) {
						symbol = s;
						count++;
					}
					else if (s == symbol)
						count++;
					else
						break;
				}
				if (symbol < 0 && count >= 3)
					symbol = def.Wild;   // all wilds: pay as the wild symbol
				if (symbol >= 0 && count >= 3 && def.Pays[symbol] != null) {
					long pay = def.Pays[symbol][count - 3];
					if (pay > 0)
						wins.Add(new LineWin(l, symbol, count, pay));
				}
			}
			// Expanding symbol (bonus): pays on every line if it covers 3+ reels, anywhere.
			if (expanding >= 0) {
				int reels = 0;
				for (int reel = 0; reel < Reels; reel++) {
					for (int row = 0; row < Rows; row++) {
						if (grid[reel, row].Symbol == expanding) {
							reels++;
							break;
						}
					}
				}
				if (reels >= 3) {
					long pay = def.Pays[expanding][Math.Min(reels, 5) - 3];
					for (int l = 0; l < LineCount; l++)
						wins.Add(new LineWin(l, expanding, reels, pay));
				}
			}
			return wins;
		}

		private int CountScatters(SlotCell[,] grid)
		{
			int n = 0;
			foreach (SlotCell c in grid)
				if (c.Symbol == def.Scatter)
					n++;
			return n;
		}

		/// <summary>Plays one spin (base game or free spin) and returns what happened.</summary>
		public SpinOutcome Spin()
		{
			var outcome = new SpinOutcome();
			bool bonus = InBonus;
			var grid = new SlotCell[Reels, Rows];
			for (int reel = 0; reel < Reels; reel++) {
				for (int row = 0; row < Rows; row++) {
					grid[reel, row] = bonus && def.Feature == SlotFeature.StickyWilds && sticky[reel, row]
						? new SlotCell { Symbol = def.Wild, Sticky = true }
						: RollCell();
				}
			}

			long win = 0;
			int orbSum = 0;
			outcome.Steps.Add((SlotCell[,])grid.Clone());
			List<LineWin> wins = Evaluate(grid, bonus && def.Feature == SlotFeature.ExpandingSymbol ? ExpandingSymbol : -1);
			outcome.StepWins.Add(wins);
			foreach (LineWin w in wins)
				win += w.Win;

			switch (def.Feature) {
				case SlotFeature.CascadeOrbs:
					// Tumble: remove winning cells, drop new ones from the top, repeat while there are wins.
					while (wins.Count > 0 && outcome.Steps.Count < 12) {
						var remove = new bool[Reels, Rows];
						foreach (LineWin w in wins) {
							for (int reel = 0; reel < w.Count; reel++)
								remove[reel, Lines[w.Line][reel]] = true;
						}
						for (int reel = 0; reel < Reels; reel++) {
							var keep = new List<SlotCell>();
							for (int row = Rows - 1; row >= 0; row--)
								if (!remove[reel, row])
									keep.Add(grid[reel, row]);
							for (int row = Rows - 1; row >= 0; row--) {
								int idx = Rows - 1 - row;
								grid[reel, row] = idx < keep.Count ? keep[idx] : RollCell();
							}
						}
						outcome.Steps.Add((SlotCell[,])grid.Clone());
						wins = Evaluate(grid);
						outcome.StepWins.Add(wins);
						foreach (LineWin w in wins)
							win += w.Win;
					}
					foreach (SlotCell c in grid)
						if (c.Symbol == def.Special)
							orbSum += c.Value;
					if (win > 0 && orbSum > 0) {
						if (bonus) {
							BonusMultiplier += orbSum;
							outcome.Multiplier = BonusMultiplier;
						}
						else
							outcome.Multiplier = orbSum;
						win *= outcome.Multiplier;
					}
					else if (bonus && win > 0 && BonusMultiplier > 1) {
						outcome.Multiplier = BonusMultiplier;
						win *= BonusMultiplier;
					}
					break;

				case SlotFeature.MoneyCollect:
					if (bonus) {
						int wilds = 0, fishValue = 0;
						foreach (SlotCell c in grid) {
							if (c.Symbol == def.Wild)
								wilds++;
							if (Array.IndexOf(def.FishSymbols, c.Symbol) >= 0)
								fishValue += c.Value;
						}
						// Values are in total-bet / 10 = line-bet units, so they add directly.
						win += (long)wilds * fishValue * BonusMultiplier;
						int before = WildsCollected / 4;
						WildsCollected += wilds;
						if (WildsCollected / 4 > before && BonusMultiplier < 10) {
							FreeSpinsLeft += 10;
							BonusMultiplier = BonusMultiplier == 1 ? 2 : BonusMultiplier == 2 ? 3 : 10;
							outcome.FreeSpinsAwarded += 10;
						}
						outcome.Multiplier = BonusMultiplier;
					}
					break;

				case SlotFeature.StickyWilds:
					if (bonus) {
						for (int reel = 0; reel < Reels; reel++)
							for (int row = 0; row < Rows; row++)
								if (grid[reel, row].Symbol == def.Wild)
									sticky[reel, row] = true;
					}
					break;
			}

			// Scatters pay anywhere and trigger (or retrigger) free spins.
			outcome.Scatters = CountScatters(outcome.Steps[0]);
			if (outcome.Scatters >= 3) {
				win += def.ScatterPays[Math.Min(outcome.Scatters, 5) - 3] * LineCount;
				if (!bonus) {
					FreeSpinsLeft = def.FreeSpins;
					BonusMultiplier = 1;
					WildsCollected = 0;
					Array.Clear(sticky);
					if (def.Feature == SlotFeature.ExpandingSymbol) {
						do
							ExpandingSymbol = next(def.SymbolCount);
						while (ExpandingSymbol == def.Wild || ExpandingSymbol == def.Scatter || def.Pays[ExpandingSymbol] == null);
						outcome.ExpandingSymbolChosen = ExpandingSymbol;
					}
					outcome.FreeSpinsAwarded += def.FreeSpins;
				}
				else {
					FreeSpinsLeft += 5;
					outcome.FreeSpinsAwarded += 5;
				}
			}
			if (bonus)
				FreeSpinsLeft--;

			outcome.Win = win;
			return outcome;
		}
	}
}
