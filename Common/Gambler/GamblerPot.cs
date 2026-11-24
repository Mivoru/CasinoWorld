using System;
using System.Collections.Generic;
using System.Linq;
using CasinoWorld.Common.Effects;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CasinoWorld.Common.Gambler
{
	/// <summary>A poker hand All In can deal: chance weight, odds while it runs, and how long it runs.</summary>
	public record PokerHand(string Key, int Weight, float Jackpot, float Bust, int Ticks, Color Color)
	{
		// Average and DPS gain are measured in tools/gambler_dps.py ("All In").
		public static readonly PokerHand[] All = {
			new("Pair", 450, 0.20f, 0.10f, 300, new Color(235, 225, 205)),
			new("TwoPair", 250, 0.25f, 0.10f, 300, new Color(150, 220, 255)),
			new("Three", 150, 0.25f, 0.10f, 360, new Color(120, 240, 150)),
			new("FullHouse", 90, 0.30f, 0f, 360, new Color(215, 140, 255)),
			new("Four", 45, 0.35f, 0f, 420, new Color(255, 160, 70)),
			new("RoyalFlush", 15, 0.45f, 0f, 480, new Color(255, 220, 80)),
		};

		public static PokerHand Draw(bool better)
		{
			int roll = Main.rand.Next(All.Sum(h => h.Weight));
			for (int k = 0; k < All.Length; k++)
				if ((roll -= All[k].Weight) < 0)
					return All[better ? Math.Min(k + 1, All.Length - 1) : k];
			return All[0];
		}

		/// <summary>Five cards that show this hand, as suit * 13 + rank (rank 0 = ace).</summary>
		public int[] Deal()
		{
			int[] ranks = Enumerable.Range(0, 13).OrderBy(_ => Main.rand.Next()).ToArray();
			int[] suits = Enumerable.Range(0, 4).OrderBy(_ => Main.rand.Next()).ToArray();
			if (Key == "RoyalFlush")
				return new[] { 9, 10, 11, 12, 0 }.Select(r => suits[0] * 13 + r).ToArray();
			int[] groups = Key switch {
				"Pair" => new[] { 2, 1, 1, 1 },
				"TwoPair" => new[] { 2, 2, 1 },
				"Three" => new[] { 3, 1, 1 },
				"FullHouse" => new[] { 3, 2 },
				_ => new[] { 4, 1 },
			};
			var cards = new List<int>();
			for (int g = 0; g < groups.Length; g++)
				for (int k = 0; k < groups[g]; k++)
					cards.Add(suits[(k + g) % 4] * 13 + ranks[g]);
			return cards.ToArray();
		}
	}

	/// <summary>
	/// The Gambler's own resource: the Pot. Gambler hits feed it (busts feed it too: losses make the pot
	/// grow), it drains when you stop fighting. Full pot: press the All In key. A shockwave opens it, the
	/// dealer deals a poker hand and the hand sets the odds; a tenth of the damage dealt meanwhile is paid
	/// out as a closing blast.
	/// </summary>
	public class GamblerPot : ModPlayer
	{
		public const float Max = 100f;
		private const int DrainDelay = 180;
		private const float Payout = 0.10f;
		private const int PayoutCap = 6;   // times the held weapon's damage

		public static ModKeybind AllInKey { get; private set; }

		/// <summary>The bound key, or null when the player has none.</summary>
		public static string KeyName => !Main.dedServ && AllInKey?.GetAssignedKeys() is { Count: > 0 } keys ? keys[0] : null;

		public float Pot;
		/// <summary>Pot gain multiplier from equipment, reset every tick.</summary>
		public float GainMultiplier = 1f;
		/// <summary>Multiplies the Pot gain of jackpots (High Roller set), reset every tick.</summary>
		public float JackpotFeed = 1f;
		/// <summary>Pit Boss set: All In deals a hand one rank better.</summary>
		public bool BetterHands;
		private int sinceHit;
		private bool announcedFull, toldKey, warnedUnbound;

		/// <summary>Centre of the HUD meter as a fraction of the screen, null = top centre; saved per character.</summary>
		public Vector2? MeterPosition;

		// The running hand; known to the owner only, who also rolls their own hits.
		public PokerHand Hand;
		public int[] Cards = new int[5];
		public int HandAge;
		private long dealt;
		private bool wasActive;

		public bool Full => Pot >= Max;

		public bool AllInActive => Player.HasBuff(ModContent.BuffType<Content.Buffs.AllIn>());

		public float JackpotBonus => Hand?.Jackpot ?? 0f;
		public float BustBonus => Hand?.Bust ?? 0f;

		/// <summary>Remaining part of the running All In, 1 to 0.</summary>
		public float AllInLeft
		{
			get {
				int index = Player.FindBuffIndex(ModContent.BuffType<Content.Buffs.AllIn>());
				return index < 0 ? 0f : Player.buffTime[index] / (float)(Hand?.Ticks ?? 300);
			}
		}

		public override void Load() => AllInKey = KeybindLoader.RegisterKeybind(Mod, "AllIn", "V");

		public override void Unload() => AllInKey = null;

		/// <summary>Called by GamblerPlayer for every gambler hit roll.</summary>
		public void Feed(bool jackpot, bool bust)
		{
			if (AllInActive)
				return;
			Pot = Math.Min(Max, Pot + (jackpot ? 3f * JackpotFeed : bust ? 2f : 1f) * GainMultiplier);
			sinceHit = 0;
		}

		/// <summary>Counts damage towards the closing payout.</summary>
		public void Tally(int damage)
		{
			if (AllInActive)
				dealt += damage;
		}

		public override void PostUpdate()
		{
			if (Player.whoAmI != Main.myPlayer)
				return;
			bool active = AllInActive;
			if (active)
				HandAge++;
			else if (wasActive)
				CashOut();
			wasActive = active;

			if (++sinceHit > DrainDelay && !Full && Pot > 0)
				Pot = Math.Max(0, Pot - 0.25f);
			if (Full && !announcedFull) {
				announcedFull = true;
				SoundEngine.PlaySound(CasinoSounds.ChipStack with { Pitch = 0.4f });
				CasinoFx.Ring(Player.Center, new Color(255, 210, 90), 12, 3f);
				if (KeyName == null && !warnedUnbound) {
					warnedUnbound = true;
					Main.NewText(Language.GetTextValue("Mods.CasinoWorld.Gambler.UnboundHelp"), new Color(255, 120, 100));
				}
			}
			if (!Full)
				announcedFull = false;

			// Explained once per character, the first time a gambler weapon is in hand.
			if (!toldKey && Player.HeldItem.CountsAsClass<GamblerDamageClass>()) {
				toldKey = true;
				string key = KeyName;
				Main.NewText(key == null ? Language.GetTextValue("Mods.CasinoWorld.Gambler.UnboundHelp") : Language.GetTextValue("Mods.CasinoWorld.Gambler.KeyHelp", key), new Color(255, 215, 90));
			}
		}

		private int HeldDamage => Player.HeldItem.CountsAsClass<GamblerDamageClass>() ? Player.GetWeaponDamage(Player.HeldItem) : 10;

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (AllInKey?.JustPressed != true || AllInActive || Player.dead)
				return;
			if (!Full) {
				SoundEngine.PlaySound(CasinoSounds.ChipFlick with { Pitch = -0.6f, Volume = 0.5f });
				return;
			}
			Pot = 0;
			dealt = 0;
			HandAge = 0;
			Hand = PokerHand.Draw(BetterHands);
			Cards = Hand.Deal();
			Player.AddBuff(ModContent.BuffType<Content.Buffs.AllIn>(), Hand.Ticks);
			if (Hand.Weight <= 90)
				SoundEngine.PlaySound(CasinoSounds.RareReveal);
			// Opening shockwave: three times the held gambler weapon's damage around the player.
			Projectile.NewProjectile(Player.GetSource_Misc("AllIn"), Player.Center, Vector2.Zero, ModContent.ProjectileType<Content.Projectiles.AllInBurst>(), HeldDamage * 3, 8f, Player.whoAmI);
		}

		private void CashOut()
		{
			int damage = (int)Math.Min(dealt * Payout, HeldDamage * PayoutCap);
			Hand = null;
			dealt = 0;
			if (damage <= 0 || Player.dead)
				return;
			CombatText.NewText(Player.Hitbox, new Color(255, 220, 90), Language.GetTextValue("Mods.CasinoWorld.Gambler.CashOut", damage), true);
			Projectile.NewProjectile(Player.GetSource_Misc("AllIn"), Player.Center, Vector2.Zero, ModContent.ProjectileType<Content.Projectiles.AllInBurst>(), damage, 8f, Player.whoAmI, 1f);
		}

		public override void OnRespawn() => Pot = 0;

		public override void ResetEffects()
		{
			GainMultiplier = JackpotFeed = 1f;
			BetterHands = false;
		}

		public override void SaveData(TagCompound tag)
		{
			tag["potKeyTold"] = toldKey;
			if (MeterPosition is Vector2 position)
				tag["potMeterCentre"] = position;
		}

		public override void LoadData(TagCompound tag)
		{
			toldKey = tag.GetBool("potKeyTold");
			MeterPosition = tag.ContainsKey("potMeterCentre") ? tag.Get<Vector2>("potMeterCentre") : null;
		}
	}
}
