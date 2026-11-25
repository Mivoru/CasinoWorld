using System;
using System.Collections.Generic;
using System.Linq;
using CasinoWorld.Common.Games;
using CasinoWorld.Content.Tiles.Interior;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Content.Items.Decor
{
	public enum DecorKind { Furniture, Wall, Block, Wallpaper }

	/// <summary>Placeable casino furniture, blocks and walls, one item per tile; registered in <see cref="DecorCatalog"/>.</summary>
	[Autoload(false)]
	public class DecorItem : ModItem
	{
		private readonly string name, texture;
		private readonly Func<int> content;
		private readonly int cellsX, cellsY;

		public readonly DecorKind Kind;
		public readonly int Price;

		protected override bool CloneNewInstances => true;
		public override string Name => name;
		public override string Texture => texture;

		public DecorItem(string name, string texture, DecorKind kind, Func<int> content, int price, int cellsX = 0, int cellsY = 0)
		{
			this.name = name;
			this.texture = texture;
			this.content = content;
			this.cellsX = cellsX;
			this.cellsY = cellsY;
			Kind = kind;
			Price = price;
		}

		public override void SetStaticDefaults() => Item.ResearchUnlockCount = Kind is DecorKind.Block ? 100 : Kind == DecorKind.Wallpaper ? 400 : 1;

		public override void SetDefaults()
		{
			if (Kind == DecorKind.Wallpaper)
				Item.DefaultToPlaceableWall((ushort)content());
			else
				Item.DefaultToPlaceableTile(content());
			Item.width = Item.height = 24;
			Item.value = Price;
			Item.rare = Kind is DecorKind.Furniture or DecorKind.Wall ? ItemRarityID.LightRed : ItemRarityID.White;
		}

		// The sprite is a tile sheet with gaps: put the cells together and fit them into the slot.
		private void DrawIcon(SpriteBatch sb, Vector2 center, float size, Color color)
		{
			Texture2D tex = ModContent.Request<Texture2D>(texture, AssetRequestMode.ImmediateLoad).Value;
			if (cellsX == 0) {
				var source = new Rectangle(0, 0, Math.Min(32, tex.Width), Math.Min(32, tex.Height));
				sb.Draw(tex, center, source, color, 0f, source.Size() / 2f, size / 32f * 0.85f, SpriteEffects.None, 0f);
				return;
			}
			float scale = Math.Min(1f, size / (16f * Math.Max(cellsX, cellsY)));
			Vector2 corner = center - new Vector2(cellsX, cellsY) * 8f * scale;
			for (int x = 0; x < cellsX; x++)
				for (int y = 0; y < cellsY; y++)
					sb.Draw(tex, corner + new Vector2(x, y) * 16f * scale, new Rectangle(x * 18, y * 18, 16, 16), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
		{
			DrawIcon(spriteBatch, position, 34f * Main.inventoryScale, drawColor);
			return false;
		}

		public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
		{
			DrawIcon(spriteBatch, Item.Center - Main.screenPosition, 28f, lightColor);
			return false;
		}
	}

	/// <summary>Makes an item for every piece of casino furniture and every pattern block and wall.</summary>
	public static class DecorCatalog
	{
		// Pieces that only make sense inside a casino (games, staff places).
		private static readonly HashSet<string> Skip = new() { nameof(FortuneWheel), nameof(LedBoard), nameof(PoleStage), nameof(PrizeCounter), nameof(CashMachine) };

		public static readonly List<DecorItem> All = new();

		/// <summary>Called from Mod.Load, when all tiles and walls exist.</summary>
		public static void Register(Mod mod)
		{
			All.Clear();
			foreach (InteriorFurniture tile in mod.GetContent<InteriorFurniture>().ToArray()) {
				if (Skip.Contains(tile.Name))
					continue;
				int cells = tile.Width * tile.Height;
				// 8 gold per tile of size, at least 40 gold: a decorated penthouse costs some tens of platinum.
				int price = tile is MonumentOfExcess ? (int)(100 * Coins.Platinum) : (int)(Math.Max(5, cells) * 8 * Coins.Gold);
				bool wall = tile.Name.StartsWith("Painting") || tile.Name.StartsWith("Neon") || tile is NightWindow or WallSconce or VelvetDrape or CrystalChandelier or RingPendant;
				Add(mod, new DecorItem(tile.Name + "Item", tile.Texture, wall ? DecorKind.Wall : DecorKind.Furniture, () => tile.Type, price, tile.Width, tile.Height));
			}
			foreach (PatternTile tile in mod.GetContent<PatternTile>().ToArray())
				Add(mod, new DecorItem(tile.Name + "Item", tile.Texture, DecorKind.Block, () => tile.Type, (int)(20 * Coins.Silver)));
			foreach (PatternWall wall in mod.GetContent<PatternWall>().ToArray())
				Add(mod, new DecorItem(wall.Name + "Item", wall.Texture, DecorKind.Wallpaper, () => wall.Type, (int)(10 * Coins.Silver)));
		}

		private static void Add(Mod mod, DecorItem item)
		{
			mod.AddContent(item);
			All.Add(item);
		}
	}
}

namespace CasinoWorld.Content.Tiles.Interior
{
	/// <summary>A pyramid of solid gold with a crown on top. It does nothing. It costs a hundred platinum.</summary>
	public class MonumentOfExcess : AnimatedFurniture
	{
		public override int Width => 5;
		public override int Height => 7;
		protected override Color MapColor => new(255, 210, 80);

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) => (r, g, b) = (0.8f, 0.62f, 0.25f);

		protected override void DrawLive(SpriteBatch sb, Vector2 o, float t, int seed)
		{
			Common.Effects.PixelFx.Glow(sb, o + new Vector2(40, 34), 48f, new Color(255, 220, 110) * (0.2f + 0.06f * (float)Math.Sin(t * 2f)));
			for (int k = 0; k < 3; k++) {
				float f = (t * 0.7f + k * 0.33f) % 1f;
				int step = (int)(t * 0.7f + k * 0.33f) * 37 + k * 23;
				Common.Effects.PixelFx.Twinkle(sb, o + new Vector2(10 + step % 60, 14 + step * 7 % 90), new Color(255, 245, 190), f);
			}
		}
	}
}
