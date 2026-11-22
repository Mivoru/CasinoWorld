using System.Collections.Generic;
using CasinoWorld.Common.Net;
using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items
{
	/// <summary>
	/// Builds the surface casino where the player points. Shows a green or red preview while held.
	/// Reusable: using it again moves the casino. Every new character starts with one.
	/// </summary>
	public class CasinoBlueprint : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 28;
			Item.height = 28;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = Item.useAnimation = 30;
			Item.UseSound = SoundID.Item4;
			Item.consumable = false;
			Item.maxStack = 1;
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.buyPrice(silver: 50);
		}

		/// <summary>Site under the mouse: the facade's left column and the ground row under it.</summary>
		public static (int left, int groundY) SiteAtMouse()
		{
			Point tile = Main.MouseWorld.ToTileCoordinates();
			return (tile.X - CasinoFacade.Width / 2, tile.Y + 1);
		}

		public override bool CanUseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer)
				return true;
			if (CasinoTravel.InCasino)
				return false;

			(int left, int groundY) = SiteAtMouse();
			return SurfaceCasinoStructure.CanBuildAt(left, groundY);
		}

		public override bool? UseItem(Player player)
		{
			// UseItem runs once at the start of each use; only the local player builds (sends the request).
			if (player.whoAmI != Main.myPlayer)
				return true;

			(int left, int groundY) = SiteAtMouse();
			if (Main.netMode == NetmodeID.SinglePlayer) {
				SurfaceCasinoStructure.Build(left, groundY);
				Main.NewText(Language.GetTextValue("Mods.CasinoWorld.Items.CasinoBlueprint.Built"), new Color(255, 215, 60));
			}
			else {
				CasinoNet.SendPlaceSurfaceCasino(Mod, left, groundY);
			}
			return true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Wood, 25)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}

	/// <summary>Gives every new character a Casino Blueprint.</summary>
	public class BlueprintStarterPlayer : ModPlayer
	{
		public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
		{
			if (!mediumCoreDeath)
				yield return new Item(ModContent.ItemType<CasinoBlueprint>());
		}
	}
}
