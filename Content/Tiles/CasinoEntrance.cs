using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CasinoWorld.Content.Tiles
{
	/// <summary>
	/// Base for casino entrances: one big front-view tile the player walks in front of.
	/// Only the door area reacts to clicks. The tile cannot be mined, replaced or blown up,
	/// and it has no anchors, so digging the ground around it does not break it.
	/// Windows and signs glow (a _Glow texture with the same layout) and give off light
	/// according to a per-tile light table generated from that glow mask.
	/// </summary>
	public abstract class CasinoEntrance : ModTile
	{
		protected abstract int EntranceWidth { get; }
		protected abstract int EntranceHeight { get; }

		/// <summary>Door area in local tile coordinates.</summary>
		public abstract Rectangle DoorArea { get; }

		/// <summary>Light strength per tile [y, x], 0..255.</summary>
		protected abstract byte[,] Light { get; }

		protected abstract Color MapColor { get; }

		/// <summary>Light colour multiplier (r, g, b) applied to the light table.</summary>
		protected virtual Vector3 LightColor => new(1.05f, 0.78f, 0.42f);

		/// <summary>Sends the local player into this entrance's casino.</summary>
		public abstract void Enter();

		private Asset<Texture2D> glowTexture;

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = false;
			Main.tileWaterDeath[Type] = false;
			TileID.Sets.PreventsTileRemovalIfOnTopOfIt[Type] = true;
			TileID.Sets.PreventsTileReplaceIfOnTopOfIt[Type] = true;

			TileObjectData.newTile.Width = EntranceWidth;
			TileObjectData.newTile.Height = EntranceHeight;
			TileObjectData.newTile.CoordinateWidth = 16;
			TileObjectData.newTile.CoordinatePadding = 2;
			TileObjectData.newTile.CoordinateHeights = Enumerable.Repeat(16, EntranceHeight).ToArray();
			TileObjectData.newTile.Origin = new Point16(0, EntranceHeight - 1);
			TileObjectData.newTile.AnchorBottom = AnchorData.Empty;
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.newTile.LavaDeath = false;
			TileObjectData.addTile(Type);

			AddMapEntry(MapColor, CreateMapEntryName());
			DustType = DustID.GoldCoin;
			MinPick = int.MaxValue;
		}

		public override void Unload()
		{
			glowTexture = null;
		}

		public bool IsDoorArea(int i, int j)
		{
			Tile tile = Main.tile[i, j];
			return DoorArea.Contains(tile.TileFrameX / 18, tile.TileFrameY / 18);
		}

		/// <summary>Door rectangle in world pixels for an entrance whose top-left tile is <paramref name="topLeft"/>.</summary>
		public Rectangle DoorWorldRect(Point topLeft)
			=> new((topLeft.X + DoorArea.X) * 16, (topLeft.Y + DoorArea.Y) * 16, DoorArea.Width * 16, DoorArea.Height * 16);

		public override bool CanKillTile(int i, int j, ref bool blockDamaged) => false;

		public override bool CanExplode(int i, int j) => false;

		public override bool CanReplace(int i, int j, int tileTypeBeingPlaced) => false;

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => IsDoorArea(i, j);

		public override bool RightClick(int i, int j)
		{
			if (!IsDoorArea(i, j))
				return false;

			Enter();
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			if (!IsDoorArea(i, j))
				return;

			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = -1;
			player.cursorItemIconText = Language.GetTextValue("Mods.CasinoWorld.UI.EnterCasino");
		}

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			Tile tile = Main.tile[i, j];
			int x = tile.TileFrameX / 18, y = tile.TileFrameY / 18;
			if (x >= EntranceWidth || y >= EntranceHeight)
				return;

			float strength = Light[y, x] / 255f;
			r = strength * LightColor.X;
			g = strength * LightColor.Y;
			b = strength * LightColor.Z;
		}

		/// <summary>Draws the glowing parts at full brightness, so they stay lit in the dark.</summary>
		public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Tile tile = Main.tile[i, j];
			glowTexture ??= ModContent.Request<Texture2D>(Texture + "_Glow");
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			Vector2 position = new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen;

			float flicker = 0.9f + 0.1f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 4f + i * 0.7f);
			spriteBatch.Draw(glowTexture.Value, position, new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16), Color.White * flicker);
		}
	}
}
