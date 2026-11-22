using System;
using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Players
{
	public class CasinoPlayer : ModPlayer
	{
		// Ticks to wait after entering a world before moving the player back to the casino door.
		// Vanilla places the player at spawn around OnEnterWorld, so the move must happen later.
		private const int RestoreDelay = 3;
		private int restoreTimer = -1;

		/// <summary>Top centre of a casino door the local player stands at, in world pixels; null if none.</summary>
		public Vector2? NearbyDoor { get; private set; }

		private Action nearbyDoorAction;

		public override void OnEnterWorld()
		{
			restoreTimer = RestoreDelay;
		}

		public override void PostUpdate()
		{
			if (Player.whoAmI != Main.myPlayer)
				return;

			if (restoreTimer >= 0 && restoreTimer-- == 0)
				CasinoTravel.TryRestorePosition(Player);

			FindDoor();

			// Up (W by default) uses the door, like entering a minecart or climbing a rope.
			if (NearbyDoor != null && Player.controlUp && Player.releaseUp && !Player.mouseInterface)
				nearbyDoorAction?.Invoke();
		}

		/// <summary>Looks for an entrance door or a standalone casino door overlapping the player.</summary>
		private void FindDoor()
		{
			NearbyDoor = null;
			nearbyDoorAction = null;
			Rectangle hitbox = Player.Hitbox;
			hitbox.Inflate(8, 0);

			if (!CasinoTravel.InCasino) {
				foreach ((CasinoEntrance entrance, Point topLeft) in CasinoWorldSystem.Entrances()) {
					Rectangle door = entrance.DoorWorldRect(topLeft);
					if (door.Intersects(hitbox)) {
						NearbyDoor = new Vector2(door.Center.X, door.Top);
						nearbyDoorAction = entrance.Enter;
						return;
					}
				}
			}

			int doorType = ModContent.TileType<CasinoDoor>();
			Point min = hitbox.TopLeft().ToTileCoordinates();
			Point max = hitbox.BottomRight().ToTileCoordinates();
			for (int x = min.X; x <= max.X; x++) {
				for (int y = min.Y; y <= max.Y; y++) {
					if (!WorldGen.InWorld(x, y))
						continue;
					Tile tile = Main.tile[x, y];
					if (tile.HasTile && tile.TileType == doorType) {
						int left = x - tile.TileFrameX / 18 % 2;
						int top = y - tile.TileFrameY / 18 % 3;
						NearbyDoor = new Vector2(left * 16 + 16, top * 16);
						nearbyDoorAction = CasinoTravel.UseDoor;
						return;
					}
				}
			}
		}
	}
}
