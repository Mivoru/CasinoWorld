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

		/// <summary>
		/// Martingale Medallion: every gambler hit without a jackpot adds +2 % jackpot chance (up to 75 %),
		/// a jackpot resets it. Measured in tools/gambler_dps.py: +13 % / +9 % / +8 % DPS at set Luck 4 / 10 / 13.
		/// </summary>
		public bool Martingale;
		public const float MartingaleStep = 0.02f;
		private int martingaleMisses;

		/// <summary>Extra jackpot chance from the held weapon's Gambler prefix (Lucky, Rigged...), set by the prefix.</summary>
		public float PrefixJackpot;

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
			Martingale = false;
			PrefixJackpot = Content.Prefixes.GamblerPrefix.JackpotOf(Player.HeldItem);
			// Gambler hits never crit, so crit from vanilla reforges (Keen, Zealous, Godly...) becomes half as much jackpot chance.
			if (Player.HeldItem.CountsAsClass<GamblerDamageClass>())
				PrefixJackpot += Player.HeldItem.crit * 0.005f;
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

			float jackpotChance = JackpotChance;
			if (Martingale)
				jackpotChance = Math.Min(jackpotChance + MartingaleStep * martingaleMisses, 0.75f);
			GamblerPot pot = Player.GetModPlayer<GamblerPot>();
			float bustChance = BustChance;
			if (pot.AllInActive) {
				jackpotChance = Math.Min(jackpotChance + Content.Buffs.AllIn.JackpotBonus, 0.75f);
				bustChance = Math.Min(1f - jackpotChance, bustChance + Content.Buffs.AllIn.BustBonus);
			}
			jackpotChance += PrefixJackpot;

			float roll = Main.rand.NextFloat();
			pot.Feed(roll < jackpotChance, roll >= jackpotChance && roll < jackpotChance + bustChance);
			if (Martingale && roll >= jackpotChance)
				martingaleMisses++;
			if (roll < jackpotChance) {
				martingaleMisses = 0;
				modifiers.SourceDamage *= JackpotMultiplier;
				lastHitJackpot = true;
				if (HotStreakSet)
					Player.AddBuff(ModContent.BuffType<Content.Buffs.HotStreak>(), Content.Buffs.HotStreak.Duration);
				CombatText.NewText(target.Hitbox, new Color(255, 215, 60), Language.GetTextValue("Mods.CasinoWorld.Gambler.Jackpot"), dramatic: true);
				SoundEngine.PlaySound(CasinoSounds.JackpotProc, target.Center);
				Games.DailyChallenges.Count(Player, Games.Challenge.Jackpots);
				for (int k = 0; k < 10; k++)
					Dust.NewDust(target.position, target.width, target.height, DustID.GoldCoin, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-5f, -1f));
				Effects.CasinoFx.Ring(target.Center, new Color(255, 220, 90), 14, 4.5f, 1.3f);
				Effects.CasinoFx.Ring(target.Center, new Color(255, 250, 220), 8, 2.5f, 1f);
				Lighting.AddLight(target.Center, 1.2f, 0.9f, 0.3f);
			}
			else if (roll < jackpotChance + bustChance && HouseEdge && houseEdgeCooldown <= 0) {
				houseEdgeCooldown = 600;
				CombatText.NewText(target.Hitbox, new Color(120, 220, 255), Language.GetTextValue("Mods.CasinoWorld.Gambler.Saved"));
				Effects.CasinoFx.Ring(target.Center, new Color(120, 220, 255), 10, 3f);
			}
			else if (roll < jackpotChance + bustChance) {
				modifiers.SourceDamage *= BustMultiplier;
				CombatText.NewText(target.Hitbox, Color.Gray, Language.GetTextValue("Mods.CasinoWorld.Gambler.Bust"));
				for (int k = 0; k < 4; k++) {
					Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.Smoke, 0, -1f, 150, default, 0.9f);
					d.noGravity = true;
				}
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
