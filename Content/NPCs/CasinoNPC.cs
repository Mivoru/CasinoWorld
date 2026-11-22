using CasinoWorld.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

		/// <summary>Own sprite sheet (Content/NPCs/People, rendered in Blender); null falls back to the vanilla look.</summary>
		protected virtual string CustomTexture => null;

		/// <summary>Frames in the custom sheet.</summary>
		protected virtual int CustomFrames => 1;

		public override string Texture => CustomTexture ?? "Terraria/Images/NPC_" + VanillaLook;

		/// <summary>
		/// Vanilla-style look drawn with the player renderer. When set, it replaces the sprite sheet
		/// (which then only serves as the map/fallback texture).
		/// </summary>
		protected virtual PersonLook Look => null;

		/// <summary>True inside the high rollers casino, where the staff wear black and gold uniforms.</summary>
		protected static bool HighRollers => SubworldLibrary.SubworldSystem.Current is Subworlds.UndergroundCasino;

		/// <summary>Body pose for this tick (see <see cref="PersonDoll"/>), or -1 for the pose from movement.</summary>
		protected virtual int BodyPose(int tick) => -1;

		/// <summary>Leg pose for this tick, or -1 for the pose from movement.</summary>
		protected virtual int LegPose(int tick) => -1;

		/// <summary>Extra vertical offset in pixels (for example a dance hop).</summary>
		protected virtual float DrawLift(int tick) => 0f;

		/// <summary>Extra horizontal offset in pixels (for example circling a pole).</summary>
		protected virtual float DrawShiftX(int tick) => 0f;

		/// <summary>Body rotation in radians.</summary>
		protected virtual float DrawRotation(int tick) => 0f;

		/// <summary>Facing for this tick (0 = the NPC's own direction).</summary>
		protected virtual int DrawFacing(int tick) => 0;

		private PersonDoll doll;
		private bool dollFailed;

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			PersonLook look = Look;
			if (look == null || dollFailed || NPC.IsABestiaryIconDummy)
				return DrawSprite(spriteBatch, screenPos, drawColor);
			try {
				doll ??= new PersonDoll();
				int tick = (int)Main.GameUpdateCount + NPC.whoAmI * 37;
				bool talking = Main.LocalPlayer.talkNPC == NPC.whoAmI;
				doll.BodyPose = talking ? -1 : BodyPose(tick);
				doll.LegPose = talking ? -1 : LegPose(tick);
				doll.Rotation = talking ? 0f : DrawRotation(tick);
				doll.Facing = talking ? 0 : DrawFacing(tick);
				Vector2 shift = talking ? Vector2.Zero : new Vector2(DrawShiftX(tick), -DrawLift(tick));
				doll.Draw(NPC, look, NPC.Bottom + new Vector2(0f, NPC.gfxOffY) + shift);
			}
			catch (System.Exception e) {
				// Never crash the game over a cosmetic: fall back to the sprite sheet for good.
				dollFailed = true;
				Mod.Logger.Warn("Casino person renderer failed, using sprite: " + e.Message);
			}
			return false;
		}

		/// <summary>Draws the sprite sheet (fallback); returns whether vanilla should draw instead.</summary>
		protected virtual bool DrawSprite(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => true;

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = CustomTexture != null ? CustomFrames : Main.npcFrameCount[VanillaLook];
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
