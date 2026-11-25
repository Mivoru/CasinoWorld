using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasinoWorld.Common.Club;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Net;
using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Games
{
	/// <summary>
	/// Coin Duel: a player leaves a stake on the table, another matches it, the server tosses a coin and the
	/// winner takes both stakes (no fee, no loyalty tokens). Alone, the house tosses against you and pays 1.96.
	/// Stakes are taken on the player's own machine when offered, and come back there when withdrawn or on leaving.
	/// </summary>
	public class CoinDuelSystem : CasinoTableGameSystem
	{
		public const int TossTicks = 110;
		private const double HouseReturn = 1.96;

		public readonly record struct Offer(int Player, string Name, long Stake);

		// Host: open offers. Client: the last list received.
		public List<Offer> Offers { get; private set; } = new();

		// Client
		public int Chip;
		private long myStake, matchedStake;
		public int TossTimer = -1;
		public bool TossWon, TossHeads;
		public long TossPrize;
		public string TossRival = "";
		public string Message = "";
		public Color MessageColor = Color.White;

		public bool Offering => myStake > 0;
		public bool Tossing => TossTimer >= 0 && TossTimer < TossTicks;

		protected override int TableTileType => ModContent.TileType<CoinDuelTable>();

		protected override UIState CreateState()
		{
			var state = new UIState();
			var panel = new CoinDuelPanel();
			panel.Width.Set(CoinDuelPanel.PanelWidth, 0f);
			panel.Height.Set(CoinDuelPanel.PanelHeight, 0f);
			panel.HAlign = panel.VAlign = 0.5f;
			state.Append(panel);
			return state;
		}

		public static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.UI.Duel." + key, args);

		public static long[] Stakes => (Casino?.ChipValues ?? new long[] { 100 }).Append(VipClub.MaxBet).Distinct().ToArray();

		public long Stake => Stakes[Math.Clamp(Chip, 0, Stakes.Length - 1)];

		public override void ClearWorld()
		{
			Offers = new();
			myStake = matchedStake = 0;
			TossTimer = -1;
		}

		// ------------------------------------------------------------------ host

		private enum Request : byte { Sync, Offer, Withdraw, Match }

		public override void PostUpdateEverything()
		{
			if (Main.netMode == NetmodeID.Server && Offers.RemoveAll(o => !Main.player[o.Player].active) > 0)
				Broadcast(-1);
		}

		public void HandleRequest(BinaryReader reader, int sender)
		{
			var request = (Request)reader.ReadByte();
			int rival = reader.ReadByte();
			long stake = reader.ReadInt64();
			switch (request) {
				case Request.Sync:
					Broadcast(sender);
					break;
				case Request.Offer:
					Offers.RemoveAll(o => o.Player == sender);
					if (stake > 0)
						Offers.Add(new Offer(sender, Main.player[sender].name, stake));
					Broadcast(-1);
					break;
				case Request.Withdraw:
					// The stake goes back only if nobody matched it in the meantime.
					if (Offers.RemoveAll(o => o.Player == sender) > 0)
						SendRefused(sender, true);
					Broadcast(-1);
					break;
				case Request.Match:
					int index = Offers.FindIndex(o => o.Player == rival && o.Stake == stake && rival != sender);
					if (index < 0) {
						SendRefused(sender, false);
						Broadcast(sender);
						break;
					}
					Offers.RemoveAt(index);
					Broadcast(-1);
					ModPacket packet = Mod.GetPacket();
					packet.Write((byte)CasinoMessage.DuelResult);
					packet.Write((byte)rival);
					packet.Write((byte)sender);
					packet.Write(Main.rand.NextBool());      // heads: the one who offered wins
					packet.Write(stake);
					packet.Send();
					break;
			}
		}

		private void SendRefused(int player, bool withdrawn)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)CasinoMessage.DuelRefused);
			packet.Write(withdrawn);
			packet.Send(player);
		}

		private void Broadcast(int toClient)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)CasinoMessage.DuelOffers);
			packet.Write((byte)Offers.Count);
			foreach (Offer o in Offers) {
				packet.Write((byte)o.Player);
				packet.Write(o.Name);
				packet.Write(o.Stake);
			}
			packet.Send(toClient);
		}

		// ------------------------------------------------------------------ client

		private void Send(Request request, int rival = 0, long stake = 0)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				return;
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)CasinoMessage.DuelRequest);
			packet.Write((byte)request);
			packet.Write((byte)rival);
			packet.Write(stake);
			packet.Send();
		}

		public void Enter(Terraria.DataStructures.Point16 table)
		{
			Open(table);
			Message = Text(Main.netMode == NetmodeID.MultiplayerClient ? "Hint" : "HintSolo");
			MessageColor = Color.White;
			Send(Request.Sync);
		}

		public void ReceiveOffers(BinaryReader reader)
		{
			int count = reader.ReadByte();
			var list = new List<Offer>(count);
			for (int k = 0; k < count; k++)
				list.Add(new Offer(reader.ReadByte(), reader.ReadString(), reader.ReadInt64()));
			Offers = list;
		}

		public void ReceiveResult(BinaryReader reader)
		{
			int host = reader.ReadByte(), guest = reader.ReadByte();
			bool heads = reader.ReadBoolean();
			long stake = reader.ReadInt64();
			int winner = heads ? host : guest;
			if (Main.myPlayer == host)
				myStake = 0;
			else if (Main.myPlayer == guest)
				matchedStake = 0;
			else {
				// Spectators see it above the winner.
				if (CasinoTravel.InCasino)
					CombatText.NewText(Main.player[winner].Hitbox, CasinoDraw.Gold, "+" + Coins.Format(stake * 2), true);
				return;
			}
			bool won = winner == Main.myPlayer;
			if (won)
				Coins.Give(Main.LocalPlayer, stake * 2);      // paid at once; the toss only shows it
			StartToss(won, heads, won ? stake * 2 : 0, Main.player[Main.myPlayer == host ? guest : host].name);
			CasinoRecords.Report(Main.LocalPlayer, CasinoGame.Duel, won ? stake : -stake);
			CasinoStats.Local?.Duel(won);
		}

		public void Refused(bool withdrawn)
		{
			if (withdrawn) {
				Coins.Give(Main.LocalPlayer, myStake);
				myStake = 0;
				Message = Text("Withdrawn");
				MessageColor = Color.LightGray;
				return;
			}
			Coins.Give(Main.LocalPlayer, matchedStake);
			matchedStake = 0;
			Message = Text("Gone");
			MessageColor = Color.OrangeRed;
		}

		private void StartToss(bool won, bool heads, long prize, string rival)
		{
			TossWon = won;
			TossHeads = heads;
			TossPrize = prize;
			TossRival = rival;
			TossTimer = 0;
			Message = Text("Versus", rival);
			MessageColor = Color.White;
			SoundEngine.PlaySound(CasinoSounds.DuelToss);
		}

		private bool CanPay(long stake)
		{
			if (!CasinoConfig.Instance.GamblingEnabled || !Coins.TryTake(Main.LocalPlayer, stake)) {
				Message = Language.GetTextValue(CasinoConfig.Instance.GamblingEnabled ? "Mods.CasinoWorld.UI.Slots.NotEnough" : "Mods.CasinoWorld.UI.GamblingDisabled");
				MessageColor = Color.OrangeRed;
				return false;
			}
			return true;
		}

		public void OfferOrWithdraw()
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || Tossing)
				return;
			if (Offering) {
				Send(Request.Withdraw);
				return;
			}
			if (!CanPay(Stake))
				return;
			myStake = Stake;
			Send(Request.Offer, 0, myStake);
			Message = Text("Offered", Coins.Format(myStake));
			MessageColor = CasinoDraw.Gold;
			SoundEngine.PlaySound(CasinoSounds.ChipStack);
		}

		/// <summary>Leaving the world: the stake comes along (the server drops offers of players who are gone).</summary>
		private void TakeBack()
		{
			if (myStake <= 0)
				return;
			Coins.Give(Main.LocalPlayer, myStake);
			myStake = 0;
			Send(Request.Withdraw);
		}

		public void Match(Offer offer)
		{
			if (Tossing || matchedStake > 0 || offer.Player == Main.myPlayer || !CanPay(offer.Stake))
				return;
			matchedStake = offer.Stake;
			CasinoStats.Local?.Wager(offer.Stake, false);
			Send(Request.Match, offer.Player, offer.Stake);
		}

		public void HouseToss()
		{
			if (Tossing)
				return;
			long stake = Stake;
			if (!CasinoRewards.TryWager(Main.LocalPlayer, stake)) {
				Message = Language.GetTextValue("Mods.CasinoWorld.UI.Slots.NotEnough");
				MessageColor = Color.OrangeRed;
				return;
			}
			bool won = Main.rand.NextBool();
			long prize = won ? (long)(stake * HouseReturn) : 0;
			Coins.Give(Main.LocalPlayer, prize);
			CasinoRecords.Report(Main.LocalPlayer, CasinoGame.Duel, prize - stake);
			CasinoStats.Local?.Duel(won);
			StartToss(won, won, prize, Text("House"));
		}

		protected override void UpdateGame(Player player)
		{
			if (TossTimer < 0)
				return;
			TossTimer++;
			if (TossTimer == TossTicks) {
				Message = Text(TossWon ? "Won" : "Lost", TossRival, Coins.Format(TossPrize));
				MessageColor = TossWon ? Color.LightGreen : Color.LightGray;
				SoundEngine.PlaySound(TossWon ? CasinoSounds.SmallWin : CasinoSounds.HiLoLose);
				if (TossWon) {
					UiFx.Win(TossPrize, TossPrize / 2);
					CombatText.NewText(player.Hitbox, CasinoDraw.Gold, "+" + Coins.Format(TossPrize), true);
				}
			}
		}

		// Leaving the casino (or the game) takes an open offer along.
		public override void PreSaveAndQuit()
		{
			TakeBack();
			base.PreSaveAndQuit();
		}

		public override void OnWorldUnload()
		{
			if (!Main.dedServ)
				TakeBack();
			base.OnWorldUnload();
		}
	}

	public class CoinDuelPanel : UIElement
	{
		public const int PanelWidth = 640, PanelHeight = 460;

		private static CoinDuelSystem S => ModContent.GetInstance<CoinDuelSystem>();
		private static Asset<Texture2D> coinTex;

		private Rectangle Area => GetDimensions().ToRectangle();
		private Rectangle CloseButton => new(Area.Right - 34, Area.Y + 12, 22, 22);
		private Rectangle OfferButton => new(Area.X + 30, Area.Bottom - 64, 260, 42);
		private Rectangle HouseButton => new(Area.Right - 290, Area.Bottom - 64, 260, 42);
		private Rectangle ChipRect(int k) => new(Area.X + 30 + k * 66, Area.Bottom - 122, 60, 46);
		private Rectangle Row(int k) => new(Area.X + 280, Area.Y + 92 + k * 42, Area.Width - 310, 36);

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			Point m = evt.MousePosition.ToPoint();
			if (CloseButton.Contains(m))
				S.Close();
			else if (OfferButton.Contains(m))
				S.OfferOrWithdraw();
			else if (HouseButton.Contains(m))
				S.HouseToss();
			else {
				for (int k = 0; k < CoinDuelSystem.Stakes.Length; k++)
					if (ChipRect(k).Contains(m) && !S.Offering) {
						S.Chip = k;
						SoundEngine.PlaySound(SoundID.MenuTick);
						return;
					}
				List<CoinDuelSystem.Offer> offers = Others();
				for (int k = 0; k < Math.Min(4, offers.Count); k++)
					if (Row(k).Contains(m))
						S.Match(offers[k]);
			}
		}

		private static List<CoinDuelSystem.Offer> Others() => S.Offers.Where(o => o.Player != Main.myPlayer).ToList();

		protected override void DrawSelf(SpriteBatch sb)
		{
			Rectangle a = Area;
			Point m = CasinoTableGameSystem.UIMouse;
			float t = Main.GlobalTimeWrappedHourly;
			CasinoDraw.Panel(sb, a);
			Utils.DrawBorderString(sb, CoinDuelSystem.Text("Title"), new Vector2(a.X + 20, a.Y + 14), CasinoDraw.Gold, 1.1f);
			CasinoDraw.Fit(sb, CoinDuelSystem.Text("Rules"), new Vector2(a.Center.X, a.Y + 52), Color.LightGray, 0.7f, a.Width - 50, 0.5f, 0.5f);

			// The coin: rises, turns and lands.
			coinTex ??= ModContent.Request<Texture2D>("CasinoWorld/Content/Projectiles/Boss/GiantCoin", AssetRequestMode.ImmediateLoad);
			Vector2 spot = new(a.X + 145, a.Y + 220);
			float p = S.TossTimer < 0 ? 1f : Math.Min(1f, S.TossTimer / (float)CoinDuelSystem.TossTicks);
			float rise = (float)Math.Sin(p * MathHelper.Pi) * 110f;
			int face = p < 1f ? new[] { 0, 1, 2, 1 }[(int)(p * p * 10 + S.TossTimer * 0.3f) % 4] : S.TossHeads || S.TossTimer < 0 ? 0 : 2;
			CasinoDraw.Fill(sb, new Rectangle((int)(spot.X - 34 + rise * 0.12f), (int)spot.Y + 42, (int)(68 - rise * 0.24f), 6), Color.Black * 0.35f);
			if (S.TossTimer >= CoinDuelSystem.TossTicks && S.TossWon)
				PixelFx.Burst(sb, spot, CasinoDraw.Gold * 0.6f, t, 2);
			Rectangle frame = coinTex.Value.Frame(1, 3, 0, face);
			sb.Draw(coinTex.Value, PixelFx.Snap(spot - new Vector2(0, rise)), frame, Color.White, 0f, frame.Size() / 2f, 1.5f, SpriteEffects.None, 0f);

			// Open offers.
			List<CoinDuelSystem.Offer> offers = Others();
			Utils.DrawBorderString(sb, CoinDuelSystem.Text("Offers"), new Vector2(a.X + 280, a.Y + 70), CasinoDraw.Gold, 0.85f);
			for (int k = 0; k < Math.Min(4, offers.Count); k++) {
				Rectangle r = Row(k);
				bool hover = r.Contains(m) && !S.Tossing;
				CasinoDraw.Fill(sb, r, hover ? CasinoDraw.Gold : CasinoDraw.Gold * 0.5f);
				CasinoDraw.Fill(sb, CasinoDraw.Shrink(r, 2), hover ? CasinoDraw.Felt : CasinoDraw.FeltDark);
				CasinoDraw.Fit(sb, offers[k].Name, new Vector2(r.X + 10, r.Center.Y + 3), Color.White, 0.85f, 120, 0f, 0.5f);
				CasinoDraw.Fit(sb, CoinDuelSystem.Text("Accept", Coins.Format(offers[k].Stake)), new Vector2(r.Right - 10, r.Center.Y + 3), Color.LightGreen, 0.8f, 180, 1f, 0.5f);
			}
			if (offers.Count == 0)
				CasinoDraw.Fit(sb, CoinDuelSystem.Text("NoOffers"), new Vector2(a.X + 280, a.Y + 104), Color.LightGray * 0.8f, 0.75f, a.Width - 310);

			CasinoDraw.Fit(sb, S.Message, new Vector2(a.Center.X, a.Bottom - 146), S.MessageColor, 0.9f, a.Width - 60, 0.5f, 0.5f);
			long[] stakes = CoinDuelSystem.Stakes;
			for (int k = 0; k < stakes.Length; k++)
				CasinoDraw.ChipButton(sb, ChipRect(k), Coins.Format(stakes[k]), k, ChipRect(k).Contains(m), k == S.Chip);
			CasinoDraw.Fit(sb, Language.GetTextValue("Mods.CasinoWorld.UI.Poker.Balance", Coins.Format(Coins.Count(Main.LocalPlayer))), new Vector2(a.Right - 30, a.Bottom - 100), Color.White * 0.8f, 0.75f, 220, 1f, 0.5f);
			bool multiplayer = Main.netMode == NetmodeID.MultiplayerClient;
			CasinoDraw.Button(sb, OfferButton, CoinDuelSystem.Text(S.Offering ? "Withdraw" : "Offer", Coins.Format(S.Stake)), S.Offering ? CasinoDraw.Red : CasinoDraw.FeltDark, OfferButton.Contains(m), multiplayer && !S.Tossing, 0.85f);
			CasinoDraw.Button(sb, HouseButton, CoinDuelSystem.Text("TossHouse", Coins.Format(S.Stake)), CasinoDraw.ButtonGold, HouseButton.Contains(m), !S.Tossing, 0.85f);
			CasinoDraw.Button(sb, CloseButton, "X", CasinoDraw.Red, CloseButton.Contains(m), true, 0.8f);
		}
	}
}
