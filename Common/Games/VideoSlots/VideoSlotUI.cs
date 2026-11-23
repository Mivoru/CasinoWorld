using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Games.VideoSlots
{
	public class VideoSlotUIState : UIState
	{
		public override void OnInitialize()
		{
			var panel = new VideoSlotPanel();
			panel.Width.Set(VideoSlotPanel.PanelWidth, 0f);
			panel.Height.Set(VideoSlotPanel.PanelHeight, 0f);
			panel.HAlign = 0.5f;
			panel.VAlign = 0.5f;
			Append(panel);
		}
	}

	/// <summary>
	/// Video slot window. A themed stage above the grid (Moon Lord, the Angler, the aurora and its animals,
	/// the blood moon hunter) acts out each step of the spin: deathrays reveal orbs, the fishing line collects
	/// cash, snowballs and shots turn cells wild. Side panel: bonus status and pay table.
	/// </summary>
	public class VideoSlotPanel : UIElement
	{
		public const int PanelWidth = 800, PanelHeight = 600;

		private enum Action { Close, Bet, Spin }

		private readonly record struct Button(Rectangle Rect, Action Kind, string Label, bool Enabled, int Index = 0);

		private sealed class Particle
		{
			public Vector2 Pos, Vel;
			public Color Color;
			public int Life, Max;
			public float Size;
			public bool Coin, Gravity;
		}

		private readonly List<Particle> particles = new();
		private int seenStep = -1;
		private SpinResult seenResult;
		private VideoPhase seenPhase;
		private readonly HashSet<(int step, int id)> fired = new();

		/// <summary>True the first time an event of the current step is asked for (draw can run more than once per tick).</summary>
		private bool Once(int id) => fired.Add((Round.Step, id));

		private static Asset<Texture2D> mlHead, mlHand, orbs;

		private static VideoSlotSystem System => ModContent.GetInstance<VideoSlotSystem>();

		private static VideoSlotRound Round => System.Round;

		private static long[] Chips => CasinoTableGameSystem.Casino?.ChipValues ?? new long[] { 100 };

		private static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.UI.VideoSlots." + key, args);

		private Rectangle Area => GetDimensions().ToRectangle();

		private Rectangle Stage => new(Area.X + 18, Area.Y + 36, 520, 94);

		private int Cell => Round.Machine == null ? 80 : Math.Min(84, Math.Min(520 / Round.Machine.Reels, 330 / Round.Machine.Rows));

		private Rectangle Grid
		{
			get {
				int w = Cell * Round.Machine.Reels, h = Cell * Round.Machine.Rows;
				return new Rectangle(Area.X + 18 + (520 - w) / 2, Area.Y + 136 + (330 - h) / 2, w, h);
			}
		}

		private Rectangle CellRect(int reel, int row) => new(Grid.X + reel * Cell, Grid.Y + row * Cell, Cell, Cell);

		private IEnumerable<Button> Buttons()
		{
			Rectangle a = Area;
			VideoSlotRound round = Round;
			yield return new Button(new Rectangle(a.Right - 34, a.Y + 12, 22, 22), Action.Close, "X", true);
			long[] chips = Chips;
			int y = a.Bottom - 104;
			for (int k = 0; k < chips.Length; k++)
				yield return new Button(new Rectangle(a.X + 22 + k * 64, y, 58, 44), Action.Bet, Games.Coins.Format(chips[k]), round.CanSpin, k);
			yield return new Button(new Rectangle(a.X + 22 + chips.Length * 64 + 10, y - 2, 130, 48), Action.Spin, Language.GetTextValue("Mods.CasinoWorld.UI.Slots.Spin"), round.CanSpin);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
			UpdateParticles();
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			Point mouse = evt.MousePosition.ToPoint();
			foreach (Button button in Buttons()) {
				if (!button.Enabled || !button.Rect.Contains(mouse))
					continue;
				VideoSlotRound round = Round;
				long[] chips = Chips;
				round.BetIndex = Math.Clamp(round.BetIndex, 0, chips.Length - 1);
				switch (button.Kind) {
					case Action.Close:
						System.Close();
						break;
					case Action.Bet:
						round.BetIndex = button.Index;
						SoundEngine.PlaySound(SoundID.MenuTick);
						break;
					case Action.Spin:
						round.TrySpin(Main.LocalPlayer, chips[round.BetIndex]);
						break;
				}
				break;
			}
		}

		// ================================================================== drawing

		protected override void DrawSelf(SpriteBatch sb)
		{
			VideoSlotRound round = Round;
			if (round.Machine == null)
				return;
			mlHead ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/VideoSlots/MoonLordHead");
			mlHand ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/VideoSlots/MoonLordHand");
			orbs ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/VideoSlots/Orbs");
			Rectangle a = Area;
			string key = round.Key;
			Color accent = SlotThemes.Accent(key), body = SlotThemes.Body(key);
			Point mouse = CasinoTableGameSystem.UIMouse;
			float t = Main.GlobalTimeWrappedHourly;
			TrackSteps(round);

			CasinoDraw.Cabinet(sb, a, accent, Color.Lerp(body, Color.Black, 0.35f));
			DrawStage(sb, round, key, accent, t);
			DrawGrid(sb, round, key, accent, t);
			DrawStageForeground(sb, round, key, t);
			DrawOverlays(sb, round, key, accent, t);
			DrawSide(sb, round, key, accent, new Rectangle(a.X + 552, a.Y + 16, 230, 470));
			DrawParticles(sb);

			foreach (Button button in Buttons()) {
				bool hover = button.Rect.Contains(mouse);
				if (button.Kind == Action.Close)
					CasinoDraw.Button(sb, button.Rect, "X", CasinoDraw.Red, hover, true, 0.8f);
				else if (button.Kind == Action.Bet)
					CasinoDraw.ChipButton(sb, button.Rect, button.Label, button.Index, hover, round.BetIndex == button.Index);
				else
					CasinoDraw.Button(sb, button.Rect, round.InBonusRun ? Text("Auto") : button.Label, CasinoDraw.ButtonGold, hover, button.Enabled, 1.1f);
			}

			long money = Games.Coins.Count(Main.LocalPlayer);
			Utils.DrawBorderString(sb, Language.GetTextValue("Mods.CasinoWorld.UI.Slots.Money", Games.Coins.Format(money)), new Vector2(a.X + 24, a.Bottom - 50), Color.White, 0.85f);
			Utils.DrawBorderString(sb, Text("Bet", Games.Coins.Format(Chips[Math.Clamp(round.BetIndex, 0, Chips.Length - 1)])), new Vector2(a.X + 300, a.Bottom - 50), Color.White, 0.85f);
			CasinoDraw.Fit(sb, round.Message, new Vector2(a.X + 24, a.Bottom - 28), round.MessageColor, 0.85f, a.Width - 48);
		}

		/// <summary>Spawns particles when a new step or phase begins (UI side only).</summary>
		private void TrackSteps(VideoSlotRound round)
		{
			if (round.Result != seenResult) {
				seenResult = round.Result;
				seenStep = -1;
				fired.Clear();
			}
			if (round.Phase == VideoPhase.Steps && round.Step != seenStep && round.Result != null) {
				seenStep = round.Step;
				SlotStep s = round.CurrentStep;
				if (s.Kind == StepKind.Tumble && round.Step > 0) {
					foreach (WinGroup w in round.Result.Steps[round.Step - 1].Wins)
						foreach (Pos p in w.Cells)
							Burst(CellRect(p.Reel, p.Row).Center.ToVector2(), SlotThemes.Accent(round.Key), 6, false);
				}
				if (s.Kind == StepKind.Multiply)
					for (int k = 0; k < 30; k++)
						Burst(Grid.Center.ToVector2(), Color.Gold, 1, true);
			}
			if (round.Phase != seenPhase) {
				if (round.Phase == VideoPhase.Outro || round.Phase == VideoPhase.Intro)
					for (int k = 0; k < 40; k++)
						Burst(new Vector2(Grid.Center.X, Grid.Y), Color.Gold, 1, true);
				seenPhase = round.Phase;
			}
			if (round.BigWinTimer > 0 && round.BigWinTimer % 4 == 0)
				Burst(new Vector2(Grid.Center.X + Main.rand.NextFloat(-120, 120), Grid.Center.Y), Color.Gold, 2, true);
		}

		// ------------------------------------------------------------------ stage

		private void DrawStage(SpriteBatch sb, VideoSlotRound round, string key, Color accent, float t)
		{
			Rectangle s = Stage;
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(s, -3), accent * 0.7f);
			Color top, bottom;
			switch (key) {
				case "LunarGods": top = new Color(10, 8, 30); bottom = new Color(40, 30, 80); break;
				case "Angler": top = new Color(120, 190, 240); bottom = new Color(30, 90, 150); break;
				case "Boreal": top = new Color(6, 12, 34); bottom = new Color(24, 50, 90); break;
				default: top = new Color(40, 0, 6); bottom = new Color(120, 14, 24); break;
			}
			for (int k = 0; k < s.Height; k += 4)
				CasinoDraw.Fill(sb, new Rectangle(s.X, s.Y + k, s.Width, 4), Color.Lerp(top, bottom, k / (float)s.Height));

			switch (key) {
				case "LunarGods":
					for (int k = 0; k < 40; k++) {
						float tw = 0.4f + 0.6f * (float)Math.Abs(Math.Sin(t * 1.5f + k * 1.7f));
						CasinoDraw.Fill(sb, new Rectangle(s.X + (k * 137) % s.Width, s.Y + (k * 59) % s.Height, 2, 2), Color.White * tw);
					}
					DrawMoonLordBack(sb, round, t);
					break;
				case "Angler": {
					// Sun low over the bay, a distant island, layered water with glints.
					FillCircle(sb, new Vector2(s.Right - 90, s.Y + 44), 22, new Color(255, 230, 150));
					FillCircle(sb, new Vector2(s.Right - 90, s.Y + 44), 28, new Color(255, 200, 120) * 0.3f);
					for (int x = 0; x < 140; x += 4) {
						int h = (int)(14 * Math.Sin(x / 140f * Math.PI)) + (x % 12 == 0 ? 2 : 0);
						CasinoDraw.Fill(sb, new Rectangle(s.X + 300 + x, s.Y + 58 - h, 4, h), new Color(60, 110, 110));
					}
					int sea = s.Y + 58;
					for (int y = sea; y < s.Bottom; y += 3) {
						float d = (y - sea) / (float)(s.Bottom - sea);
						CasinoDraw.Fill(sb, new Rectangle(s.X, y, s.Width, 3), Color.Lerp(new Color(60, 140, 200), new Color(16, 50, 100), d));
					}
					for (int k = 0; k < 24; k++) {
						float x = (k * 97 + t * (8 + k % 3 * 4)) % s.Width;
						int y = sea + 4 + (k * 13) % (s.Bottom - sea - 6);
						CasinoDraw.Fill(sb, new Rectangle(s.X + (int)x, y, 6 + k % 3 * 3, 1), new Color(220, 245, 255) * (0.35f + 0.3f * (float)Math.Sin(t * 3 + k)));
					}
					// Sun path on the water.
					for (int y = sea + 2; y < s.Bottom; y += 4)
						CasinoDraw.Fill(sb, new Rectangle(s.Right - 100 + (int)(Math.Sin(t * 2 + y) * 4), y, 20, 1), new Color(255, 230, 160) * 0.5f);
					// Pier: planks with grain, posts into the water.
					for (int x = 0; x < 150; x += 10) {
						CasinoDraw.Fill(sb, new Rectangle(s.X + x, sea - 6, 9, 7), new Color(150, 100, 60));
						CasinoDraw.Fill(sb, new Rectangle(s.X + x, sea - 6, 9, 2), new Color(190, 140, 90));
						CasinoDraw.Fill(sb, new Rectangle(s.X + x + 9, sea - 6, 1, 7), new Color(80, 50, 30));
					}
					foreach (int x in new[] { 8, 70, 138 }) {
						CasinoDraw.Fill(sb, new Rectangle(s.X + x, sea + 1, 7, s.Bottom - sea - 1), new Color(100, 66, 40));
						CasinoDraw.Fill(sb, new Rectangle(s.X + x, sea + 1, 2, s.Bottom - sea - 1), new Color(140, 96, 60));
					}
					break;
				}
				case "Boreal":
					for (int band = 0; band < 3; band++) {
						Color c = band == 1 ? new Color(160, 80, 220) : new Color(60, 230, 160);
						for (int x = 0; x < s.Width; x += 6) {
							float y = 30 + band * 14 + (float)Math.Sin(t * 0.8f + x * 0.02f + band) * 12;
							CasinoDraw.Fill(sb, new Rectangle(s.X + x, s.Y + (int)y, 6, 16), c * (0.18f + 0.1f * (float)Math.Sin(t * 2 + x * 0.05f)));
						}
					}
					for (int k = 0; k < 30; k++) {
						float y = (t * 20 + k * 37) % s.Height, x = (k * 53 + (float)Math.Sin(t + k) * 8) % s.Width;
						CasinoDraw.Fill(sb, new Rectangle(s.X + (int)Math.Abs(x), s.Y + (int)y, 2, 2), Color.White * 0.8f);
					}
					CasinoDraw.Fill(sb, new Rectangle(s.X, s.Bottom - 16, s.Width, 16), new Color(220, 235, 250));
					break;
				default:
					float rise = round.InBonusRun ? 1f : 0.7f;
					Vector2 moon = new(s.Right - 120, s.Y + 60 - rise * 20);
					for (int r = 44; r > 0; r -= 4)
						FillCircle(sb, moon, r, Color.Lerp(new Color(255, 60, 50), new Color(150, 10, 20), r / 44f) * (r == 44 ? 0.4f : 1f));
					for (int k = 0; k < 5; k++) {
						float bx = (t * 40 + k * 113) % (s.Width + 40) - 20, by = 20 + k * 14 + (float)Math.Sin(t * 6 + k) * 4;
						bool flap = (int)(t * 10 + k) % 2 == 0;
						CasinoDraw.Fill(sb, new Rectangle(s.X + (int)bx - 4, s.Y + (int)by, 8, 2), Color.Black);
						CasinoDraw.Fill(sb, new Rectangle(s.X + (int)bx - 6, s.Y + (int)by + (flap ? -2 : 2), 3, 2), Color.Black);
						CasinoDraw.Fill(sb, new Rectangle(s.X + (int)bx + 3, s.Y + (int)by + (flap ? -2 : 2), 3, 2), Color.Black);
					}
					CasinoDraw.Fill(sb, new Rectangle(s.X, s.Bottom - 14, s.Width, 14), new Color(30, 4, 10));
					break;
			}

			Utils.DrawBorderString(sb, Text("Title" + key), new Vector2(s.Center.X, Area.Y + 22), accent, 1f, 0.5f, 0.5f);
			if (key == "Boreal")
				DrawAnimals(sb, round, s, t);
		}

		/// <summary>Figures that act on the grid are drawn after it so beams, lines and shots cross the cells.</summary>
		private void DrawStageForeground(SpriteBatch sb, VideoSlotRound round, string key, float t)
		{
			switch (key) {
				case "LunarGods": DrawMoonLord(sb, round, t); break;
				case "Angler": DrawAngler(sb, round, t); break;
				case "BloodMoon": DrawHunter(sb, round, t); break;
				case "Boreal": DrawSnowballs(sb, round, t); break;
			}
		}

		private bool ActiveStep(VideoSlotRound round, out SlotStep step, out int tick)
		{
			step = round.CurrentStep;
			tick = round.Timer;
			return round.Phase == VideoPhase.Steps && step != null;
		}

		// Moon Lord: raises his hands, opens his forehead eye and fires one deathray per new orb.
		private const int BeamStart = 16, BeamGap = 10, BeamLife = 18;

		private float MoonBob => (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.6f) * 3;

		private Vector2 MoonHeadTop => new(Stage.Center.X, Stage.Y - 4 + MoonBob);

		private Vector2 MoonEye => MoonHeadTop + new Vector2(0, 27);

		/// <summary>How far the hands are raised (0..1) and whether the eyes are open, for the current step.</summary>
		private void MoonPose(VideoSlotRound round, out float raise, out bool eyeOpen, out bool revealing, out SlotStep step, out int tick)
		{
			revealing = ActiveStep(round, out step, out tick) && step.Fx.Count > 0 && step.Kind is StepKind.Land or StepKind.Tumble;
			bool multiplying = round.Phase == VideoPhase.Steps && step?.Kind == StepKind.Multiply;
			raise = revealing ? MathHelper.Clamp(tick / 12f, 0, 1) : multiplying ? 1f : 0f;
			if (revealing && tick > BeamStart + BeamGap * step.Fx.Count + BeamLife)
				raise = MathHelper.Clamp(1 - (tick - (BeamStart + BeamGap * step.Fx.Count + BeamLife)) / 10f, 0, 1);
			eyeOpen = revealing && tick > 8 && raise > 0.2f || multiplying;
		}

		private Vector2 MoonHand(int side, float raise, float t)
			=> new(Stage.Center.X + side * (150 - raise * 30), Stage.Y + 74 - raise * 46 + (float)Math.Sin(t * 2 + side) * 3);

		private void DrawMoonLordBack(SpriteBatch sb, VideoSlotRound round, float t)
		{
			Texture2D head = mlHead.Value;
			MoonPose(round, out float raise, out bool eyeOpen, out _, out _, out _);
			// Arms from the shoulders (hidden behind the reels) up to the hands.
			for (int side = -1; side <= 1; side += 2) {
				Vector2 shoulder = MoonHeadTop + new Vector2(side * 50, 108);
				Vector2 wrist = MoonHand(side, raise, t) + new Vector2(0, 30);
				Vector2 elbow = (shoulder + wrist) / 2 + new Vector2(side * 30, 10);
				Line(sb, shoulder, elbow, new Color(90, 78, 60), 18);
				Line(sb, elbow, wrist, new Color(90, 78, 60), 15);
				Line(sb, shoulder, elbow, new Color(164, 146, 112), 13);
				Line(sb, elbow, wrist, new Color(164, 146, 112), 10);
				Line(sb, shoulder + new Vector2(0, -4), elbow + new Vector2(0, -4), new Color(196, 178, 140), 3);
			}
			var frame = new Rectangle(eyeOpen ? head.Width / 2 : 0, 0, head.Width / 2, head.Height);
			// Soft glow behind the bust when he is active.
			if (raise > 0) {
				Color glow = new Color(90, 220, 200) * (0.25f * raise);
				glow.A = 0;
				FillCircle(sb, MoonHeadTop + new Vector2(0, 40), 70, glow);
			}
			sb.Draw(head, MoonHeadTop, frame, Color.White, 0f, new Vector2(head.Width / 4f, 0), 1f, SpriteEffects.None, 0f);
		}

		private void DrawMoonLord(SpriteBatch sb, VideoSlotRound round, float t)
		{
			Texture2D hand = mlHand.Value;
			MoonPose(round, out float raise, out bool eyeOpen, out bool revealing, out SlotStep step, out int tick);
			for (int side = -1; side <= 1; side += 2) {
				var hfr = new Rectangle(eyeOpen ? hand.Width / 2 : 0, 0, hand.Width / 2, hand.Height);
				float tilt = side * (0.25f - raise * 0.2f);
				sb.Draw(hand, MoonHand(side, raise, t), hfr, Color.White, tilt, new Vector2(hand.Width / 4f, hand.Height * 0.55f), 0.9f, side < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
				if (eyeOpen) {
					Color palm = new Color(120, 240, 230) * (0.4f + 0.2f * (float)Math.Sin(t * 10 + side));
					palm.A = 0;
					FillCircle(sb, MoonHand(side, raise, t) + new Vector2(0, 2), 10, palm);
				}
			}
			if (eyeOpen) {
				Color glow = new Color(120, 230, 255) * (0.5f + 0.3f * (float)Math.Sin(t * 12));
				glow.A = 0;
				FillCircle(sb, MoonEye, 9, glow);
			}
			if (!revealing)
				return;
			for (int k = 0; k < step.Fx.Count; k++) {
				int start = BeamStart + BeamGap * k;
				if (tick < start || tick > start + BeamLife)
					continue;
				float life = (tick - start) / (float)BeamLife;
				Vector2 target = CellRect(step.Fx[k].Reel, step.Fx[k].Row).Center.ToVector2();
				Beam(sb, MoonEye, target, 1f - Math.Abs(life * 2 - 1));
				if (tick >= start + 6 && Once(100 + k))
					Burst(target, new Color(140, 240, 255), 12, false);
			}
		}

		private static void Beam(SpriteBatch sb, Vector2 from, Vector2 to, float strength)
		{
			Color outer = new Color(60, 200, 255) * (0.55f * strength), inner = new Color(220, 255, 255) * strength;
			outer.A = 0;
			inner.A = 0;
			Line(sb, from, to, outer, 18 * strength + 4);
			Line(sb, from, to, outer, 10 * strength + 2);
			Line(sb, from, to, inner, 4 * strength + 1);
			FillCircle(sb, to, 16 * strength + 4, outer);
		}

		// The Angler stands on the pier; in a collect he casts his line to every fish in turn.
		private Vector2 AnglerFeet => new(Stage.X + 64, Stage.Y + 52);

		private Vector2 RodTip => AnglerFeet + new Vector2(64, -64);

		private void DrawAngler(SpriteBatch sb, VideoSlotRound round, float t)
		{
			Main.instance.LoadNPC(NPCID.Angler);
			Texture2D tex = TextureAssets.Npc[NPCID.Angler].Value;
			int frames = Main.npcFrameCount[NPCID.Angler];
			var frame = new Rectangle(0, 0, tex.Width, tex.Height / Math.Max(1, frames));
			sb.Draw(tex, AnglerFeet, frame, Color.White, 0f, new Vector2(frame.Width / 2f, frame.Height - 4), 1.2f, SpriteEffects.FlipHorizontally, 0f);
			// Golden rod held out over the water.
			Main.instance.LoadItem(ItemID.GoldenFishingRod);
			Texture2D rod = TextureAssets.Item[ItemID.GoldenFishingRod].Value;
			Vector2 grip = AnglerFeet + new Vector2(10, -26);
			float angle = (RodTip - grip).ToRotation();
			sb.Draw(rod, grip, null, Color.White, angle + MathHelper.PiOver4, new Vector2(0, rod.Height), (RodTip - grip).Length() / (rod.Width * 1.41f), SpriteEffects.None, 0f);
			if (ActiveStep(round, out SlotStep step, out int tick) && step.Kind == StepKind.Collect) {
				for (int k = 0; k < step.Fx.Count; k++) {
					int start = 8 + 16 * k;
					if (tick < start)
						continue;
					Vector2 fish = CellRect(step.Fx[k].Reel, step.Fx[k].Row).Center.ToVector2();
					if (tick < start + 14)
						Line(sb, RodTip, Vector2.Lerp(RodTip, fish, MathHelper.Clamp((tick - start) / 6f, 0, 1)), Color.White * 0.85f, 1);
					if (tick >= start + 6 && Once(200 + k)) {
						foreach (Pos target in step.Targets)
							CoinFlight(fish, CellRect(target.Reel, target.Row).Center.ToVector2());
						SoundEngine.PlaySound(CasinoSounds.CoinsPayout with { Volume = 0.5f, Pitch = Math.Min(0.8f, k * 0.12f) });
					}
				}
			}
			else {
				Vector2 bob = new(RodTip.X + 60, Stage.Y + 60 + (float)Math.Sin(t * 3) * 2);
				Line(sb, RodTip, bob, Color.White * 0.6f, 1);
				CasinoDraw.Fill(sb, new Rectangle((int)bob.X - 3, (int)bob.Y - 4, 6, 4), Color.Red);
				CasinoDraw.Fill(sb, new Rectangle((int)bob.X - 3, (int)bob.Y, 6, 3), Color.White);
			}
		}

		// The hunter's boomstick at the right edge of the stage aims at the cells it shoots.
		private Vector2 Muzzle => new(Stage.Right - 50, Stage.Bottom - 30);

		private void DrawHunter(SpriteBatch sb, VideoSlotRound round, float t)
		{
			Main.instance.LoadItem(ItemID.Boomstick);
			Texture2D gun = TextureAssets.Item[ItemID.Boomstick].Value;
			float aim = MathHelper.Pi + 0.4f + (float)Math.Sin(t) * 0.05f;
			bool shooting = ActiveStep(round, out SlotStep step, out int tick) && step.Kind == StepKind.Shots;
			if (shooting) {
				int k = Math.Clamp((tick - 6) / 12, 0, step.Fx.Count - 1);
				Vector2 target = CellRect(step.Fx[k].Reel, step.Fx[k].Row).Center.ToVector2();
				aim = (target - Muzzle).ToRotation();
				for (int i = 0; i < step.Fx.Count; i++) {
					int start = 6 + 12 * i;
					if (tick >= start && tick < start + 5) {
						Vector2 tg = CellRect(step.Fx[i].Reel, step.Fx[i].Row).Center.ToVector2();
						Color tracer = new Color(255, 220, 140) * 0.9f;
						tracer.A = 0;
						Line(sb, Muzzle, tg, tracer, 3);
						FillCircle(sb, Muzzle, 10, tracer);
						if (Once(300 + i)) {
							Burst(tg, new Color(200, 20, 30), 10, false);
							if (i > 0)
								SoundEngine.PlaySound(SoundID.Item36 with { Volume = 0.5f });
						}
					}
				}
			}
			sb.Draw(gun, Muzzle, null, Color.White, aim, new Vector2(gun.Width * 0.8f, gun.Height / 2f), 1.6f, Math.Cos(aim) < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);
		}

		private void DrawSnowballs(SpriteBatch sb, VideoSlotRound round, float t)
		{
			if (!ActiveStep(round, out SlotStep step, out int tick) || step.Kind != StepKind.Shots)
				return;
			for (int k = 0; k < step.Fx.Count; k++) {
				int start = 6 + 12 * k;
				if (tick < start || tick > start + 10)
					continue;
				Vector2 target = CellRect(step.Fx[k].Reel, step.Fx[k].Row).Center.ToVector2();
				Vector2 from = new(Stage.X + 60 + k * 30, Stage.Bottom - 20);
				float p = (tick - start) / 10f;
				Vector2 pos = Vector2.Lerp(from, target, p) - new Vector2(0, (float)Math.Sin(p * Math.PI) * 60);
				FillCircle(sb, pos, 6, Color.White);
				if (tick >= start + 9 && Once(400 + k))
					Burst(target, new Color(200, 240, 255), 14, false);
			}
		}

		private static readonly int[] Animals = { NPCID.Penguin, NPCID.SnowFlinx, NPCID.Wolf, NPCID.IceGolem };

		private void DrawAnimals(SpriteBatch sb, VideoSlotRound round, Rectangle s, float t)
		{
			int chosen = round.InBonusRun ? round.Machine.Feature : -1;
			for (int k = 0; k < 4; k++) {
				Vector2 pos = new(s.X + 300 + k * 56, s.Bottom - 12);
				float bob = k == chosen ? (float)Math.Abs(Math.Sin(t * 5)) * -6 : 0;
				Color c = chosen < 0 || chosen == k ? Color.White : Color.White * 0.35f;
				DrawIcon(sb, -Animals[k], pos + new Vector2(0, -20 + bob), 40f, c);
			}
		}

		// ------------------------------------------------------------------ grid

		private void DrawGrid(SpriteBatch sb, VideoSlotRound round, string key, Color accent, float t)
		{
			SlotMachine m = round.Machine;
			Rectangle g = Grid;
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(g, -5), accent * 0.75f);
			CasinoDraw.Fill(sb, CasinoDraw.Shrink(g, -3), Color.Black);
			CasinoDraw.Fill(sb, g, new Color(12, 10, 18));
			int[] icons = SlotThemes.Icons(key);
			SpinResult result = round.Result;
			bool stepping = round.Phase == VideoPhase.Steps;
			SlotStep step = round.CurrentStep;
			SlotCell[,] grid = step?.Grid;
			if (round.Phase == VideoPhase.Spinning && result != null)
				grid = result.Steps[0].Grid;
			SlotCell[,] prev = stepping && round.Step > 0 ? result.Steps[round.Step - 1].Grid : null;

			// Tumble: kept cells fall by the number of removed cells below them, new ones fall from above.
			int[,] drop = null;
			if (stepping && step.Kind == StepKind.Tumble && round.Step > 0) {
				drop = new int[m.Reels, m.Rows];
				var removed = new bool[m.Reels, m.Rows];
				foreach (WinGroup w in result.Steps[round.Step - 1].Wins)
					foreach (Pos p in w.Cells)
						removed[p.Reel, p.Row] = true;
				for (int r = 0; r < m.Reels; r++) {
					int count = 0;
					for (int row = 0; row < m.Rows; row++)
						if (removed[r, row])
							count++;
					for (int row = 0; row < m.Rows; row++)
						drop[r, row] = count;
				}
			}
			float fall = drop == null ? 1f : MathHelper.Clamp(round.Timer / 12f, 0, 1);
			fall = 1 - (1 - fall) * (1 - fall);

			var winCells = new HashSet<Pos>();
			if (stepping)
				foreach (WinGroup w in step.Wins)
					foreach (Pos p in w.Cells)
						winCells.Add(p);

			for (int reel = 0; reel < m.Reels; reel++) {
				bool spinning = round.Phase == VideoPhase.Spinning && round.Timer < round.StopTick(reel);
				if (round.Teasing(reel)) {
					Color glow = accent * ((round.TeaseNext(reel) ? 0.45f : 0.2f) + 0.25f * (float)Math.Sin(t * 14));
					CasinoDraw.Fill(sb, new Rectangle(g.X + reel * Cell - 3, g.Y - 3, Cell + 6, g.Height + 6), glow);
				}
				for (int row = 0; row < m.Rows; row++) {
					Rectangle cell = CellRect(reel, row);
					CasinoDraw.Fill(sb, CasinoDraw.Shrink(cell, 2), (reel + row) % 2 == 0 ? new Color(30, 26, 40) : new Color(24, 21, 32));
				}
				if (spinning) {
					DrawSpinningReel(sb, round, reel, icons, t);
					continue;
				}
				if (grid == null) {
					for (int row = 0; row < m.Rows; row++)
						DrawSymbol(sb, key, icons, new SlotCell { Symbol = (reel * 3 + row * 2) % Math.Min(8, m.SymbolCount) }, CellRect(reel, row).Center.ToVector2(), 1f, Color.White, m);
					continue;
				}
				int sinceStop = round.Phase == VideoPhase.Spinning ? round.Timer - round.StopTick(reel) : 99;
				float landBounce = sinceStop < 8 ? (float)Math.Sin(sinceStop / 8f * Math.PI) * 6 : 0;
				for (int row = 0; row < m.Rows; row++) {
					Rectangle cell = CellRect(reel, row);
					SlotCell c = grid[reel, row];
					var pos = new Pos(reel, row);
					Vector2 center = cell.Center.ToVector2() + new Vector2(0, landBounce);
					if (drop != null)
						center.Y -= (1 - fall) * drop[reel, row] * Cell;
					if (drop != null && center.Y < g.Y + Cell * 0.3f)
						continue;

					// Cells changed by this step's event show their old symbol until the event reaches them.
					if (round.Phase == VideoPhase.Spinning && key == "LunarGods" && c.Symbol == LunarGodsSlot.Orb) {
						DrawPendingOrb(sb, center, t);
						continue;
					}
					int hitTick = HitTick(round, key, step, pos);
					if (hitTick >= 0 && round.Timer < hitTick) {
						if (key == "LunarGods" && c.Symbol == LunarGodsSlot.Orb) {
							DrawPendingOrb(sb, center, t);
							continue;
						}
						if (prev != null)
							c = prev[reel, row];
					}

					float scale = 1f;
					Color color = Color.White;
					if (winCells.Contains(pos)) {
						scale = 1.08f + 0.08f * (float)Math.Sin(t * 12);
						Color hl = Color.Lerp(Color.Gold, Color.White, 0.5f + 0.5f * (float)Math.Sin(t * 10)) * 0.35f;
						CasinoDraw.Fill(sb, CasinoDraw.Shrink(cell, 3), hl);
					}
					else if (stepping && step.Wins.Count > 0 && step.Kind is StepKind.Land or StepKind.Tumble)
						color = Color.White * 0.55f;
					DrawSymbol(sb, key, icons, c, center, scale, color, m);
					if (winCells.Contains(pos))
						DrawFrame(sb, cell, Color.Gold);
					if (c.Sticky)
						DrawFrame(sb, cell, key == "Boreal" ? new Color(170, 230, 255) : Color.Red);
				}
				if (stepping && step.Kind == StepKind.Expand && step.Fx.Count > 0 && step.Fx[0].Reel == reel) {
					float p = MathHelper.Clamp(round.Timer / 30f, 0, 1);
					var ice = new Rectangle(g.X + reel * Cell, g.Y, Cell, (int)(g.Height * p));
					CasinoDraw.Fill(sb, ice, new Color(170, 230, 255) * 0.35f);
					DrawFrame(sb, new Rectangle(g.X + reel * Cell, g.Y, Cell, g.Height), new Color(200, 240, 255));
				}
			}

			// Paylines of the current step.
			if (stepping && (step.Kind is StepKind.Land or StepKind.Tumble or StepKind.Shots or StepKind.Expand)) {
				foreach (WinGroup w in step.Wins) {
					if (w.Line < 0)
						continue;
					int[] line = m.Rows == 3 ? VideoLines.Lines5x3[w.Line] : VideoLines.Lines5x4[w.Line];
					Color c = Color.Lerp(Color.Gold, Color.White, 0.5f + 0.5f * (float)Math.Sin(t * 10 + w.Line)) * 0.8f;
					for (int r = 0; r < m.Reels - 1; r++)
						Line(sb, CellRect(r, line[r]).Center.ToVector2(), CellRect(r + 1, line[r + 1]).Center.ToVector2(), c, 2);
				}
			}

			// Running win of this spin.
			if (round.ShownUnits > 0 && round.Phase != VideoPhase.Spinning) {
				string text = Text("WinLabel", Games.Coins.Format(round.Coins(round.ShownUnits)));
				var box = new Rectangle(g.Center.X - 110, g.Bottom - 32, 220, 26);
				CasinoDraw.Fill(sb, box, Color.Black * 0.6f);
				Utils.DrawBorderString(sb, text, box.Center.ToVector2() + new Vector2(0, 3), Color.Gold, 0.9f, 0.5f, 0.5f);
			}

			if (stepping && step.Kind == StepKind.Multiply)
				DrawMultiply(sb, round, step, t);
			if (stepping && step.Kind == StepKind.Collect)
				DrawCollect(sb, round, step);
			if (stepping && step.Kind == StepKind.Upgrade)
				DrawUpgrade(sb, round, step, icons);
		}

		/// <summary>Tick within the step at which an event reaches this cell, or -1.</summary>
		private int HitTick(VideoSlotRound round, string key, SlotStep step, Pos pos)
		{
			if (round.Phase != VideoPhase.Steps || step == null)
				return -1;
			int k = step.Fx.IndexOf(pos);
			if (k < 0)
				return -1;
			return step.Kind switch {
				StepKind.Land or StepKind.Tumble when key == "LunarGods" => BeamStart + BeamGap * k + 6,
				StepKind.Shots when key == "Boreal" => 6 + 12 * k + 10,
				StepKind.Shots when key == "Angler" => 10,
				StepKind.Shots => 6 + 12 * k,
				StepKind.Expand => (int)(30f * (pos.Row + 1) / round.Machine.Rows),
				_ => -1,
			};
		}

		private void DrawSpinningReel(SpriteBatch sb, VideoSlotRound round, int reel, int[] icons, float t)
		{
			SlotMachine m = round.Machine;
			Rectangle g = Grid;
			bool tease = round.TeaseNext(reel);
			float speed = tease ? 0.35f : 0.55f;
			float scroll = round.Timer * speed + reel * 1.7f;
			int baseIndex = (int)Math.Floor(scroll);
			float frac = scroll - baseIndex;
			int count = Math.Min(8, m.SymbolCount);
			for (int k = -1; k <= m.Rows; k++) {
				float y = g.Y + (k + frac) * Cell + Cell / 2f;
				if (y < g.Y + Cell * 0.35f || y > g.Bottom - Cell * 0.35f)
					continue;
				int sym = Math.Abs((baseIndex - k) * 7 + reel * 3) % count;
				if ((baseIndex - k + reel) % 5 == 0)
					sym = m.Scatter;
				DrawSymbol(sb, round.Key, icons, new SlotCell { Symbol = sym, Value = 2 }, new Vector2(g.X + reel * Cell + Cell / 2f, y), 1f, Color.White * 0.75f, m);
			}
			CasinoDraw.Fill(sb, new Rectangle(g.X + reel * Cell + 2, g.Y, Cell - 4, g.Height), Color.White * 0.05f);
		}

		private void DrawSymbol(SpriteBatch sb, string key, int[] icons, SlotCell c, Vector2 center, float scale, Color color, SlotMachine m)
		{
			float size = Cell * 0.62f * scale;
			if (key == "LunarGods" && c.Symbol == LunarGodsSlot.Orb) {
				DrawOrb(sb, center, c.Value, scale, color);
				return;
			}
			int icon = icons[Math.Clamp(c.Symbol, 0, icons.Length - 1)];
			if (key == "Angler" && c.Symbol == AnglerSlot.Fish)
				icon = SlotThemes.FishIcon(c.Value);
			if (c.Symbol == m.Scatter) {
				Color halo = SlotThemes.Accent(key) * 0.35f * (color.A / 255f);
				halo.A = 0;
				FillCircle(sb, center, Cell * 0.42f, halo);
			}
			DrawIcon(sb, icon, center, size, color);
			float labelY = center.Y + Cell * 0.32f;
			if (c.Symbol == m.Wild)
				Utils.DrawBorderString(sb, c.Mult > 1 ? "WILD x" + c.Mult : Text("Wild"), new Vector2(center.X, labelY), Color.Gold * (color.A / 255f), 0.6f, 0.5f, 0.5f);
			else if (c.Symbol == m.Scatter)
				Utils.DrawBorderString(sb, Text("Scatter"), new Vector2(center.X, labelY), SlotThemes.Accent(key) * (color.A / 255f), 0.55f, 0.5f, 0.5f);
			else if (key == "Angler" && c.Symbol == AnglerSlot.Fish && c.Value > 0)
				Utils.DrawBorderString(sb, Games.Coins.Format(Round.Coins(c.Value) > 0 ? Round.Coins(c.Value) : c.Value), new Vector2(center.X, labelY), Color.Gold * (color.A / 255f), 0.6f, 0.5f, 0.5f);
		}

		private void DrawOrb(SpriteBatch sb, Vector2 center, int value, float scale, Color color)
		{
			Texture2D tex = orbs.Value;
			int tier = value >= 50 ? 3 : value >= 15 ? 2 : value >= 5 ? 1 : 0;
			var frame = new Rectangle(tier * tex.Width / 4, 0, tex.Width / 4, tex.Height);
			float s = Cell * 0.7f / frame.Width * scale;
			Color halo = (tier == 3 ? Color.Gold : tier == 2 ? Color.MediumPurple : tier == 1 ? Color.DeepSkyBlue : Color.LimeGreen) * 0.3f;
			halo.A = 0;
			FillCircle(sb, center, Cell * 0.45f, halo * (0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6)));
			sb.Draw(tex, center, frame, color, 0f, frame.Size() / 2f, s, SpriteEffects.None, 0f);
			Utils.DrawBorderString(sb, "x" + value, center + new Vector2(0, 3), Color.White * (color.A / 255f), value >= 100 ? 0.85f : 1f, 0.5f, 0.5f);
		}

		private void DrawPendingOrb(SpriteBatch sb, Vector2 center, float t)
		{
			for (int k = 0; k < 6; k++) {
				float ang = t * 6 + k * MathHelper.TwoPi / 6;
				CasinoDraw.Fill(sb, new Rectangle((int)(center.X + Math.Cos(ang) * 12) - 2, (int)(center.Y + Math.Sin(ang) * 12) - 2, 4, 4), new Color(140, 240, 255) * 0.7f);
			}
		}

		private void DrawMultiply(SpriteBatch sb, VideoSlotRound round, SlotStep step, float t)
		{
			int tick = round.Timer;
			Vector2 target = new(Grid.Center.X, Grid.Center.Y);
			foreach (Pos p in step.Fx) {
				Vector2 from = CellRect(p.Reel, p.Row).Center.ToVector2();
				float k = MathHelper.Clamp(tick / 24f, 0, 1);
				Color c = new Color(150, 240, 255) * (1 - k);
				c.A = 0;
				Line(sb, from, Vector2.Lerp(from, target, k), c, 3);
			}
			float pop = MathHelper.Clamp((tick - 20) / 10f, 0, 1);
			if (pop <= 0)
				return;
			var box = new Rectangle((int)target.X - 150, (int)target.Y - 46, 300, 92);
			CasinoDraw.Fill(sb, box, Color.Black * 0.75f * pop);
			DrawFrame(sb, box, Color.Gold * pop);
			Utils.DrawBorderString(sb, Games.Coins.Format(round.Coins(step.Value)) + "  x" + step.Multiplier, target + new Vector2(0, -18), Color.White * pop, 1.1f, 0.5f, 0.5f);
			Utils.DrawBorderString(sb, "= " + Games.Coins.Format(round.Coins(step.Win)), target + new Vector2(0, 20), Color.Gold * pop, 1.4f + 0.1f * (float)Math.Sin(t * 8), 0.5f, 0.5f);
		}

		private void DrawCollect(SpriteBatch sb, VideoSlotRound round, SlotStep step)
		{
			int tick = round.Timer, end = 8 + 16 * step.Fx.Count;
			foreach (Pos p in step.Targets) {
				Rectangle cell = CellRect(p.Reel, p.Row);
				DrawFrame(sb, cell, Color.Gold);
			}
			if (tick < end)
				return;
			var box = new Rectangle(Grid.Center.X - 140, Grid.Center.Y - 30, 280, 60);
			CasinoDraw.Fill(sb, box, Color.Black * 0.7f);
			string mult = step.Multiplier > 1 ? "  x" + step.Multiplier : "";
			Utils.DrawBorderString(sb, Text("Collected", Games.Coins.Format(round.Coins(step.Win))) + mult, box.Center.ToVector2() + new Vector2(0, 3), Color.Gold, 1.05f, 0.5f, 0.5f);
		}

		private void DrawUpgrade(SpriteBatch sb, VideoSlotRound round, SlotStep step, int[] icons)
		{
			var box = new Rectangle(Grid.Center.X - 160, Grid.Center.Y - 50, 320, 100);
			CasinoDraw.Fill(sb, box, Color.Black * 0.8f);
			DrawFrame(sb, box, Color.Red);
			Utils.DrawBorderString(sb, Text("Upgrade"), new Vector2(box.Center.X, box.Y + 14), Color.OrangeRed, 0.9f, 0.5f, 0.5f);
			Vector2 left = new(box.Center.X - 70, box.Center.Y + 12), right = new(box.Center.X + 70, box.Center.Y + 12);
			DrawIcon(sb, icons[step.Value], left, 44f, Color.White * 0.6f);
			Line(sb, left + new Vector2(-18, -18), left + new Vector2(18, 18), Color.Red, 4);
			Line(sb, left + new Vector2(18, -18), left + new Vector2(-18, 18), Color.Red, 4);
			Utils.DrawBorderString(sb, "->", box.Center.ToVector2() + new Vector2(0, 14), Color.White, 1.2f, 0.5f, 0.5f);
			DrawIcon(sb, icons[step.Value2], right, 44f * (1 + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10)), Color.White);
		}

		// ------------------------------------------------------------------ overlays

		private void DrawOverlays(SpriteBatch sb, VideoSlotRound round, string key, Color accent, float t)
		{
			Rectangle g = CasinoDraw.Shrink(Grid, -4);
			if (round.Phase == VideoPhase.Intro) {
				CasinoDraw.Fill(sb, g, Color.Black * 0.8f);
				int spins = round.Machine.FreeSpinsLeft;
				if (key == "Boreal")
					DrawLottery(sb, round, g, t);
				else {
					float pulse = 1.4f + 0.12f * (float)Math.Sin(t * 8);
					Utils.DrawBorderString(sb, Text("FreeSpins", spins), new Vector2(g.Center.X, g.Center.Y - 30), accent, pulse, 0.5f, 0.5f);
					CasinoDraw.Wrapped(sb, Text("Feature" + key), new Vector2(g.Center.X, g.Center.Y + 14), Color.White, 0.75f, g.Width - 30, 0.5f);
				}
			}
			else if (round.Phase == VideoPhase.Outro) {
				CasinoDraw.Fill(sb, g, Color.Black * 0.8f);
				float k = MathHelper.Clamp(round.Timer / 90f, 0, 1);
				Utils.DrawBorderString(sb, Text("BonusOver"), new Vector2(g.Center.X, g.Center.Y - 34), accent, 1.2f, 0.5f, 0.5f);
				Utils.DrawBorderString(sb, Games.Coins.Format((long)(round.BonusTotal * k)), new Vector2(g.Center.X, g.Center.Y + 14), Color.Gold, 1.6f + 0.1f * (float)Math.Sin(t * 8), 0.5f, 0.5f);
			}
			if (round.BigWinTimer > 0) {
				long units = round.BigWinUnits;
				string label = units >= SlotMachine.Unit * 100 ? Text("EpicWin") : units >= SlotMachine.Unit * 50 ? Text("MegaWin") : Text("BigWin");
				float k = MathHelper.Clamp((150 - round.BigWinTimer) / 70f, 0, 1);
				float scale = 1.8f + 0.2f * (float)Math.Sin(t * 10);
				var box = new Rectangle(g.Center.X - 200, g.Center.Y - 60, 400, 120);
				CasinoDraw.Fill(sb, box, Color.Black * 0.6f);
				Utils.DrawBorderString(sb, label, new Vector2(g.Center.X, g.Center.Y - 22), Color.Lerp(Color.Gold, Color.White, 0.5f + 0.5f * (float)Math.Sin(t * 14)), scale, 0.5f, 0.5f);
				Utils.DrawBorderString(sb, Games.Coins.Format((long)(round.Coins(units) * k)), new Vector2(g.Center.X, g.Center.Y + 28), Color.Gold, 1.3f, 0.5f, 0.5f);
			}
		}

		private int lastLit = -1;

		/// <summary>Boreal bonus lottery: a light runs over the four animals, slows down and stops on the chosen one.</summary>
		private void DrawLottery(SpriteBatch sb, VideoSlotRound round, Rectangle g, float t)
		{
			int chosen = round.Machine.Feature;
			int tick = round.Timer;
			const int SpinEnd = 150;
			int lit;
			if (tick >= SpinEnd)
				lit = chosen;
			else {
				// Steps get slower; total steps chosen so the light ends on the right animal.
				double p = tick / (double)SpinEnd;
				int steps = 16 + chosen;
				lit = (int)(steps * (1 - Math.Pow(1 - p, 2.2))) % 4;
			}
			if (lit != lastLit) {
				lastLit = lit;
				SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.2f + tick / 200f });
			}
			if (tick >= SpinEnd && Once(500))
				SoundEngine.PlaySound(SoundID.Item30);
			Utils.DrawBorderString(sb, Text("Lottery"), new Vector2(g.Center.X, g.Y + 22), new Color(190, 235, 255), 1f, 0.5f, 0.5f);
			int cw = Math.Min(110, g.Width / 4 - 10);
			for (int k = 0; k < 4; k++) {
				var card = new Rectangle(g.Center.X - 2 * (cw + 8) + k * (cw + 8) + 4, g.Y + 48, cw, 130);
				bool on = k == lit;
				CasinoDraw.Fill(sb, card, on ? new Color(190, 235, 255) : new Color(40, 60, 90));
				CasinoDraw.Fill(sb, CasinoDraw.Shrink(card, 3), on ? new Color(30, 60, 100) : new Color(16, 24, 40));
				float bob = on && tick >= SpinEnd ? (float)Math.Abs(Math.Sin(t * 6)) * -6 : 0;
				DrawIcon(sb, -Animals[k], new Vector2(card.Center.X, card.Y + 52 + bob), 64f, on ? Color.White : Color.White * 0.5f);
				Utils.DrawBorderString(sb, Text("Animal" + k), new Vector2(card.Center.X, card.Bottom - 22), on ? Color.White : Color.Gray, 0.7f, 0.5f, 0.5f);
			}
			if (tick >= SpinEnd) {
				CasinoDraw.Wrapped(sb, Text("AnimalFeature" + chosen, round.Machine.FreeSpinsLeft), new Vector2(g.Center.X, g.Bottom - 52), Color.White, 0.75f, g.Width - 30, 0.5f);
				if (Once(501))
					for (int k = 0; k < 30; k++)
						Burst(new Vector2(g.Center.X - 2 * (cw + 8) + chosen * (cw + 8) + 4 + cw / 2, g.Y + 100), new Color(200, 240, 255), 1, false);
			}
		}

		// ------------------------------------------------------------------ side panel

		private void DrawSide(SpriteBatch sb, VideoSlotRound round, string key, Color accent, Rectangle r)
		{
			CasinoDraw.Fill(sb, r, Color.Black * 0.45f);
			SlotMachine m = round.Machine;
			float y = r.Y + 8;
			if (round.InBonusRun) {
				CasinoDraw.Fill(sb, new Rectangle(r.X + 4, (int)y - 2, r.Width - 8, 24), accent * (0.5f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6)));
				Utils.DrawBorderString(sb, Text("FreeSpinsLeft", m.FreeSpinsLeft, m.FreeSpinsTotal), new Vector2(r.Center.X, y + 12), Color.White, 0.85f, 0.5f, 0.5f);
				y += 28;
				Utils.DrawBorderString(sb, Text("BonusSoFar", Games.Coins.Format(round.BonusTotal)), new Vector2(r.X + 10, y), Color.Gold, 0.75f);
				y += 22;
				switch (m) {
					case LunarGodsSlot:
						Utils.DrawBorderString(sb, Text("TotalMult", m.BonusMultiplier), new Vector2(r.X + 10, y), new Color(150, 240, 255), 0.9f);
						y += 26;
						break;
					case AnglerSlot angler:
						Utils.DrawBorderString(sb, Text("Anglers", angler.WildsCollected % 4, m.BonusMultiplier), new Vector2(r.X + 10, y), new Color(150, 220, 255), 0.75f);
						for (int k = 0; k < 4; k++)
							CasinoDraw.Fill(sb, new Rectangle(r.X + 10 + k * 22, (int)y + 22, 18, 8), k < angler.WildsCollected % 4 ? Color.Gold : Color.DimGray);
						y += 40;
						break;
					case BorealSlot:
						Utils.DrawBorderString(sb, Text("Animal" + m.Feature), new Vector2(r.X + 10, y), new Color(190, 235, 255), 0.9f);
						y += 26;
						break;
					case BloodMoonSlot hunter:
						Utils.DrawBorderString(sb, Text("Trophies", hunter.WildsCollected % 3, hunter.Upgrades), new Vector2(r.X + 10, y), new Color(255, 120, 120), 0.75f);
						int[] icons = SlotThemes.Icons(key);
						for (int k = 0; k < 4; k++) {
							Vector2 c = new(r.X + 22 + k * 40, y + 38);
							DrawIcon(sb, icons[k], c, 26f, k < hunter.Upgrades ? Color.White * 0.35f : Color.White);
							if (k < hunter.Upgrades) {
								Line(sb, c + new Vector2(-12, -12), c + new Vector2(12, 12), Color.Red, 3);
								Line(sb, c + new Vector2(12, -12), c + new Vector2(-12, 12), Color.Red, 3);
							}
						}
						y += 62;
						break;
				}
				y += 4;
			}

			Utils.DrawBorderString(sb, Language.GetTextValue("Mods.CasinoWorld.UI.Slots.Pays"), new Vector2(r.X + 10, y), accent, 0.85f);
			y += 20;
			y += CasinoDraw.Wrapped(sb, Text("PayType" + key), new Vector2(r.X + 10, y), Color.LightGray, 0.55f, r.Width - 20) + 4;
			int[] all = SlotThemes.Icons(key);
			foreach (PayRow row in m.PayTable()) {
				if (y > r.Bottom - 70)
					break;
				DrawIcon(sb, all[row.Symbol], new Vector2(r.X + 20, y + 10), 22f, Color.White);
				CasinoDraw.Fit(sb, row.Pays, new Vector2(r.X + 38, y + 3), row.Symbol == m.Wild ? Color.Gold : Color.White, 0.6f, r.Width - 46);
				y += 23;
			}
			DrawIcon(sb, all[m.Scatter], new Vector2(r.X + 20, y + 10), 22f, Color.White);
			CasinoDraw.Fit(sb, Text("ScatterRule" + key), new Vector2(r.X + 38, y + 3), accent, 0.55f, r.Width - 46);
			y += 26;
			CasinoDraw.Wrapped(sb, Text("Feature" + key), new Vector2(r.X + 10, y), accent * 0.9f, 0.55f, r.Width - 20);
		}

		// ------------------------------------------------------------------ particles and helpers

		private void Burst(Vector2 at, Color color, int count, bool coins)
		{
			for (int k = 0; k < count; k++) {
				particles.Add(new Particle {
					Pos = at,
					Vel = Main.rand.NextVector2Circular(coins ? 4 : 3, coins ? 4 : 3) - new Vector2(0, coins ? 3 : 0.5f),
					Color = color,
					Life = 0,
					Max = Main.rand.Next(30, 60),
					Size = Main.rand.NextFloat(2, 5),
					Coin = coins,
					Gravity = coins,
				});
			}
		}

		private void CoinFlight(Vector2 from, Vector2 to)
		{
			for (int k = 0; k < 6; k++) {
				particles.Add(new Particle {
					Pos = from + Main.rand.NextVector2Circular(8, 8),
					Vel = (to - from) / 24f + Main.rand.NextVector2Circular(0.6f, 0.6f),
					Color = Color.Gold,
					Max = 24,
					Size = 3,
					Coin = true,
				});
			}
		}

		private void UpdateParticles()
		{
			for (int k = particles.Count - 1; k >= 0; k--) {
				Particle p = particles[k];
				p.Pos += p.Vel;
				if (p.Gravity)
					p.Vel.Y += 0.18f;
				p.Vel *= 0.98f;
				if (++p.Life >= p.Max)
					particles.RemoveAt(k);
			}
			if (particles.Count > 600)
				particles.RemoveRange(0, particles.Count - 600);
		}

		private void DrawParticles(SpriteBatch sb)
		{
			Texture2D coin = TextureAssets.Coin[2].Value;
			int fh = coin.Height / 8;
			foreach (Particle p in particles) {
				float fade = 1f - p.Life / (float)p.Max;
				if (p.Coin) {
					var frame = new Rectangle(0, ((p.Life / 4) % 8) * fh, coin.Width, fh - 2);
					sb.Draw(coin, p.Pos, frame, Color.White * fade, 0f, frame.Size() / 2f, 1f, SpriteEffects.None, 0f);
				}
				else {
					Color c = p.Color * fade;
					c.A = 0;
					CasinoDraw.Fill(sb, new Rectangle((int)p.Pos.X, (int)p.Pos.Y, (int)p.Size, (int)p.Size), c);
				}
			}
		}

		private static void DrawFrame(SpriteBatch sb, Rectangle r, Color c)
		{
			CasinoDraw.Fill(sb, new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, 2), c);
			CasinoDraw.Fill(sb, new Rectangle(r.X + 2, r.Bottom - 4, r.Width - 4, 2), c);
			CasinoDraw.Fill(sb, new Rectangle(r.X + 2, r.Y + 2, 2, r.Height - 4), c);
			CasinoDraw.Fill(sb, new Rectangle(r.Right - 4, r.Y + 2, 2, r.Height - 4), c);
		}

		private static void Line(SpriteBatch sb, Vector2 a, Vector2 b, Color c, float width)
		{
			Vector2 d = b - a;
			if (d.LengthSquared() < 0.01f)
				return;
			sb.Draw(TextureAssets.MagicPixel.Value, a, new Rectangle(0, 0, 1, 1), c, d.ToRotation(), new Vector2(0, 0.5f), new Vector2(d.Length(), width), SpriteEffects.None, 0f);
		}

		private static void FillCircle(SpriteBatch sb, Vector2 center, float radius, Color c)
		{
			for (int y = -(int)radius; y <= radius; y += 2) {
				int half = (int)Math.Sqrt(Math.Max(0, radius * radius - y * y));
				CasinoDraw.Fill(sb, new Rectangle((int)center.X - half, (int)center.Y + y, half * 2, 2), c);
			}
		}

		/// <summary>Draws an item icon (id &gt; 0) or an NPC portrait (id &lt; 0, first frame, facing right).</summary>
		public static void DrawIcon(SpriteBatch sb, int id, Vector2 center, float maxSize, Color color)
		{
			if (id == 0)
				return;
			Texture2D tex;
			Rectangle frame;
			SpriteEffects fx = SpriteEffects.None;
			if (id > 0) {
				Main.instance.LoadItem(id);
				tex = TextureAssets.Item[id].Value;
				frame = Main.itemAnimations[id] != null ? Main.itemAnimations[id].GetFrame(tex) : tex.Frame();
			}
			else {
				int type = -id;
				Main.instance.LoadNPC(type);
				tex = TextureAssets.Npc[type].Value;
				frame = new Rectangle(0, 0, tex.Width, tex.Height / Math.Max(1, Main.npcFrameCount[type]));
				fx = SpriteEffects.FlipHorizontally;
			}
			float scale = Math.Min(2f, maxSize / Math.Max(frame.Width, frame.Height));
			sb.Draw(tex, center, frame, color, 0f, frame.Size() / 2f, scale, fx, 0f);
		}
	}
}
