using CasinoWorld.Common.Systems;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.NPCs
{
	/// <summary>
	/// Base for the people inside a casino: friendly, unkillable, never despawn,
	/// can be talked to, and hidden from the bestiary.
	/// Until custom sprites exist they borrow a vanilla town NPC's look (<see cref="VanillaLook"/>).
	/// </summary>
	public abstract class CasinoNPC : ModNPC
	{
		/// <summary>Vanilla NPC whose sprite sheet this NPC uses as a placeholder.</summary>
		protected abstract int VanillaLook { get; }

		/// <summary>Number of lines under Mods.CasinoWorld.NPCs.[Name].Chat in the localization file.</summary>
		protected abstract int ChatLines { get; }

		public override string Texture => "Terraria/Images/NPC_" + VanillaLook;

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = Main.npcFrameCount[VanillaLook];
			NPCID.Sets.NPCBestiaryDrawOffset[Type] = new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true };
			NPCID.Sets.ActsLikeTownNPC[Type] = true;
			NPCID.Sets.NoTownNPCHappiness[Type] = true;
		}

		public override void SetDefaults()
		{
			NPC.width = 18;
			NPC.height = 40;
			NPC.aiStyle = -1;
			NPC.friendly = true;
			NPC.damage = 0;
			NPC.defense = 15;
			NPC.lifeMax = 250;
			NPC.knockBackResist = 0f;
			NPC.dontTakeDamage = true;
			NPC.dontTakeDamageFromHostiles = true;
			NPC.immortal = true;
			NPC.npcSlots = 0f;
			NPC.HitSound = SoundID.NPCHit1;
		}

		public override bool CanChat() => true;

		public override bool CheckActive() => false;

		public override bool? CanBeHitByItem(Player player, Item item) => false;

		public override bool? CanBeHitByProjectile(Projectile projectile) => false;

		public override string GetChat() => ChatLine(Main.rand.Next(ChatLines));

		protected string ChatLine(int index) => Language.GetTextValue($"Mods.CasinoWorld.NPCs.{Name}.Chat.{index}");

		/// <summary>Turns the NPC towards the local player while they talk to it.</summary>
		protected void FaceTalkingPlayer()
		{
			Player player = Main.LocalPlayer;
			if (player.talkNPC == NPC.whoAmI) {
				NPC.direction = player.Center.X < NPC.Center.X ? -1 : 1;
				NPC.spriteDirection = NPC.direction;
			}
		}
	}

	/// <summary>Freezes time and blocks enemy spawns inside casinos.</summary>
	public class CasinoSpawnBlocker : GlobalNPC
	{
		public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
		{
			if (CasinoTravel.InCasino)
				maxSpawns = 0;
		}
	}
}
