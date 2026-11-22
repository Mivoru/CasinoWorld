using CasinoWorld.Content.Subworlds;
using Microsoft.Xna.Framework;
using SubworldLibrary;
using Terraria;

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

		public static bool InCasino => SubworldSystem.Current is CasinoSubworld;

		/// <summary>Enters the given casino and remembers where to come back to.</summary>
		public static void Enter<T>() where T : CasinoSubworld
		{
			if (InCasino)
				return;

			returnPosition = Main.LocalPlayer.position;
			returnWorldId = Main.worldID;
			if (!SubworldSystem.Enter<T>())
				returnPosition = null;
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
			if (Main.worldID != returnWorldId)
				return;

			player.Teleport(position, -1);
			player.velocity = Vector2.Zero;
			player.fallStart = (int)(position.Y / 16f);
		}
	}
}
