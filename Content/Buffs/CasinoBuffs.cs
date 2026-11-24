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

	/// <summary>All In: high stakes from a full Pot. The dealt hand (GamblerPot.Hand) sets the odds and the duration.</summary>
	public class AllIn : ModBuff
	{
		public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
		{
			GamblerPot pot = Main.LocalPlayer.GetModPlayer<GamblerPot>();
			if (pot.Hand != null)
				tip = Language.GetTextValue("Mods.CasinoWorld.Gambler.AllInBuff", Language.GetTextValue("Mods.CasinoWorld.Gambler.Hands." + pot.Hand.Key),
					(int)System.Math.Round(pot.JackpotBonus * 100), (int)System.Math.Round(pot.BustBonus * 100));
		}

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = false;
			Main.buffNoSave[Type] = true;
			BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			if (Main.rand.NextBool(3))
				Common.Effects.CasinoFx.Spark(player.Center + Main.rand.NextVector2Circular(20, 28), new Microsoft.Xna.Framework.Vector2(0, -1f),
					Main.rand.NextBool() ? new Microsoft.Xna.Framework.Color(255, 70, 70) : new Microsoft.Xna.Framework.Color(255, 220, 90), 0.8f);
			Lighting.AddLight(player.Center, 0.5f, 0.3f, 0.1f);
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
