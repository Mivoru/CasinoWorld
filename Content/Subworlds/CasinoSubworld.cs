using SubworldLibrary;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Subworlds
{
	/// <summary>
	/// Base class for every casino interior dimension.
	/// Shared rules: players keep their progress (money won or lost is saved),
	/// the interior is rebuilt on every visit, and time, enemy spawns and events are off.
	/// Each casino also sets its own betting limits and spawns its own staff and guests.
	/// </summary>
	public abstract class CasinoSubworld : Subworld
	{
		// Player inventory and coins must persist, otherwise gambling would be free.
		public override bool NoPlayerSaving => false;

		// The interior is generated fresh on each visit; nothing in it needs saving yet.
		public override bool ShouldSave => false;

		// Normal updates keep NPCs (staff and guests) running. Spawns, time and events are
		// switched off separately by CasinoSpawnBlocker and CasinoTimeFreeze.
		public override bool NormalUpdates => true;

		/// <summary>Chip values offered at the tables, in copper coins, smallest first.</summary>
		public abstract long[] ChipValues { get; }

		/// <summary>Largest total bet allowed in one round, in copper coins.</summary>
		public abstract long MaxBet { get; }

		/// <summary>Smallest stake that can drop a jackpot item, in copper.</summary>
		public virtual long MinJackpotStake => 10 * Common.Games.Coins.Silver;

		/// <summary>Stake at which the jackpot item chance reaches its full value, in copper.</summary>
		public virtual long FullJackpotStake => Common.Games.Coins.Gold;

		/// <summary>Items that big wins in this casino can drop (Gambler weapons and armour).</summary>
		public abstract int[] JackpotItems { get; }

		/// <summary>Tile x positions guests walk between (tables, machines, bar).</summary>
		public abstract int[] GuestSpots { get; }

		/// <summary>Spawns the staff and guests. Runs on the server or in single player only.</summary>
		protected abstract void SpawnPeople();

		public override void OnLoad()
		{
			Main.dayTime = true;
			Main.time = 27000;
			if (Main.netMode != NetmodeID.MultiplayerClient)
				SpawnPeople();
		}

		/// <summary>
		/// Soft ambient light given off by every background wall of the hall (see CasinoWallProtection),
		/// so the room is evenly lit like a real interior instead of patchy torchlight.
		/// </summary>
		public virtual Microsoft.Xna.Framework.Vector3 Ambient => new(0.30f, 0.22f, 0.15f);

		/// <summary>Spawns a casino NPC standing on the floor at the given tile.</summary>
		protected static void SpawnAt<T>(int tileX, int floorY, int facing = 1) where T : ModNPC
		{
			NPC.NewNPC(new EntitySource_SpawnNPC(), tileX * 16 + 8, floorY * 16, ModContent.NPCType<T>(), ai0: facing);
		}
	}
}
