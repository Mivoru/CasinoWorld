using System.Collections.Generic;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.Accessories;
using CasinoWorld.Content.Items.Armor;
using CasinoWorld.Content.Items.Mounts;
using CasinoWorld.Content.Items.Vanity;
using CasinoWorld.Content.Items.Weapons;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace CasinoWorld.Common.Crates
{
	public enum CrateRarity : byte { Blue, Purple, Pink, Red, Gold }

	public enum CrateKind : byte { Lucky, HighRoller }

	/// <summary>Game stage that decides what crates contain (taken from the world's defeated bosses).</summary>
	public enum CrateStage : byte { Early, PostSkeletron, Hardmode, PostPlantera, PostMoonLord }

	/// <summary>One possible prize. Golden = a gold variant of a Gambler weapon (+10 % damage, gold look).</summary>
	public readonly record struct CrateDrop(int ItemType, int MinStack = 1, int MaxStack = 1, bool Golden = false);

	/// <summary>
	/// Loot tables for casino crates, opened CS:GO style. The contents follow the game stage, so a crate
	/// opened after the Moon Lord holds endgame loot, not Hermes Boots.
	///
	///   Blue: supplies of the stage (potions, coins, tokens, materials)
	///   Purple: useful vanilla gear and materials of the stage, plenty of coins and tokens
	///   Pink: Gambler accessories, luxury and vanity
	///   Red: Gambler weapons and armour of the current tier
	///   Gold: a golden Gambler weapon, the Gamblorghini, or a pile of platinum
	///
	/// Rarity odds: Lucky 60/25/10/4/1 %, High Roller 45/30/15/8/2 % (CS:GO cases: 79.9/16/3.2/0.64/0.26 %).
	/// Pity: at the latest every 10th crate without Pink or better gives Pink, and the 80th crate without
	/// Gold gives Gold. Prices come from the expected value of the contents (tools/crate_ev.py).
	/// </summary>
	public static class CrateLoot
	{
		public static readonly int[] LuckyWeights = { 600, 250, 100, 40, 10 };       // per mille
		public static readonly int[] HighRollerWeights = { 450, 300, 150, 80, 20 };

		public const int PityRare = 10;   // crates without Pink or better
		public const int PityGold = 80;   // crates without Gold

		public static readonly Color[] RarityColors = {
			new(75, 105, 255), new(136, 71, 255), new(211, 44, 230), new(235, 75, 75), new(255, 205, 40),
		};

		private static Dictionary<CrateStage, List<CrateDrop>[]> tables;

		public static void Unload() => tables = null;

		public static int[] Weights(CrateKind kind) => kind == CrateKind.HighRoller ? HighRollerWeights : LuckyWeights;

		public static CrateStage CurrentStage =>
			NPC.downedMoonlord ? CrateStage.PostMoonLord
			: NPC.downedPlantBoss ? CrateStage.PostPlantera
			: Main.hardMode ? CrateStage.Hardmode
			: NPC.downedBoss3 ? CrateStage.PostSkeletron
			: CrateStage.Early;

		private static Dictionary<CrateStage, List<CrateDrop>[]> Tables => tables ??= Build();

		private static CrateDrop[] Golden(params int[] weapons)
		{
			var drops = new CrateDrop[weapons.Length];
			for (int k = 0; k < weapons.Length; k++)
				drops[k] = new CrateDrop(weapons[k], Golden: true);
			return drops;
		}

		private static List<CrateDrop>[] Stage(CrateDrop[] blue, CrateDrop[] purple, CrateDrop[] pink, CrateDrop[] red, CrateDrop[] gold)
			=> new[] { new List<CrateDrop>(blue), new List<CrateDrop>(purple), new List<CrateDrop>(pink), new List<CrateDrop>(red), new List<CrateDrop>(gold) };

		private static Dictionary<CrateStage, List<CrateDrop>[]> Build()
		{
			int token = ModContent.ItemType<LoyaltyToken>();
			int clover = ModContent.ItemType<FourLeafClover>(), ace = ModContent.ItemType<AceUpTheSleeve>(), golex = ModContent.ItemType<GolexWatch>();
			int car = ModContent.ItemType<Gamblorghini>();
			int dice = ModContent.ItemType<LoadedDice>(), deck = ModContent.ItemType<CardDeck>(), roller = ModContent.ItemType<HighRoller>();
			int lever = ModContent.ItemType<JackpotLever>(), hand = ModContent.ItemType<DealersHand>(), wheel = ModContent.ItemType<WheelOfFortune>();
			int snake = ModContent.ItemType<SnakeEyes>(), flush = ModContent.ItemType<RoyalFlush>(), cannon = ModContent.ItemType<JackpotCannon>();
			int spades = ModContent.ItemType<AceOfSpades>(), doubling = ModContent.ItemType<DoublingDice>(), revolver = ModContent.ItemType<RouletteRevolver>();
			int stardust = ModContent.ItemType<StardustDeck>(), meteor = ModContent.ItemType<MeteorDice>(), infinity = ModContent.ItemType<InfinitySlot>();
			var gamblerSet = new[] { ModContent.ItemType<GamblerFedora>(), ModContent.ItemType<GamblerVest>(), ModContent.ItemType<GamblerShoes>() };
			var pinkBase = new[] { new CrateDrop(clover), new CrateDrop(ace), new CrateDrop(golex) };

			CrateDrop[] With(CrateDrop[] a, params CrateDrop[] b)
			{
				var list = new List<CrateDrop>(a);
				list.AddRange(b);
				return list.ToArray();
			}

			CrateDrop[] Gear(params int[] items)
			{
				var drops = new CrateDrop[items.Length];
				for (int k = 0; k < items.Length; k++)
					drops[k] = new CrateDrop(items[k]);
				return drops;
			}

			return new() {
				[CrateStage.Early] = Stage(
					new CrateDrop[] { new(token, 10, 20), new(ItemID.LesserHealingPotion, 4, 6), new(ItemID.GoldCoin, 1, 3),
						new(ItemID.IronskinPotion, 2, 2), new(ItemID.SwiftnessPotion, 2, 2), new(ItemID.Bomb, 5, 8) },
					new CrateDrop[] { new(ItemID.HermesBoots), new(ItemID.CloudinaBottle), new(ItemID.BandofRegeneration),
						new(ItemID.MagicMirror), new(token, 40, 60), new(ItemID.GoldCoin, 5, 8) },
					With(pinkBase, new CrateDrop(ItemID.TopHat), new CrateDrop(ItemID.Sunglasses)),
					With(Gear(dice, deck, roller), Gear(gamblerSet)),
					With(Golden(dice, deck, roller), new CrateDrop(car), new CrateDrop(ItemID.PlatinumCoin, 1, 1))),

				[CrateStage.PostSkeletron] = Stage(
					new CrateDrop[] { new(token, 20, 35), new(ItemID.HealingPotion, 4, 6), new(ItemID.GoldCoin, 3, 6),
						new(ItemID.RegenerationPotion, 2, 2), new(ItemID.SpelunkerPotion, 2, 2), new(ItemID.ArcheryPotion, 2, 2) },
					new CrateDrop[] { new(token, 80, 120), new(ItemID.GoldCoin, 10, 15), new(ItemID.CobaltShield),
						new(ItemID.FeralClaws), new(ItemID.AnkletoftheWind), new(ItemID.GoldenKey) },
					With(pinkBase, new CrateDrop(ModContent.ItemType<TheHouseMask>()), new CrateDrop(ModContent.ItemType<GoldenChip>())),
					With(Gear(dice, deck, roller, lever, hand, wheel), Gear(gamblerSet)),
					With(Golden(dice, deck, roller, lever, hand, wheel), new CrateDrop(car), new CrateDrop(ItemID.PlatinumCoin, 1, 2))),

				[CrateStage.Hardmode] = Stage(
					new CrateDrop[] { new(token, 40, 70), new(ItemID.GreaterHealingPotion, 3, 5), new(ItemID.GoldCoin, 10, 20),
						new(ItemID.SoulofLight, 3, 6), new(ItemID.SoulofNight, 3, 6), new(ItemID.LifeforcePotion, 2, 2) },
					new CrateDrop[] { new(token, 150, 220), new(ItemID.GoldCoin, 30, 50), new(ItemID.MythrilBar, 8, 12),
						new(ItemID.PhilosophersStone), new(ItemID.CrossNecklace), new(ItemID.StarCloak) },
					With(pinkBase, new CrateDrop(ItemID.TitanGlove)),
					Gear(snake, flush, cannon),
					With(Golden(snake, flush, cannon), new CrateDrop(car), new CrateDrop(ItemID.PlatinumCoin, 2, 3))),

				[CrateStage.PostPlantera] = Stage(
					new CrateDrop[] { new(token, 80, 120), new(ItemID.GreaterHealingPotion, 5, 8), new(ItemID.GoldCoin, 30, 50),
						new(ItemID.Ectoplasm, 3, 6), new(ItemID.ChlorophyteBar, 3, 5), new(ItemID.EndurancePotion, 2, 2) },
					new CrateDrop[] { new(token, 300, 400), new(ItemID.PlatinumCoin, 1, 1), new(ItemID.ChlorophyteBar, 10, 15),
						new(ItemID.SpectreBar, 8, 12), new(ItemID.AvengerEmblem), new(ItemID.ShroomiteBar, 6, 10) },
					With(pinkBase, new CrateDrop(ItemID.DestroyerEmblem)),
					Gear(spades, doubling, revolver),
					With(Golden(spades, doubling, revolver), new CrateDrop(car), new CrateDrop(ItemID.PlatinumCoin, 3, 5))),

				[CrateStage.PostMoonLord] = Stage(
					new CrateDrop[] { new(token, 150, 220), new(ItemID.SuperHealingPotion, 4, 6), new(ItemID.PlatinumCoin, 1, 1),
						new(ItemID.FragmentSolar, 5, 10), new(ItemID.FragmentNebula, 5, 10), new(ItemID.FragmentStardust, 5, 10) },
					new CrateDrop[] { new(token, 500, 700), new(ItemID.LunarBar, 6, 10), new(ItemID.PlatinumCoin, 2, 3),
						new(ItemID.FragmentVortex, 15, 25) },
					With(pinkBase, new CrateDrop(ItemID.CelestialShell)),
					Gear(stardust, meteor, infinity),
					With(Golden(stardust, meteor, infinity), new CrateDrop(car), new CrateDrop(ItemID.PlatinumCoin, 8, 12))),
			};
		}

		public static CrateRarity RollRarity(CrateKind kind, UnifiedRandom rand)
		{
			int[] weights = Weights(kind);
			int roll = rand.Next(1000);
			for (int k = 0; k < weights.Length; k++) {
				roll -= weights[k];
				if (roll < 0)
					return (CrateRarity)k;
			}
			return CrateRarity.Blue;
		}

		public static CrateDrop RollDrop(CrateStage stage, CrateRarity rarity, UnifiedRandom rand)
		{
			List<CrateDrop> pool = Tables[stage][(int)rarity];
			return pool[rand.Next(pool.Count)];
		}

		/// <summary>Rolls a prize, applying the pity counters (which it also updates).</summary>
		public static (CrateDrop drop, CrateRarity rarity) Roll(CrateKind kind, CrateStage stage, UnifiedRandom rand, ref int sinceRare, ref int sinceGold)
		{
			CrateRarity rarity = RollRarity(kind, rand);
			if (sinceGold + 1 >= PityGold)
				rarity = CrateRarity.Gold;
			else if (sinceRare + 1 >= PityRare && rarity < CrateRarity.Pink)
				rarity = CrateRarity.Pink;

			sinceGold = rarity == CrateRarity.Gold ? 0 : sinceGold + 1;
			sinceRare = rarity >= CrateRarity.Pink ? 0 : sinceRare + 1;
			return (RollDrop(stage, rarity, rand), rarity);
		}

		/// <summary>A random entry for the decorative strip (no pity, does not touch counters).</summary>
		public static (CrateDrop drop, CrateRarity rarity) RollFiller(CrateKind kind, CrateStage stage, UnifiedRandom rand)
		{
			CrateRarity rarity = RollRarity(kind, rand);
			return (RollDrop(stage, rarity, rand), rarity);
		}

		/// <summary>Builds the prize item, marking golden variants.</summary>
		public static Item MakeItem(CrateDrop drop, int stack)
		{
			var item = new Item(drop.ItemType, stack);
			if (drop.Golden && item.TryGetGlobalItem(out GoldenGambler golden))
				golden.Golden = true;
			return item;
		}
	}
}
