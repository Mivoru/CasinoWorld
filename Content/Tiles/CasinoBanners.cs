using CasinoWorld.Content.NPCs.Enemies;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CasinoWorld.Content.Tiles
{
	/// <summary>
	/// Enemy banners, one style per enemy (vanilla layout: 1x3, hangs from a ceiling or platform). Nearby
	/// banners give the usual banner bonus against their enemy. Kill counts and drops come from NPC.Banner.
	/// </summary>
	public class CasinoBanners : ModTile
	{
		public enum Style { ChipSlime, CardSoldier, SlotMimic, LuckyBat, RoyalGuard, RouletteWraith, LoanShark, BrokeGambler }

		public static int NpcFor(int style) => (Style)style switch {
			Style.ChipSlime => ModContent.NPCType<ChipSlime>(),
			Style.CardSoldier => ModContent.NPCType<CardSoldier>(),
			Style.SlotMimic => ModContent.NPCType<SlotMimic>(),
			Style.LuckyBat => ModContent.NPCType<LuckyBat>(),
			Style.RoyalGuard => ModContent.NPCType<RoyalGuard>(),
			Style.RouletteWraith => ModContent.NPCType<RouletteWraith>(),
			Style.LoanShark => ModContent.NPCType<LoanShark>(),
			Style.BrokeGambler => ModContent.NPCType<BrokeGambler>(),
			_ => -1,
		};

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.DisableSmartCursor[Type] = true;
			TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2Top);
			TileObjectData.newTile.Height = 3;
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
			TileObjectData.newTile.StyleHorizontal = true;
			TileObjectData.newTile.StyleWrapLimit = 111;
			TileObjectData.newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.SolidBottom | AnchorType.PlanterBox, TileObjectData.newTile.Width, 0);
			TileObjectData.newTile.DrawYOffset = -2;
			TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
			TileObjectData.newAlternate.AnchorTop = new AnchorData(AnchorType.Platform, TileObjectData.newTile.Width, 0);
			TileObjectData.newAlternate.DrawYOffset = -10;
			TileObjectData.addAlternate(0);
			TileObjectData.addTile(Type);
			DustType = -1;
			AddMapEntry(new Color(200, 160, 60), Language.GetText("MapObject.Banner"));
		}

		public override void NearbyEffects(int i, int j, bool closer)
		{
			if (closer)
				return;
			int npc = NpcFor(Main.tile[i, j].TileFrameX / 18);
			if (npc < 0)
				return;
			Main.SceneMetrics.NPCBannerBuff[npc] = true;
			Main.SceneMetrics.hasBanner = true;
		}
	}
}

namespace CasinoWorld.Content.Items.Banners
{
	public abstract class CasinoBannerItem : ModItem
	{
		protected abstract Tiles.CasinoBanners.Style Style { get; }

		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.CasinoBanners>(), (int)Style);
			Item.width = 10;
			Item.height = 24;
			Item.SetShopValues(ItemRarityColor.Blue1, Item.buyPrice(silver: 10));
		}
	}

	public class ChipSlimeBanner : CasinoBannerItem { protected override Tiles.CasinoBanners.Style Style => Tiles.CasinoBanners.Style.ChipSlime; }
	public class CardSoldierBanner : CasinoBannerItem { protected override Tiles.CasinoBanners.Style Style => Tiles.CasinoBanners.Style.CardSoldier; }
	public class SlotMimicBanner : CasinoBannerItem { protected override Tiles.CasinoBanners.Style Style => Tiles.CasinoBanners.Style.SlotMimic; }
	public class LuckyBatBanner : CasinoBannerItem { protected override Tiles.CasinoBanners.Style Style => Tiles.CasinoBanners.Style.LuckyBat; }
	public class RoyalGuardBanner : CasinoBannerItem { protected override Tiles.CasinoBanners.Style Style => Tiles.CasinoBanners.Style.RoyalGuard; }
	public class RouletteWraithBanner : CasinoBannerItem { protected override Tiles.CasinoBanners.Style Style => Tiles.CasinoBanners.Style.RouletteWraith; }
	public class LoanSharkBanner : CasinoBannerItem { protected override Tiles.CasinoBanners.Style Style => Tiles.CasinoBanners.Style.LoanShark; }
	public class BrokeGamblerBanner : CasinoBannerItem { protected override Tiles.CasinoBanners.Style Style => Tiles.CasinoBanners.Style.BrokeGambler; }
}
