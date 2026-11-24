using Microsoft.Xna.Framework;
using Terraria.Localization;

namespace CasinoWorld.Common.Games
{
	/// <summary>Auto spin choices and button looks shared by the classic and the video slots.</summary>
	public static class AutoSpin
	{
		public static readonly int[] Counts = { 10, 25, 50, 100 };

		public static string Label(int left) => left > 0
			? Language.GetTextValue("Mods.CasinoWorld.UI.Slots.AutoStop", left)
			: Language.GetTextValue("Mods.CasinoWorld.UI.Slots.AutoStart");

		public static Color Tint(int left, bool main) => !main ? new Color(60, 60, 84) : left > 0 ? new Color(170, 40, 46) : new Color(40, 110, 160);
	}
}
