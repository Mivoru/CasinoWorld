using Terraria.ModLoader;

namespace CasinoWorld.Common.Gambler
{
	/// <summary>
	/// Gambler damage. Takes generic bonuses (like "+10% damage") but never crits the normal way:
	/// instead every hit rolls Jackpot or Bust, see <see cref="GamblerPlayer"/>.
	/// </summary>
	public class GamblerDamageClass : DamageClass
	{
		public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
			=> damageClass == Generic ? StatInheritanceData.Full : StatInheritanceData.None;

		public override bool GetEffectInheritance(DamageClass damageClass) => false;

		// Without this no reforge, vanilla or ours, can be rolled on a Gambler weapon.
		public override bool GetPrefixInheritance(DamageClass damageClass) => damageClass == Generic;

		public override bool UseStandardCritCalcs => false;
	}
}
