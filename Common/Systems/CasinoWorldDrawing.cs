using System.Linq;
using CasinoWorld.Common.Players;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CasinoWorld.Common.Systems
{
	/// <summary>
	/// World-space overlays: the blueprint placement preview and the "enter" prompt above casino doors.
	/// </summary>
	public class CasinoWorldDrawing : ModSystem
	{
		public override void PostDrawTiles()
		{
			if (Main.gameMenu || Main.dedServ)
				return;

			Player player = Main.LocalPlayer;
			bool preview = player.HeldItem.type == ModContent.ItemType<CasinoBlueprint>() && !player.mouseInterface && !CasinoTravel.InCasino;
			Vector2? prompt = player.GetModPlayer<CasinoPlayer>().NearbyDoor;
			if (!preview && prompt == null)
				return;

			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
				Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			if (preview)
				DrawBlueprintPreview();
			if (prompt is Vector2 doorTop)
				DrawEnterPrompt(doorTop);

			Main.spriteBatch.End();
		}

		private static void DrawBlueprintPreview()
		{
			(int left, int groundY) = CasinoBlueprint.SiteAtMouse();
			bool valid = SurfaceCasinoStructure.CanBuildAt(left, groundY);
			Color tint = (valid ? Color.LightGreen : Color.Red) * 0.45f;

			Texture2D texture = TextureAssets.Tile[ModContent.TileType<CasinoFacade>()].Value;
			int top = groundY - CasinoFacade.Height;
			for (int dx = 0; dx < CasinoFacade.Width; dx++) {
				for (int dy = 0; dy < CasinoFacade.Height; dy++) {
					Vector2 pos = new Vector2(left + dx, top + dy) * 16f - Main.screenPosition;
					Main.spriteBatch.Draw(texture, pos, new Rectangle(dx * 18, dy * 18, 16, 16), tint);
				}
			}

			string text = Language.GetTextValue(valid ? "Mods.CasinoWorld.Items.CasinoBlueprint.Valid" : "Mods.CasinoWorld.Items.CasinoBlueprint.Invalid");
			Vector2 textPos = new Vector2(left + CasinoFacade.Width / 2f, top) * 16f - Main.screenPosition - new Vector2(0, 14);
			Utils.DrawBorderString(Main.spriteBatch, text, textPos, valid ? Color.LightGreen : Color.OrangeRed, 1f, 0.5f, 1f);
		}

		private static void DrawEnterPrompt(Vector2 doorTop)
		{
			string up = UpKeyName();
			string key = CasinoTravel.InCasino ? "Mods.CasinoWorld.UI.ExitPrompt" : "Mods.CasinoWorld.UI.EnterPrompt";
			string text = Language.GetTextValue(key, up);
			float bob = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 2f;
			Utils.DrawBorderString(Main.spriteBatch, text, doorTop - Main.screenPosition + new Vector2(0, -10 + bob), new Color(255, 230, 140), 0.9f, 0.5f, 1f);
		}

		/// <summary>Name of the key bound to "Up", for example "W".</summary>
		private static string UpKeyName()
		{
			var keys = PlayerInput.CurrentProfile?.InputModes[InputMode.Keyboard].KeyStatus["Up"];
			return keys != null && keys.Count > 0 ? keys.First() : "Up";
		}
	}
}
