using Terraria;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Systems
{
	/// <summary>
	/// Casinos have no clock: it is always the same time of day inside, and no
	/// blood moon, eclipse, rain or invasion can start there.
	/// </summary>
	public class CasinoTimeFreeze : ModSystem
	{
		public override void PostUpdateTime()
		{
			if (!CasinoTravel.InCasino)
				return;

			Main.dayTime = true;
			Main.time = 27000;
			Main.bloodMoon = false;
			Main.eclipse = false;
			Main.invasionType = 0;
			Main.raining = false;
			Main.rainTime = 0;
			Main.slimeRain = false;
		}
	}
}
