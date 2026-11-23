using Terraria.Audio;
using Terraria.ModLoader;

namespace CasinoWorld.Common
{
	/// <summary>
	/// Casino sound effects and music, generated with ElevenLabs (tools/elevenlabs_gen.py) and converted to .ogg.
	/// Small pitch variance keeps repeated sounds (chips, cards, ticks) from sounding mechanical.
	/// </summary>
	public static class CasinoSounds
	{
		private static SoundStyle S(string name, float volume = 1f, float pitchVariance = 0f, int maxInstances = 3)
			=> new($"CasinoWorld/Assets/Sounds/{name}") { Volume = volume, PitchVariance = pitchVariance, MaxInstances = maxInstances };

		public static readonly SoundStyle ChipPlace = S("ChipPlace", 0.7f, 0.25f, 6);
		public static readonly SoundStyle ChipStack = S("ChipStack", 0.8f, 0.15f);
		public static readonly SoundStyle RouletteSpin = S("RouletteSpin", 0.9f, 0f, 2);
		public static readonly SoundStyle RouletteBallDrop = S("RouletteBallDrop", 0.9f);
		public static readonly SoundStyle ReelSpin = S("ReelSpin", 0.7f, 0f, 2);
		public static readonly SoundStyle ReelStop = S("ReelStop", 0.8f, 0.1f, 4);
		public static readonly SoundStyle LeverPull = S("LeverPull", 0.9f, 0.1f);
		public static readonly SoundStyle SmallWin = S("SmallWin", 0.8f);
		public static readonly SoundStyle Jackpot = S("Jackpot", 1f, 0f, 1);
		public static readonly SoundStyle CardDeal = S("CardDeal", 0.8f, 0.3f, 6);
		public static readonly SoundStyle CardShuffle = S("CardShuffle", 0.8f);
		public static readonly SoundStyle CoinsPayout = S("CoinsPayout", 0.8f, 0.1f);
		public static readonly SoundStyle CrateOpen = S("CrateOpen", 0.9f);
		public static readonly SoundStyle CrateTick = S("CrateTick", 0.5f, 0.15f, 8);
		public static readonly SoundStyle RareReveal = S("RareReveal", 1f, 0f, 1);
		public static readonly SoundStyle UpgradeWork = S("UpgradeWork", 0.8f, 0f, 1);
		public static readonly SoundStyle UpgradeSuccess = S("UpgradeSuccess", 1f);
		public static readonly SoundStyle UpgradeFail = S("UpgradeFail", 0.9f);
		public static readonly SoundStyle BossSpin = S("BossSpin", 1f, 0f, 1);
		public static readonly SoundStyle BossBust = S("BossBust", 1f, 0f, 1);
		public static readonly SoundStyle BossBreak = S("BossBreak", 1.1f, 0f, 1);
		public static readonly SoundStyle BossBell = S("BossBell", 1f, 0.05f, 2);
		public static readonly SoundStyle DoorEnter = S("DoorEnter", 0.9f, 0f, 1);
		public static readonly SoundStyle VaultDoor = S("VaultDoor", 1f, 0f, 1);
		public static readonly SoundStyle DiceThrow = S("DiceThrow", 0.6f, 0.2f, 4);
		public static readonly SoundStyle DiceBounce = S("DiceBounce", 0.55f, 0.3f, 6);
		public static readonly SoundStyle CardThrow = S("CardThrow", 0.55f, 0.25f, 6);
		public static readonly SoundStyle CardHit = S("CardHit", 0.55f, 0.25f, 6);
		public static readonly SoundStyle ChipFlick = S("ChipFlick", 0.55f, 0.2f, 6);
		public static readonly SoundStyle SlotTease = S("SlotTease", 0.8f, 0f, 1);
		public static readonly SoundStyle BonusFanfare = S("BonusFanfare", 0.9f, 0f, 1);
		public static readonly SoundStyle BigWin = S("BigWin", 0.9f, 0f, 1);
		public static readonly SoundStyle ChipHit = S("ChipHit", 0.5f, 0.25f, 6);
		public static readonly SoundStyle JackpotProc = S("JackpotProc", 0.6f, 0.1f, 3);
		public static readonly SoundStyle BustProc = S("BustProc", 0.35f, 0.1f, 2);
		public static readonly SoundStyle ChipExplode = S("ChipExplode", 0.8f, 0.15f, 4);
		public static readonly SoundStyle RouletteBallBounce = S("RouletteBallBounce", 0.7f, 0.1f, 2);
		public static readonly SoundStyle CardFlip = S("CardFlip", 0.7f, 0.2f, 4);
		public static readonly SoundStyle CardBridge = S("CardBridge", 0.7f, 0f, 1);
		public static readonly SoundStyle CashRegister = S("CashRegister", 0.7f, 0.05f, 2);
		public static readonly SoundStyle CrowdCheer = S("CrowdCheer", 0.6f, 0f, 1);
		public static readonly SoundStyle ElevatorDing = S("ElevatorDing", 0.8f, 0f, 1);
		public static readonly SoundStyle ChipSlimeHit = S("ChipSlimeHit", 0.6f, 0.2f, 4);
		public static readonly SoundStyle ChipSlimeDeath = S("ChipSlimeDeath", 0.8f, 0.1f, 3);
		public static readonly SoundStyle CardSoldierHit = S("CardSoldierHit", 0.6f, 0.2f, 4);
		public static readonly SoundStyle CardSoldierDeath = S("CardSoldierDeath", 0.8f, 0.1f, 3);
		public static readonly SoundStyle SlotMimicHit = S("SlotMimicHit", 0.6f, 0.2f, 4);
		public static readonly SoundStyle SlotMimicDeath = S("SlotMimicDeath", 0.9f, 0f, 2);
		public static readonly SoundStyle HouseLaugh = S("HouseLaugh", 1f, 0f, 1);
		public static readonly SoundStyle HookChain = S("HookChain", 0.6f, 0.15f, 3);
	}

	/// <summary>Music slots of the casino tracks (autoloaded from Assets/Music).</summary>
	public static class CasinoMusic
	{
		public static int Lounge => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/CasinoLounge");
		public static int HighRollers => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/HighRollers");
		public static int TheHouse => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/TheHouse");
		public static int TheHouseWheel => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/TheHouseWheel");
	}
}
