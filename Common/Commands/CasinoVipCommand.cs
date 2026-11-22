using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Tiles;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Commands
{
	/// <summary>
	/// Development command: builds the underground casino entrance where the player stands,
	/// so it can be tested without entering hardmode. Usage: /casinovip
	/// </summary>
	public class CasinoVipCommand : ModCommand
	{
		public override CommandType Type => CommandType.World;

		public override string Command => "casinovip";

		public override string Usage => "/casinovip";

		public override string Description => Language.GetTextValue("Mods.CasinoWorld.Commands.CasinoVip.Description");

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			Player player = caller.Player;
			int left = (int)(player.Center.X / 16f) - UndergroundCasinoFacade.Width / 2;
			int top = (int)((player.position.Y + player.height) / 16f) - UndergroundCasinoFacade.Height;

			UndergroundCasinoStructure.Build(left, top);
			caller.Reply(Language.GetTextValue("Mods.CasinoWorld.Commands.CasinoVip.Done"));
		}
	}
}
