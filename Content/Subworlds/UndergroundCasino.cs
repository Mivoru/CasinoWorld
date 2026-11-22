using System.Collections.Generic;
using CasinoWorld.Common.Games;
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
	/// Hardmode casino for high rollers: the three-storey hall from <see cref="CasinoInterior"/> in night
	/// colours (noir marble, amethyst damask, midnight velvet), with the upgrade machine in the lobby.
	/// Higher limits and hardmode jackpots.
	/// </summary>
	public class UndergroundCasino : CasinoSubworld
	{
		private const int GuestCount = 12;

		public override int Width => CasinoInterior.WorldWidth;
		public override int Height => CasinoInterior.WorldHeight;

		// Hardmode stakes: 1 gold to 1 platinum chips, 5 platinum per round.
		public override long[] ChipValues => new[] { Coins.Gold, 10 * Coins.Gold, 50 * Coins.Gold, Coins.Platinum };
		public override long MaxBet => 5 * Coins.Platinum;
		public override long MinJackpotStake => Coins.Gold;
		public override long FullJackpotStake => 10 * Coins.Gold;

		/// <summary>Jackpots drop the highest unlocked tier: hardmode, post-Plantera or post-Moon Lord.</summary>
		public override int[] JackpotItems => NPC.downedMoonlord
			? new[] { ModContent.ItemType<StardustDeck>(), ModContent.ItemType<MeteorDice>(), ModContent.ItemType<InfinitySlot>() }
			: NPC.downedPlantBoss
				? new[] { ModContent.ItemType<AceOfSpades>(), ModContent.ItemType<DoublingDice>(), ModContent.ItemType<RouletteRevolver>() }
				: new[] { ModContent.ItemType<SnakeEyes>(), ModContent.ItemType<RoyalFlush>(), ModContent.ItemType<JackpotCannon>() };

		public override Point[] GuestSpots => CasinoInterior.Spots;

		public override List<GenPass> Tasks => new() { new UndergroundCasinoPass() };

		public override Vector3 Ambient => new(0.55f, 0.46f, 0.68f);

		protected override void SpawnPeople() => CasinoInterior.SpawnPeople(true, GuestCount);
	}

	public class UndergroundCasinoPass : GenPass
	{
		public UndergroundCasinoPass() : base("HighRollerHall", 1) { }

		protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
		{
			progress.Message = Language.GetTextValue("Mods.CasinoWorld.Subworlds.Generating");
			CasinoInterior.Build(true);
		}
	}
}
