using System;
using System.Collections.Generic;

namespace CasinoWorld.Common.Games.VideoSlots
{
	// Video slot engines. No Terraria types here, so tools/slotsim can simulate millions of spins.
	// Design: design/CasinoWorld/video_slots.md. Wins are in units of 1/100 of the total bet.

	public struct SlotCell
	{
		public int Symbol;
		public int Value;      // orb multiplier (Lunar Gods), fish cash in units (Angler)
		public int Mult;       // wild multiplier (Blood Moon, Penguin walking wilds)
		public bool Sticky;    // held wild (Wolf), walking wild (Penguin), frozen reel (Golem)
	}

	public readonly record struct Pos(int Reel, int Row);

	public sealed class WinGroup
	{
		public int Symbol;
		public int Count;
		public long Win;
		public int Line = -1;
		public readonly List<Pos> Cells = new();
	}

	public enum StepKind
	{
		Land,       // the reels stopped
		Tumble,     // winners removed, new symbols fell in
		Multiply,   // orbs on screen multiply the tumble sequence win
		Collect,    // Anglers collect fish cash (Fx = fish, Targets = Anglers)
		Shots,      // cells turned wild by a shot or snowball (Fx)
		Expand,     // a whole reel froze wild (Fx)
		Upgrade,    // a low symbol was removed from the reels (Value = removed, Value2 = new)
	}

	public sealed class SlotStep
	{
		public StepKind Kind;
		public SlotCell[,] Grid;
		public readonly List<WinGroup> Wins = new();
		public readonly List<Pos> Fx = new();
		public readonly List<Pos> Targets = new();
		public long Win;
		public int Multiplier = 1;
		public int Value, Value2;
	}

	public sealed class SpinResult
	{
		public readonly List<SlotStep> Steps = new();
		public long Win;
		public int FreeSpinsAwarded;
		public bool BonusStarted;
		public int Feature = -1;
		public int TeaseFrom = -1;     // first reel that gets the anticipation treatment
		public int Scatters;
		public bool FreeSpin;
	}

	public readonly record struct PayRow(int Symbol, string Pays);

	public abstract class SlotMachine
	{
		public const int Unit = 100;

		protected readonly Func<int, int> Rng;

		protected SlotMachine(Func<int, int> rng) => Rng = rng;

		public abstract string Key { get; }
		public abstract int Reels { get; }
		public abstract int Rows { get; }
		public abstract int SymbolCount { get; }
		public abstract int Scatter { get; }
		public abstract int Wild { get; }
		/// <summary>Scatters needed to start the bonus.</summary>
		public virtual int ScattersNeeded => 3;

		/// <summary>Scatters needed to retrigger during free spins; 0 = scatters do nothing in the bonus.</summary>
		public virtual int ScattersNeededInBonus => 3;

		/// <summary>Scales every pay; tuned with tools/slotsim to the target return.</summary>
		public double PayScale = 1.0;

		public int FreeSpinsLeft;
		public int FreeSpinsTotal;
		public int BonusMultiplier = 1;
		public int Feature = -1;
		public long BonusWin;
		public bool InBonus => FreeSpinsLeft > 0;

		public abstract SpinResult Spin();

		public abstract IEnumerable<PayRow> PayTable();

		protected long Scale(double units) => (long)Math.Round(units * PayScale);

		protected int Pick(int[] weights)
		{
			int total = 0;
			foreach (int w in weights)
				total += w;
			int roll = Rng(total);
			for (int k = 0; k < weights.Length; k++) {
				roll -= weights[k];
				if (roll < 0)
					return k;
			}
			return 0;
		}

		protected bool Chance(int oneIn) => Rng(oneIn) == 0;

		protected int CountSymbol(SlotCell[,] grid, int symbol)
		{
			int n = 0;
			foreach (SlotCell c in grid)
				if (c.Symbol == symbol)
					n++;
			return n;
		}

		/// <summary>First reel after which the scatters on screen are one short of the trigger (-1 if never).</summary>
		protected int Tease(SlotCell[,] grid)
		{
			int need = InBonus ? ScattersNeededInBonus : ScattersNeeded;
			if (need <= 0)
				return -1;
			int seen = 0;
			for (int reel = 0; reel < Reels; reel++) {
				if (seen >= need - 1)
					return reel;
				for (int row = 0; row < Rows; row++)
					if (grid[reel, row].Symbol == Scatter)
						seen++;
			}
			return -1;
		}

		protected static SlotCell[,] Copy(SlotCell[,] grid) => (SlotCell[,])grid.Clone();

		protected SlotStep AddStep(SpinResult result, StepKind kind, SlotCell[,] grid)
		{
			var step = new SlotStep { Kind = kind, Grid = Copy(grid) };
			result.Steps.Add(step);
			return step;
		}

		/// <summary>Bookkeeping shared by every machine at the end of a spin.</summary>
		protected void EndSpin(SpinResult result, bool freeSpin)
		{
			result.FreeSpin = freeSpin;
			if (freeSpin) {
				FreeSpinsLeft--;
				BonusWin += result.Win;
			}
		}

		protected void StartBonus(SpinResult result, int spins)
		{
			FreeSpinsLeft = FreeSpinsTotal = spins;
			BonusMultiplier = 1;
			BonusWin = 0;
			result.BonusStarted = true;
			result.FreeSpinsAwarded = spins;
		}

		protected void AddSpins(SpinResult result, int spins)
		{
			FreeSpinsLeft += spins;
			FreeSpinsTotal += spins;
			result.FreeSpinsAwarded += spins;
		}

		/// <summary>Left-to-right line wins (3+), wilds substitute; pays[symbol] = line-bet multiples for 3/4/5.</summary>
		protected List<WinGroup> EvaluateLines(SlotCell[,] grid, int[][] lines, double[][] pays, Func<Pos, int> cellMult = null)
		{
			var wins = new List<WinGroup>();
			for (int l = 0; l < lines.Length; l++) {
				int symbol = -1, count = 0, mult = 1;
				for (int reel = 0; reel < Reels; reel++) {
					SlotCell c = grid[reel, lines[l][reel]];
					if (c.Symbol == Scatter)
						break;
					bool wild = c.Symbol == Wild;
					if (!wild) {
						if (symbol < 0)
							symbol = c.Symbol;
						else if (c.Symbol != symbol)
							break;
					}
					if (cellMult != null)
						mult = Math.Max(mult, cellMult(new Pos(reel, lines[l][reel])));
					count++;
				}
				if (symbol < 0 && count >= 3)
					symbol = Wild;
				// W W W A B: three wilds may pay more than four A's.
				int wildRun = 0;
				while (wildRun < Reels && grid[wildRun, lines[l][wildRun]].Symbol == Wild)
					wildRun++;
				if (symbol >= 0 && symbol != Wild && wildRun >= 3 && pays[Wild] != null
					&& (count < 3 || pays[symbol] == null || pays[Wild][wildRun - 3] > pays[symbol][count - 3])) {
					symbol = Wild;
					count = wildRun;
				}
				if (symbol < 0 || count < 3 || pays[symbol] == null)
					continue;
				double pay = pays[symbol][count - 3];
				if (pay <= 0)
					continue;
				var win = new WinGroup { Symbol = symbol, Count = count, Line = l, Win = Scale(pay * Unit / lines.Length * mult) };
				for (int reel = 0; reel < count; reel++)
					win.Cells.Add(new Pos(reel, lines[l][reel]));
				wins.Add(win);
			}
			return wins;
		}

		protected static long Sum(List<WinGroup> wins)
		{
			long s = 0;
			foreach (WinGroup w in wins)
				s += w.Win;
			return s;
		}
	}

	// ======================================================================= Gates of the Lunar Gods
	/// <summary>6x5, pays anywhere (8+), tumbles, multiplier orbs revealed by Moon Lord's deathray.</summary>
	public sealed class LunarGodsSlot : SlotMachine
	{
		public const int Orb = 10;
		public static readonly int[] OrbValues = { 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25, 50, 100, 250, 500 };
		private static readonly int[] OrbWeights = { 300, 220, 160, 120, 90, 70, 55, 40, 30, 20, 14, 6, 3, 1, 1 };
		// 0-4 fragments and ore (low), 5-8 premiums, 9 scatter, 10 orb.
		private static readonly int[] BaseWeights = { 52, 50, 48, 44, 40, 30, 24, 20, 14, 8, 6 };
		private static readonly int[] BonusWeights = { 52, 50, 48, 44, 40, 30, 24, 20, 14, 8, 15 };
		// Total-bet multiples for 8-9, 10-11 and 12+ of a kind.
		private static readonly double[][] Pays = {
			new[] { 0.25, 0.75, 2.0 }, new[] { 0.4, 0.9, 4.0 }, new[] { 0.5, 1.0, 5.0 }, new[] { 0.8, 1.2, 8.0 }, new[] { 1.0, 1.5, 10.0 },
			new[] { 1.5, 2.0, 12.0 }, new[] { 2.0, 5.0, 15.0 }, new[] { 2.5, 10.0, 25.0 }, new[] { 10.0, 25.0, 50.0 },
		};
		private static readonly double[] ScatterPays = { 3, 5, 100 };   // 4, 5, 6+ sigils

		public LunarGodsSlot(Func<int, int> rng) : base(rng) => PayScale = Tuning.LunarGods;

		public override string Key => "LunarGods";
		public override int Reels => 6;
		public override int Rows => 5;
		public override int SymbolCount => 11;
		public override int Scatter => 9;
		public override int Wild => -1;
		public override int ScattersNeeded => 4;

		private SlotCell Roll(bool bonus)
		{
			var c = new SlotCell { Symbol = Pick(bonus ? BonusWeights : BaseWeights) };
			if (c.Symbol == Orb)
				c.Value = OrbValues[Pick(OrbWeights)];
			return c;
		}

		private List<WinGroup> Evaluate(SlotCell[,] grid)
		{
			var wins = new List<WinGroup>();
			for (int s = 0; s < Pays.Length; s++) {
				int n = CountSymbol(grid, s);
				if (n < 8)
					continue;
				var w = new WinGroup { Symbol = s, Count = n, Win = Scale(Pays[s][n >= 12 ? 2 : n >= 10 ? 1 : 0] * Unit) };
				for (int r = 0; r < Reels; r++)
					for (int row = 0; row < Rows; row++)
						if (grid[r, row].Symbol == s)
							w.Cells.Add(new Pos(r, row));
				wins.Add(w);
			}
			return wins;
		}

		public override SpinResult Spin()
		{
			bool bonus = InBonus;
			var result = new SpinResult();
			var grid = new SlotCell[Reels, Rows];
			for (int r = 0; r < Reels; r++)
				for (int row = 0; row < Rows; row++)
					grid[r, row] = Roll(bonus);
			result.TeaseFrom = Tease(grid);

			SlotStep step = AddStep(result, StepKind.Land, grid);
			for (int r = 0; r < Reels; r++)
				for (int row = 0; row < Rows; row++)
					if (grid[r, row].Symbol == Orb)
						step.Fx.Add(new Pos(r, row));
			List<WinGroup> wins = Evaluate(grid);
			step.Wins.AddRange(wins);
			long sequence = step.Win = Sum(wins);

			int guard = 0;
			while (wins.Count > 0 && guard++ < 20) {
				var remove = new bool[Reels, Rows];
				foreach (WinGroup w in wins)
					foreach (Pos p in w.Cells)
						remove[p.Reel, p.Row] = true;
				var fresh = new List<Pos>();
				for (int r = 0; r < Reels; r++) {
					var keep = new List<SlotCell>();
					for (int row = Rows - 1; row >= 0; row--)
						if (!remove[r, row])
							keep.Add(grid[r, row]);
					for (int row = Rows - 1; row >= 0; row--) {
						int idx = Rows - 1 - row;
						if (idx < keep.Count)
							grid[r, row] = keep[idx];
						else {
							grid[r, row] = Roll(bonus);
							if (grid[r, row].Symbol == Orb)
								fresh.Add(new Pos(r, row));
						}
					}
				}
				step = AddStep(result, StepKind.Tumble, grid);
				step.Fx.AddRange(fresh);
				wins = Evaluate(grid);
				step.Wins.AddRange(wins);
				step.Win = Sum(wins);
				sequence += step.Win;
			}

			int orbSum = 0;
			var orbs = new List<Pos>();
			for (int r = 0; r < Reels; r++)
				for (int row = 0; row < Rows; row++)
					if (grid[r, row].Symbol == Orb) {
						orbSum += grid[r, row].Value;
						orbs.Add(new Pos(r, row));
					}
			long total = sequence;
			if (sequence > 0 && orbSum > 0) {
				int mult = orbSum;
				if (bonus) {
					BonusMultiplier = BonusMultiplier == 1 ? orbSum : BonusMultiplier + orbSum;
					mult = BonusMultiplier;
				}
				total = sequence * mult;
				step = AddStep(result, StepKind.Multiply, grid);
				step.Fx.AddRange(orbs);
				step.Multiplier = mult;
				step.Win = total;
				step.Value = (int)Math.Min(int.MaxValue, sequence);
			}

			int scatters = result.Scatters = CountSymbol(grid, Scatter);
			if (!bonus && scatters >= 4) {
				total += Scale(ScatterPays[Math.Min(scatters, 6) - 4] * Unit);
				StartBonus(result, 15);
			}
			else if (bonus && scatters >= 3)
				AddSpins(result, 5);

			result.Win = total;
			EndSpin(result, bonus);
			return result;
		}

		public override IEnumerable<PayRow> PayTable()
		{
			for (int s = Pays.Length - 1; s >= 0; s--)
				yield return new PayRow(s, $"{Fmt(Pays[s][0])} / {Fmt(Pays[s][1])} / {Fmt(Pays[s][2])}");
		}

		private string Fmt(double v) => (v * PayScale).ToString(v * PayScale < 10 ? "0.##" : "0");
	}

	// ======================================================================= Angler's Bonanza
	/// <summary>5x3, 10 lines. Fish carry cash; in free spins every Angler wild collects it.</summary>
	public sealed class AnglerSlot : SlotMachine
	{
		public const int Fish = 7;
		public static readonly int[] FishValues = { 5, 10, 20, 30, 50, 100, 250, 500, 1000 };   // units (1/100 total bet)
		private static readonly int[] FishWeights = { 400, 260, 140, 80, 50, 30, 12, 4, 1 };
		// 0-3 bait (low), 4-6 tackle (high), 7 fish, 8 Angler wild, 9 Golden Crate scatter.
		private static readonly int[] BaseWeights = { 40, 36, 30, 24, 12, 9, 7, 10, 0, 5 };
		private static readonly int[] BonusWeights = { 40, 36, 30, 24, 12, 9, 7, 12, 6, 4 };
		private static readonly double[][] Pays = {
			new[] { 5.0, 10, 50 }, new[] { 5.0, 10, 50 }, new[] { 5.0, 15, 60 }, new[] { 5.0, 15, 80 },
			new[] { 10.0, 40, 150 }, new[] { 15.0, 50, 200 }, new[] { 20.0, 100, 500 }, new[] { 10.0, 40, 200 }, new[] { 20.0, 100, 1000 }, null,
		};
		private static readonly int[][] Lines = VideoLines.Lines5x3;
		private int wildsCollected;

		public AnglerSlot(Func<int, int> rng) : base(rng) => PayScale = Tuning.Angler;

		public override string Key => "Angler";
		public override int Reels => 5;
		public override int Rows => 3;
		public override int SymbolCount => 10;
		public override int Scatter => 9;
		public override int Wild => 8;
		public override int ScattersNeededInBonus => 0;   // retriggers come from collecting Anglers
		public int WildsCollected => wildsCollected;

		private SlotCell Roll(bool bonus)
		{
			var c = new SlotCell { Symbol = Pick(bonus ? BonusWeights : BaseWeights) };
			if (c.Symbol == Fish)
				c.Value = (int)Scale(FishValues[Pick(FishWeights)]);
			return c;
		}

		public override SpinResult Spin()
		{
			bool bonus = InBonus;
			var result = new SpinResult();
			var grid = new SlotCell[Reels, Rows];
			for (int r = 0; r < Reels; r++)
				for (int row = 0; row < Rows; row++)
					grid[r, row] = Roll(bonus);
			result.TeaseFrom = Tease(grid);

			SlotStep step = AddStep(result, StepKind.Land, grid);
			List<WinGroup> wins = EvaluateLines(grid, Lines, Pays);
			step.Wins.AddRange(wins);
			long total = step.Win = Sum(wins);

			if (bonus) {
				var fish = new List<Pos>();
				var anglers = new List<Pos>();
				for (int r = 0; r < Reels; r++)
					for (int row = 0; row < Rows; row++) {
						if (grid[r, row].Symbol == Fish)
							fish.Add(new Pos(r, row));
						else if (grid[r, row].Symbol == Wild)
							anglers.Add(new Pos(r, row));
					}
				// Lucky cast: fish but no Angler, sometimes he jumps in anyway.
				if (fish.Count > 0 && anglers.Count == 0 && Chance(5)) {
					var spots = new List<Pos>();
					for (int r = 0; r < Reels; r++)
						for (int row = 0; row < Rows; row++)
							if (grid[r, row].Symbol != Fish && grid[r, row].Symbol != Scatter)
								spots.Add(new Pos(r, row));
					if (spots.Count > 0) {
						Pos p = spots[Rng(spots.Count)];
						grid[p.Reel, p.Row] = new SlotCell { Symbol = Wild };
						anglers.Add(p);
						step = AddStep(result, StepKind.Shots, grid);
						step.Fx.Add(p);
					}
				}
				if (fish.Count > 0 && anglers.Count > 0) {
					long cash = 0;
					foreach (Pos p in fish)
						cash += grid[p.Reel, p.Row].Value;
					step = AddStep(result, StepKind.Collect, grid);
					step.Fx.AddRange(fish);
					step.Targets.AddRange(anglers);
					step.Multiplier = BonusMultiplier;
					step.Win = cash * anglers.Count * BonusMultiplier;
					total += step.Win;
				}
				int before = wildsCollected / 4;
				wildsCollected += anglers.Count;
				for (int level = before; level < wildsCollected / 4 && BonusMultiplier < 10; level++) {
					BonusMultiplier = BonusMultiplier == 1 ? 2 : BonusMultiplier == 2 ? 3 : 10;
					AddSpins(result, 10);
				}
			}

			int scatters = result.Scatters = CountSymbol(grid, Scatter);
			if (!bonus && scatters >= 3) {
				total += Scale(new[] { 2, 10, 50 }[Math.Min(scatters, 5) - 3] * Unit);
				StartBonus(result, new[] { 10, 15, 20 }[Math.Min(scatters, 5) - 3]);
				wildsCollected = 0;
			}

			result.Win = total;
			EndSpin(result, bonus);
			return result;
		}

		public override IEnumerable<PayRow> PayTable()
		{
			for (int s = 8; s >= 0; s--)
				yield return new PayRow(s, $"{Pays[s][0] * PayScale:0.#} / {Pays[s][1] * PayScale:0.#} / {Pays[s][2] * PayScale:0}");
		}
	}

	// ======================================================================= Boreal Wilds
	/// <summary>5x4, 20 lines. Three crates start an animal lottery; each animal is a different bonus.</summary>
	public sealed class BorealSlot : SlotMachine
	{
		public const int Penguin = 0, Flinx = 1, Wolf = 2, Golem = 3;
		private static readonly int[] FeatureWeights = { 30, 30, 25, 15 };
		private static readonly int[] FeatureSpins = { 12, 12, 10, 10 };
		// 0-3 low, 4-7 animals (Penguin, Flinx, Wolf, Ice Golem), 8 Ice Mirror wild, 9 Frozen Crate scatter.
		private static readonly int[] BaseWeights = { 33, 33, 31, 28, 15, 13, 11, 9, 7, 4 };
		private static readonly double[][] Pays = {
			new[] { 4.0, 10, 40 }, new[] { 4.0, 10, 40 }, new[] { 5.0, 12, 50 }, new[] { 5.0, 15, 60 },
			new[] { 10.0, 30, 120 }, new[] { 12.0, 40, 150 }, new[] { 15.0, 60, 250 }, new[] { 25.0, 100, 500 }, new[] { 25.0, 100, 500 }, null,
		};
		private static readonly int[][] Lines = VideoLines.Lines5x4;
		private readonly SlotCell[,] held = new SlotCell[5, 4];

		public BorealSlot(Func<int, int> rng) : base(rng) => PayScale = Tuning.Boreal;

		public override string Key => "Boreal";
		public override int Reels => 5;
		public override int Rows => 4;
		public override int SymbolCount => 10;
		public override int Scatter => 9;
		public override int Wild => 8;

		private SlotCell Roll() => new() { Symbol = Pick(BaseWeights) };

		public override SpinResult Spin()
		{
			bool bonus = InBonus;
			var result = new SpinResult();
			var grid = new SlotCell[Reels, Rows];
			for (int r = 0; r < Reels; r++)
				for (int row = 0; row < Rows; row++)
					grid[r, row] = Roll();

			if (bonus && (Feature == Penguin || Feature == Wolf)) {
				// Held wilds come back: Wolf in place, Penguin one reel to the left.
				var next = new SlotCell[Reels, Rows];
				for (int r = 0; r < Reels; r++)
					for (int row = 0; row < Rows; row++) {
						if (!held[r, row].Sticky)
							continue;
						int nr = Feature == Penguin ? r - 1 : r;
						if (nr >= 0)
							next[nr, row] = held[r, row];
					}
				Array.Copy(next, held, next.Length);
				for (int r = 0; r < Reels; r++)
					for (int row = 0; row < Rows; row++)
						if (held[r, row].Sticky)
							grid[r, row] = held[r, row];
			}
			result.TeaseFrom = Tease(grid);
			SlotStep step = AddStep(result, StepKind.Land, grid);

			int mult = 1;
			if (bonus && Feature == Flinx) {
				int balls = 1 + Rng(3);
				step = AddStep(result, StepKind.Shots, grid);
				for (int k = 0; k < balls; k++) {
					int r = Rng(Reels), row = Rng(Rows);
					if (grid[r, row].Symbol == Scatter || grid[r, row].Symbol == Wild)
						continue;
					grid[r, row] = new SlotCell { Symbol = Wild };
					step.Fx.Add(new Pos(r, row));
				}
				step.Grid = Copy(grid);
			}
			else if (bonus && Feature == Golem) {
				int r = 1 + Rng(3);
				step = AddStep(result, StepKind.Expand, grid);
				for (int row = 0; row < Rows; row++) {
					if (grid[r, row].Symbol != Scatter)
						grid[r, row] = new SlotCell { Symbol = Wild, Sticky = true };
					step.Fx.Add(new Pos(r, row));
				}
				step.Grid = Copy(grid);
				mult = 2;
			}

			Func<Pos, int> cellMult = null;
			if (bonus && Feature == Penguin) {
				for (int r = 0; r < Reels; r++)
					for (int row = 0; row < Rows; row++)
						if (grid[r, row].Symbol == Wild && !grid[r, row].Sticky)
							grid[r, row] = held[r, row] = new SlotCell { Symbol = Wild, Sticky = true, Mult = 2 };
				SlotCell[,] g = grid;
				cellMult = p => g[p.Reel, p.Row].Symbol == Wild ? 2 : 1;
				step.Grid = Copy(grid);
			}
			else if (bonus && Feature == Wolf) {
				for (int r = 0; r < Reels; r++)
					for (int row = 0; row < Rows; row++)
						if (grid[r, row].Symbol == Wild && !grid[r, row].Sticky)
							grid[r, row] = held[r, row] = new SlotCell { Symbol = Wild, Sticky = true };
				step.Grid = Copy(grid);
			}

			List<WinGroup> wins = EvaluateLines(grid, Lines, Pays, cellMult);
			if (mult > 1)
				foreach (WinGroup w in wins)
					w.Win *= mult;
			step.Wins.AddRange(wins);
			step.Multiplier = mult;
			long total = step.Win = Sum(wins);

			int scatters = result.Scatters = CountSymbol(grid, Scatter);
			if (!bonus && scatters >= 3) {
				total += Scale(new[] { 2, 10, 50 }[Math.Min(scatters, 5) - 3] * Unit);
				Feature = Pick(FeatureWeights);
				result.Feature = Feature;
				Array.Clear(held);
				StartBonus(result, FeatureSpins[Feature]);
			}
			else if (bonus && scatters >= 3)
				AddSpins(result, 5);

			result.Win = total;
			EndSpin(result, bonus);
			if (!InBonus)
				Array.Clear(held);
			return result;
		}

		public override IEnumerable<PayRow> PayTable()
		{
			for (int s = 8; s >= 0; s--)
				yield return new PayRow(s, $"{Pays[s][0] * PayScale:0.#} / {Pays[s][1] * PayScale:0.#} / {Pays[s][2] * PayScale:0}");
		}
	}

	// ======================================================================= Blood Moon Hunter
	/// <summary>5x4, 1024 ways. Hunter wilds carry multipliers; Blood Shot turns cells wild; trophy hunt upgrades symbols.</summary>
	public sealed class BloodMoonSlot : SlotMachine
	{
		// 0-3 low, 4-7 high, 8 Hunter wild (reels 2-5), 9 Blood Moon scatter.
		private static readonly int[] BaseWeights = { 26, 25, 24, 23, 19, 16, 13, 10, 3, 5 };
		private static readonly int[] WildMults = { 1, 2, 3 };
		private static readonly int[] WildMultWeights = { 70, 20, 10 };
		// Total-bet multiples per way for 3/4/5 reels: even the smallest win is a real part of the bet.
		private static readonly double[][] Pays = {
			new[] { 0.3, 0.6, 1.2 }, new[] { 0.3, 0.6, 1.2 }, new[] { 0.35, 0.7, 1.5 }, new[] { 0.4, 0.8, 1.8 },
			new[] { 0.6, 1.2, 3.0 }, new[] { 0.8, 1.6, 4.0 }, new[] { 1.0, 2.0, 5.0 }, new[] { 1.5, 3.0, 8.0 },
		};
		private readonly int[] upgraded = { -1, -1, -1, -1 };   // low symbol -> premium it became
		private int wildsCollected, upgrades;

		public BloodMoonSlot(Func<int, int> rng) : base(rng) => PayScale = Tuning.BloodMoon;

		public override string Key => "BloodMoon";
		public override int Reels => 5;
		public override int Rows => 3;
		public override int SymbolCount => 10;
		public override int Scatter => 9;
		public override int Wild => 8;
		public int Upgrades => upgrades;
		public int WildsCollected => wildsCollected;

		private SlotCell Roll(int reel)
		{
			int s;
			do
				s = Pick(BaseWeights);
			while (s == Wild && reel == 0);
			if (s < 4 && upgraded[s] >= 0)
				s = upgraded[s];
			var c = new SlotCell { Symbol = s };
			if (s == Wild)
				c.Mult = WildMults[Pick(WildMultWeights)];
			return c;
		}

		private List<WinGroup> Evaluate(SlotCell[,] grid)
		{
			var wins = new List<WinGroup>();
			for (int s = 0; s < Pays.Length; s++) {
				long ways = 1;
				int reels = 0, mult = 1;
				var cells = new List<Pos>();
				for (int r = 0; r < Reels; r++) {
					int n = 0;
					for (int row = 0; row < Rows; row++) {
						SlotCell c = grid[r, row];
						if (c.Symbol == s || c.Symbol == Wild) {
							n++;
							cells.Add(new Pos(r, row));
							if (c.Symbol == Wild)
								mult = Math.Max(mult, c.Mult);
						}
					}
					if (n == 0)
						break;
					ways *= n;
					reels++;
				}
				if (reels < 3)
					continue;
				var w = new WinGroup { Symbol = s, Count = reels, Win = Scale(Pays[s][reels - 3] * Unit * Math.Min(ways, 6) * mult) };   // ways capped so wins stay chunky
				foreach (Pos p in cells)
					if (p.Reel < reels)
						w.Cells.Add(p);
				wins.Add(w);
			}
			return wins;
		}

		public override SpinResult Spin()
		{
			bool bonus = InBonus;
			var result = new SpinResult();
			var grid = new SlotCell[Reels, Rows];
			for (int r = 0; r < Reels; r++)
				for (int row = 0; row < Rows; row++)
					grid[r, row] = Roll(r);
			result.TeaseFrom = Tease(grid);
			SlotStep step = AddStep(result, StepKind.Land, grid);

			if (Chance(bonus ? 5 : 16)) {
				int shots = 3 + Rng(4);
				step = AddStep(result, StepKind.Shots, grid);
				for (int k = 0; k < shots; k++) {
					int r = 1 + Rng(Reels - 1), row = Rng(Rows);
					if (grid[r, row].Symbol == Scatter || grid[r, row].Symbol == Wild)
						continue;
					grid[r, row] = new SlotCell { Symbol = Wild, Mult = 1 };
					step.Fx.Add(new Pos(r, row));
				}
				step.Grid = Copy(grid);
			}

			List<WinGroup> wins = Evaluate(grid);
			step.Wins.AddRange(wins);
			long total = step.Win = Sum(wins);

			if (bonus) {
				int before = wildsCollected / 3;
				wildsCollected += CountSymbol(grid, Wild);
				while (wildsCollected / 3 > before && upgrades < 4) {
					before++;
					int low = upgrades;
					upgraded[low] = 4 + upgrades;
					upgrades++;
					step = AddStep(result, StepKind.Upgrade, grid);
					step.Value = low;
					step.Value2 = upgraded[low];
					AddSpins(result, 2);
				}
			}

			int scatters = result.Scatters = CountSymbol(grid, Scatter);
			if (!bonus && scatters >= 3) {
				total += Scale(new[] { 2, 10, 50 }[Math.Min(scatters, 5) - 3] * Unit);
				StartBonus(result, new[] { 10, 12, 15 }[Math.Min(scatters, 5) - 3]);
				wildsCollected = upgrades = 0;
				Array.Fill(upgraded, -1);
			}
			else if (bonus && scatters >= 3)
				AddSpins(result, 3);

			result.Win = total;
			EndSpin(result, bonus);
			if (!InBonus) {
				Array.Fill(upgraded, -1);
				upgrades = wildsCollected = 0;
			}
			return result;
		}

		public override IEnumerable<PayRow> PayTable()
		{
			for (int s = 7; s >= 0; s--)
				yield return new PayRow(s, $"{Pays[s][0] * PayScale:0.##} / {Pays[s][1] * PayScale:0.##} / {Pays[s][2] * PayScale:0.#}");
		}
	}

	public static class VideoLines
	{
		public static readonly int[][] Lines5x3 = {
			new[] { 1, 1, 1, 1, 1 }, new[] { 0, 0, 0, 0, 0 }, new[] { 2, 2, 2, 2, 2 }, new[] { 0, 1, 2, 1, 0 }, new[] { 2, 1, 0, 1, 2 },
			new[] { 0, 0, 1, 2, 2 }, new[] { 2, 2, 1, 0, 0 }, new[] { 1, 0, 0, 0, 1 }, new[] { 1, 2, 2, 2, 1 }, new[] { 1, 0, 1, 2, 1 },
		};

		public static readonly int[][] Lines5x4 = {
			new[] { 0, 0, 0, 0, 0 }, new[] { 1, 1, 1, 1, 1 }, new[] { 2, 2, 2, 2, 2 }, new[] { 3, 3, 3, 3, 3 },
			new[] { 0, 1, 2, 1, 0 }, new[] { 3, 2, 1, 2, 3 }, new[] { 1, 2, 3, 2, 1 }, new[] { 2, 1, 0, 1, 2 },
			new[] { 0, 0, 1, 2, 3 }, new[] { 3, 3, 2, 1, 0 }, new[] { 1, 0, 0, 0, 1 }, new[] { 2, 3, 3, 3, 2 },
			new[] { 0, 1, 1, 1, 0 }, new[] { 3, 2, 2, 2, 3 }, new[] { 1, 1, 0, 1, 1 }, new[] { 2, 2, 3, 2, 2 },
			new[] { 1, 2, 1, 2, 1 }, new[] { 2, 1, 2, 1, 2 }, new[] { 0, 1, 0, 1, 0 }, new[] { 3, 2, 3, 2, 3 },
		};
	}

	/// <summary>Pay scales per machine, measured with tools/slotsim (dotnet run -c Release -- tune).</summary>
	public static class Tuning
	{
		public static double LunarGods = 0.515, Angler = 1.61, Boreal = 1.579, BloodMoon = 0.385;
	}

	public static class SlotGames
	{
		public static readonly string[] Keys = { "LunarGods", "Angler", "Boreal", "BloodMoon" };

		public static SlotMachine Create(string key, Func<int, int> rng) => key switch {
			"LunarGods" => new LunarGodsSlot(rng),
			"Angler" => new AnglerSlot(rng),
			"Boreal" => new BorealSlot(rng),
			_ => new BloodMoonSlot(rng),
		};
	}
}
