using CasinoWorld.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CasinoWorld.Content.Tiles
{
	/// <summary>Marble foundation with a gold band; glows faintly inside casinos so the floor edge is not a black void.</summary>
	public class CasinoFoundation : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileSolid[Type] = true;
			Main.tileBlockLight[Type] = true;
			AddMapEntry(new Color(214, 208, 200));
			DustType = DustID.Marble;
		}

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			if (!CasinoTravel.InCasino)
				return;
			r = 0.22f;
			g = 0.17f;
			b = 0.12f;
		}
	}

	/// <summary>
	/// Brass elevator (3x4). Right-click the upper half of the doors to ride one floor up, the lower half
	/// to ride one floor down (to the next elevator in the same column). The doors chime; the ride is instant.
	/// </summary>
	public class CasinoElevator : ModTile
	{
		public const int Width = 3;
		public const int Height = 4;
		private const int SearchRange = 60;

		private static Asset<Texture2D> glow;

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = false;
			TileObjectData.newTile.Width = Width;
			TileObjectData.newTile.Height = Height;
			TileObjectData.newTile.CoordinateWidth = 16;
			TileObjectData.newTile.CoordinatePadding = 2;
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16, 16 };
			TileObjectData.newTile.Origin = new Point16(0, Height - 1);
			TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, Width, 0);
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(200, 150, 70), CreateMapEntryName());
		}

		public override void Unload()
		{
			glow = null;
		}

		private static Point16 TopLeft(int i, int j)
		{
			Tile tile = Main.tile[i, j];
			return new Point16(i - tile.TileFrameX / 18 % Width, j - tile.TileFrameY / 18 % Height);
		}

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

		/// <summary>Finds the nearest elevator above (up) or below (down) in the same column, or -1.</summary>
		private int FindNext(Point16 origin, bool up)
		{
			for (int d = 1; d <= SearchRange; d++) {
				int y = up ? origin.Y - d : origin.Y + d;
				if (!WorldGen.InWorld(origin.X, y))
					break;
				Tile tile = Main.tile[origin.X, y];
				if (tile.HasTile && tile.TileType == Type && tile.TileFrameX % 54 == 0 && tile.TileFrameY % 72 == 0)
					return y;
			}
			return -1;
		}

		/// <summary>Upper half of the doors = up, lower half = down; falls back to the only direction there is.</summary>
		private int Destination(Point16 origin, out bool up)
		{
			up = Main.MouseWorld.Y < (origin.Y + Height / 2f) * 16f;
			int target = FindNext(origin, up);
			if (target < 0) {
				up = !up;
				target = FindNext(origin, up);
			}
			return target;
		}

		public override bool RightClick(int i, int j)
		{
			Point16 origin = TopLeft(i, j);
			int other = Destination(origin, out _);
			if (other < 0)
				return false;
			Player player = Main.LocalPlayer;
			Vector2 destination = new Vector2(origin.X + Width / 2f, other + Height) * 16f - new Vector2(player.width / 2f, player.height);
			player.Teleport(destination, TeleportationStyleID.TeleporterTile);
			player.velocity = Vector2.Zero;
			if (Main.netMode == NetmodeID.MultiplayerClient)
				NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0, player.whoAmI, destination.X, destination.Y, TeleportationStyleID.TeleporterTile);
			SoundEngine.PlaySound(global::CasinoWorld.Common.CasinoSounds.ElevatorDing, destination);
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = -1;
			Destination(TopLeft(i, j), out bool up);
			player.cursorItemIconText = Language.GetTextValue(up ? "Mods.CasinoWorld.UI.ElevatorUp" : "Mods.CasinoWorld.UI.ElevatorDown");
		}

		public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Tile tile = Main.tile[i, j];
			glow ??= ModContent.Request<Texture2D>(Texture + "_Glow");
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			spriteBatch.Draw(glow.Value, new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen,
				new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16), Color.White);
		}
	}

	/// <summary>
	/// Tiered marble fountain (8x7). The water jets, falling drops and splashes are particles,
	/// so the fountain is always in motion; it also gives off a soft blue light.
	/// </summary>
	public class CasinoFountain : ModTile
	{
		public const int Width = 8;
		public const int Height = 7;

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = false;
			TileObjectData.newTile.Width = Width;
			TileObjectData.newTile.Height = Height;
			TileObjectData.newTile.CoordinateWidth = 16;
			TileObjectData.newTile.CoordinatePadding = 2;
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16, 16, 16, 16, 16 };
			TileObjectData.newTile.Origin = new Point16(0, Height - 1);
			TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, Width, 0);
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(200, 210, 230), CreateMapEntryName());
			DustType = DustID.Marble;
		}

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			r = 0.05f;
			g = 0.12f;
			b = 0.22f;
		}

		/// <summary>Water effects, spawned from the fountain's top-left tile while a player is near.</summary>
		public override void NearbyEffects(int i, int j, bool closer)
		{
			Tile tile = Main.tile[i, j];
			if (Main.dedServ || tile.TileFrameX != 0 || tile.TileFrameY != 0 || Main.gamePaused)
				return;

			Vector2 top = new Vector2(i + Width / 2f, j + 0.6f) * 16f;
			// Jet from the golden die on top, arcing to both sides.
			for (int k = 0; k < 2; k++) {
				Dust jet = Dust.NewDustPerfect(top, DustID.Water, new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-3.2f, -2.2f)), 0, default, 1.2f);
				jet.noGravity = false;
				jet.fadeIn = 1f;
			}
			// Spill from the middle bowl.
			if (Main.rand.NextBool(2)) {
				Vector2 bowl = new Vector2(i + (Main.rand.NextBool() ? 2.0f : 6.0f), j + 3.1f) * 16f;
				Dust spill = Dust.NewDustPerfect(bowl, DustID.Water, new Vector2(0, 0.5f), 0, default, 1f);
				spill.noGravity = false;
			}
			// Splashes and sparkles on the basin surface.
			if (Main.rand.NextBool(3)) {
				Vector2 basin = new Vector2(i + Main.rand.NextFloat(0.8f, 7.2f), j + 5.4f) * 16f;
				Dust splash = Dust.NewDustPerfect(basin, DustID.BlueTorch, new Vector2(0, -0.6f), 120, default, 0.6f);
				splash.noGravity = true;
			}
		}
	}
}
