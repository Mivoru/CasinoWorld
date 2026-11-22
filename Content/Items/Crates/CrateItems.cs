using CasinoWorld.Common.Crates;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items.Crates
{
	/// <summary>
	/// Base for casino crates. Right-click in the inventory to open it with the CS:GO style strip.
	/// Found in the world (crate tiles in caves, some chests), dropped by casino enemies,
	/// and sold for loyalty tokens at the prize counter.
	/// </summary>
	public abstract class CrateItem : ModItem
	{
		public abstract CrateKind Kind { get; }

		public static int ItemTypeFor(CrateKind kind) => kind == CrateKind.HighRoller
			? ModContent.ItemType<HighRollerCrate>()
			: ModContent.ItemType<LuckyCrate>();

		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 5;
			// Count as fishing crates (crate potions, sorting, Angler-style catches).
			ItemID.Sets.IsFishingCrate[Type] = true;
			if (Kind == CrateKind.HighRoller)
				ItemID.Sets.IsFishingCrateHardmode[Type] = true;
		}

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.maxStack = 99;
			Item.consumable = true;
		}

		public override bool CanRightClick() => !ModContent.GetInstance<CrateSystem>().IsSpinning;

		public override void RightClick(Player player)
		{
			if (player.whoAmI == Main.myPlayer)
				ModContent.GetInstance<CrateSystem>().Open(Kind);
		}
	}

	public class LuckyCrate : CrateItem
	{
		public override CrateKind Kind => CrateKind.Lucky;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.value = Item.sellPrice(silver: 50);
			Item.rare = ItemRarityID.Blue;
		}
	}

	public class HighRollerCrate : CrateItem
	{
		public override CrateKind Kind => CrateKind.HighRoller;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.value = Item.sellPrice(gold: 2);
			Item.rare = ItemRarityID.Pink;
		}
	}
}
