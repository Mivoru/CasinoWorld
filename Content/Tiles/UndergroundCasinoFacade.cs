using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Subworlds;
using Microsoft.Xna.Framework;

namespace CasinoWorld.Content.Tiles
{
	/// <summary>
	/// Vault entrance of the hardmode underground casino, set into a rock arch with crystals, mine timbers
	/// and a neon sign (rendered in Blender: blender/CasinoWorld/render_underground.py). Opens when the world
	/// enters hardmode. Its ragged rock edge is transparent so it blends into the surrounding cave.
	/// </summary>
	public partial class UndergroundCasinoFacade : CasinoEntrance
	{
		public const int Width = 28;
		public const int Height = 18;

		public override int EntranceWidth => Width;
		public override int EntranceHeight => Height;
		public override Rectangle DoorArea => new(11, 10, 6, 8);
		protected override byte[,] Light => LightMap;
		protected override Color MapColor => new(120, 60, 170);
		protected override (byte r, byte g, byte b)[] MapPalette => FacadeMapColors.UndergroundCasinoFacadePalette;
		protected override byte[,] MapIndex => FacadeMapColors.UndergroundCasinoFacadeIndex;
		public override Vector3 LightColor => new(0.95f, 0.45f, 1.05f);

		public override void Enter() => CasinoTravel.Enter<UndergroundCasino>();
	}
}
