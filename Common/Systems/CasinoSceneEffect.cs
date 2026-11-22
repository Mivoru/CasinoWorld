using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Systems
{
	/// <summary>Music inside the casinos. Placeholder vanilla track until custom music exists.</summary>
	public class CasinoSceneEffect : ModSceneEffect
	{
		public override int Music => MusicID.TownDay;

		public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

		public override bool IsSceneEffectActive(Player player) => CasinoTravel.InCasino;
	}
}
