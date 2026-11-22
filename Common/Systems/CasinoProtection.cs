using Terraria.ModLoader;

namespace CasinoWorld.Common.Systems
{
	/// <summary>
	/// Casino interiors cannot be mined, built on or blown up.
	/// Tile interaction (doors, tables) still works.
	/// </summary>
	public class CasinoTileProtection : GlobalTile
	{
		public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged) => !CasinoTravel.InCasino;

		public override bool CanPlace(int i, int j, int type) => !CasinoTravel.InCasino;

		public override bool CanReplace(int i, int j, int type, int tileTypeBeingPlaced) => !CasinoTravel.InCasino;

		public override bool CanExplode(int i, int j, int type) => !CasinoTravel.InCasino;

		public override bool Slope(int i, int j, int type) => !CasinoTravel.InCasino;
	}

	public class CasinoWallProtection : GlobalWall
	{
		public override bool CanPlace(int i, int j, int type) => !CasinoTravel.InCasino;

		public override void KillWall(int i, int j, int type, ref bool fail)
		{
			if (CasinoTravel.InCasino)
				fail = true;
		}

		public override bool CanExplode(int i, int j, int type) => !CasinoTravel.InCasino;

		public override void ModifyLight(int i, int j, int type, ref float r, ref float g, ref float b)
		{
			if (type == 0 || SubworldLibrary.SubworldSystem.Current is not Content.Subworlds.CasinoSubworld casino)
				return;
			Microsoft.Xna.Framework.Vector3 ambient = casino.Ambient;
			r = System.Math.Max(r, ambient.X);
			g = System.Math.Max(g, ambient.Y);
			b = System.Math.Max(b, ambient.Z);
		}
	}
}
