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
		public static readonly SoundStyle RevolverShot = S("RevolverShot", 0.65f, 0.15f, 4);
		public static readonly SoundStyle CannonShot = S("CannonShot", 0.65f, 0.15f, 4);
		public static readonly SoundStyle ChipShotgun = S("ChipShotgun", 0.65f, 0.15f, 4);
		public static readonly SoundStyle RakeSweep = S("RakeSweep", 0.65f, 0.15f, 4);
		public static readonly SoundStyle BarThrow = S("BarThrow", 0.65f, 0.15f, 4);
		public static readonly SoundStyle BarSlam = S("BarSlam", 0.65f, 0.15f, 4);
		public static readonly SoundStyle SpectralThrow = S("SpectralThrow", 0.65f, 0.15f, 4);
		public static readonly SoundStyle MeteorCall = S("MeteorCall", 0.65f, 0.15f, 4);
		public static readonly SoundStyle SlotBlaster = S("SlotBlaster", 0.65f, 0.15f, 4);
		public static readonly SoundStyle CoinRain = S("CoinRain", 0.65f, 0.15f, 4);
		public static readonly SoundStyle GalaxyThrow = S("GalaxyThrow", 0.65f, 0.15f, 4);
		public static readonly SoundStyle PlinkoPeg = S("PlinkoPeg", 0.45f, 0f, 6);
		public static readonly SoundStyle PlinkoLand = S("PlinkoLand", 0.8f, 0f, 2);
		public static readonly SoundStyle DerbyBell = S("DerbyBell", 0.8f, 0f, 2);
		public static readonly SoundStyle DerbyStart = S("DerbyStart", 0.8f, 0f, 2);
		public static readonly SoundStyle HiLoWin = S("HiLoWin", 0.8f, 0f, 2);
		public static readonly SoundStyle HiLoLose = S("HiLoLose", 0.8f, 0f, 2);
		public static readonly SoundStyle NearMiss = S("NearMiss", 0.8f, 0f, 2);
		public static readonly SoundStyle HeartBeat = S("HeartBeat", 0.8f, 0f, 2);
		public static readonly SoundStyle MegaWin = S("MegaWin", 0.8f, 0f, 2);
		public static readonly SoundStyle GateOpen = S("GateOpen", 0.8f, 0f, 2);
		public static readonly SoundStyle WaterRise = S("WaterRise", 0.8f, 0f, 2);
		public static readonly SoundStyle IceShatter = S("IceShatter", 0.8f, 0f, 2);
		public static readonly SoundStyle HuntHorn = S("HuntHorn", 0.8f, 0f, 2);
		public static readonly SoundStyle DiamondBeam = S("DiamondBeam", 0.8f, 0f, 2);
		public static readonly SoundStyle ElevatorRide = S("ElevatorRide", 0.7f, 0f, 1);
		public static readonly SoundStyle FortunaVoice = S("FortunaVoice", 1f, 0f, 1);
		public static readonly SoundStyle FortunaSpin = S("FortunaSpin", 0.9f, 0f, 2);
		public static readonly SoundStyle FortunaBeam = S("FortunaBeam", 0.9f, 0f, 2);
		public static readonly SoundStyle FortunaCharge = S("FortunaCharge", 0.9f, 0f, 2);
		public static readonly SoundStyle FortunaReel = S("FortunaReel", 0.9f, 0f, 4);
		public static readonly SoundStyle FortunaHole = S("FortunaHole", 1f, 0f, 1);
		public static readonly SoundStyle FortunaBall = S("FortunaBall", 0.7f, 0.1f, 3);
		public static readonly SoundStyle FortunaTear = S("FortunaTear", 1f, 0f, 1);
		public static readonly SoundStyle FortunaDeath = S("FortunaDeath", 1f, 0f, 1);
		public static readonly SoundStyle OphanimSpin = S("OphanimSpin", 0.6f, 0.1f, 3);
		public static readonly SoundStyle Achievement = S("Achievement", 0.8f, 0f, 1);
		public static readonly SoundStyle VipUp = S("VipUp", 0.9f, 0f, 1);
		public static readonly SoundStyle PokerCheck = S("PokerCheck", 0.8f, 0.1f, 3);
		public static readonly SoundStyle PokerFold = S("PokerFold", 0.7f, 0.1f, 3);
		public static readonly SoundStyle DuelToss = S("DuelToss", 0.9f, 0f, 2);
		public static readonly SoundStyle HookChain = S("HookChain", 0.6f, 0.15f, 3);
	}

	/// <summary>Music slots of the casino tracks (autoloaded from Assets/Music).</summary>
	public static class CasinoMusic
	{
		public static int Lounge => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/CasinoLounge");
		public static int HighRollers => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/HighRollers");
		public static int TheHouse => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/TheHouse");
		public static int TheHouseWheel => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/TheHouseWheel");
		public static int PitBoss => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/PitBoss");
		public static int BonusLunar => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/BonusLunar");
		public static int BonusAngler => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/BonusAngler");
		public static int BonusBoreal => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/BonusBoreal");
		public static int BonusBlood => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/BonusBlood");
		public static int Fortuna => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/Fortuna");
		public static int FortunaTwo => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/FortunaTwo");
		public static int Penthouse => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/Penthouse");
		public static int PitBossTwo => MusicLoader.GetMusicSlot(ModContent.GetInstance<CasinoWorld>(), "Assets/Music/PitBossTwo");
	}
}
