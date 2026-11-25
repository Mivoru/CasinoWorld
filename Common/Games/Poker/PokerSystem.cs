using System;
using System.Collections.Generic;
using System.IO;
using CasinoWorld.Common.Club;
using CasinoWorld.Common.Net;
using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Games.Poker
{
	/// <summary>What one player may see of the table; built by the host for each player.</summary>
	public class PokerView
	{
		public class SeatView
		{
			public bool Bot, Folded, AllIn;
			public string Name = "", Last = "";
			public long Stack, Bet, Net;
			public int CardA = -1, CardB = -1, Value;
		}

		public PokerStreet Street;
		public readonly int[] Board = { -1, -1, -1, -1, -1 };
		public int Button, Turn = -1, Raises, Timer, HandId, Me = -1;
		public long Pot, SmallBet, HighBet;
		public readonly SeatView[] Seat = new SeatView[PokerTable.Seats];

		public PokerView()
		{
			for (int k = 0; k < Seat.Length; k++)
				Seat[k] = new SeatView();
		}

		public SeatView Mine => Me >= 0 ? Seat[Me] : null;
		public bool MyTurn => Me >= 0 && Turn == Me;
		public long ToCall => Mine == null ? 0 : Math.Min(Mine.Stack, HighBet - Mine.Bet);
		public bool CanRaise => Mine != null && Raises < PokerTable.MaxRaises && Mine.Stack > HighBet - Mine.Bet;
		public long StreetBet => Street is PokerStreet.Turn or PokerStreet.River ? SmallBet * 2 : SmallBet;
		public long BuyIn => SmallBet * 2 * PokerTable.BuyInBigBets;

		public static void Write(BinaryWriter w, PokerTable t, int player)
		{
			int me = t.IndexOf(player);
			w.Write((byte)t.Street);
			foreach (int card in t.Board)
				w.Write((sbyte)card);
			w.Write((sbyte)t.Button);
			w.Write((sbyte)t.Turn);
			w.Write((byte)t.Raises);
			w.Write((short)Math.Clamp(t.Timer, 0, short.MaxValue));
			w.Write(t.HandId);
			w.Write((sbyte)me);
			w.Write(t.Pot);
			w.Write(t.SmallBet);
			w.Write(t.HighBet);
			for (int k = 0; k < PokerTable.Seats; k++) {
				PokerSeat s = t.Seat[k];
				bool open = k == me || t.Shown(s);
				w.Write(s.Bot);
				w.Write(s.Folded);
				w.Write(s.AllIn);
				w.Write(s.Name);
				w.Write(s.Last);
				w.Write(s.Stack);
				w.Write(s.Bet);
				w.Write(s.Net);
				w.Write((sbyte)(open ? s.Hole[0] : -1));
				w.Write((sbyte)(open ? s.Hole[1] : -1));
				w.Write(open && t.Street == PokerStreet.Showdown ? s.Value : 0);
			}
		}

		public static PokerView Read(BinaryReader r)
		{
			var v = new PokerView { Street = (PokerStreet)r.ReadByte() };
			for (int k = 0; k < 5; k++)
				v.Board[k] = r.ReadSByte();
			v.Button = r.ReadSByte();
			v.Turn = r.ReadSByte();
			v.Raises = r.ReadByte();
			v.Timer = r.ReadInt16();
			v.HandId = r.ReadInt32();
			v.Me = r.ReadSByte();
			v.Pot = r.ReadInt64();
			v.SmallBet = r.ReadInt64();
			v.HighBet = r.ReadInt64();
			foreach (SeatView s in v.Seat) {
				s.Bot = r.ReadBoolean();
				s.Folded = r.ReadBoolean();
				s.AllIn = r.ReadBoolean();
				s.Name = r.ReadString();
				s.Last = r.ReadString();
				s.Stack = r.ReadInt64();
				s.Bet = r.ReadInt64();
				s.Net = r.ReadInt64();
				s.CardA = r.ReadSByte();
				s.CardB = r.ReadSByte();
				s.Value = r.ReadInt32();
			}
			return v;
		}
	}

	/// <summary>
	/// The casino's Hold'em table. The host (server or single player) runs <see cref="PokerTable"/> and sends every
	/// player their own view; clients send actions. Money: a player pays the buy-in when sitting down and takes
	/// the last known stack when leaving, on their own machine, so quitting in any way never loses it.
	/// </summary>
	public class PokerSystem : CasinoTableGameSystem
	{
		// Host
		private PokerTable host;
		private readonly HashSet<int> watchers = new();

		// Client
		public PokerView View { get; private set; }
		public string Message = "";
		public Color MessageColor = Color.White;
		private long pendingCost, pendingBuyIn;
		private int reportedHand;
		public uint ViewAt { get; private set; }
		private bool settled = true;

		public bool Seated => View?.Me >= 0;

		protected override int TableTileType => ModContent.TileType<PokerTableTile>();

		protected override UIState CreateState()
		{
			var state = new UIState();
			var panel = new PokerPanel();
			panel.Width.Set(PokerPanel.PanelWidth, 0f);
			panel.Height.Set(PokerPanel.PanelHeight, 0f);
			panel.HAlign = panel.VAlign = 0.5f;
			state.Append(panel);
			return state;
		}

		public static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.UI.Poker." + key, args);

		public override void ClearWorld()
		{
			host = null;
			watchers.Clear();
			View = null;
			pendingCost = pendingBuyIn = 0;
			settled = true;
		}

		// ------------------------------------------------------------------ host

		public override void PostUpdateEverything()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || Casino == null || host == null)
				return;
			if (Main.netMode == NetmodeID.Server) {
				watchers.RemoveWhere(w => !Main.player[w].active);
				foreach (PokerSeat s in host.Seat)
					if (!s.Bot && !Main.player[s.Player].active)
						host.Leave(s.Player);
			}
			if (watchers.Count == 0 && !host.HasHumans)
				return;
			host.Update();
			if (!host.Dirty)
				return;
			host.Dirty = false;
			foreach (int player in watchers)
				SendView(player);
		}

		private void SendView(int player)
		{
			if (Main.netMode == NetmodeID.Server) {
				ModPacket packet = Mod.GetPacket();
				packet.Write((byte)CasinoMessage.PokerView);
				PokerView.Write(packet, host, player);
				packet.Send(player);
				return;
			}
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream);
			PokerView.Write(writer, host, player);
			stream.Position = 0;
			using var reader = new BinaryReader(stream);
			Receive(PokerView.Read(reader));
		}

		private enum Request : byte { Watch, Sit, Act, Leave }

		/// <summary>Host: a player's request.</summary>
		private void Handle(int player, Request request, long amount)
		{
			if (Casino == null)
				return;
			host ??= new PokerTable(new Random(Main.rand.Next())) { SmallBet = Casino.ChipValues[1], Dirty = true };
			switch (request) {
				case Request.Watch:
					watchers.Add(player);
					host.Dirty = true;
					break;
				case Request.Sit:
					watchers.Add(player);
					if (amount <= 0 || amount > host.BuyIn || !host.Sit(player, Main.player[player].name, amount))
						Refuse(player, amount);
					break;
				case Request.Act:
					host.Act(player, (PokerAction)Math.Clamp(amount, 0, 2));
					break;
				case Request.Leave:
					watchers.Remove(player);
					host.Leave(player);
					break;
			}
		}

		public void HandleRequest(BinaryReader reader, int sender)
		{
			var request = (Request)reader.ReadByte();
			Handle(sender, request, reader.ReadInt64());
		}

		private void Refuse(int player, long amount)
		{
			if (Main.netMode == NetmodeID.Server) {
				ModPacket packet = Mod.GetPacket();
				packet.Write((byte)CasinoMessage.PokerRefused);
				packet.Write(amount);
				packet.Send(player);
			}
			else
				Refused(amount);
		}

		// ------------------------------------------------------------------ client

		private void Send(Request request, long amount = 0)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				ModPacket packet = Mod.GetPacket();
				packet.Write((byte)CasinoMessage.PokerRequest);
				packet.Write((byte)request);
				packet.Write(amount);
				packet.Send();
			}
			else
				Handle(Main.myPlayer, request, amount);
		}

		/// <summary>Called by the table tile.</summary>
		public void Enter(Terraria.DataStructures.Point16 table)
		{
			Open(table);
			if (!IsOpen)
				return;
			settled = false;
			Message = Text("Hint");
			MessageColor = Color.White;
			Send(Request.Watch);
		}

		public void Refused(long amount)
		{
			if (pendingBuyIn <= 0)
				return;
			Coins.Give(Main.LocalPlayer, Math.Min(amount, pendingBuyIn));
			pendingBuyIn = 0;
			Message = Text("Full");
			MessageColor = Color.OrangeRed;
		}

		public void Receive(PokerView view)
		{
			PokerView old = View;
			View = view;
			ViewAt = Main.GameUpdateCount;
			pendingCost = 0;
			if (view.Me >= 0)
				pendingBuyIn = 0;
			if (!IsOpen)
				return;
			Sounds(old, view);
			if (view.Street == PokerStreet.Showdown && view.Me >= 0 && view.HandId != reportedHand && (view.Mine.Net != 0 || !view.Mine.Folded)) {
				reportedHand = view.HandId;
				PokerView.SeatView me = view.Mine;
				CasinoRecords.Report(Main.LocalPlayer, CasinoGame.Poker, me.Net);
				CasinoStats.Local?.PokerHand(me.Net, me.Value > 0 ? PokerHands.Category(me.Value) : -1);
				if (me.Net > 0) {
					Message = Text("Won", Coins.Format(me.Net));
					MessageColor = Color.LightGreen;
					UiFx.Win(me.Net, view.SmallBet * 2);
					SoundEngine.PlaySound(CasinoSounds.CoinsPayout);
				}
				else {
					Message = Text("Lost", Coins.Format(-me.Net));
					MessageColor = Color.LightGray;
				}
			}
			else if (view.MyTurn && (old == null || !old.MyTurn || old.Street != view.Street)) {
				Message = Text("YourTurn");
				MessageColor = CasinoDraw.Gold;
				SoundEngine.PlaySound(CasinoSounds.ElevatorDing with { Volume = 0.35f, Pitch = 0.4f });
			}
			else if (view.HandId != (old?.HandId ?? 0))
				Message = "";
		}

		private static void Sounds(PokerView old, PokerView now)
		{
			if (old == null)
				return;
			if (now.HandId != old.HandId)
				SoundEngine.PlaySound(CasinoSounds.CardShuffle with { Volume = 0.5f });
			else if (now.Street != old.Street && now.Street is PokerStreet.Flop or PokerStreet.Turn or PokerStreet.River)
				SoundEngine.PlaySound(CasinoSounds.CardFlip);
			for (int k = 0; k < PokerTable.Seats; k++) {
				string last = now.Seat[k].Last;
				if (last == old.Seat[k].Last && now.Seat[k].Bet == old.Seat[k].Bet)
					continue;
				if (last == "Fold")
					SoundEngine.PlaySound(CasinoSounds.PokerFold);
				else if (last == "Check")
					SoundEngine.PlaySound(CasinoSounds.PokerCheck);
				else if (now.Seat[k].Bet > old.Seat[k].Bet)
					SoundEngine.PlaySound(CasinoSounds.ChipPlace);
			}
		}

		/// <summary>Sits down, or adds chips when the stack is gone.</summary>
		public void BuyIn()
		{
			if (View == null || pendingBuyIn > 0 || Seated && View.Mine.Stack > 0)
				return;
			if (!CasinoConfig.Instance.GamblingEnabled || !Coins.TryTake(Main.LocalPlayer, View.BuyIn)) {
				Message = Language.GetTextValue(CasinoConfig.Instance.GamblingEnabled ? "Mods.CasinoWorld.UI.Slots.NotEnough" : "Mods.CasinoWorld.UI.GamblingDisabled");
				MessageColor = Color.OrangeRed;
				return;
			}
			pendingBuyIn = View.BuyIn;
			SoundEngine.PlaySound(CasinoSounds.ChipStack);
			Send(Request.Sit, pendingBuyIn);
		}

		public void Act(PokerAction action)
		{
			if (View is not { MyTurn: true } || pendingCost > 0)
				return;
			long cost = action == PokerAction.Fold ? 0 : View.ToCall + (action == PokerAction.Raise ? View.StreetBet : 0);
			pendingCost = Math.Min(cost, View.Mine.Stack);
			CasinoStats.Local?.Wager(pendingCost, false);
			Send(Request.Act, (long)action);
		}

		/// <summary>The stack in front of the player and a buy-in on its way; chips in the pot of a running hand are lost.</summary>
		public override long Owed
		{
			get {
				if (settled)
					return 0;
				PokerView view = View;
				return pendingBuyIn + (view is { Me: >= 0 } ? Math.Max(0, view.Mine.Stack - pendingCost) : 0);
			}
		}

		protected override void OnClose(Player player) => Settle(player);

		/// <summary>Leaves the table with whatever is in front of the player (a running hand is folded).</summary>
		private void Settle(Player player)
		{
			if (settled)
				return;
			settled = true;
			long cash = pendingBuyIn;
			if (Seated)
				cash += Math.Max(0, View.Mine.Stack - pendingCost);
			pendingBuyIn = pendingCost = 0;
			View = null;
			Coins.Give(player, cash);
			if (cash > 0)
				SoundEngine.PlaySound(CasinoSounds.CashRegister);
			Send(Request.Leave);
		}
	}
}
