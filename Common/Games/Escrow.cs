using System.Collections.Generic;
using CasinoWorld.Common.Crates;
using CasinoWorld.Common.Upgrades;
using CasinoWorld.Content.Tiles.Interior;
using Terraria;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Games
{
	/// <summary>
	/// What the running games owe the local player: stakes whose round is still open, wins already rolled but not yet
	/// paid, and items inside a machine. CasinoPlayer saves it with the character, so a crash or a lost connection in
	/// the middle of a round costs nothing. A normal exit settles every round first, and then nothing is owed.
	/// </summary>
	public static class Escrow
	{
		public static long Coins
		{
			get {
				long total = ModContent.GetInstance<SicBoSystem>().Owed + ModContent.GetInstance<ScratchCardSystem>().Owed;
				foreach (CasinoTableGameSystem game in ModContent.GetContent<CasinoTableGameSystem>())
					total += game.Owed;
				return total;
			}
		}

		public static List<Item> Items
		{
			get {
				var items = new List<Item>();
				Item slot = ModContent.GetInstance<UpgradeSystem>().Slot;
				if (slot is { IsAir: false })
					items.Add(slot.Clone());
				Item crate = ModContent.GetInstance<CrateSystem>().Opening.Owed;
				if (crate != null)
					items.Add(crate);
				Item wheel = FortuneWheel.Owed;
				if (wheel != null)
					items.Add(wheel);
				return items;
			}
		}
	}
}
