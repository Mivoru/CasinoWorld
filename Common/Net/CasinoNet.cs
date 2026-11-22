using System.IO;
using CasinoWorld.Common.Games.Roulette;
using CasinoWorld.Common.Systems;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Net
{
	public enum CasinoMessage : byte
	{
		RouletteSpinRequest, // client -> server: table
		RouletteSpinResult,  // server -> all clients: player, table, number
		PlaceSurfaceCasino,  // client -> server: left, ground row (Casino Blueprint)
		HouseLeverHit,       // client -> server: boss index (lever hit during a spin)
		ReportWin,           // client -> casino server: game, net win
		RecordsSync,         // casino server -> clients: leaderboard
		RecordsRequest,      // client -> casino server: send me the leaderboard
	}

	/// <summary>Writes and dispatches the mod's network packets.</summary>
	public static class CasinoNet
	{
		public static void Handle(BinaryReader reader, int sender)
		{
			var message = (CasinoMessage)reader.ReadByte();
			var roulette = ModContent.GetInstance<RouletteSystem>();

			switch (message) {
				case CasinoMessage.RouletteSpinRequest when Main.netMode == NetmodeID.Server:
					roulette.HandleSpinRequest(reader, sender);
					break;

				case CasinoMessage.RouletteSpinResult when Main.netMode == NetmodeID.MultiplayerClient:
					roulette.HandleSpinResult(reader);
					break;

				case CasinoMessage.PlaceSurfaceCasino when Main.netMode == NetmodeID.Server: {
					int left = reader.ReadInt16();
					int groundY = reader.ReadInt16();
					if (SurfaceCasinoStructure.CanBuildAt(left, groundY))
						SurfaceCasinoStructure.Build(left, groundY);
					break;
				}

				case CasinoMessage.ReportWin when Main.netMode == NetmodeID.Server:
					Games.CasinoRecords.HandleReport(reader, sender);
					break;

				case CasinoMessage.RecordsRequest when Main.netMode == NetmodeID.Server:
					Games.CasinoRecords.Broadcast(sender);
					break;

				case CasinoMessage.RecordsSync when Main.netMode == NetmodeID.MultiplayerClient:
					Games.CasinoRecords.HandleSync(reader);
					break;

				default:
					ModContent.GetInstance<RouletteSystem>().Mod.Logger.Warn($"Unexpected packet {message} (netMode {Main.netMode}).");
					break;
			}
		}

		public static void SendPlaceSurfaceCasino(Mod mod, int left, int groundY)
		{
			ModPacket packet = mod.GetPacket();
			packet.Write((byte)CasinoMessage.PlaceSurfaceCasino);
			packet.Write((short)left);
			packet.Write((short)groundY);
			packet.Send();
		}

		public static void SendRouletteSpinRequest(Mod mod, Point16 table)
		{
			ModPacket packet = mod.GetPacket();
			packet.Write((byte)CasinoMessage.RouletteSpinRequest);
			packet.Write(table.X);
			packet.Write(table.Y);
			packet.Send();
		}

		public static void SendRouletteSpinResult(Mod mod, int player, Point16 table, int number)
		{
			ModPacket packet = mod.GetPacket();
			packet.Write((byte)CasinoMessage.RouletteSpinResult);
			packet.Write((byte)player);
			packet.Write(table.X);
			packet.Write(table.Y);
			packet.Write((byte)number);
			packet.Send();
		}
	}
}
