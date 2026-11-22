using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CasinoWorld.Common.Gambler
{
	/// <summary>
	/// The Gambler's dice roll on every hit, plus casino bookkeeping (loyalty token progress).
	///
	/// Each gambler hit rolls once:
	///   Jackpot: chance J, damage x3
	///   Bust:    chance B, damage x0.5
	///   Normal:  otherwise
	/// Expected damage multiplier = 1 + 2J - 0.5B.
	/// Base values J = 5 %, B = 20 % give exactly 1.0, so a gambler weapon with N damage
	/// deals N on average, like any other class. Each point of Luck adds 1 % jackpot and removes
	/// 2 % bust: +3 % average damage per point while bust is above 0 (Luck 0..10),
	/// then +2 % per point. Jackpot chance is capped at 50 %.
	/// </summary>
	public class GamblerPlayer : ModPlayer
	{
		public const float BaseJackpotChance = 0.05f;
		public const float BaseBustChance = 0.20f;
		public const float JackpotMultiplier = 3f;
		public const float BustMultiplier = 0.5f;
		public const float JackpotPerLuck = 0.01f;
		public const float BustPerLuck = 0.02f;
		public const float MaxJackpotChance = 0.5f;

		/// <summary>Luck from equipment, reset every tick.</summary>
		public int Luck;

		/// <summary>Extra jackpot chance from equipment (for example Ace Up the Sleeve), reset every tick.</summary>
		public float BonusJackpotChance;

		/// <summary>Wearing the full Gambler set: jackpots grant Hot Streak.</summary>
		public bool HotStreakSet;

		/// <summary>Spectral set: jackpots release two homing spectral cards.</summary>
		public bool SpectralSet;

		/// <summary>Celestial set: jackpots call down a meteor die on the target.</summary>
		public bool CelestialSet;

		// Set by Roll when the current hit is a jackpot, used once the hit lands.
		private bool lastHitJackpot;

		/// <summary>House Edge (expert): the first bust every 10 seconds becomes a normal hit.</summary>
		public bool HouseEdge;
		private int houseEdgeCooldown;

		/// <summary>Copper wagered since the last loyalty token was paid out.</summary>
		public long TokenProgress;

		public float JackpotChance => Math.Clamp(BaseJackpotChance + Luck * JackpotPerLuck + BonusJackpotChance, 0f, MaxJackpotChance);

		public float BustChance => Math.Clamp(BaseBustChance - Luck * BustPerLuck, 0f, 1f);

		public override void ResetEffects()
		{
			Luck = 0;
			BonusJackpotChance = 0f;
			HotStreakSet = false;
			HouseEdge = false;
			SpectralSet = false;
			CelestialSet = false;
			if (houseEdgeCooldown > 0)
				houseEdgeCooldown--;
		}

		public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (item.CountsAsClass<GamblerDamageClass>())
				Roll(target, ref modifiers);
		}

		public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (proj.CountsAsClass<GamblerDamageClass>())
				Roll(target, ref modifiers);
		}

		private void Roll(NPC target, ref NPC.HitModifiers modifiers)
		{
			modifiers.DisableCrit();
			lastHitJackpot = false;

			float roll = Main.rand.NextFloat();
			if (roll < JackpotChance) {
				modifiers.SourceDamage *= JackpotMultiplier;
				lastHitJackpot = true;
				if (HotStreakSet)
					Player.AddBuff(ModContent.BuffType<Content.Buffs.HotStreak>(), Content.Buffs.HotStreak.Duration);
				CombatText.NewText(target.Hitbox, new Color(255, 215, 60), Language.GetTextValue("Mods.CasinoWorld.Gambler.Jackpot"), dramatic: true);
				SoundEngine.PlaySound(CasinoSounds.JackpotProc, target.Center);
				for (int k = 0; k < 8; k++)
					Dust.NewDust(target.position, target.width, target.height, DustID.GoldCoin, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-4f, -1f));
			}
			else if (roll < JackpotChance + BustChance && HouseEdge && houseEdgeCooldown <= 0) {
				houseEdgeCooldown = 600;
				CombatText.NewText(target.Hitbox, new Color(120, 220, 255), Language.GetTextValue("Mods.CasinoWorld.Gambler.Saved"));
			}
			else if (roll < JackpotChance + BustChance) {
				modifiers.SourceDamage *= BustMultiplier;
				CombatText.NewText(target.Hitbox, Color.Gray, Language.GetTextValue("Mods.CasinoWorld.Gambler.Bust"));
				if (Main.rand.NextBool(3))   // not on every bust, it would get tiring
					SoundEngine.PlaySound(CasinoSounds.BustProc, target.Center);
			}
		}

		public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) => JackpotProcs(target, damageDone);

		public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Set-bonus projectiles do not trigger further procs.
			if (proj.type != ModContent.ProjectileType<Content.Projectiles.SpectralCard>() && proj.type != ModContent.ProjectileType<Content.Projectiles.MeteorDie>())
				JackpotProcs(target, damageDone);
		}

		/// <summary>Armour set effects that fire on a jackpot hit.</summary>
		private void JackpotProcs(NPC target, int damageDone)
		{
			if (!lastHitJackpot || Player.whoAmI != Main.myPlayer)
				return;
			lastHitJackpot = false;
			var source = Player.GetSource_Misc("GamblerSet");
			if (SpectralSet) {
				for (int k = 0; k < 2; k++)
					Projectile.NewProjectile(source, Player.Center, Main.rand.NextVector2CircularEdge(10f, 10f),
						ModContent.ProjectileType<Content.Projectiles.SpectralCard>(), Math.Max(1, damageDone * 2 / 5), 2f, Player.whoAmI, 20f);
			}
			if (CelestialSet) {
				Vector2 start = target.Center + new Vector2(Main.rand.Next(-150, 150), -650);
				Projectile.NewProjectile(source, start, (target.Center - start).SafeNormalize(Vector2.UnitY) * 18f,
					ModContent.ProjectileType<Content.Projectiles.MeteorDie>(), Math.Max(1, damageDone * 3 / 5), 5f, Player.whoAmI, target.Center.Y);
			}
		}

		public override void SaveData(TagCompound tag)
		{
			if (TokenProgress > 0)
				tag["tokenProgress"] = TokenProgress;
		}

		public override void LoadData(TagCompound tag)
		{
			TokenProgress = tag.ContainsKey("tokenProgress") ? tag.GetLong("tokenProgress") : 0;
		}
	}
}
