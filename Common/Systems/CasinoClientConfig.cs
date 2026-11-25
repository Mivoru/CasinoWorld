using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace CasinoWorld.Common.Systems
{
	public enum PotMeterPlace { Hud, UnderPlayer }

	/// <summary>Client-side settings: where the Pot meter is drawn.</summary>
	public class CasinoClientConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		public static CasinoClientConfig Instance => ModContent.GetInstance<CasinoClientConfig>();

		[DefaultValue(PotMeterPlace.Hud)]
		public PotMeterPlace PotMeter;

		/// <summary>Screen shake, flashes, zoom and the jackpot marquee.</summary>
		[DefaultValue(true)]
		public bool ScreenEffects;

		/// <summary>The three kinds of screen effect one by one; all of them need ScreenEffects.</summary>
		[DefaultValue(true)]
		public bool ScreenShake;

		[DefaultValue(true)]
		public bool Flashes;

		[DefaultValue(true)]
		public bool CameraZoom;

		public static bool Shaking => Instance.ScreenEffects && Instance.ScreenShake;
		public static bool Flashing => Instance.ScreenEffects && Instance.Flashes;
		public static bool Zooming => Instance.ScreenEffects && Instance.CameraZoom;
	}
}
