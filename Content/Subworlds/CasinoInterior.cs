using System.Collections.Generic;
using CasinoWorld.Content.NPCs;
using CasinoWorld.Content.NPCs.People;
using CasinoWorld.Content.Tiles;
using CasinoWorld.Content.Tiles.Interior;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Subworlds
{
	/// <summary>
	/// Builds the three-storey casino interior shared by both casinos.
	///
	///   Ground floor (lobby): entrance, elevators, prize counter, LED leaderboard over the tables,
	///     aquarium, fountain, roulette and blackjack, slots, Wheel of Fortune, lounge.
	///   First floor (bar and lounge): back bar and counter, sofas and piano, a long row of slots, VIP tables.
	///   Second floor (cabaret): show stage with the showgirl, pole stage with the pole dancer, tables.
	///
	/// Only elevators connect the floors (no stairs, no holes in the floors). The world is sized so the
	/// camera stops exactly at the building's walls, floor and ceiling (Terraria keeps the view 41 tiles
	/// inside the world edge), so there is never an empty black void around the hall.
	/// All furniture and lights are custom (no torches, no vanilla wooden furniture).
	/// </summary>
	public static class CasinoInterior
	{
		// The camera never shows the outer 41 tiles of a world, so the hall starts right at that edge.
		public const int Edge = 41;
		public const int Left = Edge;              // left wall column
		public const int HallWidth = 150;
		public const int Right = Left + HallWidth; // right wall column
		public const int Ceiling = Edge;           // ceiling row of the top storey

		/// <summary>Floor rows, ground floor first. Multiples of 4 so the 4-row wainscot lines up.</summary>
		public static readonly int[] Floors = { 112, 88, 64 };

		public const int WorldWidth = Right + Edge + 1;
		public const int WorldHeight = 112 + 2 + Edge;

		public static int CeilingOf(int storey) => storey == 2 ? Ceiling : Floors[storey + 1] + 1;

		public static int X(int offset) => Left + offset;

		private sealed class Theme
		{
			public ushort[] Walls;
			public ushort[] FloorTiles;
			public ushort Wainscot;
		}

		private static Theme MakeTheme(bool hardmode) => hardmode
			? new Theme {
				// High-end modern: black marble with gold veins, midnight velvet lounge, LED-panel cabaret, walnut slats.
				Walls = new[] { (ushort)ModContent.WallType<BlackMarbleWall>(), (ushort)ModContent.WallType<MidnightVelvetWall>(), (ushort)ModContent.WallType<LedPanelWall>() },
				FloorTiles = new[] { (ushort)ModContent.TileType<BlackWhiteMarbleFloor>(), (ushort)ModContent.TileType<NoirCarpet>(), (ushort)ModContent.TileType<BlackWhiteMarbleFloor>() },
				Wainscot = (ushort)ModContent.WallType<WalnutSlatWall>(),
			}
			: new Theme {
				Walls = new[] { (ushort)ModContent.WallType<LobbyMarbleWall>(), (ushort)ModContent.WallType<EmeraldDamaskWall>(), (ushort)ModContent.WallType<VelvetCurtainWall>() },
				FloorTiles = new[] { (ushort)ModContent.TileType<MarbleCheckerFloor>(), (ushort)ModContent.TileType<EmeraldCarpet>(), (ushort)ModContent.TileType<VelvetCarpet>() },
				Wainscot = (ushort)ModContent.WallType<WalnutWainscotWall>(),
			};

		// ------------------------------------------------------------------ building

		/// <summary>True while building the high rollers casino: classic furniture is swapped for modern pieces.</summary>
		private static bool modern;

		/// <summary>Modern counterpart of a classic piece (same footprint or smaller) in the high rollers casino.</summary>
		private static int Swap(int type)
		{
			if (!modern)
				return type;
			if (type == ModContent.TileType<BarCounter>()) return ModContent.TileType<LedBarCounter>();
			if (type == ModContent.TileType<BarShelf>()) return ModContent.TileType<ModernBackBar>();
			if (type == ModContent.TileType<VelvetSofa>()) return ModContent.TileType<LoungeChair>();
			if (type == ModContent.TileType<CocktailTable>()) return ModContent.TileType<GlassTable>();
			if (type == ModContent.TileType<MarblePilaster>()) return ModContent.TileType<LightColumn>();
			if (type == ModContent.TileType<CrystalChandelier>()) return ModContent.TileType<RingPendant>();
			return type;
		}

		private static int HeightOf(int type) => ((InteriorFurniture)TileLoader.GetTile(type)).Height;

		public static void Build(bool hardmode)
		{
			modern = hardmode;
			Main.worldSurface = 20;
			Main.rockLayer = 25;
			Main.spawnTileX = X(5);
			Main.spawnTileY = Floors[0] - 1;
			Theme theme = MakeTheme(hardmode);
			BuildShell(theme);
			WorldGen.RangeFrame(0, 0, Main.maxTilesX - 1, Main.maxTilesY - 1);
			Lights();
			Ground(hardmode);
			BarFloor(hardmode);
			Cabaret(hardmode);
		}

		private static int StoreyOf(int y)
		{
			for (int s = 0; s < 3; s++) {
				if (y <= Floors[s] && y > CeilingOf(s))
					return s;
			}
			return -1;
		}

		private static void BuildShell(Theme theme)
		{
			ushort foundation = (ushort)ModContent.TileType<CasinoFoundation>();
			ushort ceiling = (ushort)ModContent.TileType<CofferedCeiling>();
			for (int x = 0; x < Main.maxTilesX; x++) {
				for (int y = 0; y < Main.maxTilesY; y++) {
					Tile tile = Main.tile[x, y];
					bool insideX = x > Left && x < Right;
					int storey = StoreyOf(y);
					bool air = insideX && storey >= 0 && y < Floors[storey];
					tile.HasTile = !air;
					tile.LiquidAmount = 0;
					if (!air) {
						if (insideX && storey >= 0 && y == Floors[storey])
							tile.TileType = theme.FloorTiles[storey];
						else if (insideX && (y == Ceiling || (y == Floors[1] + 1) || (y == Floors[2] + 1)))
							tile.TileType = ceiling;
						else
							tile.TileType = foundation;
					}
					if (x >= Left && x <= Right && storey >= 0) {
						int above = Floors[storey] - y;   // rows above the floor
						tile.WallType = above >= 1 && above <= 4 ? theme.Wainscot : theme.Walls[storey];
					}
				}
			}
		}

		// ------------------------------------------------------------------ placement helpers

		private static void Floor<T>(int offset, int storey) where T : InteriorFurniture
		{
			int type = Swap(ModContent.TileType<T>());
			InteriorFurniture.Place(type, X(offset), Floors[storey] - HeightOf(type));
		}

		/// <summary>Wall-mounted object whose bottom edge is <paramref name="above"/> rows above the floor.</summary>
		private static void OnWall<T>(int offset, int storey, int above) where T : InteriorFurniture
		{
			int type = Swap(ModContent.TileType<T>());
			InteriorFurniture.Place(type, X(offset), Floors[storey] - above - HeightOf(type));
		}

		private static void Hang<T>(int offset, int storey) where T : InteriorFurniture
			=> InteriorFurniture.Place(Swap(ModContent.TileType<T>()), X(offset), CeilingOf(storey) + 1);

		private static void Vanilla(int type, int offset, int storey, int style = 0)
			=> WorldGen.PlaceObject(X(offset), Floors[storey] - 1, type, mute: true, style: style);

		/// <summary>A full-height marble pilaster (stacked 2x4 segments) from the floor to the ceiling.</summary>
		private static void Pilaster(int offset, int storey)
		{
			int type = Swap(ModContent.TileType<MarblePilaster>());
			for (int y = Floors[storey] - 4; y > CeilingOf(storey); y -= 4)
				InteriorFurniture.Place(type, X(offset), y);
		}

		/// <summary>Two elevators on each side of every floor: one always goes down, the other always up.</summary>
		private static void Elevators(int storey)
		{
			Vanilla(ModContent.TileType<CasinoElevatorDown>(), 5, storey);
			Vanilla(ModContent.TileType<CasinoElevatorUp>(), 8, storey);
			Vanilla(ModContent.TileType<CasinoElevatorDown>(), 132, storey);
			Vanilla(ModContent.TileType<CasinoElevatorUp>(), 135, storey);
		}

		private static void Lights()
		{
			for (int s = 0; s < 3; s++) {
				for (int x = 3; x < HallWidth - 6; x += 18)
					Hang<CrystalChandelier>(x, s);
			}
		}

		// ------------------------------------------------------------------ storeys

		private static void Ground(bool hardmode)
		{
			const int s = 0;
			Vanilla(ModContent.TileType<CasinoDoor>(), 2, s);
			Elevators(s);
			Floor<MarblePlanter>(11, s);
			Floor<PrizeCounter>(14, s);
			OnWall<WallSconce>(21, s, 8);

			OnWall<LedBoard>(24, s, 7);
			Vanilla(ModContent.TileType<RouletteTable>(), 25, s);
			Vanilla(ModContent.TileType<BlackjackTable>(), 36, s);
			Pilaster(45, s);

			Floor<Aquarium>(48, s);
			Floor<Uplight>(61, s);
			Vanilla(ModContent.TileType<CasinoFountain>(), 63, s);
			Floor<Uplight>(72, s);
			OnWall<WallSconce>(65, s, 12);
			Pilaster(75, s);

			Vanilla(ModContent.TileType<RouletteTable>(), 78, s);
			Vanilla(ModContent.TileType<BlackjackTable>(), 87, s);
			Pilaster(95, s);

			if (hardmode) {
				Vanilla(ModContent.TileType<UpgradeMachine>(), 98, s);
				Vanilla(ModContent.TileType<VideoSlotLunarGods>(), 102, s);
				Vanilla(ModContent.TileType<VideoSlotAngler>(), 105, s);
				Vanilla(ModContent.TileType<VideoSlotBoreal>(), 108, s);
			}
			else {
				for (int k = 0; k < 4; k++)
					Vanilla(ModContent.TileType<SlotMachine>(), 98 + k * 3, s);
			}
			OnWall<FortuneWheel>(112, s, 3);
			if (modern) {
				Floor<DiceStatue>(124, s);   // centrepiece: golden dice sculpture
			}
			else {
				// Arcade corner.
				Vanilla(ModContent.TileType<PlinkoCabinet>(), 121, s);
				Vanilla(ModContent.TileType<DerbyCabinet>(), 124, s);
				Vanilla(ModContent.TileType<HiLoCabinet>(), 127, s);
			}
			OnWall<WallSconce>(139, s, 8);
			OnWall<PaintingWheel>(50, s, 9);
			OnWall<PaintingVegasNight>(80, s, 7);
			OnWall<PaintingGoldenDice>(100, s, 7);
			OnWall<PaintingWildCard>(123, s, 6);
			Pilaster(141, s);
			Floor<MarblePlanter>(145, s);
			Floor<CashMachine>(22, s);
			Floor<VelvetRope>(31, s);
			Floor<VelvetRope>(114, s);
			Floor<PalmPlanter>(138, s);
			OnWall<NightWindow>(13, s, 9);
			OnWall<NightWindow>(82, s, 13);
			OnWall<NightWindow>(143, s, 9);
			OnWall<NeonSuits>(63, s, 16);
			foreach (int x in new[] { 11, 47, 73, 91, 99 })
				Hang<VelvetDrape>(x, s);
			if (modern) {
				Floor<JackpotTotem>(120, s);
				Floor<GoldenAce>(129, s);
			}
		}

		private static void BarFloor(bool hardmode)
		{
			const int s = 1;
			Elevators(s);
			Floor<BarShelf>(12, s);
			Floor<BarCounter>(21, s);
			Floor<BarCounter>(25, s);
			Floor<BarStool>(30, s);
			Floor<BarStool>(33, s);
			OnWall<NeonBar>(22, s, 8);
			Pilaster(36, s);

			Floor<VelvetSofa>(39, s);
			Floor<CocktailTable>(45, s);
			OnWall<WallSconce>(46, s, 9);
			Floor<GrandPiano>(50, s);
			Floor<VelvetSofa>(58, s);
			Floor<MarblePlanter>(64, s);
			Pilaster(67, s);

			// The high rollers casino has a row of themed video slots instead of classic machines.
			int[] videoSlots = { ModContent.TileType<VideoSlotLunarGods>(), ModContent.TileType<VideoSlotAngler>(), ModContent.TileType<VideoSlotBoreal>(), ModContent.TileType<VideoSlotBloodMoon>() };
			for (int k = 0; k < 10; k++)
				Vanilla(hardmode ? videoSlots[k % 4] : k is 4 or 5 ? ModContent.TileType<VideoSlotAngler>() : ModContent.TileType<SlotMachine>(), 70 + k * 3, s);
			Pilaster(101, s);

			Vanilla(ModContent.TileType<BlackjackTable>(), 104, s);
			Vanilla(ModContent.TileType<RouletteTable>(), 113, s);
			OnWall<WallSconce>(111, s, 9);
			if (modern)
				Floor<ChampagneTower>(121, s);
			else
				Floor<CocktailTable>(121, s);
			Floor<ChipDisplay>(125, s);
			Floor<MarblePlanter>(130, s);
			OnWall<NightWindow>(61, s, 10);
			OnWall<NightWindow>(104, s, 13);
			OnWall<NeonSuits>(89, s, 12);
			foreach (int x in new[] { 10, 38, 69, 120 })
				Hang<VelvetDrape>(x, s);
			Vanilla(ModContent.TileType<HiLoCabinet>(), 139, s);
			Vanilla(ModContent.TileType<PlinkoCabinet>(), 142, s);
			Vanilla(ModContent.TileType<DerbyCabinet>(), 145, s);
			OnWall<PaintingRoyalFlush>(13, s, 9);
			OnWall<PaintingLuckySevens>(40, s, 7);
			OnWall<PaintingJackpot>(52, s, 7);
			OnWall<PaintingWildCard>(82, s, 7);
			OnWall<PaintingGoldenDice>(105, s, 7);
			OnWall<PaintingWheel>(122, s, 7);
		}

		/// <summary>Raised show stage: carpet top on marble, framed by pilasters, lit from the front edge.</summary>
		private static void ShowStage(int from, int to, int storey, ushort top)
		{
			ushort marble = (ushort)ModContent.TileType<MarbleCheckerFloor>();
			int floor = Floors[storey];
			for (int x = X(from); x <= X(to); x++) {
				for (int y = floor - 2; y < floor; y++) {
					Tile tile = Main.tile[x, y];
					tile.HasTile = true;
					tile.TileType = y == floor - 2 ? top : marble;
				}
			}
		}

		private static void Cabaret(bool hardmode)
		{
			const int s = 2;
			Elevators(s);
			Pilaster(11, s);
			ShowStage(13, 35, s, hardmode ? (ushort)ModContent.TileType<NoirCarpet>() : (ushort)ModContent.TileType<VelvetCarpet>());
			InteriorFurniture.Place<Uplight>(X(14), Floors[s] - 4);
			InteriorFurniture.Place<Uplight>(X(32), Floors[s] - 4);
			OnWall<NeonCabaret>(19, s, 12);
			Pilaster(37, s);

			Floor<CocktailTable>(40, s);
			Floor<CocktailTable>(44, s);
			Floor<PoleStage>(49, s);
			Floor<CocktailTable>(59, s);
			Floor<CocktailTable>(63, s);
			OnWall<WallSconce>(66, s, 9);
			Pilaster(68, s);

			Floor<VelvetSofa>(71, s);
			Floor<GrandPiano>(77, s);
			Pilaster(85, s);

			Vanilla(ModContent.TileType<RouletteTable>(), 88, s);
			Vanilla(ModContent.TileType<BlackjackTable>(), 97, s);
			for (int k = 0; k < 3; k++)
				Vanilla(hardmode ? (k == 1 ? ModContent.TileType<VideoSlotBloodMoon>() : ModContent.TileType<VideoSlotLunarGods>()) : ModContent.TileType<SlotMachine>(), 106 + k * 3, s);
			OnWall<WallSconce>(116, s, 9);
			Floor<CocktailTable>(119, s);
			Floor<VelvetSofa>(123, s);
			Floor<MarblePlanter>(129, s);
			Floor<PalmPlanter>(139, s);
			Floor<GoldenAce>(144, s);
			OnWall<NightWindow>(141, s, 9);
			OnWall<NeonSuits>(105, s, 11);
			foreach (int x in new[] { 13, 34, 46, 66, 87, 104, 118 })
				Hang<VelvetDrape>(x, s);
			OnWall<PaintingLuckySevens>(41, s, 7);
			OnWall<PaintingRoyalFlush>(59, s, 8);
			OnWall<PaintingJackpot>(73, s, 7);
			OnWall<PaintingVegasNight>(92, s, 7);
			OnWall<PaintingWildCard>(119, s, 6);
		}

		// ------------------------------------------------------------------ people

		public static readonly Point[] Spots = {
			new(X(12), Floors[0]), new(X(27), Floors[0]), new(X(38), Floors[0]), new(X(53), Floors[0]), new(X(61), Floors[0]),
			new(X(80), Floors[0]), new(X(89), Floors[0]), new(X(100), Floors[0]), new(X(124), Floors[0]),
			new(X(28), Floors[1]), new(X(41), Floors[1]), new(X(47), Floors[1]), new(X(74), Floors[1]), new(X(86), Floors[1]),
			new(X(106), Floors[1]), new(X(123), Floors[1]),
			new(X(41), Floors[2]), new(X(45), Floors[2]), new(X(60), Floors[2]), new(X(64), Floors[2]), new(X(73), Floors[2]),
			new(X(90), Floors[2]), new(X(108), Floors[2]),
		};

		private static void At<T>(int offset, int floorY, int facing = 1) where T : ModNPC
			=> NPC.NewNPC(new EntitySource_SpawnNPC(), X(offset) * 16 + 8, floorY * 16, ModContent.NPCType<T>(), ai0: facing);

		public static void SpawnPeople(bool hardmode, int guests)
		{
			// Ground floor
			At<PrizeCashier>(21, Floors[0], -1);
			At<RouletteCroupier>(32, Floors[0], -1);
			At<BlackjackDealer>(43, Floors[0], -1);
			At<RouletteCroupier>(85, Floors[0], -1);
			At<BlackjackDealer>(94, Floors[0], -1);
			// Bar floor
			At<Bartender>(16, Floors[1], 1);
			At<BlackjackDealer>(111, Floors[1], -1);
			At<RouletteCroupier>(120, Floors[1], -1);
			// Cabaret
			At<Showgirl>(24, Floors[2] - 2, 1);
			// The pole is the exact centre of the 8-tile stage.
			NPC.NewNPC(new EntitySource_SpawnNPC(), X(53) * 16, (Floors[2] - 1) * 16, ModContent.NPCType<PoleDancer>(), ai0: 1);
			At<RouletteCroupier>(95, Floors[2], -1);
			At<BlackjackDealer>(104, Floors[2], -1);
			// Easter egg: one visit in three, Togi streams from the slot row upstairs (surface casino).
			if (!hardmode && Main.rand.NextBool(3))
				At<Togi>(84, Floors[1], -1);

			for (int k = 0; k < guests; k++) {
				Point spot = Spots[Main.rand.Next(Spots.Length)];
				NPC.NewNPC(new EntitySource_SpawnNPC(), spot.X * 16 + 8, spot.Y * 16, ModContent.NPCType<CasinoGuest>());
			}
		}
	}
}
