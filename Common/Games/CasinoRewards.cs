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
	/// Loyalty tokens: one per round played (win or lose), plus one more when the stake reaches the
	/// casino's FullJackpotStake. About 15-30 tokens a minute: a Lucky Crate (75) every 3-5 minutes,
	/// the Gamblorghini (200) in 7-13 minutes. (Tokens used to scale with the money staked, which gave
	/// 1000 tokens per hardmode spin.) Doubling down or splitting in blackjack counts as another round.
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

		/// <summary>Takes a wager and credits loyalty token progress. False if the player cannot pay.</summary>
		public static bool TryWager(Player player, long amount)
		{
			if (!Coins.TryTake(player, amount))
				return false;

			int tokens = 1;
			if (SubworldSystem.Current is CasinoSubworld casino && amount >= casino.FullJackpotStake)
				tokens++;
			Coins.GiveItem(player, new Item(ModContent.ItemType<LoyaltyToken>(), tokens));
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
