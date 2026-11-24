using CasinoWorld.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Systems
{
	/// <summary>Music inside the casinos: lounge jazz on the surface, dark neon swing underground.</summary>
	public class CasinoSceneEffect : ModSceneEffect
	{
		private static int BonusMusic => ModContent.GetInstance<Games.VideoSlots.VideoSlotSystem>().Round.Key switch {
			"LunarGods" => CasinoMusic.BonusLunar,
			"Angler" => CasinoMusic.BonusAngler,
			"Boreal" => CasinoMusic.BonusBoreal,
			_ => CasinoMusic.BonusBlood,
		};

		// Each video slot has its own music while its free spins run.
		public override int Music => !Main.dedServ && ModContent.GetInstance<Games.VideoSlots.VideoSlotSystem>() is { IsOpen: true, Round.InBonusRun: true } ? BonusMusic : SubworldLibrary.SubworldSystem.Current is Content.Subworlds.UndergroundCasino
			? CasinoMusic.HighRollers
			: CasinoMusic.Lounge;

		public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

		public override bool IsSceneEffectActive(Player player) => CasinoTravel.InCasino;
	}
}
