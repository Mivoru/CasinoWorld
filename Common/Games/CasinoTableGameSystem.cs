using System.Collections.Generic;
using CasinoWorld.Content.Subworlds;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using SubworldLibrary;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Games
{
	/// <summary>
	/// Shared host for a table game window: opening from a table tile, closing when the player
	/// walks away, dies or opens the inventory, and drawing the UI layer.
	/// Only one game window is open at a time.
	/// </summary>
	public abstract class CasinoTableGameSystem : ModSystem
	{
		// Window closes when the player walks further than this from the table, in pixels.
		private const float MaxDistance = 16 * 14;

		private static CasinoTableGameSystem active;

		private UserInterface ui;
		private UIState state;

		public Point16 Table { get; private set; } = Point16.NegativeOne;

		public bool IsOpen => ui?.CurrentState != null;

		public static CasinoSubworld Casino => SubworldSystem.Current as CasinoSubworld;

		/// <summary>
		/// Mouse position in UI coordinates (UI scale applied). Element dimensions use these
		/// coordinates, so hit tests must use this instead of Main.MouseScreen.
		/// </summary>
		public static Point UIMouse => (active?.ui?.MousePosition ?? Main.MouseScreen).ToPoint();

		protected abstract int TableTileType { get; }

		/// <summary>Size of the table tile in tiles; used to find its centre for the distance check.</summary>
		protected virtual Point TableSize => new(CasinoTable.Width, CasinoTable.Height);

		protected abstract UIState CreateState();

		/// <summary>True for station-like windows that work with the inventory open (they close with it).</summary>
		protected virtual bool KeepInventoryOpen => false;

		/// <summary>Runs every frame on the client, also while the window is closed.</summary>
		protected virtual void UpdateGame(Player player) { }

		/// <summary>Coins the running round owes the local player if it never finishes (see Escrow).</summary>
		public virtual long Owed => 0;

		/// <summary>Called when the window closes; a running round must settle here.</summary>
		protected virtual void OnClose(Player player) { }

		public override void Load()
		{
			if (Main.dedServ)
				return;

			ui = new UserInterface();
			state = CreateState();
			state.Activate();
		}

		public override void Unload()
		{
			ui = null;
			state = null;
			active = null;
		}

		// Runs before the player file is saved, so a running round pays out into the inventory first.
		public override void PreSaveAndQuit()
		{
			if (IsOpen)
				Close();
		}

		public override void OnWorldUnload()
		{
			if (IsOpen)
				Close();
		}

		public void Open(Point16 table)
		{
			if (Main.dedServ || Casino == null)
				return;

			if (active != null && active != this && active.IsOpen)
				active.Close();

			active = this;
			Table = table;
			Main.playerInventory = KeepInventoryOpen;
			ui.SetState(state);
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public void Close()
		{
			OnClose(Main.LocalPlayer);
			ui?.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public override void UpdateUI(GameTime gameTime)
		{
			Player player = Main.LocalPlayer;
			UpdateGame(player);

			if (!IsOpen)
				return;

			Tile tile = Main.tile[Table.X, Table.Y];
			bool tableGone = !tile.HasTile || tile.TileType != TableTileType;
			Vector2 tableCenter = new Point16(Table.X + TableSize.X / 2, Table.Y + TableSize.Y / 2).ToWorldCoordinates();
			bool inventoryClose = KeepInventoryOpen ? !Main.playerInventory : PlayerInput.Triggers.JustPressed.Inventory;
			if (player.dead || tableGone || Casino == null || player.Distance(tableCenter) > MaxDistance || inventoryClose) {
				Close();
				return;
			}

			ui.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			if (index < 0)
				return;

			layers.Insert(index, new LegacyGameInterfaceLayer("CasinoWorld: " + Name, () => {
				if (IsOpen)
					ui.Draw(Main.spriteBatch, new GameTime());
				return true;
			}, InterfaceScaleType.UI));
		}
	}
}
