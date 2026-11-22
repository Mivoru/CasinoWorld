using CasinoWorld.Common.Gambler;
using CasinoWorld.Content.Items.Mounts;
using CasinoWorld.Content.Mounts;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Buffs
{
	/// <summary>Gambler set bonus: after a jackpot hit, +10 % gambler damage for 4 seconds.</summary>
	public class HotStreak : ModBuff
	{
		public const int Duration = 240;
		public const float DamageBonus = 0.10f;

		public override void Update(Player player, ref int buffIndex)
		{
			player.GetDamage<GamblerDamageClass>() += DamageBonus;
		}
	}

	public class GamblorghiniBuff : ModBuff
	{
		public override LocalizedText DisplayName => ModContent.GetInstance<Gamblorghini>().DisplayName;

		public override LocalizedText Description => Language.GetText("BuffDescription.MinecartLeft");

		public override void SetStaticDefaults()
		{
			BuffID.Sets.BasicMountData[Type] = new BuffID.Sets.BuffMountData {
				mountID = ModContent.MountType<GamblorghiniMount>(),
			};
		}
	}
}
