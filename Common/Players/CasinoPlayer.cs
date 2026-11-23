using System;
using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

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

		/// <summary>Characters made before the Casino Blueprint existed get one, once.</summary>
		private bool receivedBlueprint;

		/// <summary>Has entered the high rollers casino at least once (until then, a reminder on joining).</summary>
		public bool VisitedUnderground;

		private int revealTimer = -1;

		/// <summary>The casinos are small and fully built: show all of them on the map and minimap.</summary>
		private static void RevealCasinoMap()
		{
			for (int x = 0; x < Main.maxTilesX; x++)
				for (int y = 0; y < Main.maxTilesY; y++)
					if (WorldGen.InWorld(x, y))
						Main.Map.Update(x, y, 255);
			Main.refreshMap = true;
		}

		public override void OnEnterWorld()
		{
			restoreTimer = RestoreDelay;
			if (CasinoTravel.InCasino)
				revealTimer = 180;

			if (!receivedBlueprint && !CasinoTravel.InCasino) {
				receivedBlueprint = true;
				int type = ModContent.ItemType<Content.Items.CasinoBlueprint>();
				if (!Player.HasItemInAnyInventory(type))
					Player.QuickSpawnItem(Player.GetSource_Misc("CasinoWorld"), type);
			}

			// Reminder of the hardmode casino for players who have not found it yet.
			if (!VisitedUnderground && !CasinoTravel.InCasino && CasinoWorldSystem.HasUndergroundCasino)
				Main.NewText(CasinoWorldSystem.UndergroundCasinoDirections().ToString(), CasinoWorldSystem.AnnounceColor);
		}

		public override void SaveData(TagCompound tag)
		{
			tag["receivedBlueprint"] = receivedBlueprint;
			tag["visitedUnderground"] = VisitedUnderground;
		}

		public override void LoadData(TagCompound tag)
		{
			receivedBlueprint = tag.ContainsKey("receivedBlueprint") && tag.GetBool("receivedBlueprint");
			VisitedUnderground = tag.ContainsKey("visitedUnderground") && tag.GetBool("visitedUnderground");
		}

		public override void PostUpdate()
		{
			// Twice: once soon after entering, once more when all tile sections have arrived in multiplayer.
			if (Player.whoAmI == Main.myPlayer && revealTimer >= 0 && --revealTimer % 150 == 0) {
				RevealCasinoMap();
				if (revealTimer <= 0)
					revealTimer = -1;
			}
			if (Player.whoAmI != Main.myPlayer)
				return;

			if (restoreTimer >= 0 && restoreTimer-- == 0)
				CasinoTravel.TryRestorePosition(Player);

			FindDoor();
		}

		/// <summary>Up (W by default) uses the door, read from the input triggers like vanilla does.</summary>
		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (NearbyDoor != null && PlayerInput.Triggers.JustPressed.Up && !Player.mouseInterface)
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
