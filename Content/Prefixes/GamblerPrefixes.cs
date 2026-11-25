using System.Collections.Generic;
using CasinoWorld.Common.Gambler;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Prefixes
{
	/// <summary>
	/// Reforges only Gambler weapons get. Gambler hits cannot crit, so these trade in jackpot chance
	/// instead. Lucky is the best roll (like Godly / Unreal), Cursed and Unlucky are the bad ones.
	/// </summary>
	public abstract class GamblerPrefix : ModPrefix
	{
		protected virtual float Damage => 0f;
		protected virtual float Speed => 0f;          // + faster
		protected virtual float Knockback => 0f;
		public virtual float Jackpot => 0f;
		protected abstract float Value { get; }

		private static readonly Dictionary<int, float> jackpotByType = new();

		public override PrefixCategory Category => PrefixCategory.AnyWeapon;

		public override void SetStaticDefaults() => jackpotByType[Type] = Jackpot;

		public override void Unload() => jackpotByType.Clear();

		/// <summary>Extra jackpot chance of the given item's prefix (0 if it has none of ours).</summary>
		public static float JackpotOf(Item item) => item != null && !item.IsAir && jackpotByType.TryGetValue(item.prefix, out float j) ? j : 0f;

		public override bool CanRoll(Item item) => item.CountsAsClass<GamblerDamageClass>();

		public override float RollChance(Item item) => 1.2f;

		public override void SetStats(ref float damageMult, ref float knockbackMult, ref float useTimeMult, ref float scaleMult, ref float shootSpeedMult, ref float manaMult, ref int critBonus)
		{
			damageMult += Damage;
			knockbackMult += Knockback;
			useTimeMult -= Speed;
		}

		public override void ModifyValue(ref float valueMult) => valueMult *= Value;

		public override IEnumerable<TooltipLine> GetTooltipLines(Item item)
		{
			if (Jackpot != 0f) {
				yield return new TooltipLine(Mod, "PrefixJackpot", Language.GetTextValue("Mods.CasinoWorld.Prefixes.JackpotLine", (Jackpot > 0 ? "+" : "") + (int)(Jackpot * 100))) {
					IsModifier = true,
					IsModifierBad = Jackpot < 0,
				};
			}
		}
	}

	public class Lucky : GamblerPrefix
	{
		protected override float Damage => 0.12f;
		protected override float Speed => 0.06f;   // 5 % rounds away on use time 9 and 10, and the prefix could not be rolled there
		public override float Jackpot => 0.04f;
		protected override float Value => 1.9f;
	}

	public class Rigged : GamblerPrefix
	{
		protected override float Damage => 0.05f;
		public override float Jackpot => 0.06f;
		protected override float Value => 1.6f;
	}

	public class Loaded : GamblerPrefix
	{
		protected override float Damage => 0.15f;
		protected override float Knockback => 0.1f;
		protected override float Value => 1.5f;
	}

	public class Shady : GamblerPrefix
	{
		protected override float Speed => 0.1f;
		public override float Jackpot => 0.02f;
		protected override float Value => 1.35f;
	}

	public class Cursed : GamblerPrefix
	{
		protected override float Damage => -0.1f;
		public override float Jackpot => -0.03f;
		protected override float Value => 0.6f;
	}

	public class Unlucky : GamblerPrefix
	{
		public override float Jackpot => -0.04f;
		protected override float Value => 0.75f;
	}
}
