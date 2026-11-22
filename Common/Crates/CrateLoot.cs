using System.Collections.Generic;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.Accessories;
using CasinoWorld.Content.Items.Armor;
using CasinoWorld.Content.Items.Mounts;
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

	public readonly record struct CrateDrop(int ItemType, int MinStack = 1, int MaxStack = 1);

	/// <summary>
	/// Loot tables for casino crates, opened CS:GO style. Rarity odds per crate:
	///   Blue 60 %, Purple 25 %, Pink 10 %, Red 4 %, Gold 1 %
	/// (CS:GO cases: 79.92 / 15.98 / 3.20 / 0.64 / 0.26 %). Crates are rarer here than cases are
	/// in CS, so the top tiers come up about 4-6 times as often.
	/// Within a rarity every entry is equally likely.
	/// </summary>
	public static class CrateLoot
	{
		public static readonly int[] RarityWeights = { 600, 250, 100, 40, 10 };   // per mille

		public static readonly Color[] RarityColors = {
			new(75, 105, 255), new(136, 71, 255), new(211, 44, 230), new(235, 75, 75), new(255, 205, 40),
		};

		private static Dictionary<CrateKind, List<CrateDrop>[]> tables;

		public static void Unload() => tables = null;

		private static Dictionary<CrateKind, List<CrateDrop>[]> Tables => tables ??= Build();

		private static Dictionary<CrateKind, List<CrateDrop>[]> Build()
		{
			int token = ModContent.ItemType<LoyaltyToken>();
			var lucky = new[] {
				new List<CrateDrop> {   // Blue: supplies
					new(token, 10, 25), new(ItemID.LesserHealingPotion, 4, 8), new(ItemID.GoldCoin, 2, 5),
					new(ItemID.Bomb, 5, 10), new(ItemID.IronskinPotion, 2, 3), new(ItemID.SwiftnessPotion, 2, 3),
				},
				new List<CrateDrop> {   // Purple: useful vanilla gear
					new(ItemID.HermesBoots), new(ItemID.CloudinaBottle), new(ItemID.BandofRegeneration),
					new(ItemID.MagicMirror), new(ItemID.GoldCoin, 10, 20), new(token, 50, 80),
				},
				new List<CrateDrop> {   // Pink: gambler accessories and luxury
					new(ModContent.ItemType<FourLeafClover>()), new(ModContent.ItemType<AceUpTheSleeve>()),
					new(ModContent.ItemType<GolexWatch>()),
				},
				new List<CrateDrop> {   // Red: gambler weapons and armour
					new(ModContent.ItemType<LoadedDice>()), new(ModContent.ItemType<CardDeck>()), new(ModContent.ItemType<HighRoller>()),
					new(ModContent.ItemType<GamblerFedora>()), new(ModContent.ItemType<GamblerVest>()), new(ModContent.ItemType<GamblerShoes>()),
				},
				new List<CrateDrop> {   // Gold
					new(ModContent.ItemType<Gamblorghini>()), new(ItemID.PlatinumCoin, 1, 1),
				},
			};
			var highRoller = new[] {
				new List<CrateDrop> {
					new(token, 30, 60), new(ItemID.GreaterHealingPotion, 4, 8), new(ItemID.GoldCoin, 10, 20),
					new(ItemID.SoulofLight, 3, 6), new(ItemID.SoulofNight, 3, 6), new(ItemID.LifeforcePotion, 2, 3),
				},
				new List<CrateDrop> {
					new(ItemID.HallowedBar, 5, 10), new(ItemID.PhilosophersStone), new(ItemID.CrossNecklace),
					new(ItemID.GoldCoin, 30, 60), new(token, 150, 200),
				},
				new List<CrateDrop> {
					new(ModContent.ItemType<AceUpTheSleeve>()), new(ModContent.ItemType<FourLeafClover>()),
					new(ItemID.AvengerEmblem), new(ModContent.ItemType<GolexWatch>()),
				},
				new List<CrateDrop> {
					new(ModContent.ItemType<SnakeEyes>()), new(ModContent.ItemType<RoyalFlush>()), new(ModContent.ItemType<JackpotCannon>()),
				},
				new List<CrateDrop> {
					new(ItemID.PlatinumCoin, 2, 5), new(ModContent.ItemType<Gamblorghini>()),
				},
			};
			return new() { [CrateKind.Lucky] = lucky, [CrateKind.HighRoller] = highRoller };
		}

		public static CrateRarity RollRarity(UnifiedRandom rand)
		{
			int roll = rand.Next(1000);
			for (int k = 0; k < RarityWeights.Length; k++) {
				roll -= RarityWeights[k];
				if (roll < 0)
					return (CrateRarity)k;
			}
			return CrateRarity.Blue;
		}

		public static (CrateDrop drop, CrateRarity rarity) Roll(CrateKind kind, UnifiedRandom rand)
		{
			CrateRarity rarity = RollRarity(rand);
			List<CrateDrop> pool = Tables[kind][(int)rarity];
			return (pool[rand.Next(pool.Count)], rarity);
		}

		public static IReadOnlyList<CrateDrop> Pool(CrateKind kind, CrateRarity rarity) => Tables[kind][(int)rarity];
	}
}
