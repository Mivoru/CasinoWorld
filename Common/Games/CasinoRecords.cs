using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Games
{
	/// <summary>One big win for the casino leaderboard.</summary>
	public readonly record struct CasinoWin(string Player, long Amount, byte Game, long UtcTicks)
	{
		public DateTime Time => new(UtcTicks, DateTimeKind.Utc);
	}

	public enum CasinoGame : byte { Roulette, Blackjack, Slots }

	/// <summary>
	/// The casino's leaderboard of biggest wins (net profit of one round): today, the last 7 days and
	/// all time. Shared by everyone who plays in the same world.
	///
	/// Storage: a small file per main world under the tModLoader save folder
	/// (CasinoWorld/records_[world id].dat). Casinos are subworlds (in multiplayer, separate server
	/// processes), so the main world's save cannot be used directly; the file is read and written by
	/// whichever process runs the casino. Clients report wins to that server, which broadcasts the board.
	/// </summary>
	public class CasinoRecords : ModSystem
	{
		public const int Shown = 3;
		private const int MaxWeekly = 200;
		private const int MaxAllTime = 10;

		/// <summary>Main world id, copied into the casino subworld (CasinoSubworld.CopyMainWorldData).</summary>
		public static string WorldKey = "";

		public static List<CasinoWin> Week = new();
		public static List<CasinoWin> AllTime = new();

		/// <summary>The latest win worth showing in the marquee (null for none).</summary>
		public static CasinoWin? Latest;

		public override void ClearWorld()
		{
			Week.Clear();
			AllTime.Clear();
			Latest = null;
		}

		public static IEnumerable<CasinoWin> Today
		{
			get {
				DateTime today = DateTime.Now.Date;
				return Week.Where(w => w.Time.ToLocalTime().Date == today).OrderByDescending(w => w.Amount).Take(Shown);
			}
		}

		public static IEnumerable<CasinoWin> ThisWeek => Week.OrderByDescending(w => w.Amount).Take(Shown);

		public static IEnumerable<CasinoWin> Best => AllTime.OrderByDescending(w => w.Amount).Take(Shown);

		private static string FilePath
		{
			get {
				string key = string.IsNullOrEmpty(WorldKey) ? "default" : string.Concat(WorldKey.Where(char.IsLetterOrDigit));
				return Path.Combine(Main.SavePath, "CasinoWorld", $"records_{key}.dat");
			}
		}

		// ------------------------------------------------------------------ reporting

		/// <summary>Called by the games on the client that played the round. Only real wins are recorded.</summary>
		public static void Report(Player player, CasinoGame game, long netWin)
		{
			if (netWin <= 0 || player.whoAmI != Main.myPlayer)
				return;
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				ModPacket packet = ModContent.GetInstance<CasinoWorld>().GetPacket();
				packet.Write((byte)Net.CasinoMessage.ReportWin);
				packet.Write((byte)game);
				packet.Write(netWin);
				packet.Send();
				return;
			}
			Add(new CasinoWin(player.name, netWin, (byte)game, DateTime.UtcNow.Ticks));
		}

		/// <summary>Server side: a client reported a win.</summary>
		public static void HandleReport(BinaryReader reader, int sender)
		{
			byte game = reader.ReadByte();
			long amount = reader.ReadInt64();
			// Basic sanity limit: no single round pays more than 1000 platinum.
			if (amount <= 0 || amount > 1000L * Coins.Platinum || game > (byte)CasinoGame.Slots)
				return;
			Add(new CasinoWin(Main.player[sender].name, amount, game, DateTime.UtcNow.Ticks));
		}

		private static void Add(CasinoWin win)
		{
			Load();
			Week.Add(win);
			AllTime.Add(win);
			Prune();
			Latest = win;
			Save();
			if (Main.netMode == NetmodeID.Server)
				Broadcast(-1);
		}

		private static void Prune()
		{
			DateTime cutoff = DateTime.UtcNow.AddDays(-7);
			Week = Week.Where(w => w.Time >= cutoff).OrderByDescending(w => w.Amount).Take(MaxWeekly).ToList();
			AllTime = AllTime.OrderByDescending(w => w.Amount).Take(MaxAllTime).ToList();
		}

		// ------------------------------------------------------------------ storage

		public static void Load()
		{
			try {
				Week.Clear();
				AllTime.Clear();
				if (!File.Exists(FilePath))
					return;
				using var reader = new BinaryReader(File.OpenRead(FilePath));
				ReadLists(reader);
				Prune();
			}
			catch (Exception e) {
				ModContent.GetInstance<CasinoWorld>().Logger.Warn("Could not read casino records: " + e.Message);
			}
		}

		private static void Save()
		{
			try {
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				using var writer = new BinaryWriter(File.Create(FilePath));
				WriteLists(writer);
			}
			catch (Exception e) {
				ModContent.GetInstance<CasinoWorld>().Logger.Warn("Could not save casino records: " + e.Message);
			}
		}

		private static void WriteList(BinaryWriter writer, List<CasinoWin> list)
		{
			writer.Write((short)list.Count);
			foreach (CasinoWin w in list) {
				writer.Write(w.Player ?? "");
				writer.Write(w.Amount);
				writer.Write(w.Game);
				writer.Write(w.UtcTicks);
			}
		}

		private static List<CasinoWin> ReadList(BinaryReader reader)
		{
			int count = reader.ReadInt16();
			var list = new List<CasinoWin>(count);
			for (int k = 0; k < count; k++)
				list.Add(new CasinoWin(reader.ReadString(), reader.ReadInt64(), reader.ReadByte(), reader.ReadInt64()));
			return list;
		}

		private static void WriteLists(BinaryWriter writer)
		{
			WriteList(writer, Week);
			WriteList(writer, AllTime);
		}

		private static void ReadLists(BinaryReader reader)
		{
			Week = ReadList(reader);
			AllTime = ReadList(reader);
		}

		// ------------------------------------------------------------------ sync

		/// <summary>Server: sends the board (top entries only) to one client, or everyone with -1.</summary>
		public static void Broadcast(int toClient)
		{
			ModPacket packet = ModContent.GetInstance<CasinoWorld>().GetPacket();
			packet.Write((byte)Net.CasinoMessage.RecordsSync);
			var week = Week.OrderByDescending(w => w.Amount).Take(50).ToList();
			WriteList(packet, week);
			WriteList(packet, AllTime);
			packet.Write(Latest.HasValue);
			if (Latest.HasValue)
				WriteList(packet, new List<CasinoWin> { Latest.Value });
			packet.Send(toClient);
		}

		public static void HandleSync(BinaryReader reader)
		{
			ReadLists(reader);
			Latest = reader.ReadBoolean() ? ReadList(reader).FirstOrDefault() : null;
		}

		/// <summary>Client: asks the casino server for the board after entering.</summary>
		public static void RequestSync()
		{
			ModPacket packet = ModContent.GetInstance<CasinoWorld>().GetPacket();
			packet.Write((byte)Net.CasinoMessage.RecordsRequest);
			packet.Send();
		}

		public static string GameName(byte game) => Language.GetTextValue("Mods.CasinoWorld.UI.Records.Game" + (CasinoGame)game);
	}
}
