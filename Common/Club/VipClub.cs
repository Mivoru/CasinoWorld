using System.Collections.Generic;
using System.IO;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Games;
using CasinoWorld.Common.Net;
using CasinoWorld.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace CasinoWorld.Common.Club
{
	/// <summary>
	/// VIP club: four levels bought for coins at the Concierge. Perks are prestige and higher table limits, never
	/// combat power. The limits apply to games with a house edge only (roulette, blackjack, Sic Bo, duels), so
	/// betting more is itself a sink; slots return over 100 % and keep their limit.
	/// </summary>
	public static class VipClub
	{
		public const int MaxLevel = 4;
		public static readonly long[] Price = { 0, Coins.Platinum, 5 * Coins.Platinum, 20 * Coins.Platinum, 75 * Coins.Platinum };
		public static readonly int[] Limit = { 1, 1, 2, 3, 5 };
		public static readonly Color[] Colors = { Color.Gray, new(210, 220, 235), new(255, 210, 90), new(170, 230, 255), new(140, 255, 245) };
		public const long PenthousePrice = 25 * Coins.Platinum;
		public const int PenthouseLevel = 3;

		public static string LevelName(int level) => Language.GetTextValue("Mods.CasinoWorld.UI.Vip.Level" + level);

		/// <summary>World progress needed for a level.</summary>
		public static bool Open(int level) => level switch {
			2 => Main.hardMode,
			3 => NPC.downedPlantBoss,
			4 => NPC.downedMoonlord,
			_ => true,
		};

		public static int LocalLevel => Main.dedServ || Main.gameMenu ? 0 : Main.LocalPlayer.GetModPlayer<VipPlayer>().Level;

		/// <summary>Table limit of this casino for the local player.</summary>
		public static long MaxBet => (CasinoTableGameSystem.Casino?.MaxBet ?? 0) * Limit[LocalLevel];

		/// <summary>Buys the next level; returns the text the Concierge says.</summary>
		public static string BuyNext(Player player)
		{
			VipPlayer vip = player.GetModPlayer<VipPlayer>();
			int next = vip.Level + 1;
			if (next > MaxLevel)
				return Text("Top");
			if (!Open(next))
				return Text("Locked" + next);
			if (!Coins.TryTake(player, Price[next]))
				return Text("NoMoney", Coins.Format(Price[next]));
			vip.Level = next;
			vip.Sync();
			SoundEngine.PlaySound(CasinoSounds.VipUp);
			UiFx.Banner(Text("Welcome", LevelName(next)), Colors[next], 0, next >= 3 ? 2 : 1);
			UiFx.Coins_(UiFx.ScreenCenter, 50, 12f);
			CasinoFx.Ring(player.Center, Colors[next], 30, 7f, 1.4f);
			return Text("Bought" + next);
		}

		public static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.UI.Vip." + key, args);
	}

	public class VipPlayer : ModPlayer
	{
		public int Level;
		public bool VisitedPenthouse;

		public override void SaveData(TagCompound tag)
		{
			tag["level"] = Level;
			tag["penthouse"] = VisitedPenthouse;
		}

		public override void LoadData(TagCompound tag)
		{
			Level = (int)MathHelper.Clamp(tag.GetInt("level"), 0, VipClub.MaxLevel);
			VisitedPenthouse = tag.GetBool("penthouse");
		}

		public override void OnEnterWorld()
		{
			if (!CasinoTravel.InCasino || Level < 1)
				return;
			Player.AddBuff(Main.hardMode ? BuffID.WellFed2 : BuffID.WellFed, 60 * 60 * 8);
			Main.NewText(VipClub.Text("Drink"), VipClub.Colors[Level]);
		}

		// ---- multiplayer: others see the badge

		public void Sync() => SyncPlayer(-1, Player.whoAmI, false);

		public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
		{
			if (Main.netMode == NetmodeID.SinglePlayer)
				return;
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)CasinoMessage.VipLevel);
			packet.Write((byte)Player.whoAmI);
			packet.Write((byte)Level);
			packet.Send(toWho, fromWho);
		}

		public static void HandleSync(BinaryReader reader, int sender)
		{
			int who = reader.ReadByte();
			int level = reader.ReadByte();
			if (Main.netMode == NetmodeID.Server)
				who = sender;
			VipPlayer vip = Main.player[who].GetModPlayer<VipPlayer>();
			vip.Level = (int)MathHelper.Clamp(level, 0, VipClub.MaxLevel);
			if (Main.netMode == NetmodeID.Server)
				vip.SyncPlayer(-1, sender, false);
		}
	}

	/// <summary>Badges over the heads of members inside the casinos.</summary>
	public class VipBadges : ModSystem
	{
		private static Asset<Texture2D> badge;

		public override void Unload() => badge = null;

		public static void Draw(SpriteBatch sb, int level, Vector2 center, float scale = 1f)
		{
			badge ??= ModContent.Request<Texture2D>("CasinoWorld/Assets/UI/VipBadge", AssetRequestMode.ImmediateLoad);
			Rectangle frame = badge.Value.Frame(1, VipClub.MaxLevel, 0, level - 1);
			sb.Draw(badge.Value, center, frame, Color.White, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
		}

		public override void PostDrawTiles()
		{
			if (!CasinoTravel.InCasino && !CasinoTravel.InPenthouse)
				return;
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
			foreach (Player player in Main.ActivePlayers) {
				int level = player.GetModPlayer<VipPlayer>().Level;
				if (level <= 0 || player.dead)
					continue;
				Vector2 at = player.Top - Main.screenPosition + new Vector2(0, -22 + player.gfxOffY + (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 2f);
				if (level >= 3)
					PixelFx.Glow(Main.spriteBatch, at, 12f, VipClub.Colors[level] * 0.6f);
				Draw(Main.spriteBatch, level, PixelFx.Snap(at));
			}
			Main.spriteBatch.End();
		}
	}
}
