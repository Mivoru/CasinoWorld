using CasinoWorld.Content.Buffs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Mounts
{
	/// <summary>
	/// Sports-car minecart. Same speed as a normal minecart (it is a cosmetic prize),
	/// with gold sparks and a horn-like landing sound.
	/// </summary>
	public class GamblorghiniMount : ModMount
	{
		public override void SetStaticDefaults()
		{
			MountID.Sets.Cart[Type] = true;
			MountID.Sets.FacePlayersVelocity[Type] = true;

			Mount.SetAsMinecart(MountData, ModContent.BuffType<GamblorghiniBuff>(), MountData.frontTexture);
			MountData.spawnDust = DustID.GoldCoin;
		}

		public override void UpdateEffects(Player player)
		{
			if (System.Math.Abs(player.velocity.X) > 4f && Main.rand.NextBool(6)) {
				Vector2 exhaust = player.Center + new Vector2(-player.direction * 22f, 14f);
				Dust dust = Dust.NewDustPerfect(player.RotatedRelativePoint(exhaust), DustID.GoldCoin);
				dust.noGravity = true;
				dust.scale = 0.6f;
				dust.velocity = new Vector2(-player.direction * 1.5f, -0.3f);
			}
		}
	}
}
