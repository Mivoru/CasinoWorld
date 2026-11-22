using System;
using CasinoWorld.Content.Buffs;
using CasinoWorld.Content.Projectiles;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CasinoWorld.Content.Items.Vanity
{
	/// <summary>Master mode relic of The House.</summary>
	public class TheHouseRelicItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<TheHouseRelic>());
			Item.width = 28;
			Item.height = 34;
			Item.rare = ItemRarityID.Master;
			Item.master = true;
			Item.value = Item.buyPrice(gold: 5);
		}
	}

	/// <summary>Master mode pet: a little slot machine that follows you and spins its reels.</summary>
	public class MiniSlotItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.CloneDefaults(ItemID.ZephyrFish);
			Item.shoot = ModContent.ProjectileType<MiniSlotPet>();
			Item.buffType = ModContent.BuffType<MiniSlotBuff>();
			Item.rare = ItemRarityID.Master;
			Item.master = true;
			Item.width = Item.height = 22;
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer)
				player.AddBuff(Item.buffType, 3600);
			return true;
		}
	}
}

namespace CasinoWorld.Content.Buffs
{
	public class MiniSlotBuff : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.buffNoTimeDisplay[Type] = true;
			Main.vanityPet[Type] = true;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			bool unused = false;
			player.BuffHandle_SpawnPetIfNeededAndSetTime(buffIndex, ref unused, ModContent.ProjectileType<MiniSlotPet>());
		}
	}
}

namespace CasinoWorld.Content.Projectiles
{
	public class MiniSlotPet : ModProjectile
	{
		public override void SetStaticDefaults()
		{
			Main.projFrames[Type] = 4;
			Main.projPet[Type] = true;
		}

		public override void SetDefaults()
		{
			Projectile.CloneDefaults(ProjectileID.ZephyrFish);
			AIType = ProjectileID.ZephyrFish;
			Projectile.width = 26;
			Projectile.height = 30;
		}

		public override bool PreAI()
		{
			Main.player[Projectile.owner].zephyrfish = false;
			return true;
		}

		public override void AI()
		{
			Player player = Main.player[Projectile.owner];
			if (!player.dead && player.HasBuff(ModContent.BuffType<MiniSlotBuff>()))
				Projectile.timeLeft = 2;
			Lighting.AddLight(Projectile.Center, 0.35f, 0.2f, 0.3f);
			if (Main.rand.NextBool(240))
				CombatText.NewText(Projectile.Hitbox, new Color(255, 215, 60), Main.rand.NextBool(8) ? "777!" : "ding!");
		}
	}
}

namespace CasinoWorld.Content.Tiles
{
	/// <summary>
	/// Master mode relic: a gold figure of The House floating above a pedestal, with a soft
	/// pulsing glow (same idea as vanilla relics, drawn in SpecialDraw).
	/// </summary>
	public class TheHouseRelic : ModTile
	{
		private const int FrameWidth = 18 * 3;
		private const int FrameHeight = 18 * 4;

		private Asset<Texture2D> relicTexture;

		public override string Texture => "CasinoWorld/Content/Tiles/TheHouseRelicPedestal";

		public override void Load()
		{
			if (!Main.dedServ)
				relicTexture = ModContent.Request<Texture2D>("CasinoWorld/Content/Tiles/TheHouseRelic");
		}

		public override void Unload()
		{
			relicTexture = null;
		}

		public override void SetStaticDefaults()
		{
			Main.tileShine[Type] = 400;
			Main.tileFrameImportant[Type] = true;
			TileID.Sets.InteractibleByNPCs[Type] = true;
			TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
			TileObjectData.newTile.LavaDeath = false;
			TileObjectData.newTile.DrawYOffset = 2;
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(233, 207, 94), Language.GetText("MapObject.Relic"));
		}

		public override bool CreateDust(int i, int j, ref int type) => false;

		public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
		{
			if (drawData.tileFrameX % FrameWidth == 0 && drawData.tileFrameY % FrameHeight == 0)
				Main.instance.TilesRenderer.AddSpecialLegacyPoint(i, j);
		}

		public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Vector2 offScreen = new(Main.offScreenRange);
			if (Main.drawToScreen)
				offScreen = Vector2.Zero;

			Point p = new(i, j);
			Tile tile = Main.tile[p.X, p.Y];
			if (tile == null || !tile.HasTile)
				return;

			Texture2D texture = relicTexture.Value;
			Vector2 worldPos = p.ToWorldCoordinates(24f, 64f);
			Color color = Lighting.GetColor(p.X, p.Y);
			Vector2 origin = new(texture.Width / 2f, texture.Height);

			const float TwoPi = (float)Math.PI * 2f;
			float offset = (float)Math.Sin(Main.GlobalTimeWrappedHourly * TwoPi / 5f);
			Vector2 drawPos = worldPos + offScreen - Main.screenPosition + new Vector2(0f, -40f) + new Vector2(0f, offset * 4f);

			spriteBatch.Draw(texture, drawPos, null, color, 0f, origin, 1f, SpriteEffects.None, 0f);

			float scale = (float)Math.Sin(Main.GlobalTimeWrappedHourly * TwoPi / 2f) * 0.3f + 0.7f;
			Color effectColor = color;
			effectColor.A = 0;
			effectColor = effectColor * 0.1f * scale;
			for (float n = 0f; n < 1f; n += 1f / 6f)
				spriteBatch.Draw(texture, drawPos + (TwoPi * n).ToRotationVector2() * (6f + offset * 2f), null, effectColor, 0f, origin, 1f, SpriteEffects.None, 0f);
		}
	}
}
