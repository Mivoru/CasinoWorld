using CasinoWorld.Common;
using CasinoWorld.Common.Gambler;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items.Tools
{
	/// <summary>
	/// Chip Chain: a gold coin chain with a chip claw. Two hooks, reach and speed between the
	/// Ruby and Diamond gem hooks. Bought with loyalty tokens at the prize counter.
	/// </summary>
	public class ChipChainHook : ModItem
	{
		public override void SetDefaults()
		{
			Item.CloneDefaults(ItemID.RubyHook);
			Item.shootSpeed = 15f;
			Item.shoot = ModContent.ProjectileType<ChipChainHookProjectile>();
			Item.UseSound = CasinoSounds.HookChain;
			Item.rare = ItemRarityID.Orange;
			Item.value = Item.sellPrice(gold: 1);
		}
	}

	public class ChipChainHookProjectile : ModProjectile
	{
		private static Asset<Texture2D> chain;

		public override void Load() => chain = ModContent.Request<Texture2D>("CasinoWorld/Content/Items/Tools/ChipChainHookChain");

		public override void Unload() => chain = null;

		public override void SetDefaults() => Projectile.CloneDefaults(ProjectileID.GemHookRuby);

		public override bool? CanUseGrapple(Player player)
		{
			int hooks = 0;
			foreach (Projectile projectile in Main.ActiveProjectiles) {
				if (projectile.owner == Main.myPlayer && projectile.type == Type)
					hooks++;
			}
			return hooks <= 1;
		}

		public override float GrappleRange() => 400f;

		public override void NumGrappleHooks(Player player, ref int numHooks) => numHooks = 2;

		public override void GrappleRetreatSpeed(Player player, ref float speed) => speed = 16f;

		public override void GrapplePullSpeed(Player player, ref float speed) => speed = 12f;

		public override void AI()
		{
			// A glint of gold now and then along the flight.
			if (Main.rand.NextBool(8))
				Dust.NewDustPerfect(Projectile.Center, DustID.GoldCoin, Vector2.Zero, 0, default, 0.7f).noGravity = true;
		}

		public override bool PreDrawExtras()
		{
			Vector2 playerCenter = Main.player[Projectile.owner].MountedCenter;
			Vector2 center = Projectile.Center;
			Vector2 toPlayer = playerCenter - center;
			float rotation = toPlayer.ToRotation() - MathHelper.PiOver2;
			float distance = toPlayer.Length();
			int link = 0;
			while (distance > 16f && !float.IsNaN(distance)) {
				toPlayer /= distance;
				toPlayer *= chain.Height();
				center += toPlayer;
				toPlayer = playerCenter - center;
				distance = toPlayer.Length();
				Color light = Lighting.GetColor((int)center.X / 16, (int)(center.Y / 16));
				// Every other coin turns a little, so the chain twinkles as it moves.
				float twist = (link++ % 2 == 0 ? 0f : 0.25f) * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 6f + link);
				Main.EntitySpriteDraw(chain.Value, center - Main.screenPosition, null, light, rotation + twist, chain.Size() / 2f, 1f, SpriteEffects.None);
			}
			return false;
		}
	}
}

namespace CasinoWorld.Content.Items.Accessories
{
	/// <summary>
	/// Royal Flush Wings: two fans of playing cards. Post-Plantera tier flight (like Spooky/Tattered
	/// wings) and +1 Luck. Crafted from souls of flight, ectoplasm and loyalty tokens.
	/// </summary>
	[AutoloadEquip(EquipType.Wings)]
	public class RoyalFlushWings : ModItem
	{
		public override void SetStaticDefaults()
		{
			ArmorIDs.Wing.Sets.Stats[Item.wingSlot] = new Terraria.DataStructures.WingStats(160, 8f, 2f);
		}

		public override void SetDefaults()
		{
			Item.width = 30;
			Item.height = 28;
			Item.accessory = true;
			Item.value = Item.sellPrice(gold: 8);
			Item.rare = ItemRarityID.Yellow;
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 1;
		}

		public override void VerticalWingSpeeds(Player player, ref float ascentWhenFalling, ref float ascentWhenRising,
			ref float maxCanAscendMultiplier, ref float maxAscentMultiplier, ref float constantAscend)
		{
			ascentWhenFalling = 0.85f;
			ascentWhenRising = 0.15f;
			maxCanAscendMultiplier = 1f;
			maxAscentMultiplier = 2.5f;
			constantAscend = 0.135f;
		}

		public override void EquipFrameEffects(Player player, EquipType type)
		{
			// Cards flutter off while flying.
			if (player.velocity.Y != 0f && Main.rand.NextBool(14) && !player.ShouldNotDraw) {
				Dust card = Dust.NewDustDirect(player.position + new Vector2(-player.direction * 12f, 8f), player.width, 12, DustID.Confetti_Pink, 0f, 1f, 0, Color.White, 0.8f);
				card.noGravity = false;
			}
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.SoulofFlight, 20)
				.AddIngredient(ItemID.Ectoplasm, 10)
				.AddIngredient<LoyaltyToken>(400)
				.AddTile(TileID.MythrilAnvil)
				.Register();
		}
	}

	/// <summary>High Roller's Ring: +2 Luck and +8 % gambler damage. Hardmode crates and the prize counter.</summary>
	public class HighRollersRing : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 24;
			Item.height = 24;
			Item.accessory = true;
			Item.value = Item.sellPrice(gold: 4);
			Item.rare = ItemRarityID.LightPurple;
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetModPlayer<GamblerPlayer>().Luck += 2;
			player.GetDamage<GamblerDamageClass>() += 0.08f;
		}
	}

	/// <summary>Martingale Medallion: after a bust, the next gambler hit gets +25 % jackpot chance. Hardmode crates.</summary>
	public class MartingaleMedallion : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 24;
			Item.height = 28;
			Item.accessory = true;
			Item.value = Item.sellPrice(gold: 4);
			Item.rare = ItemRarityID.LightPurple;
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetModPlayer<GamblerPlayer>().Martingale = true;
		}
	}

	/// <summary>Dealer's Visor: +1 Luck and +2 % jackpot chance. Hardmode, prize counter.</summary>
	public class DealersVisor : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 28;
			Item.height = 16;
			Item.accessory = true;
			Item.value = Item.sellPrice(gold: 2);
			Item.rare = ItemRarityID.Pink;
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			GamblerPlayer gambler = player.GetModPlayer<GamblerPlayer>();
			gambler.Luck += 1;
			gambler.BonusJackpotChance += 0.02f;
		}
	}
}

namespace CasinoWorld.Content.Items.Armor
{
	/// <summary>White High Roller suit: vanity, bought with loyalty tokens.</summary>
	[AutoloadEquip(EquipType.Head)]
	public class WhiteFedora : ModItem
	{
		public override void SetStaticDefaults() => ArmorIDs.Head.Sets.DrawHatHair[Item.headSlot] = true;

		public override void SetDefaults()
		{
			Item.width = 26;
			Item.height = 16;
			Item.vanity = true;
			Item.rare = ItemRarityID.Pink;
			Item.value = Item.sellPrice(gold: 1);
		}
	}

	[AutoloadEquip(EquipType.Body)]
	public class WhiteTuxedo : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 26;
			Item.height = 20;
			Item.vanity = true;
			Item.rare = ItemRarityID.Pink;
			Item.value = Item.sellPrice(gold: 1);
		}
	}

	[AutoloadEquip(EquipType.Legs)]
	public class WhiteSpats : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 22;
			Item.height = 14;
			Item.vanity = true;
			Item.rare = ItemRarityID.Pink;
			Item.value = Item.sellPrice(gold: 1);
		}
	}
}
