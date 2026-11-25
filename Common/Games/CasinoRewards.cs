using System;
using CasinoWorld.Common.Gambler;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Subworlds;
using Microsoft.Xna.Framework;
using SubworldLibrary;
using CasinoWorld.Common;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Games
{
	/// <summary>
	/// Rewards from gambling.
	///
	/// Loyalty tokens: paid per round, win or lose, by stake: 1 below 1 gold, 5 from 1 gold, 10 from
	/// 1 platinum. Doubling down or splitting in blackjack counts as another round.
	///
	/// Jackpots: big wins can drop Gambler weapons and armour from the casino's jackpot pool.
	/// The chance scales with the stake up to the casino's FullJackpotStake, and stakes below its
	/// MinJackpotStake never drop anything, so small-bet farming does not work.
	///
	/// Expected cost per jackpot at full stake (1 gold), from the game odds:
	///   Roulette straight bet: wins 1/37, drop 25 % => 1 drop per 148 bets,
	///     house edge 2.7 % => about 4 gold lost per drop.
	///   Blackjack natural: about 4.75 % of hands, drop 12 % => 1 drop per 175 hands,
	///     house edge about 0.5-2 % => about 1-3.5 gold lost per drop.
	/// </summary>
	public static class CasinoRewards
	{

		public const float RouletteStraightJackpot = 0.25f;
		public const float BlackjackNaturalJackpot = 0.12f;

		/// <summary>Loyalty tokens for one round with the given stake.</summary>
		public static int TokensFor(long stake) => stake >= Coins.Platinum ? 10 : stake >= Coins.Gold ? 5 : 1;

		/// <summary>Takes a wager and credits loyalty token progress. False if the player cannot pay.</summary>
		public static bool TryWager(Player player, long amount)
		{
			if (!Systems.CasinoConfig.Instance.GamblingEnabled) {
				if (player.whoAmI == Main.myPlayer)
					Main.NewText(Terraria.Localization.Language.GetTextValue("Mods.CasinoWorld.UI.GamblingDisabled"), new Microsoft.Xna.Framework.Color(255, 160, 120));
				return false;
			}
			if (!Coins.TryTake(player, amount))
				return false;
			DailyChallenges.Count(player, Challenge.Wagers);

			Coins.GiveItem(player, new Item(ModContent.ItemType<LoyaltyToken>(), TokensFor(amount)));
			if (player.whoAmI == Main.myPlayer && Club.CasinoStats.Local is { } stats) {
				stats.Wager(amount);
				stats.TokensEarned += TokensFor(amount);
			}
			return true;
		}

		/// <summary>Rolls for a jackpot item after a big win with the given stake.</summary>
		public static void TryJackpot(Player player, long stake, float chanceAtFullStake)
		{
			if (SubworldSystem.Current is not CasinoSubworld casino || stake < casino.MinJackpotStake || chanceAtFullStake <= 0f)
				return;

			int[] pool = casino.JackpotItems;
			if (pool.Length == 0)
				return;

			float chance = chanceAtFullStake * Math.Min(1f, stake / (float)casino.FullJackpotStake);
			if (Main.rand.NextFloat() >= chance)
				return;

			int itemType = pool[Main.rand.Next(pool.Length)];
			Coins.GiveItem(player, new Item(itemType));
			CombatText.NewText(player.Hitbox, new Color(255, 215, 60), Language.GetTextValue("Mods.CasinoWorld.Gambler.JackpotItem"), dramatic: true);
			SoundEngine.PlaySound(CasinoSounds.Jackpot, player.Center);
			SoundEngine.PlaySound(CasinoSounds.CrowdCheer, player.Center);
			for (int k = 0; k < 30; k++)
				Dust.NewDust(player.position, player.width, player.height, DustID.GoldCoin, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6f, -1f));
			Main.NewText(Language.GetTextValue("Mods.CasinoWorld.Gambler.JackpotMessage", Lang.GetItemNameValue(itemType)), new Color(255, 215, 60));
		}
	}
}
