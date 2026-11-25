using System.Collections.Generic;
using System.IO;
using CasinoWorld.Common.Net;
using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Subworlds;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Games.Roulette
{
	/// <summary>
	/// Runs the local roulette round and handles its network messages.
	/// In multiplayer the server rolls the number, so every player at the table sees the same result.
	/// </summary>
	public class RouletteSystem : CasinoTableGameSystem
	{
		public RouletteRound Round { get; } = new();

		// Results of other players, shown above their table once their spin animation is over.
		private readonly List<(Point16 table, int number, int delay)> spectatorResults = new();

		protected override int TableTileType => ModContent.TileType<RouletteTable>();

		protected override UIState CreateState() => new RouletteUIState();

		public override void PreSaveAndQuit()
		{
			base.PreSaveAndQuit();
			Round.CancelWaiting(Main.LocalPlayer);
		}

		public override void OnWorldUnload()
		{
			base.OnWorldUnload();
			Round.CancelWaiting(Main.LocalPlayer);
			spectatorResults.Clear();
		}

		protected override void OnClose(Player player) => Round.FinishImmediately(player);

		protected override void UpdateGame(Player player)
		{
			Round.Update(player);
			UpdateSpectators();
		}

		/// <summary>Starts a spin: takes the coins and asks for the result (from the server in multiplayer).</summary>
		public void Spin()
		{
			CasinoSubworld casino = Casino;
			if (casino == null || !Round.TrySpin(Main.LocalPlayer, Club.VipClub.MaxBet))
				return;

			if (Main.netMode == NetmodeID.SinglePlayer)
				Round.ReceiveResult(Main.rand.Next(RouletteRules.Pockets));
			else
				CasinoNet.SendRouletteSpinRequest(Mod, Table);
		}

		/// <summary>Server side: rolls a number for a player and tells every client.</summary>
		internal void HandleSpinRequest(BinaryReader reader, int sender)
		{
			var table = new Point16(reader.ReadInt16(), reader.ReadInt16());
			int number = Main.rand.Next(RouletteRules.Pockets);
			CasinoNet.SendRouletteSpinResult(Mod, sender, table, number);
		}

		/// <summary>Client side: a result arrived for some player.</summary>
		internal void HandleSpinResult(BinaryReader reader)
		{
			int player = reader.ReadByte();
			var table = new Point16(reader.ReadInt16(), reader.ReadInt16());
			int number = reader.ReadByte();

			if (player == Main.myPlayer)
				Round.ReceiveResult(number);
			else
				spectatorResults.Add((table, number, RouletteRound.SpinTicks));
		}

		private void UpdateSpectators()
		{
			for (int k = spectatorResults.Count - 1; k >= 0; k--) {
				(Point16 table, int number, int delay) = spectatorResults[k];
				if (--delay > 0) {
					spectatorResults[k] = (table, number, delay);
					continue;
				}

				spectatorResults.RemoveAt(k);
				if (!CasinoTravel.InCasino)
					continue;

				Color color = number == 0 ? Color.LimeGreen : RouletteRules.IsRed(number) ? Color.Red : Color.White;
				var area = new Rectangle(table.X * 16, table.Y * 16 - 16, CasinoTable.Width * 16, 16);
				CombatText.NewText(area, color, number.ToString());
			}
		}
	}
}
