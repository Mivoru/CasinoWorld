using System;
using System.Collections.Generic;
using CasinoWorld.Common.Crates;
using CasinoWorld.Common.Games;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.NPCs.Boss;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CasinoWorld.Common.Club
{
	/// <summary>
	/// Lifetime record of one character: what they bet and won, crates, jackpots, bosses. Feeds the Gambler's Ledger
	/// and the achievements. Counted on the player's own machine and saved with the character.
	/// </summary>
	public class CasinoStats : ModPlayer
	{
		public static readonly int Games = Enum.GetValues<CasinoGame>().Length;

		public long Wagered, WonTotal, LostTotal, BiggestWin, BiggestStake;
		public int Rounds, Streak, BestStreak;
		public int[] GameRounds = new int[Games], GameWins = new int[Games];
		public long[] GameNet = new long[Games];
		public int[] CrateRarities = new int[5];
		public int JackpotHits, TripleJackpots, AllIns, RoyalHands, Challenges, TokensEarned;
		public int BestPokerHand = -1, DuelWins, PokerPots;
		public int WildCardKills, PitBossKills, FortunaKills, RaidWins, Contracts;
		public bool LedgerGiven;

		public readonly HashSet<string> Unlocked = new();

		/// <summary>The local player's record; null on a server or in the menu.</summary>
		public static CasinoStats Local => Main.dedServ || Main.gameMenu || !Main.LocalPlayer.active ? null : Main.LocalPlayer.GetModPlayer<CasinoStats>();

		public int Crates
		{
			get {
				int n = 0;
				foreach (int c in CrateRarities)
					n += c;
				return n;
			}
		}

		public void Wager(long amount, bool countRound = true)
		{
			Wagered += amount;
			BiggestStake = Math.Max(BiggestStake, amount);
			if (countRound)
				Rounds++;
		}

		/// <summary>One settled round: net profit, negative for a loss.</summary>
		public void Settled(CasinoGame game, long net)
		{
			int g = (int)game;
			GameRounds[g]++;
			GameNet[g] += net;
			if (net > 0) {
				GameWins[g]++;
				WonTotal += net;
				BiggestWin = Math.Max(BiggestWin, net);
				BestStreak = Math.Max(BestStreak, ++Streak);
			}
			else if (net < 0) {
				LostTotal -= net;
				Streak = 0;
			}
		}

		public void PokerHand(long net, int category)
		{
			if (net <= 0)
				return;
			PokerPots++;
			BestPokerHand = Math.Max(BestPokerHand, category);
		}

		public void Duel(bool won)
		{
			if (won)
				DuelWins++;
		}

		public void Crate(CrateRarity rarity) => CrateRarities[(int)rarity]++;

		// ------------------------------------------------------------------ bosses

		// A boss counts when it vanishes with no life left and this player had hurt it.
		private readonly Dictionary<int, int> watched = new();

		private void WatchBosses()
		{
			foreach (NPC npc in Main.ActiveNPCs)
				if (npc.ModNPC is TheWildCard or ThePitBoss or Fortuna or Content.NPCs.Enemies.GuardCaptain && npc.playerInteraction[Player.whoAmI])
					watched[npc.whoAmI] = npc.type;
			if (watched.Count == 0)
				return;
			foreach ((int index, int type) in new Dictionary<int, int>(watched)) {
				NPC npc = Main.npc[index];
				if (npc.active && npc.type == type)
					continue;
				watched.Remove(index);
				if (npc.life > 0)
					continue;
				if (type == ModContent.NPCType<TheWildCard>())
					WildCardKills++;
				else if (type == ModContent.NPCType<ThePitBoss>())
					PitBossKills++;
				else if (type == ModContent.NPCType<Content.NPCs.Enemies.GuardCaptain>())
					RaidWins++;
				else
					FortunaKills++;
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// The server tracks interaction; mark it here as well so clients know.
			if ((target.boss || target.ModNPC is Content.NPCs.Enemies.GuardCaptain) && Player.whoAmI == Main.myPlayer) {
				target.playerInteraction[Player.whoAmI] = true;
				if (target.ModNPC is TheWildCard or ThePitBoss or Fortuna or Content.NPCs.Enemies.GuardCaptain)
					watched[target.whoAmI] = target.type;   // a single hit can kill before the next update
			}
		}

		public override void PostUpdate()
		{
			if (Player.whoAmI != Main.myPlayer)
				return;
			WatchBosses();
			if (Main.GameUpdateCount % 30 == 0)
				Achievements.Check(this);
		}

		public override void OnEnterWorld()
		{
			watched.Clear();
			if (LedgerGiven)
				return;
			LedgerGiven = true;
			Coins.GiveItem(Player, new Item(ModContent.ItemType<GamblersLedger>()));
		}

		// ------------------------------------------------------------------ saving

		public override void SaveData(TagCompound tag)
		{
			tag["longs"] = new List<long> { Wagered, WonTotal, LostTotal, BiggestWin, BiggestStake };
			tag["ints"] = new[] { Rounds, Streak, BestStreak, JackpotHits, TripleJackpots, AllIns, RoyalHands, Challenges, TokensEarned, BestPokerHand, DuelWins, PokerPots, WildCardKills, PitBossKills, FortunaKills, RaidWins, Contracts };
			tag["gameRounds"] = GameRounds;
			tag["gameWins"] = GameWins;
			tag["gameNet"] = new List<long>(GameNet);
			tag["crates"] = CrateRarities;
			tag["unlocked"] = new List<string>(Unlocked);
			tag["ledger"] = LedgerGiven;
		}

		public override void LoadData(TagCompound tag)
		{
			IList<long> l = tag.GetList<long>("longs");
			long L(int k) => k < l.Count ? l[k] : 0;
			(Wagered, WonTotal, LostTotal, BiggestWin, BiggestStake) = (L(0), L(1), L(2), L(3), L(4));
			int[] n = tag.GetIntArray("ints");
			int I(int k, int none = 0) => k < n.Length ? n[k] : none;
			(Rounds, Streak, BestStreak, JackpotHits, TripleJackpots) = (I(0), I(1), I(2), I(3), I(4));
			(AllIns, RoyalHands, Challenges, TokensEarned, BestPokerHand) = (I(5), I(6), I(7), I(8), I(9, -1));
			(DuelWins, PokerPots, WildCardKills, PitBossKills, FortunaKills) = (I(10), I(11), I(12), I(13), I(14));
			(RaidWins, Contracts) = (I(15), I(16));
			Copy(tag.GetIntArray("gameRounds"), GameRounds = new int[Games]);
			Copy(tag.GetIntArray("gameWins"), GameWins = new int[Games]);
			Copy(tag.GetIntArray("crates"), CrateRarities = new int[5]);
			IList<long> net = tag.GetList<long>("gameNet");
			GameNet = new long[Games];
			for (int k = 0; k < Games && k < net.Count; k++)
				GameNet[k] = net[k];
			Unlocked.Clear();
			foreach (string key in tag.GetList<string>("unlocked"))
				Unlocked.Add(key);
			LedgerGiven = tag.GetBool("ledger");
		}

		private static void Copy(int[] from, int[] to) => Array.Copy(from, to, Math.Min(from.Length, to.Length));
	}

	/// <summary>One goal: progress against a target, a token reward and an item as its picture.</summary>
	public record Achievement(string Key, int Tokens, long Goal, Func<CasinoStats, long> Progress, Func<int> Icon, bool Money = false)
	{
		public string Name => Language.GetTextValue($"Mods.CasinoWorld.Achievements.{Key}.Name");

		public string Description => Language.GetTextValue($"Mods.CasinoWorld.Achievements.{Key}.Text");

		public bool Done(CasinoStats s) => s.Unlocked.Contains(Key);
	}

	public static class Achievements
	{
		private static long Bit(bool b) => b ? 1 : 0;

		private static Func<int> Of<T>() where T : ModItem => ModContent.ItemType<T>;

		private static Func<int> Vanilla(int type) => () => type;

		private static int Wins(CasinoStats s, CasinoGame g) => s.GameWins[(int)g];

		private static int Level(CasinoStats s) => s.Player.GetModPlayer<VipPlayer>().Level;

		public static readonly Achievement[] All = {
			new("FirstBet", 10, 1, s => s.Rounds, Of<LoyaltyToken>()),
			new("Regular", 25, 100, s => s.Rounds, Of<LoyaltyToken>()),
			new("Fixture", 60, 1000, s => s.Rounds, Vanilla(ItemID.GoldChandelier)),
			new("HighStakes", 40, Coins.Platinum, s => s.BiggestStake, Vanilla(ItemID.PlatinumCoin), true),
			new("Wagered10", 50, 10 * Coins.Platinum, s => s.Wagered, Vanilla(ItemID.GoldCoin), true),
			new("Wagered100", 100, 100 * Coins.Platinum, s => s.Wagered, Vanilla(ItemID.PiggyBank), true),
			new("BigWin", 40, Coins.Platinum, s => s.BiggestWin, Vanilla(ItemID.PlatinumCoin), true),
			new("HugeWin", 80, 10 * Coins.Platinum, s => s.BiggestWin, Vanilla(ItemID.Safe), true),
			new("Streak5", 30, 5, s => s.BestStreak, Of<Content.Items.Accessories.FourLeafClover>()),
			new("Streak10", 80, 10, s => s.BestStreak, Of<Content.Items.Accessories.FourLeafClover>()),
			new("Roulette", 40, 25, s => Wins(s, CasinoGame.Roulette), Of<Content.Items.Weapons.WheelOfFortune>()),
			new("Blackjack", 40, 50, s => Wins(s, CasinoGame.Blackjack), Of<Content.Items.Weapons.CardDeck>()),
			new("Slots", 40, 100, s => Wins(s, CasinoGame.Slots), Of<Content.Items.Weapons.JackpotLever>()),
			new("Arcade", 25, 25, s => Wins(s, CasinoGame.Arcade), Of<Content.Items.Weapons.LuckyPenny>()),
			new("Poker", 40, 10, s => s.PokerPots, Of<Content.Items.Weapons.DealersHand>()),
			new("FullHouse", 60, 6, s => Math.Max(0, s.BestPokerHand), Of<Content.Items.Weapons.RoyalFlush>()),
			new("Duel", 40, 10, s => s.DuelWins, Of<Content.Items.Weapons.LuckyPenny>()),
			new("Crates10", 25, 10, s => s.Crates, Of<Content.Items.Crates.LuckyCrate>()),
			new("Crates100", 80, 100, s => s.Crates, Of<Content.Items.Crates.HighRollerCrate>()),
			new("Legendary", 60, 1, s => s.CrateRarities[4], Of<Content.Items.Mounts.Gamblorghini>()),
			new("Hits100", 30, 100, s => s.JackpotHits, Of<Content.Items.Weapons.LoadedDice>()),
			new("Hits2500", 100, 2500, s => s.JackpotHits, Of<Content.Items.Weapons.MeteorDice>()),
			new("Triple", 40, 1, s => s.TripleJackpots, Of<Content.Items.Weapons.InfinitySlot>()),
			new("Royal", 60, 1, s => s.RoyalHands, Of<Content.Items.Weapons.RoyalFlush>()),
			new("Challenges", 40, 10, s => s.Challenges, Of<GamblersHandbook>()),
			new("WildCard", 50, 1, s => s.WildCardKills, Of<Content.Items.Vanity.WildCardTrophy>()),
			new("PitBoss", 80, 1, s => s.PitBossKills, Of<Content.Items.PitBoss.PitBossTrophy>()),
			new("Contract", 30, 1, s => s.Contracts, Of<GamblersLedger>()),
			new("AllContracts", 200, 12, s => s.Player.GetModPlayer<ContractPlayer>().Done.Count, Of<GamblersLedger>()),
			new("Raid", 60, 3, s => s.RaidWins, Of<Content.Items.MarkedInvitation>()),
			new("Fortuna", 150, 1, s => s.FortunaKills, Of<Content.Items.Fortuna.FortunaTrophy>()),
			new("Penthouse", 100, 1, s => Bit(s.Player.GetModPlayer<VipPlayer>().VisitedPenthouse), Of<PenthouseKey>()),
			new("Diamond", 150, 4, s => Level(s), Vanilla(ItemID.Diamond)),
		};

		public static int Count(CasinoStats s)
		{
			int n = 0;
			foreach (Achievement a in All)
				if (a.Done(s))
					n++;
			return n;
		}

		public static void Check(CasinoStats s)
		{
			foreach (Achievement a in All) {
				if (a.Done(s) || a.Progress(s) < a.Goal)
					continue;
				s.Unlocked.Add(a.Key);
				s.TokensEarned += a.Tokens;
				Coins.GiveItem(s.Player, new Item(ModContent.ItemType<LoyaltyToken>(), a.Tokens));
				AchievementToast.Show(a);
			}
		}
	}
}
