using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Walls
{
	/// <summary>Crimson damask wallpaper with a gold motif (tools/interior_textures.py).</summary>
	public class CasinoWallpaper : ModWall
	{
		public override void SetStaticDefaults()
		{
			Main.wallHouse[Type] = true;
			AddMapEntry(new Color(96, 16, 28));
			DustType = DustID.RedTorch;
		}
	}

	/// <summary>Dark mahogany panelling with a gold rail, for the lower part of casino walls.</summary>
	public class CasinoPanelWall : ModWall
	{
		public override void SetStaticDefaults()
		{
			Main.wallHouse[Type] = true;
			AddMapEntry(new Color(66, 32, 18));
			DustType = DustID.WoodFurniture;
		}
	}
}
