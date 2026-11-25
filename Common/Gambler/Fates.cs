using System.Collections.Generic;
using System.Linq;
using CasinoWorld.Common.Effects;
using CasinoWorld.Common.Games;
using CasinoWorld.Common.Systems;
using CasinoWorld.Content.Items.Fortuna;
using Microsoft.Xna.Framework;
using CasinoWorld.Content.NPCs.Boss;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CasinoWorld.Common.Gambler
{
	public enum Fate { None, Control, Dealer, Fool }

	/// <summary>
	/// Fates: after Fortuna a Gambler picks one way to play, bought once with Fate Shards and switched for free outside
	/// boss fights. Each gives and takes; measured in tools/gambler_fates.py against no Fate.
	/// Control: jackpots only double, every normal hit deals 50 % more. Good at low Luck, worse at high Luck.
	/// Dealer: the Pot fills 50 % faster and All In lasts 50 % longer, but outside All In damage is 10 % lower.
	/// Fool: jackpots deal 3.25x, but at least 12 % of hits bust and a bust hurts you.
	/// </summary>
	public class FatePlayer : ModPlayer
	{
		public const int Price = 15;
		public const float ControlJackpot = 2f, ControlNormal = 1.5f;
		public const float DealerGain = 0.5f, DealerTime = 1.5f, DealerDamage = 0.9f;
		public const float FoolJackpot = 3.25f, FoolBust = 0.12f;
		private const int FoolHurt = 12, FoolCooldown = 60;

		public readonly HashSet<Fate> Owned = new();
		public Fate Active;
		private int foolTimer;

		public static Color ColorOf(Fate fate) => fate switch {
			Fate.Control => new Color(120, 200, 255),
			Fate.Dealer => new Color(255, 215, 90),
			Fate.Fool => new Color(255, 100, 120),
			_ => Color.Gray,
		};

		public static string Text(string key, params object[] args) => Language.GetTextValue("Mods.CasinoWorld.Fates." + key, args);

		private static bool BossAlive()
		{
			foreach (NPC npc in Main.ActiveNPCs)
				if (npc.boss)
					return true;
			return false;
		}

		/// <summary>Click on a Fate: buy it, wear it or take it off. Returns a message, or null when nothing needs saying.</summary>
		public string Choose(Fate fate)
		{
			if (!CasinoDowned.DownedFortuna && Owned.Count == 0)
				return Text("Locked");
			if (BossAlive())
				return Text("InFight");
			if (!Owned.Contains(fate)) {
				int shard = ModContent.ItemType<FateShard>();
				if (Player.CountItem(shard, Price) < Price)
					return Text("NoShards", Price);
				for (int k = 0; k < Price; k++)
					Player.ConsumeItem(shard);
				Owned.Add(fate);
				Active = fate;
				SoundEngine.PlaySound(CasinoSounds.VipUp);
				UiFx.Banner(Text(fate + ".Name"), ColorOf(fate), 0, 2);
				CasinoFx.Ring(Player.Center, ColorOf(fate), 30, 7f, 1.4f);
				return null;
			}
			Active = Active == fate ? Fate.None : fate;
			SoundEngine.PlaySound(CasinoSounds.CardFlip);
			return null;
		}

		public override void PostUpdateEquips()
		{
			if (foolTimer > 0)
				foolTimer--;
			if (Active == Fate.Dealer)
				Player.GetModPlayer<GamblerPot>().GainMultiplier += DealerGain;
		}

		/// <summary>A bust under the Fool's Fate: costs life, at most once a second, and never kills.</summary>
		public void FoolBusted()
		{
			if (Active != Fate.Fool || foolTimer > 0 || Player.whoAmI != Main.myPlayer)
				return;
			foolTimer = FoolCooldown;
			int loss = System.Math.Min(FoolHurt, Player.statLife - 1);
			if (loss <= 0)
				return;
			Player.statLife -= loss;
			CombatText.NewText(Player.Hitbox, CombatText.LifeRegenNegative, loss, false, true);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				NetMessage.SendData(MessageID.PlayerLifeMana, number: Player.whoAmI);
		}

		public override void SaveData(TagCompound tag)
		{
			tag["owned"] = Owned.Select(f => f.ToString()).ToList();
			tag["active"] = Active.ToString();
		}

		public override void LoadData(TagCompound tag)
		{
			Owned.Clear();
			foreach (string name in tag.GetList<string>("owned"))
				if (System.Enum.TryParse(name, out Fate fate) && fate != Fate.None)
					Owned.Add(fate);
			Active = System.Enum.TryParse(tag.GetString("active"), out Fate active) && Owned.Contains(active) ? active : Fate.None;
		}
	}
}
