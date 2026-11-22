using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CasinoWorld.Content.NPCs
{
	/// <summary>
	/// How a casino person looks: a vanilla character (hair, skin, clothing style and colours)
	/// plus optional vanity items. Drawn with the vanilla player renderer, so casino people match
	/// vanilla town NPCs exactly: same proportions, arms, hair, walk cycle and lighting.
	/// </summary>
	public sealed class PersonLook
	{
		public int Hair;
		public int SkinVariant;            // vanilla clothing style: 0-3 and 8 male, 4-7 and 9 female
		public Color Skin = new(255, 200, 160);
		public Color HairColor = new(80, 50, 30);
		public Color Eyes = new(60, 90, 120);
		public Color Shirt = Color.White;
		public Color UnderShirt = Color.White;
		public Color Pants = new(30, 30, 40);
		public Color Shoes = new(20, 20, 20);
		public int HeadVanity;             // item types, 0 for none
		public int BodyVanity;
		public int LegsVanity;
		public int Accessory;              // one visible accessory (for example sunglasses), 0 for none

		private static readonly Color[] SkinTones = {
			new(255, 220, 190), new(240, 190, 150), new(215, 160, 120), new(180, 120, 85), new(140, 90, 60), new(105, 65, 45),
		};
		private static readonly Color[] HairColors = {
			new(30, 25, 25), new(80, 50, 30), new(130, 80, 40), new(200, 150, 70), new(230, 200, 120), new(170, 60, 40),
			new(200, 200, 205), new(60, 40, 35),
		};
		private static readonly Color[] Outfits = {
			new(170, 40, 50), new(40, 70, 150), new(40, 110, 70), new(120, 60, 150), new(200, 160, 50), new(60, 60, 70),
			new(220, 120, 160), new(90, 160, 200), new(230, 230, 225), new(120, 80, 50),
		};
		private static readonly int[] GuestHats = {
			0, 0, 0, 0, ItemID.TopHat, ItemID.Fedora, ItemID.SummerHat, ItemID.SailorHat, ItemID.Fez, ItemID.GangstaHat,
		};

		/// <summary>A random but repeatable guest look (same seed gives the same person on every client).</summary>
		public static PersonLook RandomGuest(int seed)
		{
			var rand = new UnifiedRandom(seed);
			bool female = rand.NextBool();
			var look = new PersonLook {
				SkinVariant = female ? rand.Next(4, 8) : rand.Next(0, 4),
				Hair = female ? Pick(rand, 5, 6, 7, 8, 9, 10, 19, 20, 21, 25, 26, 51, 52) : Pick(rand, 0, 1, 2, 3, 4, 11, 12, 13, 15, 16, 17, 23, 24, 30),
				Skin = SkinTones[rand.Next(SkinTones.Length)],
				HairColor = HairColors[rand.Next(HairColors.Length)],
				Eyes = Color.Lerp(new Color(40, 60, 90), new Color(90, 60, 40), rand.NextFloat()),
				Shirt = Outfits[rand.Next(Outfits.Length)],
				UnderShirt = Outfits[rand.Next(Outfits.Length)],
				Pants = Color.Lerp(Outfits[rand.Next(Outfits.Length)], Color.Black, 0.5f),
				Shoes = Color.Lerp(Outfits[rand.Next(Outfits.Length)], Color.Black, 0.6f),
				HeadVanity = GuestHats[rand.Next(GuestHats.Length)],
			};
			if (rand.NextBool(8))
				look.Accessory = ItemID.Sunglasses;
			if (rand.NextBool(6)) {
				look.BodyVanity = female ? ItemID.PrincessDress : ItemID.TuxedoShirt;
				look.LegsVanity = female ? 0 : ItemID.TuxedoPants;
			}
			return look;
		}

		private static int Pick(UnifiedRandom rand, params int[] options) => options[rand.Next(options.Length)];
	}

	/// <summary>
	/// A private Player used only to draw a casino person. Each NPC keeps one, so the vanilla walk
	/// cycle (legFrameCounter) keeps running between frames.
	/// </summary>
	public sealed class PersonDoll
	{
		public const int FrameHeight = 56;

		// Vanilla body frame rows: 0 idle, 1 arm up, 2 arm up-forward, 3 arm forward, 4 arm down-forward, 5 jump.
		public const int ArmUp = 1, ArmUpForward = 2, ArmForward = 3, ArmDown = 4, Jump = 5;

		private readonly Player player = new();
		private PersonLook appliedLook;

		/// <summary>Body pose override for this frame (-1 = vanilla pose from movement).</summary>
		public int BodyPose = -1;

		/// <summary>Leg pose override for this frame (-1 = vanilla pose from movement).</summary>
		public int LegPose = -1;

		/// <summary>Rotation around the body centre (radians), for acrobatics such as pole spins.</summary>
		public float Rotation;

		/// <summary>Facing override for this frame (0 = from the NPC).</summary>
		public int Facing;

		private void Apply(PersonLook look)
		{
			if (ReferenceEquals(appliedLook, look))
				return;
			appliedLook = look;
			player.hair = look.Hair;
			player.skinVariant = look.SkinVariant;
			player.skinColor = look.Skin;
			player.hairColor = look.HairColor;
			player.eyeColor = look.Eyes;
			player.shirtColor = look.Shirt;
			player.underShirtColor = look.UnderShirt;
			player.pantsColor = look.Pants;
			player.shoeColor = look.Shoes;
			for (int k = 0; k < player.armor.Length; k++)
				player.armor[k] = new Item();
			if (look.HeadVanity > 0)
				player.armor[10] = new Item(look.HeadVanity);
			if (look.BodyVanity > 0)
				player.armor[11] = new Item(look.BodyVanity);
			if (look.LegsVanity > 0)
				player.armor[12] = new Item(look.LegsVanity);
			if (look.Accessory > 0)
				player.armor[13] = new Item(look.Accessory);
		}

		/// <summary>Draws the person standing on <paramref name="bottom"/> (world coordinates).</summary>
		public void Draw(NPC npc, PersonLook look, Vector2 bottom)
		{
			Apply(look);
			player.direction = Facing != 0 ? Facing : npc.spriteDirection == 0 ? npc.direction : npc.spriteDirection;
			player.velocity = new Vector2(npc.velocity.X, 0f);
			player.position = new Vector2(bottom.X - player.width / 2f, bottom.Y - player.height);

			player.ResetEffects();
			player.ResetVisibleAccessories();
			player.UpdateMiscCounter();
			player.UpdateDyes();
			player.PlayerFrame();

			// Vanity always shows (PlayerFrame fills head/body/legs from the armour slots).
			if (look.HeadVanity > 0)
				player.head = player.armor[10].headSlot;
			if (look.BodyVanity > 0)
				player.body = player.armor[11].bodySlot;
			if (look.LegsVanity > 0)
				player.legs = player.armor[12].legSlot;
			if (look.Accessory > 0 && player.armor[13].faceSlot > 0)
				player.face = player.armor[13].faceSlot;

			// Vanilla loads these textures lazily for real players; do the same for the doll.
			Main.instance.LoadHair(player.hair);
			if (player.head > 0)
				Main.instance.LoadArmorHead(player.head);
			if (player.body > 0)
				Main.instance.LoadArmorBody(player.body);
			if (player.legs > 0)
				Main.instance.LoadArmorLegs(player.legs);

			if (BodyPose >= 0)
				player.bodyFrame.Y = player.headFrame.Y = BodyPose * FrameHeight;
			if (LegPose >= 0)
				player.legFrame.Y = LegPose * FrameHeight;

			Main.PlayerRenderer.DrawPlayer(Main.Camera, player, player.position, Rotation, player.Size / 2f);
		}
	}
}
