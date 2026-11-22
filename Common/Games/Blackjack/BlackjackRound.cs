using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;

namespace CasinoWorld.Common.Games.Blackjack
{
	public enum BlackjackPhase
	{
		Betting,
		Dealing,     // cards are being dealt, no input
		PlayerTurn,
		DealerTurn,
		Result,
	}

	public class BlackjackHand
	{
		public readonly List<int> Cards = new();
		public long Bet;
		public bool Done;
		public bool Doubled;
		public bool FromSplit;
		public long Payout = -1; // set when the round is settled

		public int Value => BlackjackRules.Value(Cards);
		public bool Busted => Value > 21;
		public bool IsBlackjack => !FromSplit && BlackjackRules.IsBlackjack(Cards);
	}

	/// <summary>
	/// Local player's blackjack round against the house. Runs on the client: the cards
	/// affect nobody else, and coins live in the client's inventory anyway.
	/// Card deals are queued with a short delay so the round reads like a real table.
	/// </summary>
	public class BlackjackRound
	{
		private const int DealDelay = 20;
		private const int ReshuffleBelow = 52 * BlackjackRules.Decks / 4;

		private readonly List<int> shoe = new();
		private readonly Queue<Action> steps = new();
		private int stepTimer;

		public readonly List<BlackjackHand> Hands = new();
		public readonly List<int> Dealer = new();
		public bool HoleCardHidden { get; private set; }
		public int ActiveHand { get; private set; }
		public long PendingBet { get; private set; }
		private long lastBet;

		public BlackjackPhase Phase { get; private set; } = BlackjackPhase.Betting;
		public string Message { get; private set; } = "";
		public Color MessageColor { get; private set; } = Color.White;

		public bool CanBet => Phase is BlackjackPhase.Betting or BlackjackPhase.Result;

		private BlackjackHand Current => ActiveHand < Hands.Count ? Hands[ActiveHand] : null;

		public bool CanHit => Phase == BlackjackPhase.PlayerTurn && Current is { Done: false };

		public bool CanDouble => CanHit && Current.Cards.Count == 2;

		public bool CanSplit => CanHit && Hands.Count == 1 && Current.Cards.Count == 2
			&& BlackjackRules.Points(Current.Cards[0]) == BlackjackRules.Points(Current.Cards[1]);

		public void AddChip(long chip, long maxBet)
		{
			if (!CanBet)
				return;
			if (PendingBet + chip > maxBet) {
				SetMessage(Text("OverMax", Coins.Format(maxBet)), Color.OrangeRed);
				return;
			}
			PendingBet += chip;
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		public void ClearBet()
		{
			if (CanBet)
				PendingBet = 0;
		}

		public void Deal(Player player, long maxBet)
		{
			if (!CanBet)
				return;
			if (PendingBet <= 0)
				PendingBet = Math.Min(lastBet, maxBet);
			if (PendingBet <= 0) {
				SetMessage(Text("NoBet"), Color.OrangeRed);
				return;
			}
			if (!Coins.TryTake(player, PendingBet)) {
				SetMessage(Text("NotEnough"), Color.OrangeRed);
				return;
			}

			if (shoe.Count < ReshuffleBelow) {
				Shuffle();
				SetMessage(Text("Shuffle"), Color.Gold);
			}
			else {
				SetMessage("", Color.White);
			}

			SoundEngine.PlaySound(SoundID.Coins);
			Hands.Clear();
			Dealer.Clear();
			var hand = new BlackjackHand { Bet = PendingBet };
			Hands.Add(hand);
			lastBet = PendingBet;
			PendingBet = 0;
			ActiveHand = 0;
			HoleCardHidden = true;
			Phase = BlackjackPhase.Dealing;

			Queue(() => Draw(hand.Cards));
			Queue(() => Draw(Dealer));
			Queue(() => Draw(hand.Cards));
			Queue(() => Draw(Dealer));
			Queue(AfterInitialDeal);
		}

		private void AfterInitialDeal()
		{
			BlackjackHand hand = Hands[0];
			bool dealerBlackjack = BlackjackRules.IsBlackjack(Dealer);

			// Dealer peeks for blackjack; a natural on either side ends the round at once.
			if (dealerBlackjack || hand.IsBlackjack) {
				HoleCardHidden = false;
				hand.Done = true;
				Settle();
				return;
			}

			Phase = BlackjackPhase.PlayerTurn;
		}

		public void Hit()
		{
			if (!CanHit)
				return;

			BlackjackHand hand = Current;
			Draw(hand.Cards);
			if (hand.Value >= 21)
				FinishHand();
		}

		public void Stand()
		{
			if (CanHit)
				FinishHand();
		}

		public void Double(Player player)
		{
			if (!CanDouble)
				return;
			if (!Coins.TryTake(player, Current.Bet)) {
				SetMessage(Text("NotEnough"), Color.OrangeRed);
				return;
			}

			SoundEngine.PlaySound(SoundID.Coins);
			Current.Bet *= 2;
			Current.Doubled = true;
			Draw(Current.Cards);
			FinishHand();
		}

		public void Split(Player player)
		{
			if (!CanSplit)
				return;
			if (!Coins.TryTake(player, Current.Bet)) {
				SetMessage(Text("NotEnough"), Color.OrangeRed);
				return;
			}

			SoundEngine.PlaySound(SoundID.Coins);
			BlackjackHand first = Current;
			var second = new BlackjackHand { Bet = first.Bet, FromSplit = true };
			first.FromSplit = true;
			second.Cards.Add(first.Cards[1]);
			first.Cards.RemoveAt(1);
			Hands.Add(second);

			bool aces = BlackjackRules.Rank(first.Cards[0]) == 0;
			Phase = BlackjackPhase.Dealing;
			Queue(() => Draw(first.Cards));
			Queue(() => Draw(second.Cards));
			Queue(() => {
				if (aces) {
					// Split aces receive one card each and stand.
					first.Done = second.Done = true;
					StartDealerTurn();
				}
				else {
					Phase = BlackjackPhase.PlayerTurn;
					if (first.Value == 21)
						FinishHand();
				}
			});
		}

		private void FinishHand()
		{
			Current.Done = true;
			ActiveHand++;
			if (ActiveHand < Hands.Count) {
				if (Hands[ActiveHand].Value == 21)
					FinishHand();
				return;
			}
			StartDealerTurn();
		}

		private void StartDealerTurn()
		{
			Phase = BlackjackPhase.DealerTurn;
			Queue(() => {
				HoleCardHidden = false;
				SoundEngine.PlaySound(SoundID.Grab);
			});

			// The dealer only draws if some hand is still alive.
			if (Hands.All(h => h.Busted)) {
				Queue(Settle);
				return;
			}
			Queue(DealerStep);
		}

		private void DealerStep()
		{
			if (BlackjackRules.Value(Dealer) < BlackjackRules.DealerStandsOn) {
				Draw(Dealer);
				Queue(DealerStep);
			}
			else {
				Settle();
			}
		}

		private void Settle()
		{
			Player player = Main.LocalPlayer;
			int dealer = BlackjackRules.Value(Dealer);
			bool dealerBlackjack = BlackjackRules.IsBlackjack(Dealer);
			bool dealerBust = dealer > 21;
			long total = 0, staked = 0;

			foreach (BlackjackHand hand in Hands) {
				staked += hand.Bet;
				int value = hand.Value;
				if (hand.IsBlackjack && !dealerBlackjack)
					hand.Payout = hand.Bet + hand.Bet * 3 / 2;
				else if (hand.Busted || dealerBlackjack && !hand.IsBlackjack)
					hand.Payout = 0;
				else if (dealerBust || value > dealer)
					hand.Payout = hand.Bet * 2;
				else if (value == dealer)
					hand.Payout = hand.Bet;
				else
					hand.Payout = 0;
				total += hand.Payout;
			}

			Coins.Give(player, total);
			Phase = BlackjackPhase.Result;

			if (total > staked) {
				bool natural = Hands.Count == 1 && Hands[0].IsBlackjack;
				SetMessage(Text(natural ? "Blackjack" : "Win", Coins.Format(total)), Color.LightGreen);
				SoundEngine.PlaySound(SoundID.CoinPickup);
				CombatText.NewText(player.Hitbox, Color.Gold, "+" + Coins.Format(total), dramatic: natural);
			}
			else if (total == staked) {
				SetMessage(Text("Push"), Color.LightGray);
			}
			else if (total > 0) {
				SetMessage(Text("PartWin", Coins.Format(total)), Color.LightGray);
			}
			else {
				SetMessage(Text(dealerBlackjack ? "DealerBlackjack" : "Lose"), Color.LightGray);
			}
		}

		/// <summary>Advances queued deals. Call every frame on the client.</summary>
		public void Update()
		{
			if (steps.Count == 0)
				return;
			if (--stepTimer > 0)
				return;

			stepTimer = DealDelay;
			steps.Dequeue()();
		}

		/// <summary>
		/// Called when the window closes mid-round: every open hand stands and the round
		/// is played out at once, so the bet is never lost to a closed window.
		/// </summary>
		public void FinishImmediately()
		{
			if (Phase is BlackjackPhase.Betting or BlackjackPhase.Result)
				return;

			for (int guard = 0; guard < 100 && Phase != BlackjackPhase.Result; guard++) {
				while (steps.Count > 0)
					steps.Dequeue()();
				if (Phase == BlackjackPhase.PlayerTurn)
					FinishHand();
			}
		}

		private void Queue(Action step)
		{
			if (steps.Count == 0)
				stepTimer = DealDelay;
			steps.Enqueue(step);
		}

		private void Draw(List<int> into)
		{
			if (shoe.Count == 0)
				Shuffle();
			into.Add(shoe[^1]);
			shoe.RemoveAt(shoe.Count - 1);
			SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f, Pitch = 0.4f });
		}

		private void Shuffle()
		{
			shoe.Clear();
			for (int d = 0; d < BlackjackRules.Decks; d++) {
				for (int c = 0; c < 52; c++)
					shoe.Add(c);
			}
			for (int k = shoe.Count - 1; k > 0; k--) {
				int j = Main.rand.Next(k + 1);
				(shoe[k], shoe[j]) = (shoe[j], shoe[k]);
			}
		}

		private static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.UI.Blackjack." + key, args);

		private void SetMessage(string text, Color color)
		{
			Message = text;
			MessageColor = color;
		}
	}
}
