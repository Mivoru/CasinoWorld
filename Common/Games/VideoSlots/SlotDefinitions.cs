namespace CasinoWorld.Common.Games.VideoSlots
{
	/// <summary>
	/// The four video slots of the High Rollers Casino. Numbers are tuned with tools/slotsim (millions of
	/// simulated spins); keep them in sync with the RTP noted on each machine.
	/// Symbols 0-4 are low, 5-7 high, then wild and scatter (and an orb for the Lunar Gods).
	/// </summary>
	public static class SlotDefinitions
	{
		private static int[][] Pays(params int[][] p) => p;

		/// <summary>Gates of the Lunar Gods: tumbling wins and multiplier orbs (Gates of Olympus style).</summary>
		public static readonly SlotDefinition LunarGods = new() {
			Key = "LunarGods",
			Feature = SlotFeature.CascadeOrbs,
			Weights = new[] { 30, 30, 28, 26, 24, 14, 10, 7, 4, 4, 3 },
			Pays = Pays(
				new[] { 9, 20, 60 }, new[] { 8, 20, 60 }, new[] { 10, 25, 80 }, new[] { 10, 30, 100 }, new[] { 12, 40, 120 },
				new[] { 20, 80, 250 }, new[] { 30, 120, 400 }, new[] { 50, 200, 800 }, new[] { 50, 200, 1000 }, null, null),
			Wild = 8, Scatter = 9, Special = 10,
			OrbValues = new[] { 2, 3, 5, 10, 25 },
			OrbWeights = new[] { 50, 28, 14, 6, 2 },
		};

		/// <summary>Angler's Bonanza: fish carry coin values, the angler wild collects them in free spins (Big Bass style).</summary>
		public static readonly SlotDefinition Angler = new() {
			Key = "Angler",
			Feature = SlotFeature.MoneyCollect,
			Weights = new[] { 30, 30, 28, 26, 24, 16, 12, 8, 4, 4 },
			Pays = Pays(
				new[] { 13, 25, 100 }, new[] { 13, 25, 100 }, new[] { 12, 35, 120 }, new[] { 15, 35, 150 }, new[] { 20, 50, 200 },
				new[] { 25, 100, 350 }, new[] { 35, 150, 500 }, new[] { 50, 200, 750 }, new[] { 60, 250, 1250 }, null),
			Wild = 8, Scatter = 9,
			FishSymbols = new[] { 5, 6, 7 },
			FishValues = new[] { 5, 12, 25, 60, 120 },
		};

		/// <summary>Boreal Wilds: snowbound slot whose wilds freeze in place during free spins (Wild North style).</summary>
		public static readonly SlotDefinition Boreal = new() {
			Key = "Boreal",
			Feature = SlotFeature.StickyWilds,
			Weights = new[] { 30, 30, 28, 26, 24, 14, 10, 7, 4, 4 },
			Pays = Pays(
				new[] { 14, 35, 120 }, new[] { 14, 35, 120 }, new[] { 17, 45, 145 }, new[] { 17, 45, 175 }, new[] { 23, 60, 230 },
				new[] { 35, 145, 430 }, new[] { 52, 200, 720 }, new[] { 72, 290, 1150 }, new[] { 85, 350, 1500 }, null),
			Wild = 8, Scatter = 9,
		};

		/// <summary>Blood Moon Hunter: free spins with one expanding symbol that pays on every line (Hunter's Dream style).</summary>
		public static readonly SlotDefinition BloodMoon = new() {
			Key = "BloodMoon",
			Feature = SlotFeature.ExpandingSymbol,
			Weights = new[] { 30, 30, 28, 26, 24, 14, 10, 7, 4, 4 },
			Pays = Pays(
				new[] { 14, 30, 100 }, new[] { 14, 30, 100 }, new[] { 16, 38, 125 }, new[] { 16, 38, 150 }, new[] { 20, 50, 200 },
				new[] { 30, 125, 380 }, new[] { 45, 180, 640 }, new[] { 64, 250, 1000 }, new[] { 76, 300, 1500 }, null),
			Wild = 8, Scatter = 9,
		};

		public static readonly SlotDefinition[] All = { LunarGods, Angler, Boreal, BloodMoon };
	}
}
