using System.IO;
using CasinoWorld.Common.Net;
using Terraria.ModLoader;

namespace CasinoWorld
{
	public class CasinoWorld : Mod
	{
		public override void HandlePacket(BinaryReader reader, int whoAmI) => CasinoNet.Handle(reader, whoAmI);
	}
}
