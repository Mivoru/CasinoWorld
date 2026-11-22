using System;
using System.Linq;
using CasinoWorld.Common;
using CasinoWorld.Common.Games;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.Items.Crates;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CasinoWorld.Content.Tiles.Interior
{
	/// <summary>
	/// Walnut aquarium (12x8). The tank is animated in the game: tinted water, light rays, rising
	/// bubbles and custom casino fish (goldfish, neon tetras, chip fish, card fish, koi and a jellyfish).
	/// </summary>
	public class Aquarium : InteriorFurniture
	{
		public override int Width => 12;
		public override int Height => 8;
		protected override Color MapColor => new(40, 110, 170);

		// Tank interior inside the 192x128 sprite (from the Blender model).
		private static readonly Rectangle Tank = new(6, 14, 180, 86);
		private static Asset<Texture2D> fish;

		// species, swim speed, depth (0 top .. 1 bottom), phase
		private static readonly (int species, float speed, float depth, float phase)[] School = {
			(0, 0.35f, 0.30f, 0.0f), (0, 0.32f, 0.55f, 2.1f), (1, 0.6f, 0.20f, 1.0f), (1, 0.62f, 0.24f, 1.25f), (1, 0.58f, 0.28f, 1.5f),
			(2, 0.25f, 0.65f, 3.3f), (3, 0.3f, 0.45f, 4.4f), (4, 0.22f, 0.75f, 5.2f), (5, 0.08f, 0.4f, 0.7f),
		};

		public override void Unload()
		{
			base.Unload();
			fish = null;
		}

		public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
		{
			if (IsTopLeft(Main.tile[i, j], Width, Height))
				Main.instance.TilesRenderer.AddSpecialLegacyPoint(i, j);
		}

		public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
		{
			fish ??= ModContent.Request<Texture2D>("CasinoWorld/Content/Tiles/Interior/AquariumFish");
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			Vector2 origin = new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen;
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			float t = (float)Main.timeForVisualEffects / 60f + i * 0.37f;
			var tank = new Rectangle((int)origin.X + Tank.X, (int)origin.Y + Tank.Y, Tank.Width, Tank.Height);

			// Deep water tint, darker at the bottom.
			for (int band = 0; band < 6; band++) {
				var r = new Rectangle(tank.X, tank.Y + band * tank.Height / 6, tank.Width, tank.Height / 6 + 1);
				spriteBatch.Draw(pixel, r, new Color(5, 30 - band * 3, 70 - band * 6) * (0.35f + band * 0.05f));
			}
			// Slanted light rays drifting across the tank.
			for (int k = 0; k < 4; k++) {
				float x = (t * 6f + k * 47f) % (tank.Width + 40) - 20;
				for (int y = 0; y < tank.Height; y += 2) {
					int rx = (int)(x + y * 0.35f);
					if (rx >= 0 && rx < tank.Width - 6)
						spriteBatch.Draw(pixel, new Rectangle(tank.X + rx, tank.Y + y, 6, 2), new Color(140, 220, 255) * (0.07f * (1f - y / (float)tank.Height)));
				}
			}
			// Fish.
			Texture2D tex = fish.Value;
			foreach (var f in School) {
				int frame = (int)(t * 4f + f.phase * 3f) % 2;
				var source = new Rectangle(frame * 24, f.species * 16, 24, 16);
				if (f.species == 5) {
					// Jellyfish: pulses slowly up and down near the middle.
					float jy = 0.25f + 0.5f * (0.5f + 0.5f * (float)Math.Sin(t * f.speed * 3f + f.phase));
					var pos = new Vector2(tank.X + tank.Width * 0.62f, tank.Y + tank.Height * jy);
					spriteBatch.Draw(tex, pos, source, Color.White * 0.85f, 0f, new Vector2(12, 8), 1f, SpriteEffects.None, 0f);
					continue;
				}
				float u = (float)Math.Sin(t * f.speed + f.phase);
				float dir = (float)Math.Cos(t * f.speed + f.phase);
				var p = new Vector2(tank.X + 14 + (u * 0.5f + 0.5f) * (tank.Width - 28),
					tank.Y + 10 + f.depth * (tank.Height - 24) + (float)Math.Sin(t * 1.7f + f.phase * 2f) * 3f);
				spriteBatch.Draw(tex, p, source, Color.White, 0f, new Vector2(12, 8), 1f, dir >= 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
			}
			// Bubbles rising from the left and the right.
			for (int k = 0; k < 10; k++) {
				float cycle = (t * 0.35f + k * 0.173f) % 1f;
				float bx = (k % 2 == 0 ? 0.12f : 0.88f) * tank.Width + (float)Math.Sin(t * 3f + k) * 3f;
				float by = tank.Height * (1f - cycle);
				int size = 2 + k % 2 * 2;
				spriteBatch.Draw(pixel, new Rectangle(tank.X + (int)bx, tank.Y + (int)by, size, size), new Color(200, 240, 255) * 0.6f);
			}
			// Glass shine.
			spriteBatch.Draw(pixel, new Rectangle(tank.X + 8, tank.Y + 4, 2, tank.Height - 8), Color.White * 0.12f);
			spriteBatch.Draw(pixel, new Rectangle(tank.X + 14, tank.Y + 4, 1, tank.Height / 2), Color.White * 0.08f);
		}
	}

	/// <summary>
	/// The casino's LED leaderboard (20x8): biggest wins today, in the last 7 days and of all time,
	/// plus the latest big win. Shared by all players of the world (see <see cref="CasinoRecords"/>).
	/// </summary>
	public class LedBoard : InteriorFurniture
	{
		public override int Width => 20;
		public override int Height => 8;
		protected override Color MapColor => new(30, 26, 40);

		private static readonly Color Amber = new(255, 190, 70);
		private static readonly Color[] HeaderColors = { new(255, 110, 200), new(90, 220, 255), new(255, 215, 90) };

		public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
		{
			if (IsTopLeft(Main.tile[i, j], Width, Height))
				Main.instance.TilesRenderer.AddSpecialLegacyPoint(i, j);
		}

		private static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.UI.Records." + key, args);

		public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			Vector2 origin = new Vector2(i * 16, j * 16) - Main.screenPosition + offScreen;
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			float t = (float)Main.timeForVisualEffects / 60f;

			// LED dot matrix shimmer behind the text.
			for (int y = 10; y < 118; y += 4) {
				for (int x = 10; x < 310; x += 4)
					spriteBatch.Draw(pixel, new Rectangle((int)origin.X + x, (int)origin.Y + y, 2, 2), new Color(255, 120, 40) * 0.05f);
			}

			float pulse = 0.85f + 0.15f * (float)Math.Sin(t * 4f);
			Utils.DrawBorderString(spriteBatch, Text("Title"), origin + new Vector2(160, 18), Amber * pulse, 0.75f, 0.5f, 0.5f);

			var columns = new[] { (Text("Today"), CasinoRecords.Today), (Text("Week"), CasinoRecords.ThisWeek), (Text("AllTime"), CasinoRecords.Best) };
			for (int c = 0; c < 3; c++) {
				float cx = 58 + c * 102;
				Utils.DrawBorderString(spriteBatch, columns[c].Item1, origin + new Vector2(cx, 36), HeaderColors[c], 0.55f, 0.5f, 0.5f);
				var wins = columns[c].Item2.ToList();
				if (wins.Count == 0)
					Utils.DrawBorderString(spriteBatch, Text("Empty"), origin + new Vector2(cx, 56), Color.Gray, 0.45f, 0.5f, 0.5f);
				for (int k = 0; k < wins.Count; k++) {
					string name = wins[k].Player.Length > 10 ? wins[k].Player[..10] : wins[k].Player;
					float y = 52 + k * 20;
					Utils.DrawBorderString(spriteBatch, $"{k + 1}. {name}", origin + new Vector2(cx, y), Color.White, 0.42f, 0.5f, 0.5f);
					Utils.DrawBorderString(spriteBatch, Coins.Format(wins[k].Amount), origin + new Vector2(cx, y + 9), Amber, 0.42f, 0.5f, 0.5f);
				}
			}

			// Latest win, blinking at the bottom.
			if (CasinoRecords.Latest is CasinoWin latest && (int)(t * 2) % 4 != 0) {
				string line = Text("Latest", latest.Player, Coins.Format(latest.Amount), CasinoRecords.GameName(latest.Game));
				Utils.DrawBorderString(spriteBatch, line, origin + new Vector2(160, 112), new Color(120, 255, 140), 0.45f, 0.5f, 0.5f);
			}
		}
	}

	/// <summary>
	/// Wheel of Fortune on the wall (8x8). It turns slowly; right-click once a day for a free spin
	/// that pays loyalty tokens or, rarely, a Lucky Crate.
	/// </summary>
	public class FortuneWheel : InteriorFurniture
	{
		public override int Width => 8;
		public override int Height => 8;
		protected override Color MapColor => new(230, 180, 60);

		private const int SpinTicks = 240;
		private static Asset<Texture2D> full;

		// Prize per segment (12 segments): tokens, 0 = Lucky Crate.
		private static readonly int[] Prizes = { 10, 25, 10, 50, 15, 0, 10, 25, 15, 100, 10, 25 };

		private static Point16 spinningWheel = new(-1, -1);
		private static int spinTimer;
		private static int spinResult;
		private static float spinStart;
		private static int holdTimer;

		public override void Unload()
		{
			base.Unload();
			full = null;
		}

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

		private Point16 TopLeft(int i, int j)
		{
			Tile tile = Main.tile[i, j];
			return new Point16(i - tile.TileFrameX / 18 % Width, j - tile.TileFrameY / 18 % Height);
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = -1;
			player.cursorItemIconText = Language.GetTextValue(FortuneWheelPlayer.CanSpin(player) ? "Mods.CasinoWorld.UI.Wheel.Spin" : "Mods.CasinoWorld.UI.Wheel.Tomorrow");
		}

		public override bool RightClick(int i, int j)
		{
			Player player = Main.LocalPlayer;
			if (spinTimer > 0)
				return true;
			if (!FortuneWheelPlayer.CanSpin(player)) {
				Main.NewText(Language.GetTextValue("Mods.CasinoWorld.UI.Wheel.Tomorrow"), Color.Gray);
				return true;
			}
			player.GetModPlayer<FortuneWheelPlayer>().MarkSpun();
			spinningWheel = TopLeft(i, j);
			spinResult = Main.rand.Next(Prizes.Length);
			spinTimer = SpinTicks;
			spinStart = CurrentAngle();
			SoundEngine.PlaySound(CasinoSounds.RouletteSpin with { Pitch = -0.2f });
			return true;
		}

		private static float CurrentAngle() => (float)Main.timeForVisualEffects * 0.004f;

		/// <summary>Angle that puts segment <paramref name="segment"/> under the pointer at the top.</summary>
		private static float TargetAngle(int segment)
		{
			float step = MathHelper.TwoPi / 12f;
			// Segment k spans k..k+1 steps counter-clockwise from the right in the texture; screen angles run clockwise.
			return -MathHelper.PiOver2 + (segment + 0.5f) * step;
		}

		public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
		{
			if (IsTopLeft(Main.tile[i, j], Width, Height))
				Main.instance.TilesRenderer.AddSpecialLegacyPoint(i, j);
		}

		// The wheel is drawn whole and rotated in SpecialDraw, so the sliced tiles are not drawn.
		public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) => false;

		public override void PostDraw(int i, int j, SpriteBatch spriteBatch) { }

		public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
		{
			full ??= ModContent.Request<Texture2D>(Texture + "_Full");
			Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			Vector2 center = new Vector2(i * 16 + Width * 8, j * 16 + Height * 8) - Main.screenPosition + offScreen;
			bool mine = spinningWheel.X == i && spinningWheel.Y == j;

			float angle = CurrentAngle();
			if (mine && (spinTimer > 0 || holdTimer > 0)) {
				// Five fast turns that slow down onto the winning segment, then a short hold on it.
				float p = spinTimer > 0 ? 1f - spinTimer / (float)SpinTicks : 1f;
				float ease = 1f - (float)Math.Pow(1f - p, 3);
				float start = MathHelper.WrapAngle(spinStart);
				float end = TargetAngle(spinResult) + MathHelper.TwoPi * 6;
				angle = start + (end - start) * ease;
			}
			Color light = Lighting.GetColor(i + Width / 2, j + Height / 2);
			light = Color.Lerp(light, Color.White, 0.5f);
			Texture2D tex = full.Value;
			spriteBatch.Draw(tex, center, null, light, angle, tex.Size() / 2f, 1f, SpriteEffects.None, 0f);
			// Gold pointer at the top.
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			for (int k = 0; k < 6; k++)
				spriteBatch.Draw(pixel, new Rectangle((int)center.X - 6 + k, (int)center.Y - Height * 8 - 2 + k * 2, 12 - k * 2, 2), new Color(255, 215, 80));
		}

		/// <summary>Advances the local spin and pays out when it stops.</summary>
		public static void UpdateSpin()
		{
			if (holdTimer > 0)
				holdTimer--;
			if (spinTimer <= 0)
				return;
			if (--spinTimer % 12 == 0)
				SoundEngine.PlaySound(CasinoSounds.CrateTick);
			if (spinTimer > 0)
				return;
			holdTimer = 180;
			Player player = Main.LocalPlayer;
			int prize = Prizes[spinResult];
			if (prize == 0) {
				Games_GiveItem(player, new Item(ModContent.ItemType<LuckyCrate>()));
				Main.NewText(Language.GetTextValue("Mods.CasinoWorld.UI.Wheel.WonCrate"), new Color(255, 215, 80));
				SoundEngine.PlaySound(CasinoSounds.RareReveal);
			}
			else {
				Games_GiveItem(player, new Item(ModContent.ItemType<LoyaltyToken>(), prize));
				Main.NewText(Language.GetTextValue("Mods.CasinoWorld.UI.Wheel.WonTokens", prize), new Color(255, 215, 80));
				SoundEngine.PlaySound(prize >= 50 ? CasinoSounds.Jackpot : CasinoSounds.SmallWin);
			}
		}

		private static void Games_GiveItem(Player player, Item item) => Coins.GiveItem(player, item);
	}

	/// <summary>Remembers the last free Wheel of Fortune spin (one per real day) and runs the spin animation.</summary>
	public class FortuneWheelPlayer : ModPlayer
	{
		private long lastSpinDay = -1;

		private static long Today => DateTime.Now.Date.Ticks / TimeSpan.TicksPerDay;

		public static bool CanSpin(Player player) => player.GetModPlayer<FortuneWheelPlayer>().lastSpinDay != Today;

		public void MarkSpun() => lastSpinDay = Today;

		public override void PostUpdate()
		{
			if (Player.whoAmI == Main.myPlayer)
				FortuneWheel.UpdateSpin();
		}

		public override void SaveData(TagCompound tag) => tag["wheelDay"] = lastSpinDay;

		public override void LoadData(TagCompound tag) => lastSpinDay = tag.ContainsKey("wheelDay") ? tag.GetLong("wheelDay") : -1;
	}
}
