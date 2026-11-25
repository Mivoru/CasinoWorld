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
	/// Three casino challenges per in-game day, shown by the Prize Cashier. Each pays loyalty tokens when
	/// done; all three also pay a crate. Everything here belongs to one character (saved with it).
	///
	/// A new set arrives at dawn. World time stands still inside a casino, so time spent there is counted
	/// separately: a full day's worth (24 minutes) since the last new set also brings a new one.
	/// </summary>
	public class DailyChallenges : ModPlayer
	{
		private static readonly int[] Targets = { 3, 5, 8, 20, 25, 20, 1, 2 };
		private static readonly int[] Rewards = { 40, 40, 35, 25, 35, 40, 60, 30 };

		private const int TicksPerDay = (int)(Main.dayLength + Main.nightLength);

		private readonly Challenge[] today = new Challenge[3];
		private readonly int[] progress = new int[3];
		private bool bonusPaid;
		private bool rolled;

		/// <summary>Ticks spent in casinos since the last new set. Saved, so short visits add up.</summary>
		private int casinoTicks;

		/// <summary>Day or night on the last tick in the main world; null right after entering a world.</summary>
		private bool? wasDay;

		public override void OnEnterWorld() => wasDay = null;

		public override void PreUpdate()
		{
			if (Player.whoAmI != Main.myPlayer)
				return;
			if (Systems.CasinoTravel.InCasino) {
				wasDay = null;
				if (++casinoTicks >= TicksPerDay)
					NewDay();
				return;
			}
			if (wasDay == false && Main.dayTime)
				NewDay();
			wasDay = Main.dayTime;
		}

		private void NewDay()
		{
			Roll();
			Main.NewText(Language.GetTextValue("Mods.CasinoWorld.Challenges.New"), new Color(255, 215, 90));
		}

		private void Refresh()
		{
			if (!rolled)
				Roll();
		}

		/// <summary>Draws three challenges; none of them repeats from the previous set.</summary>
		private void Roll()
		{
			var pool = new System.Collections.Generic.List<Challenge>((Challenge[])Enum.GetValues(typeof(Challenge)));
			if (rolled)
				pool.RemoveAll(c => Array.IndexOf(today, c) >= 0);
			if (!Main.hardMode)
				pool.Remove(Challenge.VideoBonus);   // video slots are in the hardmode casino
			for (int k = 0; k < 3; k++) {
				int pick = Main.rand.Next(pool.Count);
				today[k] = pool[pick];
				pool.RemoveAt(pick);
				progress[k] = 0;
			}
			bonusPaid = false;
			rolled = true;
			casinoTicks = 0;
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
			if (Club.CasinoStats.Local is { } stats) {
				stats.Challenges++;
				stats.TokensEarned += Rewards[(int)kind];
			}
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
			tag["challengeCasinoTicks"] = casinoTicks;
			if (!rolled)
				return;
			tag["challengeKinds"] = new[] { (int)today[0], (int)today[1], (int)today[2] };
			tag["challengeProgress"] = (int[])progress.Clone();
			tag["challengeBonus"] = bonusPaid;
		}

		public override void LoadData(TagCompound tag)
		{
			casinoTicks = tag.GetInt("challengeCasinoTicks");
			// Saves from when challenges followed the real-world date (no tick counter) get a new set.
			rolled = tag.ContainsKey("challengeKinds") && tag.ContainsKey("challengeCasinoTicks");
			if (rolled) {
				int[] kinds = tag.GetIntArray("challengeKinds");
				int[] prog = tag.GetIntArray("challengeProgress");
				for (int k = 0; k < 3 && k < kinds.Length; k++) {
					today[k] = (Challenge)kinds[k];
					progress[k] = k < prog.Length ? prog[k] : 0;
				}
			}
			bonusPaid = rolled && tag.ContainsKey("challengeBonus") && tag.GetBool("challengeBonus");
		}
	}
}
