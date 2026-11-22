using CasinoWorld.Common.Systems;
using Terraria;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Players
{
	public class CasinoPlayer : ModPlayer
	{
		// Ticks to wait after entering a world before moving the player back to the casino door.
		// Vanilla places the player at spawn around OnEnterWorld, so the move must happen later.
		private const int RestoreDelay = 3;
		private int restoreTimer = -1;

		public override void OnEnterWorld()
		{
			restoreTimer = RestoreDelay;
		}

		public override void PostUpdate()
		{
			if (restoreTimer < 0 || Player.whoAmI != Main.myPlayer)
				return;

			if (restoreTimer-- == 0)
				CasinoTravel.TryRestorePosition(Player);
		}
	}
}
