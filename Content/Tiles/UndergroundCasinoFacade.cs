using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Subworlds;
using Microsoft.Xna.Framework;

namespace CasinoWorld.Content.Tiles
{
	/// <summary>Vault entrance of the hardmode underground casino. Opens when the world enters hardmode.</summary>
	public partial class UndergroundCasinoFacade : CasinoEntrance
	{
		public const int Width = 20;
		public const int Height = 12;

		public override int EntranceWidth => Width;
		public override int EntranceHeight => Height;
		public override Rectangle DoorArea => new(7, 5, 6, 7);
		protected override byte[,] Light => LightMap;
		protected override Color MapColor => new(120, 60, 170);
		public override Vector3 LightColor => new(0.95f, 0.45f, 1.05f);

		public override void Enter() => CasinoTravel.Enter<UndergroundCasino>();
	}
}
