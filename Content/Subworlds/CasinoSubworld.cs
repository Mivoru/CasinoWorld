using SubworldLibrary;

namespace CasinoWorld.Content.Subworlds
{
	/// <summary>
	/// Base class for every casino interior dimension.
	/// Shared rules: players keep their progress (money won or lost is saved),
	/// the interior is rebuilt on every visit, and world updates (time, spawns) are off.
	/// Each casino also sets its own betting limits.
	/// </summary>
	public abstract class CasinoSubworld : Subworld
	{
		// Player inventory and coins must persist, otherwise gambling would be free.
		public override bool NoPlayerSaving => false;

		// The interior is generated fresh on each visit; nothing in it needs saving yet.
		public override bool ShouldSave => false;

		public override bool NormalUpdates => false;

		/// <summary>Chip values offered at the tables, in copper coins, smallest first.</summary>
		public abstract long[] ChipValues { get; }

		/// <summary>Largest total bet allowed in one round, in copper coins.</summary>
		public abstract long MaxBet { get; }
	}
}
