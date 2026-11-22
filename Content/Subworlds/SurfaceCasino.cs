using System.Collections.Generic;
using CasinoWorld.Content.Items.Accessories;
using CasinoWorld.Content.Items.Armor;
using CasinoWorld.Content.Items.Weapons;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CasinoWorld.Content.Subworlds
{
	/// <summary>
	/// Interior of the pre-hardmode surface casino: the three-storey hall from <see cref="CasinoInterior"/>
	/// in cream marble, emerald damask and purple velvet. Low stakes for early-game money.
	/// </summary>
	public class SurfaceCasino : CasinoSubworld
	{
		private const int GuestCount = 12;

		public override int Width => CasinoInterior.WorldWidth;
		public override int Height => CasinoInterior.WorldHeight;

		// Pre-hardmode players have little money: 1 silver to 5 gold chips, 20 gold per round.
		public override long[] ChipValues => new long[] { 100, 1_000, 10_000, 50_000 };
		public override long MaxBet => 200_000;

		public override int[] JackpotItems => new[] {
			ModContent.ItemType<LoadedDice>(), ModContent.ItemType<CardDeck>(), ModContent.ItemType<HighRoller>(),
			ModContent.ItemType<GamblerFedora>(), ModContent.ItemType<GamblerVest>(), ModContent.ItemType<GamblerShoes>(),
			ModContent.ItemType<FourLeafClover>(), ModContent.ItemType<AceUpTheSleeve>(),
		};

		public override Point[] GuestSpots => CasinoInterior.Spots;

		public override List<GenPass> Tasks => new() { new SurfaceCasinoPass() };

		protected override void SpawnPeople() => CasinoInterior.SpawnPeople(false, GuestCount);
	}

	public class SurfaceCasinoPass : GenPass
	{
		public SurfaceCasinoPass() : base("CasinoHall", 1) { }

		protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
		{
			progress.Message = Language.GetTextValue("Mods.CasinoWorld.Subworlds.Generating");
			CasinoInterior.Build(false);
		}
	}
}
