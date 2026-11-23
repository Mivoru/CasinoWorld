using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace CasinoWorld.Common.Systems
{
	/// <summary>Server-side settings: gambling on/off, video slot payout, announcements.</summary>
	public class CasinoConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;

		public static CasinoConfig Instance => ModContent.GetInstance<CasinoConfig>();

		/// <summary>Off: the casino tables, slots and crates take no bets (the casinos, NPCs and gear stay).</summary>
		[DefaultValue(true)]
		public bool GamblingEnabled;

		/// <summary>Multiplies every video slot payout. 1.00 = the tuned 97-99 % return.</summary>
		[Range(0.8f, 1.1f)]
		[Increment(0.01f)]
		[DefaultValue(1f)]
		[Slider]
		public float VideoSlotPayout;

		/// <summary>Chat messages about the hardmode casino opening and where it is.</summary>
		[DefaultValue(true)]
		public bool Announcements;

		/// <summary>High Stakes Night: the card army can raid the world at night in hardmode.</summary>
		[DefaultValue(true)]
		public bool HighStakesNight;
	}
}
