using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CasinoWorld.Content.Tiles.Interior
{
	/// <summary>
	/// Base for the casino's custom furniture and lights (rendered in Blender, render_interior.py).
	/// Multi-tile, not solid, with an optional glow mask, and light taken per tile from the glow mask
	/// (InteriorLights, generated), so a lamp lights the room exactly where it glows.
	/// Casinos place these directly with <see cref="Place"/>; they are not meant for players.
	/// </summary>
	public abstract class InteriorFurniture : ModTile
	{
		public abstract int Width { get; }
		public abstract int Height { get; }
		protected abstract Color MapColor { get; }

		/// <summary>Light strength multiplier for the glow-derived light.</summary>
		protected virtual float LightStrength => 1f;

		private Asset<Texture2D> glow;
		private bool hasGlow;
		private float[,,] lights;

		public override string Texture => "CasinoWorld/Content/Tiles/Interior/" + Name;

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = false;
			TileObjectData.newTile.Width = Width;
			TileObjectData.newTile.Height = Height;
			TileObjectData.newTile.CoordinateWidth = 16;
			TileObjectData.newTile.CoordinatePadding = 2;
			int[] heights = new int[Height];
			for (int k = 0; k < Height; k++)
				heights[k] = 16;
			TileObjectData.newTile.CoordinateHeights = heights;
			TileObjectData.newTile.Origin = new Point16(0, Height - 1);
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.addTile(Type);
			AddMapEntry(MapColor);
			DustType = DustID.Gold;
			hasGlow = ModContent.HasAsset(Texture + "_Glow");
			// The table holds average glow per tile; normalise it so the brightest tile of each prop
			// gives full light, otherwise thin lamps (whose glow covers few pixels) barely light anything.
			if (InteriorLights.Table.TryGetValue(Name, out float[,,] raw)) {
				float max = 0.01f;
				foreach (float v in raw)
					max = System.Math.Max(max, v);
				lights = new float[raw.GetLength(0), raw.GetLength(1), 3];
				for (int y = 0; y < raw.GetLength(0); y++) {
					for (int x = 0; x < raw.GetLength(1); x++) {
						for (int c = 0; c < 3; c++)
							lights[y, x, c] = System.Math.Min(1f, raw[y, x, c] / max * 1.1f);
					}
				}
			}
		}

		public override void Unload() => glow = null;

		/// <summary>Writes the whole object with its top-left corner at (x, y), bypassing anchors.</summary>
		public static void Place(int type, int x, int y)
		{
			var furniture = (InteriorFurniture)TileLoader.GetTile(type);
			for (int dx = 0; dx < furniture.Width; dx++) {
				for (int dy = 0; dy < furniture.Height; dy++) {
					if (!WorldGen.InWorld(x + dx, y + dy))
						continue;
					Tile tile = Main.tile[x + dx, y + dy];
					tile.HasTile = true;
					tile.TileType = (ushort)type;
					tile.TileFrameX = (short)(dx * 18);
					tile.TileFrameY = (short)(dy * 18);
				}
			}
		}

		public static void Place<T>(int x, int y) where T : InteriorFurniture => Place(ModContent.TileType<T>(), x, y);

		/// <summary>Places the object standing on the floor row <paramref name="floorY"/>.</summary>
		public static void PlaceOnFloor<T>(int x, int floorY) where T : InteriorFurniture
			=> Place(ModContent.TileType<T>(), x, floorY - ModContent.GetInstance<T>().Height);

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			if (lights == null)
				return;
			Tile tile = Main.tile[i, j];
			int column = tile.TileFrameX / 18 % Width, row = tile.TileFrameY / 18 % Height;
			r = lights[row, column, 0] * LightStrength;
			g = lights[row, column, 1] * LightStrength;
			b = lights[row, column, 2] * LightStrength;
		}

		public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
		{
			if (!hasGlow)
				return;
			Tile tile = Main.tile[i, j];
			glow ??= ModContent.Request<Texture2D>(Texture + "_Glow");
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			spriteBatch.Draw(glow.Value, new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen,
				new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16), Color.White);
		}

		protected static bool IsTopLeft(Tile tile, int width, int height) => tile.TileFrameX % (width * 18) == 0 && tile.TileFrameY % (height * 18) == 0;
	}

	public class CrystalChandelier : InteriorFurniture
	{
		public override int Width => 6;
		public override int Height => 4;
		protected override Color MapColor => new(255, 220, 140);
		protected override float LightStrength => 1.6f;

		public override void NearbyEffects(int i, int j, bool closer)
		{
			// Crystals twinkle now and then.
			if (!Main.dedServ && closer && Main.rand.NextBool(240)) {
				Dust d = Dust.NewDustPerfect(new Vector2(i * 16 + 8, j * 16 + 8), DustID.GoldFlame, Vector2.Zero, 0, Color.White, 0.6f);
				d.noGravity = true;
				d.velocity *= 0.1f;
			}
		}
	}

	public class WallSconce : InteriorFurniture
	{
		public override int Width => 2;
		public override int Height => 3;
		protected override Color MapColor => new(255, 210, 120);
		protected override float LightStrength => 2f;
	}

	public class CocktailTable : InteriorFurniture
	{
		public override int Width => 3;
		public override int Height => 3;
		protected override Color MapColor => new(30, 80, 60);
		protected override float LightStrength => 1.4f;
	}

	public class BarCounter : InteriorFurniture
	{
		public override int Width => 4;
		public override int Height => 3;
		protected override Color MapColor => new(30, 90, 60);
	}

	public class BarShelf : InteriorFurniture
	{
		public override int Width => 8;
		public override int Height => 6;
		protected override Color MapColor => new(90, 50, 30);
		protected override float LightStrength => 1.2f;
	}

	public class BarStool : InteriorFurniture
	{
		public override int Width => 2;
		public override int Height => 2;
		protected override Color MapColor => new(30, 90, 60);
	}

	public class VelvetSofa : InteriorFurniture
	{
		public override int Width => 5;
		public override int Height => 3;
		protected override Color MapColor => new(70, 18, 82);
	}

	public class GrandPiano : InteriorFurniture
	{
		public override int Width => 6;
		public override int Height => 4;
		protected override Color MapColor => new(20, 20, 24);
	}

	public class MarblePlanter : InteriorFurniture
	{
		public override int Width => 2;
		public override int Height => 4;
		protected override Color MapColor => new(40, 120, 50);
	}

	public class PrizeCounter : InteriorFurniture
	{
		public override int Width => 6;
		public override int Height => 3;
		protected override Color MapColor => new(200, 170, 90);
	}

	public class NeonBar : InteriorFurniture
	{
		public override int Width => 6;
		public override int Height => 3;
		protected override Color MapColor => new(255, 80, 200);
		protected override float LightStrength => 1.5f;
	}

	public class NeonCabaret : InteriorFurniture
	{
		public override int Width => 10;
		public override int Height => 3;
		protected override Color MapColor => new(80, 220, 255);
		protected override float LightStrength => 1.5f;
	}

	public class Uplight : InteriorFurniture
	{
		public override int Width => 2;
		public override int Height => 2;
		protected override Color MapColor => new(255, 210, 120);
		protected override float LightStrength => 2.2f;
	}

	public class MarblePilaster : InteriorFurniture
	{
		public override int Width => 2;
		public override int Height => 4;
		protected override Color MapColor => new(26, 84, 64);
	}

	/// <summary>Round dance stage with a chrome pole; the pole dancer NPC performs on it.</summary>
	public class PoleStage : InteriorFurniture
	{
		public override int Width => 8;
		public override int Height => 12;
		protected override Color MapColor => new(200, 200, 210);
		protected override float LightStrength => 1.5f;
	}
}

namespace CasinoWorld.Content.Tiles.Interior
{
	/// <summary>Framed casino paintings for the walls (tools/casino_paintings.py).</summary>
	public abstract class CasinoPainting : InteriorFurniture
	{
		protected override Color MapColor => new(200, 150, 55);
	}

	public class PaintingRoyalFlush : CasinoPainting { public override int Width => 6; public override int Height => 4; }

	public class PaintingLuckySevens : CasinoPainting { public override int Width => 4; public override int Height => 6; }

	public class PaintingWheel : CasinoPainting { public override int Width => 6; public override int Height => 6; }

	public class PaintingTheHouse : CasinoPainting { public override int Width => 6; public override int Height => 8; }

	public class PaintingVegasNight : CasinoPainting { public override int Width => 10; public override int Height => 6; }

	public class PaintingJackpot : CasinoPainting { public override int Width => 8; public override int Height => 5; }

	public class PaintingGoldenDice : CasinoPainting { public override int Width => 5; public override int Height => 4; }
}
