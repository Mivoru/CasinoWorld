using System;
using System.Collections.Generic;
using CasinoWorld.Common.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CasinoWorld.Common.Gambler
{
	/// <summary>
	/// The Gambler's own resource: the Pot. Gambler hits feed it (busts feed it too: losses make the pot
	/// grow), it drains when you stop fighting. Full pot: press the All In key for five seconds of high
	/// stakes (+25 % jackpot, +10 % bust) opened by a shockwave of cards.
	/// Measured: about 14 s to fill at 5 hits/s, so All In is up ~25 % of a fight: about +12 % DPS on
	/// average (+45 % while active), plus the burst. Comparable to vanilla class tools (Nebula, stealth).
	/// </summary>
	public class GamblerPot : ModPlayer
	{
		public const float Max = 100f;
		public const int AllInTicks = 300;
		private const int DrainDelay = 180;

		public static ModKeybind AllInKey { get; private set; }

		public float Pot;
		private int sinceHit;
		private bool announcedFull;

		public bool Full => Pot >= Max;

		public bool AllInActive => Player.HasBuff(ModContent.BuffType<Content.Buffs.AllIn>());

		public override void Load()
		{
			AllInKey = KeybindLoader.RegisterKeybind(Mod, "AllIn", "V");
		}

		public override void Unload() => AllInKey = null;

		/// <summary>Called by GamblerPlayer for every gambler hit roll.</summary>
		public void Feed(bool jackpot, bool bust)
		{
			if (AllInActive)
				return;
			Pot = Math.Min(Max, Pot + (jackpot ? 3f : bust ? 2f : 1f));
			sinceHit = 0;
		}

		public override void PostUpdate()
		{
			if (Player.whoAmI != Main.myPlayer)
				return;
			if (++sinceHit > DrainDelay && !Full && Pot > 0)
				Pot = Math.Max(0, Pot - 0.25f);
			if (Full && !announcedFull) {
				announcedFull = true;
				SoundEngine.PlaySound(CasinoSounds.ChipStack with { Pitch = 0.4f });
				CasinoFx.Ring(Player.Center, new Color(255, 210, 90), 12, 3f);
			}
			if (!Full)
				announcedFull = false;
		}

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (AllInKey?.JustPressed != true || !Full || AllInActive)
				return;
			Pot = 0;
			Player.AddBuff(ModContent.BuffType<Content.Buffs.AllIn>(), AllInTicks);
			// Opening shockwave: three times the held gambler weapon's damage around the player.
			Item held = Player.HeldItem;
			int damage = held.CountsAsClass<GamblerDamageClass>() ? Player.GetWeaponDamage(held) * 3 : 30;
			Projectile.NewProjectile(Player.GetSource_Misc("AllIn"), Player.Center, Vector2.Zero, ModContent.ProjectileType<Content.Projectiles.AllInBurst>(), damage, 8f, Player.whoAmI);
		}

		public override void OnRespawn() => Pot = 0;
	}

	/// <summary>Draws the Pot meter under the local player while a gambler weapon is held or the pot is not empty.</summary>
	public class GamblerPotUI : ModSystem
	{
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Entity Health Bars");
			if (index < 0)
				return;
			layers.Insert(index + 1, new LegacyGameInterfaceLayer("CasinoWorld: Pot", () => {
				Draw(Main.spriteBatch);
				return true;
			}, InterfaceScaleType.Game));
		}

		private static void Draw(SpriteBatch sb)
		{
			Player player = Main.LocalPlayer;
			if (player.dead || Main.gameMenu)
				return;
			GamblerPot pot = player.GetModPlayer<GamblerPot>();
			bool holding = player.HeldItem.CountsAsClass<GamblerDamageClass>();
			if (!holding && pot.Pot <= 0 && !pot.AllInActive)
				return;
			Texture2D px = TextureAssets.MagicPixel.Value;
			Vector2 at = player.Bottom - Main.screenPosition + new Vector2(0, 14 + player.gfxOffY);
			var frame = new Rectangle((int)at.X - 27, (int)at.Y, 54, 10);
			float t = Main.GlobalTimeWrappedHourly;
			sb.Draw(px, frame, new Color(40, 22, 14));
			sb.Draw(px, new Rectangle(frame.X + 1, frame.Y + 1, frame.Width - 2, frame.Height - 2), new Color(150, 100, 30));
			var inner = new Rectangle(frame.X + 2, frame.Y + 2, frame.Width - 4, frame.Height - 4);
			sb.Draw(px, inner, new Color(20, 14, 20));
			float fill;
			Color c;
			if (pot.AllInActive) {
				int left = player.buffTime[player.FindBuffIndex(ModContent.BuffType<Content.Buffs.AllIn>())];
				fill = left / (float)GamblerPot.AllInTicks;
				c = Color.Lerp(new Color(255, 60, 60), new Color(255, 220, 90), 0.5f + 0.5f * (float)Math.Sin(t * 16));
			}
			else {
				fill = pot.Pot / GamblerPot.Max;
				c = pot.Full ? Color.Lerp(new Color(255, 210, 80), Color.White, 0.5f + 0.5f * (float)Math.Sin(t * 10)) : new Color(230, 170, 50);
			}
			int w = (int)(inner.Width * fill);
			sb.Draw(px, new Rectangle(inner.X, inner.Y, w, inner.Height), c);
			sb.Draw(px, new Rectangle(inner.X, inner.Y, w, 1), Color.White * 0.5f);
			// Chip notches every quarter.
			for (int k = 1; k < 4; k++)
				sb.Draw(px, new Rectangle(inner.X + inner.Width * k / 4, inner.Y, 1, inner.Height), Color.Black * 0.4f);
			if (pot.Full && !pot.AllInActive) {
				string key = GamblerPot.AllInKey?.GetAssignedKeys() is { Count: > 0 } keys ? keys[0] : "?";
				Utils.DrawBorderString(sb, Language.GetTextValue("Mods.CasinoWorld.Gambler.AllInReady", key), new Vector2(at.X, frame.Bottom + 2), new Color(255, 220, 90), 0.75f, 0.5f, 0f);
			}
		}
	}
}
