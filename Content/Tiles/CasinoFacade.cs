using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Subworlds;
using Microsoft.Xna.Framework;

namespace CasinoWorld.Content.Tiles
{
	/// <summary>Front of the pre-hardmode surface casino, built with the Casino Blueprint.</summary>
	public partial class CasinoFacade : CasinoEntrance
	{
		public const int Width = 24;
		public const int Height = 16;

		public override int EntranceWidth => Width;
		public override int EntranceHeight => Height;
		public override Rectangle DoorArea => new(10, 11, 4, 5);
		protected override byte[,] Light => LightMap;
		protected override Color MapColor => new(140, 22, 38);
		protected override (byte r, byte g, byte b)[] MapPalette => FacadeMapColors.CasinoFacadePalette;
		protected override byte[,] MapIndex => FacadeMapColors.CasinoFacadeIndex;

		public override void Enter() => CasinoTravel.Enter<SurfaceCasino>();
	}
}
