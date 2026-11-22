using CasinoWorld.Content.Items.Crates;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Crates
{
	/// <summary>
	/// Casino crates can be fished up. When a catch rolls a crate, 1 in 5 becomes a casino crate
	/// (in hardmode a third of those are High Roller Crates). Any other catch has a 1 in 80 chance
	/// to be a Lucky Crate, so fishing without crate potions still turns them up now and then.
	/// </summary>
	public class CrateFishing : ModPlayer
	{
		private const int CrateCatchOneIn = 5;
		private const int PlainCatchOneIn = 80;

		public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition)
		{
			if (attempt.inLava || attempt.inHoney || npcSpawn > 0)
				return;

			bool asCrate = attempt.crate && Main.rand.NextBool(CrateCatchOneIn);
			bool asPlain = !attempt.crate && itemDrop > 0 && Main.rand.NextBool(PlainCatchOneIn);
			if (!asCrate && !asPlain)
				return;

			itemDrop = Main.hardMode && asCrate && Main.rand.NextBool(3)
				? ModContent.ItemType<HighRollerCrate>()
				: ModContent.ItemType<LuckyCrate>();
		}
	}
}
