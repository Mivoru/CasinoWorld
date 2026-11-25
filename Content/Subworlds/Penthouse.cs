using System.Collections.Generic;
using CasinoWorld.Common;
using CasinoWorld.Common.Club;
using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Tiles;
using CasinoWorld.Content.Tiles.Interior;
using Microsoft.Xna.Framework;
using SubworldLibrary;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CasinoWorld.Content.Subworlds
{
	/// <summary>
	/// The player's own floor above the casino: an empty luxury loft that is saved with the world. Everything inside
	/// the shell can be built, furnished and torn down; only the shell itself and the door stay.
	/// </summary>
	public class Penthouse : Subworld
	{
		public const int Edge = CasinoInterior.Edge;
		public const int Left = Edge, RoomWidth = 150, Right = Left + RoomWidth;
		public const int Ceiling = Edge, Floor = Edge + 71;

		public override int Width => Right + Edge + 1;
		public override int Height => Floor + 2 + Edge;

		public override bool NoPlayerSaving => false;
		public override bool ShouldSave => true;
		public override bool NormalUpdates => true;

		public override List<GenPass> Tasks => new() { new PenthousePass() };

		public override void OnLoad()
		{
			Main.dayTime = false;
			Main.time = 16200;
		}

		public override void CopyMainWorldData()
		{
			SubworldSystem.CopyWorldData(nameof(Main.hardMode), Main.hardMode);
			SubworldSystem.CopyWorldData(nameof(NPC.downedPlantBoss), NPC.downedPlantBoss);
			SubworldSystem.CopyWorldData(nameof(NPC.downedMoonlord), NPC.downedMoonlord);
		}

		public override void ReadCopiedMainWorldData()
		{
			Main.hardMode = SubworldSystem.ReadCopiedWorldData<bool>(nameof(Main.hardMode));
			NPC.downedPlantBoss = SubworldSystem.ReadCopiedWorldData<bool>(nameof(NPC.downedPlantBoss));
			NPC.downedMoonlord = SubworldSystem.ReadCopiedWorldData<bool>(nameof(NPC.downedMoonlord));
		}

		/// <summary>True for tiles that belong to the shell.</summary>
		public static bool Shell(int i, int j) => i <= Left || i >= Right || j <= Ceiling || j >= Floor;
	}

	public class PenthousePass : GenPass
	{
		public PenthousePass() : base("Penthouse", 1) { }

		protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
		{
			progress.Message = Language.GetTextValue("Mods.CasinoWorld.Subworlds.Generating");
			Main.worldSurface = 20;
			Main.rockLayer = 25;
			Main.spawnTileX = Penthouse.Left + 6;
			Main.spawnTileY = Penthouse.Floor - 1;
			ushort foundation = (ushort)ModContent.TileType<CasinoFoundation>(), ceiling = (ushort)ModContent.TileType<CofferedCeiling>(), floor = (ushort)ModContent.TileType<BlackWhiteMarbleFloor>();
			ushort wall = (ushort)ModContent.WallType<BlackMarbleWall>(), low = (ushort)ModContent.WallType<WalnutSlatWall>();
			int right = Penthouse.Floor - 22, left = Penthouse.Floor - 44;
			for (int x = 0; x < Main.maxTilesX; x++)
				for (int y = 0; y < Main.maxTilesY; y++) {
					Tile tile = Main.tile[x, y];
					bool insideX = x > Penthouse.Left && x < Penthouse.Right;
					bool air = insideX && y > Penthouse.Ceiling && y < Penthouse.Floor;
					tile.HasTile = !air;
					if (!air)
						tile.TileType = insideX && y == Penthouse.Floor ? floor : insideX && y == Penthouse.Ceiling ? ceiling : foundation;
					if (x >= Penthouse.Left && x <= Penthouse.Right && y >= Penthouse.Ceiling && y <= Penthouse.Floor)
						tile.WallType = y >= Penthouse.Floor - 5 || y < right && y >= right - 4 && x > Penthouse.Left + 91 || y < left && y >= left - 4 && x < Penthouse.Left + 61 ? low : wall;
				}
			// Two mezzanines: right at a third, left at two thirds of the height, with platforms up to them.
			void Deck(int from, int to, int y, ushort type)
			{
				for (int x = from; x <= to; x++) {
					Tile tile = Main.tile[x, y];
					tile.HasTile = true;
					tile.TileType = type;
					Tile under = Main.tile[x, y + 1];
					under.HasTile = true;
					under.TileType = ceiling;
				}
			}
			Deck(Penthouse.Left + 92, Penthouse.Right - 1, right, (ushort)ModContent.TileType<NoirCarpet>());
			Deck(Penthouse.Left + 1, Penthouse.Left + 60, left, (ushort)ModContent.TileType<VelvetCarpet>());
			WorldGen.RangeFrame(0, 0, Main.maxTilesX - 1, Main.maxTilesY - 1);
			void Steps(int x, int bottom, int top)
			{
				for (int y = bottom; y >= top; y -= 4)
					for (int k = 0; k < 6; k++)
						WorldGen.PlaceTile(x + k, y, TileID.Platforms, true, true, -1, 29);
			}
			Steps(Penthouse.Left + 86, Penthouse.Floor - 4, right);
			Steps(Penthouse.Left + 61, Penthouse.Floor - 4, left);

			// Starting furniture; the rest is up to the owner.
			WorldGen.PlaceObject(Penthouse.Left + 3, Penthouse.Floor - 1, ModContent.TileType<CasinoDoor>(), mute: true);
			// Panorama: a wall of glass two storeys high in the middle, smaller ones on the decks.
			for (int column = 0; column < 4; column++)
				for (int row = 0; row < 5; row++)
					InteriorFurniture.Place<NightWindow>(Penthouse.Left + 34 + column * 6, Penthouse.Floor - 12 - row * 5);
			for (int column = 0; column < 4; column++)
				for (int row = 0; row < 3; row++) {
					InteriorFurniture.Place<NightWindow>(Penthouse.Left + 110 + column * 6, right - 13 - row * 5);
					InteriorFurniture.Place<NightWindow>(Penthouse.Left + 14 + column * 6, left - 11 - row * 5);
				}
			for (int k = 0; k < 7; k++)
				InteriorFurniture.Place<RingPendant>(Penthouse.Left + 10 + k * 21, Penthouse.Ceiling + 1);
			for (int k = 0; k < 3; k++) {
				InteriorFurniture.Place<RingPendant>(Penthouse.Left + 98 + k * 18, right + 2);
				InteriorFurniture.Place<RingPendant>(Penthouse.Left + 8 + k * 20, left + 2);
			}
			InteriorFurniture.PlaceOnFloor<GrandPiano>(Penthouse.Left + 112, right);
			InteriorFurniture.PlaceOnFloor<VelvetSofa>(Penthouse.Left + 122, right);
			InteriorFurniture.PlaceOnFloor<PalmPlanter>(Penthouse.Left + 136, right);
			InteriorFurniture.PlaceOnFloor<LoungeChair>(Penthouse.Left + 20, left);
			InteriorFurniture.PlaceOnFloor<GlassTable>(Penthouse.Left + 24, left);
			InteriorFurniture.PlaceOnFloor<LoungeChair>(Penthouse.Left + 14, Penthouse.Floor);
			InteriorFurniture.PlaceOnFloor<GlassTable>(Penthouse.Left + 18, Penthouse.Floor);
			InteriorFurniture.PlaceOnFloor<LoungeChair>(Penthouse.Left + 22, Penthouse.Floor);
			InteriorFurniture.PlaceOnFloor<PalmPlanter>(Penthouse.Left + 28, Penthouse.Floor);
			InteriorFurniture.PlaceOnFloor<ChampagneTower>(Penthouse.Left + 34, Penthouse.Floor);
			foreach (int x in new[] { 31, 59 })
				for (int k = 0; k < 9; k++)
					InteriorFurniture.Place<LightColumn>(Penthouse.Left + x, Penthouse.Floor - 4 - k * 4);
		}
	}

	/// <summary>Keeps the penthouse quiet: no spawns, an endless starry night, its own music.</summary>
	public class PenthouseScene : ModSceneEffect
	{
		public override int Music => CasinoMusic.Penthouse;
		public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
		public override bool IsSceneEffectActive(Player player) => CasinoTravel.InPenthouse;
	}

	public class PenthouseRules : ModSystem
	{
		public override void PostUpdateTime()
		{
			if (!CasinoTravel.InPenthouse)
				return;
			Main.dayTime = false;
			Main.time = 16200;
			Main.bloodMoon = false;
			Main.eclipse = false;
			Main.invasionType = 0;
			Main.raining = false;
			Main.slimeRain = false;
		}
	}

	public class PenthouseTiles : GlobalTile
	{
		public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged)
			=> !CasinoTravel.InPenthouse || !Penthouse.Shell(i, j) && type != ModContent.TileType<CasinoDoor>();

		public override bool CanExplode(int i, int j, int type) => !CasinoTravel.InPenthouse;

		public override bool CanReplace(int i, int j, int type, int tileTypeBeingPlaced) => !CasinoTravel.InPenthouse || !Penthouse.Shell(i, j);
	}

	public class PenthouseSpawns : GlobalNPC
	{
		public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
		{
			if (CasinoTravel.InPenthouse)
				maxSpawns = 0;
		}

		public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
		{
			if (CasinoTravel.InPenthouse)
				pool.Clear();
		}
	}
}

namespace CasinoWorld.Content.Items
{
	/// <summary>Takes its owner to the penthouse and back.</summary>
	public class PenthouseKey : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 22;
			Item.height = 34;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = Item.useAnimation = 45;
			Item.rare = ItemRarityID.Purple;
			Item.value = 0;
		}

		public override bool CanUseItem(Player player) => !CasinoTravel.InCasino;

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer)
				return true;
			if (CasinoTravel.InPenthouse)
				SubworldSystem.Exit();
			else if (CasinoTravel.EnterPenthouse())
				player.GetModPlayer<VipPlayer>().VisitedPenthouse = true;
			return true;
		}
	}
}
