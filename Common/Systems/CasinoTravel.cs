using CasinoWorld.Content.Subworlds;
using Microsoft.Xna.Framework;
using SubworldLibrary;
using CasinoWorld.Common;
using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace CasinoWorld.Common.Systems
{
	/// <summary>
	/// Moves the local player into and out of the casino dimensions.
	/// Runs on the client only: SubworldLibrary handles the server side.
	/// </summary>
	public static class CasinoTravel
	{
		// Static fields survive the world switch because the client process keeps running.
		private static Vector2? returnPosition;
		private static int returnWorldId;
		private static string returnPlayer;   // player file, so another character is not moved there

		public static bool InPenthouse => SubworldSystem.Current is Penthouse;

		public static bool InCasino => SubworldSystem.Current is CasinoSubworld;

		/// <summary>
		/// The doors stay shut while a boss is alive or an invasion or event is running: the casino is not a
		/// place to hide from a fight (and the fight would stall in the main world meanwhile).
		/// </summary>
		public static bool CanEnter(out string reasonKey)
		{
			reasonKey = null;
			foreach (NPC npc in Main.ActiveNPCs) {
				if (npc.boss || npc.type is NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail) {
					reasonKey = "Mods.CasinoWorld.UI.ClosedBoss";
					return false;
				}
			}
			if (Main.invasionType != InvasionID.None || Main.pumpkinMoon || Main.snowMoon || Main.eclipse
				|| Terraria.GameContent.Events.DD2Event.Ongoing) {
				reasonKey = "Mods.CasinoWorld.UI.ClosedEvent";
				return false;
			}
			return true;
		}

		/// <summary>Enters the given casino and remembers where to come back to.</summary>
		public static void Enter<T>() where T : CasinoSubworld
		{
			if (InCasino)
				return;
			if (!CanEnter(out string reason)) {
				Main.NewText(Language.GetTextValue(reason), new Color(255, 120, 120));
				Terraria.Audio.SoundEngine.PlaySound(SoundID.DoorClosed);
				return;
			}

			returnPosition = Main.LocalPlayer.position;
			returnWorldId = Main.worldID;
			returnPlayer = Main.ActivePlayerFileData?.Path;
			if (typeof(T) == typeof(UndergroundCasino))
				Main.LocalPlayer.GetModPlayer<Players.CasinoPlayer>().VisitedUnderground = true;
			Terraria.Audio.SoundEngine.PlaySound(typeof(T) == typeof(UndergroundCasino) ? CasinoSounds.VaultDoor : CasinoSounds.DoorEnter);
			if (!SubworldSystem.Enter<T>())
				returnPosition = null;
		}

		/// <summary>Up to the penthouse; false if the way is closed.</summary>
		public static bool EnterPenthouse()
		{
			if (SubworldSystem.AnyActive())
				return false;
			if (!CanEnter(out string reason)) {
				Main.NewText(Language.GetTextValue(reason), new Color(255, 120, 120));
				Terraria.Audio.SoundEngine.PlaySound(SoundID.DoorClosed);
				return false;
			}
			returnPosition = Main.LocalPlayer.position;
			returnWorldId = Main.worldID;
			returnPlayer = Main.ActivePlayerFileData?.Path;
			Terraria.Audio.SoundEngine.PlaySound(CasinoSounds.ElevatorRide);
			if (SubworldSystem.Enter<Penthouse>())
				return true;
			returnPosition = null;
			return false;
		}

		public static void Exit()
		{
			if (InCasino)
				SubworldSystem.Exit();
		}

		/// <summary>Standalone casino door: exits inside a casino, enters the surface casino outside.</summary>
		public static void UseDoor()
		{
			if (InCasino)
				Exit();
			else if (InPenthouse)
				SubworldSystem.Exit();
			else
				Enter<SurfaceCasino>();
		}

		/// <summary>
		/// Puts the player back at the casino door after leaving.
		/// Without this they would appear at the world spawn point.
		/// </summary>
		public static void TryRestorePosition(Player player)
		{
			if (returnPosition is not Vector2 position || SubworldSystem.AnyActive())
				return;

			returnPosition = null;
			if (Main.worldID != returnWorldId || Main.ActivePlayerFileData?.Path != returnPlayer)
				return;

			player.Teleport(position, -1);
			player.velocity = Vector2.Zero;
			player.fallStart = (int)(position.Y / 16f);
		}
	}
}
