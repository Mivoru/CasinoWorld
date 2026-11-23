using System;
using CasinoWorld.Common;
using CasinoWorld.Content.Buffs;
using CasinoWorld.Content.Projectiles;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
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
	public class WildCardRelicItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<WildCardRelic>());
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
	/// <summary>
	/// Mini slot machine pet. Its marquee lights blink; every 10-25 seconds it pulls its own lever and
	/// spins. One spin in eight hits 777: the window lights up, coins burst out and the owner gets a
	/// random helpful buff for two minutes (vanity pet, so nothing stronger than a basic potion).
	/// </summary>
	public class MiniSlotPet : ModProjectile
	{
		private const int Frames = 6, FrameIdle = 0, FrameJackpot = 4, FrameLever = 5;
		private const int SpinTicks = 80, JackpotTicks = 150;

		private static readonly int[] JackpotBuffs = {
			BuffID.Lucky, BuffID.Regeneration, BuffID.Swiftness, BuffID.Ironskin, BuffID.Shine,
			BuffID.Mining, BuffID.Endurance, BuffID.Wrath, BuffID.Rage, BuffID.Heartreach, BuffID.Builder,
		};

		private static Asset<Texture2D> glow;

		private ref float NextSpin => ref Projectile.localAI[0];
		private ref float SpinTimer => ref Projectile.localAI[1];
		private int jackpotTimer;

		public override void SetStaticDefaults()
		{
			Main.projFrames[Type] = Frames;
			Main.projPet[Type] = true;
		}

		public override void SetDefaults()
		{
			Projectile.CloneDefaults(ProjectileID.ZephyrFish);
			AIType = ProjectileID.ZephyrFish;
			Projectile.width = 26;
			Projectile.height = 30;
		}

		public override void Unload() => glow = null;

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

			if (NextSpin <= 0)
				NextSpin = Main.rand.Next(600, 1500);
			if (SpinTimer == 0 && jackpotTimer == 0 && --NextSpin <= 0)
				StartSpin();
			if (SpinTimer > 0 && ++SpinTimer >= SpinTicks)
				FinishSpin(player);
			if (jackpotTimer > 0)
				jackpotTimer--;

			float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f);
			float boost = jackpotTimer > 0 ? 1.6f : SpinTimer > 0 ? 1.15f : 1f;
			Lighting.AddLight(Projectile.Center, new Vector3(0.45f, 0.3f, 0.2f) * (0.6f + 0.4f * pulse) * boost);
			if (jackpotTimer > 0 && Main.rand.NextBool(3))
				Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12, 12), DustID.GoldCoin, Main.rand.NextVector2Circular(2, 2) - Vector2.UnitY, 0, default, 1.1f).noGravity = true;
		}

		private void StartSpin()
		{
			SpinTimer = 1;
			if (!Main.dedServ)
				SoundEngine.PlaySound(CasinoSounds.LeverPull with { Volume = 0.35f, Pitch = 0.3f }, Projectile.Center);
		}

		private void FinishSpin(Player player)
		{
			SpinTimer = 0;
			NextSpin = Main.rand.Next(600, 1500);
			bool jackpot = Main.rand.NextBool(8);
			if (Main.dedServ)
				return;
			SoundEngine.PlaySound(CasinoSounds.ReelStop with { Volume = 0.4f, Pitch = 0.4f }, Projectile.Center);
			if (!jackpot)
				return;
			jackpotTimer = JackpotTicks;
			SoundEngine.PlaySound(CasinoSounds.SmallWin with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
			CombatText.NewText(Projectile.Hitbox, new Color(255, 215, 60), "777!", true);
			for (int k = 0; k < 24; k++)
				Dust.NewDustPerfect(Projectile.Center, DustID.GoldCoin, Main.rand.NextVector2Circular(4, 4) - Vector2.UnitY * 2, 0, default, 1.3f);
			if (Projectile.owner == Main.myPlayer)
				player.AddBuff(Main.rand.Next(JackpotBuffs), 60 * 120);
		}

		public override void PostAI()
		{
			// Vanilla pet AI animates its own frames; pick ours after it.
			if (jackpotTimer > 0)
				Projectile.frame = FrameJackpot;
			else if (SpinTimer > 0)
				Projectile.frame = SpinTimer < 12 ? FrameLever : 1 + (int)(SpinTimer / 4) % 3;
			else
				Projectile.frame = FrameIdle;
			// A little hop when the lever is pulled and when the jackpot hits.
			if (SpinTimer is > 0 and < 10 || jackpotTimer > JackpotTicks - 10)
				Projectile.position.Y -= 1f;
		}

		public override void PostDraw(Color lightColor)
		{
			glow ??= ModContent.Request<Texture2D>(Texture + "_Glow");
			Texture2D tex = glow.Value;
			int h = tex.Height / Frames;
			var frame = new Rectangle(0, Projectile.frame * h, tex.Width, h);
			float pulse = 0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f);
			Color c = Color.White * (jackpotTimer > 0 ? 1f : pulse);
			c.A = 0;
			SpriteEffects fx = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			Vector2 pos = Projectile.Center - Main.screenPosition + new Vector2(0, Projectile.gfxOffY);
			Main.EntitySpriteDraw(tex, pos, frame, c, Projectile.rotation, frame.Size() / 2f, Projectile.scale, fx, 0);
			if (jackpotTimer > 0)
				Main.EntitySpriteDraw(tex, pos, frame, c * 0.5f, Projectile.rotation, frame.Size() / 2f, Projectile.scale * 1.25f, fx, 0);
		}
	}
}

namespace CasinoWorld.Content.Tiles
{
	/// <summary>
	/// Master mode relic: a gold figure of The House floating above a pedestal, with a soft
	/// pulsing glow (same idea as vanilla relics, drawn in SpecialDraw).
	/// </summary>
	public class WildCardRelic : ModTile
	{
		private const int FrameWidth = 18 * 3;
		private const int FrameHeight = 18 * 4;

		private Asset<Texture2D> relicTexture;

		public override string Texture => "CasinoWorld/Content/Tiles/WildCardRelicPedestal";

		public override void Load()
		{
			if (!Main.dedServ)
				relicTexture = ModContent.Request<Texture2D>("CasinoWorld/Content/Tiles/WildCardRelic");
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
