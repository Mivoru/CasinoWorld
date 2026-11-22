using System.Collections.Generic;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.Weapons;
using CasinoWorld.Content.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CasinoWorld.Common
{
	/// <summary>
	/// Impact sounds for Gambler projectiles, by material: cards slice, dice clatter, chips clink.
	/// Kept in one table so every weapon of the same material sounds the same.
	/// </summary>
	public class GamblerProjectileAudio : GlobalProjectile
	{
		private static Dictionary<int, SoundStyle> impacts;

		public override void SetStaticDefaults()
		{
			impacts = new() {
				[ModContent.ProjectileType<ThrownCard>()] = CasinoSounds.CardHit,
				[ModContent.ProjectileType<RoyalCard>()] = CasinoSounds.CardHit,
				[ModContent.ProjectileType<SpectralCard>()] = CasinoSounds.CardHit,
				[ModContent.ProjectileType<GalaxyCard>()] = CasinoSounds.CardHit,
				[ModContent.ProjectileType<LoadedDie>()] = CasinoSounds.DiceBounce,
				[ModContent.ProjectileType<ExplodingDie>()] = CasinoSounds.DiceBounce,
				[ModContent.ProjectileType<DoublingDie>()] = CasinoSounds.DiceBounce,
				[ModContent.ProjectileType<HighRollerChip>()] = CasinoSounds.ChipHit,
				[ModContent.ProjectileType<JackpotChip>()] = CasinoSounds.ChipHit,
				[ModContent.ProjectileType<RevolverChip>()] = CasinoSounds.ChipHit,
				[ModContent.ProjectileType<SymbolShot>()] = CasinoSounds.ReelStop with { Volume = 0.35f, Pitch = 0.4f },
			};
		}

		public override void Unload() => impacts = null;

		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (impacts != null && impacts.TryGetValue(projectile.type, out SoundStyle sound))
				SoundEngine.PlaySound(sound, projectile.Center);
		}
	}

	/// <summary>Throw sounds for Gambler weapons, by what they throw.</summary>
	public class GamblerWeaponAudio : GlobalItem
	{
		public override bool AppliesToEntity(Item entity, bool lateInstantiation)
			=> entity.ModItem is GamblerWeapon || entity.ModItem is JackpotLever;

		public override void SetDefaults(Item item)
		{
			item.UseSound = item.ModItem switch {
				LoadedDice or SnakeEyes or DoublingDice => CasinoSounds.DiceThrow,
				MeteorDice => SoundID_Item88,
				CardDeck or RoyalFlush or AceOfSpades or StardustDeck or DealersHand => CasinoSounds.CardThrow,
				HighRoller => CasinoSounds.ChipFlick,
				WheelOfFortune => CasinoSounds.RouletteBallBounce,
				_ => item.UseSound,
			};
		}

		private static SoundStyle SoundID_Item88 => Terraria.ID.SoundID.Item88;
	}

	/// <summary>Ka-ching when buying from casino staff or the Gambler.</summary>
	public class CasinoShopAudio : ModPlayer
	{
		public override void PostBuyItem(NPC vendor, Item[] shopInventory, Item item)
		{
			if (vendor.ModNPC?.Mod == Mod)
				SoundEngine.PlaySound(CasinoSounds.CashRegister with { Volume = 0.5f });
		}
	}
}
