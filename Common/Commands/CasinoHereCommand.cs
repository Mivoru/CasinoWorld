using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Tiles;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Commands
{
	/// <summary>
	/// Development command: builds the surface casino where the player stands.
	/// Lets existing worlds get a casino without creating a new world.
	/// Usage: /casinohere
	/// </summary>
	public class CasinoHereCommand : ModCommand
	{
		public override CommandType Type => CommandType.World;

		public override string Command => "casinohere";

		public override string Usage => "/casinohere";

		public override string Description => Language.GetTextValue("Mods.CasinoWorld.Commands.CasinoHere.Description");

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			Player player = caller.Player;
			int left = (int)(player.Center.X / 16f) - CasinoFacade.Width / 2;
			int groundY = (int)((player.position.Y + player.height) / 16f);

			SurfaceCasinoStructure.Build(left, groundY);
			caller.Reply(Language.GetTextValue("Mods.CasinoWorld.Commands.CasinoHere.Done"));
		}
	}
}
