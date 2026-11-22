using CasinoWorld.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Tiles.Interior
{
	/// <summary>
	/// Wall drawn from a large repeating pattern by world position (tools/casino_patterns.py), so the
	/// motif can span several tiles instead of the 16 px limit of random wall frames. An optional tint
	/// gives the hardmode casino darker variants of the same patterns.
	/// </summary>
	public abstract class PatternWall : ModWall
	{
		protected abstract string Pattern { get; }
		protected virtual Color Tint => Color.White;
		protected abstract Color MapColor { get; }

		private Asset<Texture2D> texture;

		public override string Texture => "CasinoWorld/Content/Walls/Pattern_" + Pattern;

		public override void SetStaticDefaults()
		{
			Main.wallHouse[Type] = true;
			AddMapEntry(MapColor);
			DustType = DustID.Marble;
		}

		public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
		{
			texture ??= ModContent.Request<Texture2D>(Texture);
			Texture2D tex = texture.Value;
			int tw = tex.Width / 16, th = tex.Height / 16;
			var source = new Rectangle(i % tw * 16, j % th * 16, 16, 16);
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			Color light = Lighting.GetColor(i, j);
			Color tint = Tint;
			var color = new Color(light.R * tint.R / 255, light.G * tint.G / 255, light.B * tint.B / 255);
			spriteBatch.Draw(tex, new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen, source, color);
			return false;
		}
	}

	public class LobbyMarbleWall : PatternWall
	{
		protected override string Pattern => "Lobby";
		protected override Color MapColor => new(220, 210, 190);
	}

	public class EmeraldDamaskWall : PatternWall
	{
		protected override string Pattern => "Damask";
		protected override Color MapColor => new(20, 70, 56);
	}

	public class VelvetCurtainWall : PatternWall
	{
		protected override string Pattern => "Velvet";
		protected override Color MapColor => new(70, 18, 82);
	}

	public class WalnutWainscotWall : PatternWall
	{
		protected override string Pattern => "Wainscot";
		protected override Color MapColor => new(92, 44, 20);
	}

	/// <summary>Hardmode variants: the same patterns in night colours.</summary>
	public class NoirMarbleWall : PatternWall
	{
		protected override string Pattern => "Lobby";
		protected override Color Tint => new(120, 105, 150);
		protected override Color MapColor => new(90, 80, 110);
	}

	public class AmethystDamaskWall : PatternWall
	{
		protected override string Pattern => "Damask";
		protected override Color Tint => new(190, 120, 255);
		protected override Color MapColor => new(40, 30, 70);
	}

	public class MidnightVelvetWall : PatternWall
	{
		protected override string Pattern => "Velvet";
		protected override Color Tint => new(120, 150, 255);
		protected override Color MapColor => new(30, 20, 70);
	}

	/// <summary>Solid floor/ceiling tile drawn from a repeating pattern by world position.</summary>
	public abstract class PatternTile : ModTile
	{
		protected abstract string Pattern { get; }
		protected virtual Color Tint => Color.White;
		protected abstract Color MapColor { get; }
		protected virtual bool HasGlow => false;

		private Asset<Texture2D> texture, glow;

		public override string Texture => "CasinoWorld/Content/Tiles/Pattern_" + Pattern;

		public override void SetStaticDefaults()
		{
			Main.tileSolid[Type] = true;
			Main.tileBlockLight[Type] = true;
			AddMapEntry(MapColor);
			DustType = DustID.Marble;
		}

		public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
		{
			texture ??= ModContent.Request<Texture2D>(Texture);
			Texture2D tex = texture.Value;
			int tw = tex.Width / 16, th = tex.Height / 16;
			var source = new Rectangle(i % tw * 16, j % th * 16, 16, 16);
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			Vector2 position = new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen;
			Color light = Lighting.GetColor(i, j);
			Color tint = Tint;
			spriteBatch.Draw(tex, position, source, new Color(light.R * tint.R / 255, light.G * tint.G / 255, light.B * tint.B / 255));
			if (HasGlow) {
				glow ??= ModContent.Request<Texture2D>(Texture + "_Glow");
				spriteBatch.Draw(glow.Value, position, source, Color.White);
			}
			return false;
		}
	}

	public class MarbleCheckerFloor : PatternTile
	{
		protected override string Pattern => "MarbleFloor";
		protected override Color MapColor => new(200, 196, 180);
	}

	public class EmeraldCarpet : PatternTile
	{
		protected override string Pattern => "TealCarpet";
		protected override Color MapColor => new(22, 92, 84);
	}

	public class VelvetCarpet : PatternTile
	{
		protected override string Pattern => "VelvetCarpet";
		protected override Color MapColor => new(80, 20, 70);
	}

	public class NoirCarpet : PatternTile
	{
		protected override string Pattern => "VelvetCarpet";
		protected override Color Tint => new(120, 150, 255);
		protected override Color MapColor => new(40, 30, 80);
	}

	/// <summary>Coffered ceiling with recessed lights every four tiles; the lenses glow and light the room.</summary>
	public class CofferedCeiling : PatternTile
	{
		protected override string Pattern => "Ceiling";
		protected override Color MapColor => new(200, 186, 150);
		protected override bool HasGlow => true;

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			if (i % 4 == 1 || i % 4 == 2) {
				r = 0.55f;
				g = 0.5f;
				b = 0.4f;
			}
		}
	}
}
