using System;
using System.Collections.Generic;
using CasinoWorld.Common.Games;
using CasinoWorld.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Gambler
{
	/// <summary>
	/// The Pot meter: a brass rail with marquee bulbs, filled with poker chips. Drawn on the HUD (drag it with
	/// the inventory open) or small under the player, see <see cref="CasinoClientConfig"/>. While All In
	/// runs, it shows the dealt poker hand.
	/// </summary>
	public class PotMeterUI : ModSystem
	{
		private const int Width = 184, Height = 44;
		private static readonly Rectangle Window = new(44, 14, 128, 16);
		private const int DefaultTop = 22;
		private const int CardW = 48, CardH = 68;

		private static Asset<Texture2D> frameTex, fillTex, cardsTex;
		private static float shown;
		private static bool dragging;
		private static Vector2 grab;

		public override void Unload() => frameTex = fillTex = cardsTex = null;

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int bars = layers.FindIndex(l => l.Name == "Vanilla: Entity Health Bars");
			if (bars >= 0)
				layers.Insert(bars + 1, new LegacyGameInterfaceLayer("CasinoWorld: Pot (player)", () => {
					if (Visible(out GamblerPot pot) && CasinoClientConfig.Instance.PotMeter == PotMeterPlace.UnderPlayer) {
						Player player = Main.LocalPlayer;
						Vector2 at = player.Bottom - Main.screenPosition + new Vector2(0, 8 + player.gfxOffY);
						DrawMeter(Main.spriteBatch, pot, new Vector2((int)at.X - Width / 4, (int)at.Y), 0.5f);
					}
					return true;
				}, InterfaceScaleType.Game));
			int resources = layers.FindIndex(l => l.Name == "Vanilla: Resource Bars");
			if (resources >= 0)
				layers.Insert(resources + 1, new LegacyGameInterfaceLayer("CasinoWorld: Pot (HUD)", () => {
					if (Visible(out GamblerPot pot) && CasinoClientConfig.Instance.PotMeter == PotMeterPlace.Hud)
						DrawHud(pot);
					else
						dragging = false;
					return true;
				}, InterfaceScaleType.UI));
		}

		private static bool Visible(out GamblerPot pot)
		{
			pot = null;
			Player player = Main.LocalPlayer;
			if (player.dead || player.ghost || Main.gameMenu || !player.TryGetModPlayer(out pot))
				return false;
			return player.HeldItem.CountsAsClass<GamblerDamageClass>() || pot.Pot > 0 || pot.AllInActive;
		}

		private static void DrawHud(GamblerPot pot)
		{
			var screen = new Vector2(Main.screenWidth, Main.screenHeight);
			var size = new Vector2(Width, Height);
			// Default: top centre, level with the hotbar.
			Vector2 at = pot.MeterPosition is Vector2 centre ? centre * screen - size / 2f : new Vector2((screen.X - Width) / 2f, DefaultTop);
			at = Vector2.Clamp(at, Vector2.Zero, screen - size);
			var mouse = new Vector2(Main.mouseX, Main.mouseY);
			bool hover = new Rectangle((int)at.X, (int)at.Y, Width, Height).Contains(mouse.ToPoint());
			// Only with the inventory open, so the meter never eats a shot in a fight.
			if (Main.playerInventory && (hover || dragging)) {
				Main.LocalPlayer.mouseInterface = true;
				if (Main.mouseLeft && Main.mouseLeftRelease && hover) {
					dragging = true;
					grab = mouse - at;
				}
				if (hover && !dragging && Main.mouseRight && Main.mouseRightRelease)
					pot.MeterPosition = null;
				if (!dragging && Main.mouseItem.IsAir)
					Main.instance.MouseText(Language.GetTextValue("Mods.CasinoWorld.Gambler.MeterHint", (int)pot.Pot, GamblerPot.KeyName ?? "-"));
			}
			if (!Main.mouseLeft || !Main.playerInventory)
				dragging = false;
			if (dragging) {
				at = Vector2.Clamp(mouse - grab, Vector2.Zero, screen - size);
				pot.MeterPosition = (at + size / 2f) / screen;
			}
			DrawMeter(Main.spriteBatch, pot, new Vector2((int)at.X, (int)at.Y), 1f);
		}

		private static void DrawMeter(SpriteBatch sb, GamblerPot pot, Vector2 at, float scale)
		{
			frameTex ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/PotFrame", AssetRequestMode.ImmediateLoad);
			fillTex ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/PotFill", AssetRequestMode.ImmediateLoad);
			Texture2D px = TextureAssets.MagicPixel.Value;
			float t = Main.GlobalTimeWrappedHourly;
			bool active = pot.AllInActive, full = pot.Full && !active;
			shown = MathHelper.Lerp(shown, active ? pot.AllInLeft : pot.Pot / GamblerPot.Max, 0.15f);
			Rectangle Box(float x, float y, float w, float h) => new((int)(at.X + x * scale), (int)(at.Y + y * scale), (int)Math.Ceiling(w * scale), (int)Math.Ceiling(h * scale));

			// Glow behind the whole rail when it is ready or running.
			if (full || active) {
				Color glow = (active ? new Color(255, 60, 60) : new Color(255, 200, 70)) * (0.35f + 0.2f * (float)Math.Sin(t * 8));
				glow.A = 0;
				for (int k = 0; k < 4; k++)
					sb.Draw(frameTex.Value, at + (k * MathHelper.PiOver2).ToRotationVector2() * 3f * scale, null, glow, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			}
			sb.Draw(frameTex.Value, at, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

			// Chips, whole pixels only.
			int w = (int)(Window.Width * MathHelper.Clamp(shown, 0f, 1f) + 0.5f) / 2 * 2;
			if (w > 0) {
				Color tint = active ? Color.Lerp(new Color(255, 120, 110), Color.White, 0.5f + 0.5f * (float)Math.Sin(t * 14))
					: full ? Color.Lerp(new Color(255, 215, 120), Color.White, 0.5f + 0.5f * (float)Math.Sin(t * 9)) : Color.White;
				sb.Draw(fillTex.Value, at + new Vector2(Window.X, Window.Y) * scale, new Rectangle(0, 0, w, Window.Height), tint, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				sb.Draw(px, Box(Window.X + w - 2, Window.Y, 2, Window.Height), new Color(255, 250, 210));
				float shine = t * 90f % 220f;
				if (shine < w - 8) {
					Color c = Color.White * 0.45f;
					c.A = 0;
					sb.Draw(px, Box(Window.X + shine, Window.Y, 8, Window.Height), c);
				}
			}

			// Marquee bulbs: they light up with the pot and chase when it is full or running.
			const int Bulbs = 11;
			for (int row = 0; row < 2; row++)
				for (int k = 0; k < Bulbs; k++) {
					bool on = full || active ? (k + row + (int)(t * (active ? 14 : 8))) % 3 != 0 : k < shown * Bulbs - 0.2f;
					Color bulb = on ? active && k % 2 == 0 ? new Color(255, 90, 80) : new Color(255, 232, 140) : new Color(70, 44, 30);
					float x = 47 + k * 12, y = row == 0 ? 7 : 33;
					if (on) {
						Color halo = bulb * 0.4f;
						halo.A = 0;
						sb.Draw(px, Box(x - 2, y - 2, 8, 8), halo);
					}
					sb.Draw(px, Box(x, y, 4, 4), bulb);
				}

			// Suit on the chip medallion; the suits riffle while All In runs.
			int suit = active ? (int)(t * 10) % 4 : (int)(t * 0.5f) % 4;
			sb.Draw(CasinoDraw.Suits, at + new Vector2(22, 22) * scale, new Rectangle(suit * 12, 0, 12, 12), Color.White, 0f, new Vector2(6), scale * (full ? 1.2f + 0.15f * (float)Math.Sin(t * 9) : 1.2f), SpriteEffects.None, 0f);

			var below = new Vector2(at.X + Width * scale / 2f, at.Y + Height * scale + 1);
			string key = GamblerPot.KeyName;
			if (full)
				Utils.DrawBorderString(sb, key != null ? Language.GetTextValue("Mods.CasinoWorld.Gambler.AllInReady", key) : Language.GetTextValue("Mods.CasinoWorld.Gambler.AllInUnbound"),
					below, key != null ? Color.Lerp(new Color(255, 220, 90), Color.White, 0.5f + 0.5f * (float)Math.Sin(t * 9)) : new Color(255, 110, 100), scale < 1f ? 0.7f : 0.9f, 0.5f, 0f);
			else if (!active && scale >= 1f)
				Utils.DrawBorderString(sb, key == null ? Language.GetTextValue("Mods.CasinoWorld.Gambler.AllInUnbound") : (int)pot.Pot + "%",
					below, key == null ? new Color(255, 110, 100) : new Color(230, 205, 150), 0.75f, 0.5f, 0f);
			if (active && pot.Hand != null)
				DrawHand(sb, pot, at, scale);
		}

		private static void DrawHand(SpriteBatch sb, GamblerPot pot, Vector2 at, float scale)
		{
			cardsTex ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/Cards", AssetRequestMode.ImmediateLoad);
			float size = scale < 1f ? 0.4f : 0.75f;
			float step = CardW * size + 3f;
			// Below the meter under the player (never over the character) and when there is no room above it.
			bool below = scale < 1f || at.Y < CardH * size + 40f;
			float y = below ? at.Y + Height * scale + CardH * size / 2f + 6 : at.Y - CardH * size / 2f - 8;
			float middle = at.X + Width * scale / 2f;
			int age = pot.HandAge;
			for (int k = 0; k < 5; k++) {
				float flip = MathHelper.Clamp((age - k * 5) / 12f, 0f, 1f);
				if (flip <= 0f)
					continue;
				Rectangle src = flip < 0.5f ? new Rectangle(0, 4 * CardH, CardW, CardH) : new Rectangle(pot.Cards[k] % 13 * CardW, pot.Cards[k] / 13 * CardH, CardW, CardH);
				float squash = Math.Max(0.05f, Math.Abs(flip * 2f - 1f));
				var pos = new Vector2(middle + (k - 2) * step, y + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f + k) * 1.5f);
				sb.Draw(cardsTex.Value, pos, src, Color.White, (k - 2) * 0.04f, new Vector2(CardW, CardH) / 2f, new Vector2(squash, 1f) * size, SpriteEffects.None, 0f);
			}
			if (age > 32) {
				float nameY = below ? y + CardH * size / 2f + 1 : y - CardH * size / 2f - 24;
				Utils.DrawBorderString(sb, Language.GetTextValue("Mods.CasinoWorld.Gambler.Hands." + pot.Hand.Key), new Vector2(middle, nameY), pot.Hand.Color, scale < 1f ? 0.75f : 1f, 0.5f, 0f);
			}
		}
	}
}
