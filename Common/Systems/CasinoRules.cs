using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Systems
{
	/// <summary>
	/// House rules inside the casinos: no boss or event summons (bosses belong in the main world, and a
	/// fight inside a subworld would wreck the halls), so those items simply refuse to work in here.
	/// </summary>
	public class CasinoRules : GlobalItem
	{
		private static int lastWarning;

		public override bool CanUseItem(Item item, Player player)
		{
			if (!CasinoTravel.InCasino || ItemID.Sets.SortingPriorityBossSpawns[item.type] < 0)
				return true;
			if (player.whoAmI == Main.myPlayer && Main.GameUpdateCount - lastWarning > 120) {
				lastWarning = (int)Main.GameUpdateCount;
				Main.NewText(Language.GetTextValue("Mods.CasinoWorld.UI.NoSummonsInside"), new Microsoft.Xna.Framework.Color(255, 120, 120));
			}
			return false;
		}
	}
}
