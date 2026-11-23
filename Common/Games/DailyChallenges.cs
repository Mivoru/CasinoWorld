using System;
using System.Text;
using CasinoWorld.Content.Items;
using CasinoWorld.Content.NPCs.Enemies;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CasinoWorld.Common.Games
{
	public enum Challenge { BlackjackWins, RouletteWins, SlotWins, Wagers, Jackpots, CasinoKills, VideoBonus, Crates }

	/// <summary>
	/// Three casino challenges per real-world day (like the daily Fortune Wheel), shown by the Prize
	/// Cashier. Each pays loyalty tokens when done; all three also pay a crate. Progress is local.
	/// </summary>
	public class DailyChallenges : ModPlayer
	{
		private static readonly int[] Targets = { 3, 5, 8, 20, 25, 20, 1, 2 };
		private static readonly int[] Rewards = { 40, 40, 35, 25, 35, 40, 60, 30 };

		private long day = -1;
		private readonly Challenge[] today = new Challenge[3];
		private readonly int[] progress = new int[3];
		private bool bonusPaid;

		private static long Today => DateTime.Now.Date.Ticks / TimeSpan.TicksPerDay;

		private void Refresh()
		{
			if (day == Today)
				return;
			day = Today;
			bonusPaid = false;
			var rng = new Random((int)(Today * 31 + Player.name.GetHashCode()));
			var pool = new System.Collections.Generic.List<Challenge>((Challenge[])Enum.GetValues(typeof(Challenge)));
			for (int k = 0; k < 3; k++) {
				int pick = rng.Next(pool.Count);
				today[k] = pool[pick];
				pool.RemoveAt(pick);
				progress[k] = 0;
			}
		}

		/// <summary>Counts towards today's challenges for the local player.</summary>
		public static void Count(Player player, Challenge kind, int amount = 1)
		{
			if (player.whoAmI != Main.myPlayer || Main.dedServ)
				return;
			player.GetModPlayer<DailyChallenges>().Add(kind, amount);
		}

		private void Add(Challenge kind, int amount)
		{
			Refresh();
			for (int k = 0; k < 3; k++) {
				if (today[k] != kind || progress[k] >= Targets[(int)kind])
					continue;
				progress[k] = Math.Min(Targets[(int)kind], progress[k] + amount);
				if (progress[k] >= Targets[(int)kind])
					Complete(kind);
			}
		}

		private void Complete(Challenge kind)
		{
			Coins.GiveItem(Player, new Item(ModContent.ItemType<LoyaltyToken>(), Rewards[(int)kind]));
			Main.NewText(Language.GetTextValue("Mods.CasinoWorld.Challenges.Done", Language.GetTextValue("Mods.CasinoWorld.Challenges." + kind, Targets[(int)kind]), Rewards[(int)kind]), new Color(255, 215, 90));
			SoundEngine.PlaySound(CasinoSounds.SmallWin);
			bool all = true;
			for (int k = 0; k < 3; k++)
				all &= progress[k] >= Targets[(int)today[k]];
			if (all && !bonusPaid) {
				bonusPaid = true;
				int crate = Main.hardMode ? ModContent.ItemType<Content.Items.Crates.HighRollerCrate>() : ModContent.ItemType<Content.Items.Crates.LuckyCrate>();
				Coins.GiveItem(Player, new Item(crate));
				Main.NewText(Language.GetTextValue("Mods.CasinoWorld.Challenges.AllDone"), new Color(255, 215, 90));
				SoundEngine.PlaySound(CasinoSounds.Jackpot);
			}
		}

		/// <summary>Chat text for the Prize Cashier: today's challenges with progress.</summary>
		public string Describe()
		{
			Refresh();
			var sb = new StringBuilder(Language.GetTextValue("Mods.CasinoWorld.Challenges.Header"));
			for (int k = 0; k < 3; k++) {
				Challenge c = today[k];
				int target = Targets[(int)c];
				bool done = progress[k] >= target;
				sb.Append('\n').Append(done ? "[c/80FF80:" : "[c/FFFFFF:")
					.Append(Language.GetTextValue("Mods.CasinoWorld.Challenges." + c, target))
					.Append($"  {progress[k]}/{target}  ({Rewards[(int)c]} ")
					.Append(Language.GetTextValue("Mods.CasinoWorld.Challenges.Tokens")).Append(")]");
			}
			sb.Append('\n').Append(Language.GetTextValue(bonusPaid ? "Mods.CasinoWorld.Challenges.BonusClaimed" : "Mods.CasinoWorld.Challenges.Bonus"));
			return sb.ToString();
		}

		/// <summary>Lethal hits on casino enemies count as kills for the attacker.</summary>
		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (target.life <= 0 && target.ModNPC != null && target.ModNPC.Mod == Mod && !target.boss && !target.friendly && target.ModNPC is not Content.NPCs.CasinoNPC)
				Count(Player, Challenge.CasinoKills);
		}

		public override void SaveData(TagCompound tag)
		{
			tag["challengeDay"] = day;
			tag["challengeKinds"] = new[] { (int)today[0], (int)today[1], (int)today[2] };
			tag["challengeProgress"] = progress;
			tag["challengeBonus"] = bonusPaid;
		}

		public override void LoadData(TagCompound tag)
		{
			day = tag.ContainsKey("challengeDay") ? tag.GetLong("challengeDay") : -1;
			if (tag.ContainsKey("challengeKinds")) {
				int[] kinds = tag.GetIntArray("challengeKinds");
				int[] prog = tag.GetIntArray("challengeProgress");
				for (int k = 0; k < 3 && k < kinds.Length; k++) {
					today[k] = (Challenge)kinds[k];
					progress[k] = k < prog.Length ? prog[k] : 0;
				}
			}
			bonusPaid = tag.ContainsKey("challengeBonus") && tag.GetBool("challengeBonus");
		}
	}
}
