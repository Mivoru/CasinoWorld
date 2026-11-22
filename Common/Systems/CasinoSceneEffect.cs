using CasinoWorld.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Systems
{
	/// <summary>Music inside the casinos: lounge jazz on the surface, dark neon swing underground.</summary>
	public class CasinoSceneEffect : ModSceneEffect
	{
		public override int Music => SubworldLibrary.SubworldSystem.Current is Content.Subworlds.UndergroundCasino
			? CasinoMusic.HighRollers
			: CasinoMusic.Lounge;

		public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

		public override bool IsSceneEffectActive(Player player) => CasinoTravel.InCasino;
	}
}
