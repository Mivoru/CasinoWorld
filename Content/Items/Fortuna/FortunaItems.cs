using System;
using CasinoWorld.Common;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Gambler;
using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Items.Crates;
using CasinoWorld.Content.Items.Weapons;
using CasinoWorld.Content.Projectiles;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;
using FortunaNPC = CasinoWorld.Content.NPCs.Boss.Fortuna;

namespace CasinoWorld.Content.Items.Fortuna
{
	/// <summary>Summons Fortuna anywhere in the main world after the Moon Lord.</summary>
	public class FatefulCoin : ModItem
	{
		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 3;
			ItemID.Sets.SortingPriorityBossSpawns[Type] = 19;
		}

		public override void SetDefaults()
		{
			Item.width = Item.height = 32;
			Item.maxStack = 20;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = Item.useAnimation = 40;
			Item.consumable = true;
			Item.rare = ItemRarityID.Red;
			Item.value = Item.buyPrice(gold: 50);
		}

		public override bool CanUseItem(Player player)
			=> !SubworldLibrary.SubworldSystem.AnyActive() && NPC.downedMoonlord && !NPC.AnyNPCs(ModContent.NPCType<FortunaNPC>());

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer)
				return true;
			SoundEngine.PlaySound(CasinoSounds.DuelToss, player.position);
			int type = ModContent.NPCType<FortunaNPC>();
			if (Main.netMode != NetmodeID.MultiplayerClient)
				NPC.SpawnOnPlayer(player.whoAmI, type);
			else
				NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: player.whoAmI, number2: type);
			return true;
		}

		public override void AddRecipes() => CreateRecipe()
			.AddIngredient(ItemID.LunarBar, 5)
			.AddIngredient(ItemID.FragmentSolar, 5).AddIngredient(ItemID.FragmentVortex, 5)
			.AddIngredient(ItemID.FragmentNebula, 5).AddIngredient(ItemID.FragmentStardust, 5)
			.AddIngredient(ModContent.ItemType<LoyaltyToken>(), 100)
			.AddTile(TileID.LunarCraftingStation).Register();
	}

	/// <summary>Material: buys the weapon that did not drop.</summary>
	public class FateShard : ModItem
	{
		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 25;
			ItemID.Sets.ItemNoGravity[Type] = true;
		}

		public override void SetDefaults()
		{
			Item.width = 26;
			Item.height = 32;
			Item.maxStack = Item.CommonMaxStack;
			Item.rare = ItemRarityID.Red;
			Item.value = Item.sellPrice(gold: 1);
		}

		public override Color? GetAlpha(Color lightColor) => Color.Lerp(lightColor, Color.White, 0.6f);

		public override void PostUpdate() => Lighting.AddLight(Item.Center, 0.5f, 0.3f, 0.8f);
	}

	public class FortunaBag : ModItem
	{
		public override void SetStaticDefaults()
		{
			ItemID.Sets.BossBag[Type] = true;
			Item.ResearchUnlockCount = 3;
		}

		public override void SetDefaults()
		{
			Item.maxStack = Item.CommonMaxStack;
			Item.consumable = true;
			Item.width = 34;
			Item.height = 36;
			Item.rare = ItemRarityID.Purple;
			Item.expert = true;
		}

		public override bool CanRightClick() => true;

		public override void ModifyItemLoot(ItemLoot itemLoot)
		{
			itemLoot.Add(ItemDropRule.OneFromOptions(1, ModContent.ItemType<Ophanim>(), ModContent.ItemType<DiceOfDestiny>(), ModContent.ItemType<EyeOfFortune>()));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<FateShard>(), 1, 10, 15));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<LoyaltyToken>(), 1, 400, 500));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<HighRollerCrate>(), 1, 5, 7));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<FortunasBlindfold>()));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<FortunaMask>(), 7));
			itemLoot.Add(ItemDropRule.CoinsBasedOnNPCValue(ModContent.NPCType<FortunaNPC>()));
		}
	}

	/// <summary>Expert: every tenth gambler hit is a jackpot (measured in tools/gambler_dps.py).</summary>
	public class FortunasBlindfold : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 36;
			Item.height = 30;
			Item.accessory = true;
			Item.expert = true;
			Item.rare = ItemRarityID.Expert;
			Item.value = Item.sellPrice(gold: 25);
		}

		public override void UpdateAccessory(Player player, bool hideVisual) => player.GetModPlayer<GamblerPlayer>().Blindfold = true;
	}

	[AutoloadEquip(EquipType.Head)]
	public class FortunaMask : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = Item.height = 36;
			Item.vanity = true;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.sellPrice(gold: 1);
		}
	}

	public class FortunaTrophy : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<FortunaTrophyTile>());
			Item.width = Item.height = 32;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.buyPrice(gold: 1);
		}
	}

	public class FortunaTrophyTile : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.FramesOnKillWall[Type] = true;
			TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3Wall);
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(120, 85, 60), Language.GetText("MapObject.Trophy"));
			DustType = DustID.WoodFurniture;
		}
	}

	public class FortunaRelicItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<FortunaRelic>());
			Item.width = 30;
			Item.height = 40;
			Item.rare = ItemRarityID.Master;
			Item.master = true;
			Item.value = Item.buyPrice(gold: 5);
		}
	}

	/// <summary>Master mode relic: Fortuna in gold on the vanilla relic pedestal.</summary>
	public class FortunaRelic : PitBoss.PitBossRelic
	{
		protected override string StatueTexture => "CasinoWorld/Content/Items/Fortuna/FortunaRelicStatue";
	}

	// ------------------------------------------------------------------ weapons

	/// <summary>Ophanim: hold to send out a wheel that stays at the cursor, saws, and throws chips.</summary>
	public class Ophanim : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 132;
			Item.knockBack = 2f;
			Item.useTime = Item.useAnimation = 20;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.channel = true;
			Item.shoot = ModContent.ProjectileType<OphanimWheel>();
			Item.shootSpeed = 18f;
			Item.UseSound = CasinoSounds.OphanimSpin;
			Item.width = Item.height = 38;
			Item.value = Item.sellPrice(gold: 25);
			Item.rare = ItemRarityID.Red;
		}

		public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] == 0;

		public override void AddRecipes() => Shards(this);

		internal static void Shards(ModItem item) => item.CreateRecipe().AddIngredient(ModContent.ItemType<FateShard>(), 18).AddTile(ModContent.TileType<UpgradeMachine>()).Register();
	}

	/// <summary>Dice of Destiny: two star dice per throw; doubles burst harder, a seven is a nova of chips.</summary>
	public class DiceOfDestiny : GamblerWeapon
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 190;
			Item.knockBack = 6f;
			Item.useTime = Item.useAnimation = 22;
			Item.shoot = ModContent.ProjectileType<DestinyDie>();
			Item.shootSpeed = 15f;
			Item.UseSound = CasinoSounds.DiceThrow;
			Item.width = 40;
			Item.height = 36;
			Item.value = Item.sellPrice(gold: 25);
			Item.rare = ItemRarityID.Red;
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			int a = Main.rand.Next(1, 7), b = Main.rand.Next(1, 7);
			int luck = a == b ? 2 : a + b == 7 ? 1 : 0;
			for (int k = 0; k < 2; k++)
				Projectile.NewProjectile(source, position, velocity.RotatedBy((k - 0.5f) * 0.09f), type, damage, knockback, player.whoAmI, k == 0 ? a : b, luck);
			if (luck > 0)
				CombatText.NewText(player.Hitbox, luck == 2 ? FortunaNPC.GoldLight : FortunaNPC.FateViolet, Language.GetTextValue(luck == 2 ? "Mods.CasinoWorld.Gambler.Doubles" : "Mods.CasinoWorld.Gambler.LuckySeven"));
			return false;
		}

		public override void AddRecipes() => Ophanim.Shards(this);
	}

	/// <summary>Eye of Fortune: instant golden rays that go through everything; every eighth is a triple.</summary>
	public class EyeOfFortune : GamblerWeapon
	{
		private int shots;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 132;
			Item.knockBack = 2f;
			Item.useTime = Item.useAnimation = 9;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.noUseGraphic = false;
			Item.shoot = ModContent.ProjectileType<FortuneRay>();
			Item.shootSpeed = 10f;
			Item.UseSound = CasinoSounds.SlotBlaster with { Pitch = 0.4f, Volume = 0.5f };
			Item.width = Item.height = 44;
			Item.value = Item.sellPrice(gold: 25);
			Item.rare = ItemRarityID.Red;
		}

		public override void SetStaticDefaults() => Item.staff[Type] = true;

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			Vector2 tip = position + velocity.SafeNormalize(Vector2.UnitX) * 50f;
			if (!Collision.CanHit(position, 0, 0, tip, 0, 0))
				tip = position;
			bool triple = ++shots % 8 == 0;
			if (triple)
				SoundEngine.PlaySound(CasinoSounds.JackpotProc with { Volume = 0.4f }, position);
			for (int k = triple ? -1 : 0; k <= (triple ? 1 : 0); k++)
				Projectile.NewProjectile(source, tip, velocity.RotatedBy(k * 0.12f), type, damage, knockback, player.whoAmI, triple ? 1f : 0f);
			return false;
		}

		public override void AddRecipes() => Ophanim.Shards(this);
	}
}

namespace CasinoWorld.Content.Projectiles
{
	/// <summary>Ophanim's wheel: flies to the cursor while the button is held, then comes home.</summary>
	public class OphanimWheel : ModProjectile
	{
		private float spin;

		public override void SetStaticDefaults() => CasinoFx.EnableTrail(Type, 8);

		public override void SetDefaults()
		{
			Projectile.width = Projectile.height = 46;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.timeLeft = 600;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 8;
			Projectile.netImportant = true;
		}

		public override void AI()
		{
			Player player = Main.player[Projectile.owner];
			if (player.dead || !player.active) {
				Projectile.Kill();
				return;
			}
			Projectile.timeLeft = 60;
			bool held = player.channel && player.HeldItem.type == ModContent.ItemType<Items.Fortuna.Ophanim>() && Projectile.ai[1] == 0f;
			if (held) {
				player.itemTime = player.itemAnimation = 2;
				player.heldProj = Projectile.whoAmI;
				player.ChangeDir(Projectile.Center.X < player.Center.X ? -1 : 1);
				player.itemRotation = (Projectile.Center - player.Center).ToRotation() + (player.direction < 0 ? MathHelper.Pi : 0f);
				if (Projectile.owner == Main.myPlayer) {
					Vector2 goal = player.Center + (Main.MouseWorld - player.Center).SafeNormalize(Vector2.Zero) * Math.Min(820f, Vector2.Distance(Main.MouseWorld, player.Center));
					Vector2 d = goal - Projectile.Center;
					Vector2 v = d.Length() > 30f ? d.SafeNormalize(Vector2.Zero) * Math.Min(30f, d.Length() * 0.22f) : d * 0.22f;
					if (Vector2.DistanceSquared(v, Projectile.velocity) > 4f && Main.GameUpdateCount % 3 == 0)
						Projectile.netUpdate = true;
					Projectile.velocity = v;
					if (++Projectile.ai[0] % 20 == 0)
						ThrowChip();
				}
			}
			else {
				Projectile.ai[1] = 1f;
				Vector2 d = player.Center - Projectile.Center;
				Projectile.velocity = d.SafeNormalize(Vector2.Zero) * 32f;
				if (d.Length() < 40f)
					Projectile.Kill();
			}
			spin = MathHelper.Lerp(spin, held ? 0.5f : 0.3f, 0.06f);
			Projectile.rotation += spin;
			if ((int)(Main.GameUpdateCount + Projectile.whoAmI) % 45 == 0)
				SoundEngine.PlaySound(CasinoSounds.OphanimSpin with { Volume = 0.35f }, Projectile.Center);
			CasinoFx.Spark(Projectile.Center + Main.rand.NextVector2CircularEdge(22, 22), Main.rand.NextVector2Circular(2, 2), FateColor, 0.8f);
			Lighting.AddLight(Projectile.Center, 0.8f, 0.6f, 0.3f);
		}

		private static Color FateColor => NPCs.Boss.Fortuna.GoldLight;

		private void ThrowChip()
		{
			NPC best = null;
			float range = 700f;
			foreach (NPC npc in Main.ActiveNPCs) {
				float d = npc.Distance(Projectile.Center);
				if (d < range && npc.CanBeChasedBy(Projectile)) {
					range = d;
					best = npc;
				}
			}
			if (best != null)
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.DirectionTo(best.Center) * 14f, ModContent.ProjectileType<FortuneChip>(), Projectile.damage / 2, 2f, Projectile.owner);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => CasinoFx.Sparks(target.Center, FateColor, 4, 4f, 0.9f);

		public override bool PreDraw(ref Color lightColor)
		{
			PixelFx.Glow(Main.spriteBatch, Projectile.Center - Main.screenPosition, 48f, FateColor * 0.5f);
			CasinoFx.Draw(Projectile, FateColor * 0.7f, FateColor * 0.4f);
			return false;
		}
	}

	/// <summary>Homing fate chip from the Ophanim and from lucky sevens.</summary>
	public class FortuneChip : GamblerShot
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/Boss/FateChip";
		protected override Color TrailColor => NPCs.Boss.Fortuna.FateViolet;
		protected override Color GlowColor => NPCs.Boss.Fortuna.FateViolet;
		protected override float DrawScale => 0.8f;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 14;
			Projectile.penetrate = 1;
			Projectile.tileCollide = false;
			Projectile.timeLeft = 120;
		}

		public override void AI()
		{
			Projectile.rotation += 0.25f;
			if (++Projectile.ai[0] > 6)
				Home(700f, 0.14f);
		}

		public override void OnKill(int timeLeft) => CasinoFx.ChipImpact(Projectile.Center, NPCs.Boss.Fortuna.FateViolet, false);
	}

	/// <summary>Star die: bursts on whatever it meets. ai[0] face, ai[1] 1 = the pair made seven, 2 = doubles.</summary>
	public class DestinyDie : GamblerShot
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/Boss/FateDie";
		private Color Hot => Projectile.ai[1] == 2f ? NPCs.Boss.Fortuna.GoldLight : Projectile.ai[1] == 1f ? NPCs.Boss.Fortuna.FateViolet : new Color(150, 160, 255);

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.width = Projectile.height = 26;
			Projectile.penetrate = 1;
			Projectile.timeLeft = 150;
		}

		public override void AI()
		{
			Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.22f, 18f);
			Projectile.rotation += Projectile.velocity.X * 0.02f;
			CasinoFx.Spark(Projectile.Center + Main.rand.NextVector2Circular(12, 12), -Projectile.velocity * 0.1f, Hot, 0.8f);
			Lighting.AddLight(Projectile.Center, Hot.ToVector3() * 0.5f);
		}

		public override void OnKill(int timeLeft)
		{
			bool doubles = Projectile.ai[1] == 2f;
			float radius = doubles ? 150f : 100f;
			WallHit.Blast(Projectile, radius, doubles ? 1.1f : 0.65f);
			CasinoFx.Explosion(Projectile.Center, radius, Hot, NPCs.Boss.Fortuna.FateViolet, doubles ? 2f : 0f);
			SoundEngine.PlaySound(CasinoSounds.ChipExplode with { Volume = 0.55f, Pitch = doubles ? -0.2f : 0.15f }, Projectile.Center);
			if (Projectile.ai[1] == 1f && Projectile.owner == Main.myPlayer)
				for (int k = 0; k < 4; k++)
					Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, (Main.rand.NextFloat(0.5f) + k * MathHelper.PiOver2).ToRotationVector2() * 11f, ModContent.ProjectileType<FortuneChip>(), Projectile.damage * 3 / 10, 2f, Projectile.owner);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			var frame = new Rectangle(((int)MathHelper.Clamp(Projectile.ai[0], 1, 6) - 1) * 48, 0, 48, 48);
			CasinoFx.Trail(Projectile, tex, frame, Hot, 0.8f);
			CasinoFx.Glow(Projectile, tex, frame, Hot * 0.7f, 0.8f);
			Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, Color.White, Projectile.rotation, new Vector2(24), 0.8f, SpriteEffects.None);
			return false;
		}
	}

	/// <summary>Golden ray: the whole line hits at once and fades. ai[0] 1 = triple. ai[1] length.</summary>
	public class FortuneRay : ModProjectile
	{
		public override string Texture => "CasinoWorld/Content/Projectiles/Boss/FateChip";
		private const int Show = 12;
		private const float MaxLength = 1300f;

		public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

		public override void SetDefaults()
		{
			Projectile.width = Projectile.height = 12;
			Projectile.friendly = true;
			Projectile.DamageType = ModContent.GetInstance<GamblerDamageClass>();
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.timeLeft = Show;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
		}

		public override bool ShouldUpdatePosition() => false;

		public override bool? CanDamage() => Projectile.timeLeft >= Show - 3 ? null : false;

		private Vector2 Direction => Projectile.velocity.SafeNormalize(Vector2.UnitX);

		public override void AI()
		{
			if (Projectile.localAI[0]++ > 0f)
				return;
			float[] samples = new float[3];
			Collision.LaserScan(Projectile.Center, Direction, 8f, MaxLength, samples);
			Projectile.ai[1] = (samples[0] + samples[1] + samples[2]) / 3f;
			Vector2 end = Projectile.Center + Direction * Projectile.ai[1];
			CasinoFx.Sparks(end, NPCs.Boss.Fortuna.GoldLight, 6, 4f, 0.9f);
			CasinoFx.Sparks(Projectile.Center, NPCs.Boss.Fortuna.FateRed, 3, 2f, 0.8f);
			for (float d = 0; d < Projectile.ai[1]; d += 90f)
				Lighting.AddLight(Projectile.Center + Direction * d, 0.7f, 0.5f, 0.2f);
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			float hit = 0f;
			return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + Direction * Projectile.ai[1], 18f, ref hit);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			float k = Projectile.timeLeft / (float)Show;
			Vector2 a = Projectile.Center - Main.screenPosition, b = a + Direction * Projectile.ai[1];
			Color glow = (Projectile.ai[0] == 1f ? NPCs.Boss.Fortuna.FateRed : NPCs.Boss.Fortuna.GoldLight) * (0.45f * k), core = new Color(255, 245, 200) * k;
			glow.A = core.A = 0;
			PixelFx.Line(Main.spriteBatch, a, b, glow, 14f * k);
			PixelFx.Line(Main.spriteBatch, a, b, core, 6f * k);
			PixelFx.Twinkle(Main.spriteBatch, b, Color.White, 1f - k);
			return false;
		}
	}
}
