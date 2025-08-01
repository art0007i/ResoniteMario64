using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ResoniteMario64.libsm64;

public static class SM64Constants
{
    public enum SM64TerrainType
    {
        Grass = 0x0000,
        Stone = 0x0001,
        Snow = 0x0002,
        Sand = 0x0003,
        Spooky = 0x0004,
        Water = 0x0005,
        Slide = 0x0006
    }

    public enum SM64SurfaceType
    {
        Default = 0x0000,                // Environment default
        Burning = 0x0001,                // Lava / Frostbite (in SL), but is used mostly for Lava
        Unused0004 = 0x0004,             // Unused, has no function and has parameters
        Hangable = 0x0005,               // Ceiling that Mario can climb on
        Slow = 0x0009,                   // Slow down Mario, unused
        DeathPlane = 0x000A,             // Death floor
        CloseCamera = 0x000B,            // Close camera
        Water = 0x000D,                  // Water, has no action, used on some waterboxes below
        FlowingWater = 0x000E,           // Water (flowing), has parameters
        Intangible = 0x0012,             // Intangible (Separates BBH mansion from merry-go-round, for room usage)
        VerySlippery = 0x0013,           // Very slippery, mostly used for slides
        Slippery = 0x0014,               // Slippery
        NotSlippery = 0x0015,            // Non-slippery, climbable
        TtmVines = 0x0016,               // TTM vines, has no action defined
        MgrMusic = 0x001A,               // Plays the Merry go round music, see handle_merry_go_round_music in bbh_merry_go_round.inc.c for more details
        ShallowQuicksand = 0x0021,       // Shallow Quicksand (depth of 10 units)
        DeepQuicksand = 0x0022,          // Quicksand (lethal, slow, depth of 160 units)
        InstantQuicksand = 0x0023,       // Quicksand (lethal, instant)
        DeepMovingQuicksand = 0x0024,    // Moving quicksand (flowing, depth of 160 units)
        ShallowMovingQuicksand = 0x0025, // Moving quicksand (flowing, depth of 25 units)
        Quicksand = 0x0026,              // Moving quicksand (60 units)
        MovingQuicksand = 0x0027,        // Moving quicksand (flowing, depth of 60 units)
        WallMisc = 0x0028,               // Used for some walls, Cannon to adjust the camera, and some objects like Warp Pipe
        NoiseDefault = 0x0029,           // Default floor with noise
        NoiseSlippery = 0x002A,          // Slippery floor with noise
        HorizontalWind = 0x002C,         // Horizontal wind, has parameters
        InstantMovingQuicksand = 0x002D, // Quicksand (lethal, flowing)
        Ice = 0x002E,                    // Slippery Ice, in snow levels and THI's water floor
        Hard = 0x0030,                   // Hard floor (Always has fall damage)
        TimerStart = 0x0033,             // Timer start (Peach's secret slide)
        TimerEnd = 0x0034,               // Timer stop (Peach's secret slide)
        HardSlippery = 0x0035,           // Hard and slippery (Always has fall damage)
        HardVerySlippery = 0x0036,       // Hard and very slippery (Always has fall damage)
        HardNotSlippery = 0x0037,        // Hard and Non-slippery (Always has fall damage)
        VerticalWind = 0x0038,           // Death at bottom with vertical wind
        BossFightCamera = 0x0065,        // Wide camera for BoB and WF bosses
        CameraFreeRoam = 0x0066,         // Free roam camera for THI and TTC
        Thi3Wallkick = 0x0068,           // Surface where there's a wall kick section in THI 3rd area, has no action defined
        Camera8Dir = 0x0069,             // Surface that enables far camera for platforms, used in THI
        CameraMiddle = 0x006E,           // Surface camera that returns to the middle, used on the 4 pillars of SSL
        CameraRotateRight = 0x006F,      // Surface camera that rotates to the right (Bowser 1 & THI)
        CameraRotateLeft = 0x0070,       // Surface camera that rotates to the left (BoB & TTM)
        CameraBoundary = 0x0072,         // Intangible Area, only used to restrict camera movement
        NoiseVerySlippery73 = 0x0073,    // Very slippery floor with noise, unused
        NoiseVerySlippery74 = 0x0074,    // Very slippery floor with noise, unused
        NoiseVerySlippery = 0x0075,      // Very slippery floor with noise, used in CCM
        NoCamCollision = 0x0076,         // Surface with no cam collision flag
        NoCamCollision77 = 0x0077,       // Surface with no cam collision flag, unused
        NoCamColVerySlippery = 0x0078,   // Surface with no cam collision flag, very slippery with noise (THI)
        NoCamColSlippery = 0x0079,       // Surface with no cam collision flag, slippery with noise (CCM, PSS and TTM slides)
        Switch = 0x007A,                 // Surface with no cam collision flag, non-slippery with noise, used by switches and Dorrie
        VanishCapWalls = 0x007B,         // Vanish cap walls, pass through them with Vanish Cap
        Trapdoor = 0x00FF                // Bowser Left trapdoor, has no action defined
    }

    public enum SM64InteractableType
    {
        None = -1,
        GoldCoin = 0,
        RedCoin = 1,
        BlueCoin = 2,
        Star = 3,
        NormalCap = 4,
        VanishCap = 5,
        MetalCap = 6,
        WingCap = 7,
        Damage = 8
    }

#region Music

    // seq_ids.h
    private const ushort SequenceVariation = 0x80;

    /// <summary>
    /// Represents the different music sequences in the game.
    /// </summary>
    [Flags]
    public enum MusicSequence : ushort
    {
        SoundPlayer = 0x00,
        SoundPlayerVariation = SoundPlayer | SequenceVariation,
        EventCutsceneCollectStar = 0x01,
        EventCutsceneCollectStarVariation = EventCutsceneCollectStar | SequenceVariation,
        MenuTitleScreen = 0x02,
        MenuTitleScreenVariation = MenuTitleScreen | SequenceVariation,
        LevelGrass = 0x03,
        LevelGrassVariation = LevelGrass | SequenceVariation,
        LevelInsideCastle = 0x04,
        LevelInsideCastleVariation = LevelInsideCastle | SequenceVariation,
        LevelWater = 0x05,
        LevelWaterVariation = LevelWater | SequenceVariation,
        LevelHot = 0x06,
        LevelHotVariation = LevelHot | SequenceVariation,
        LevelBossKoopa = 0x07,
        LevelBossKoopaVariation = LevelBossKoopa | SequenceVariation,
        LevelSnow = 0x08,
        LevelSnowVariation = LevelSnow | SequenceVariation,
        LevelSlide = 0x09,
        LevelSlideVariation = LevelSlide | SequenceVariation,
        LevelSpooky = 0x0A,
        LevelSpookyVariation = LevelSpooky | SequenceVariation,
        EventPiranhaPlant = 0x0B,
        EventPiranhaPlantVariation = EventPiranhaPlant | SequenceVariation,
        LevelUnderground = 0x0C,
        LevelUndergroundVariation = LevelUnderground | SequenceVariation,
        MenuStarSelect = 0x0D,
        MenuStarSelectVariation = MenuStarSelect | SequenceVariation,
        EventPowerup = 0x0E,
        EventPowerupVariation = EventPowerup | SequenceVariation,
        EventMetalCap = 0x0F,
        EventMetalCapVariation = EventMetalCap | SequenceVariation,
        EventKoopaMessage = 0x10,
        EventKoopaMessageVariation = EventKoopaMessage | SequenceVariation,
        LevelKoopaRoad = 0x11,
        LevelKoopaRoadVariation = LevelKoopaRoad | SequenceVariation,
        EventHighScore = 0x12,
        EventHighScoreVariation = EventHighScore | SequenceVariation,
        EventMerryGoRound = 0x13,
        EventMerryGoRoundVariation = EventMerryGoRound | SequenceVariation,
        EventRace = 0x14,
        EventRaceVariation = EventRace | SequenceVariation,
        EventCutsceneStarSpawn = 0x15,
        EventCutsceneStarSpawnVariation = EventCutsceneStarSpawn | SequenceVariation,
        EventBoss = 0x16,
        EventBossVariation = EventBoss | SequenceVariation,
        EventCutsceneCollectKey = 0x17,
        EventCutsceneCollectKeyVariation = EventCutsceneCollectKey | SequenceVariation,
        EventEndlessStairs = 0x18,
        EventEndlessStairsVariation = EventEndlessStairs | SequenceVariation,
        LevelBossKoopaFinal = 0x19,
        LevelBossKoopaFinalVariation = LevelBossKoopaFinal | SequenceVariation,
        EventCutsceneCredits = 0x1A,
        EventCutsceneCreditsVariation = EventCutsceneCredits | SequenceVariation,
        EventSolvePuzzle = 0x1B,
        EventSolvePuzzleVariation = EventSolvePuzzle | SequenceVariation,
        EventToadMessage = 0x1C,
        EventToadMessageVariation = EventToadMessage | SequenceVariation,
        EventPeachMessage = 0x1D,
        EventPeachMessageVariation = EventPeachMessage | SequenceVariation,
        EventCutsceneIntro = 0x1E,
        EventCutsceneIntroVariation = EventCutsceneIntro | SequenceVariation,
        EventCutsceneVictory = 0x1F,
        EventCutsceneVictoryVariation = EventCutsceneVictory | SequenceVariation,
        EventCutsceneEnding = 0x20,
        EventCutsceneEndingVariation = EventCutsceneEnding | SequenceVariation,
        MenuFileSelect = 0x21,
        MenuFileSelectVariation = MenuFileSelect | SequenceVariation,
        EventCutsceneLakitu = 0x22,
        EventCutsceneLakituVariation = EventCutsceneLakitu | SequenceVariation,
        Count,
        None = 0xFFFF
    }

    public static readonly ushort[] Musics =
    {
        (ushort)MusicSequence.MenuTitleScreen,
        (ushort)MusicSequence.MenuTitleScreenVariation,
        (ushort)MusicSequence.LevelGrass,
        (ushort)MusicSequence.LevelGrassVariation,
        (ushort)MusicSequence.LevelInsideCastle,
        (ushort)MusicSequence.LevelInsideCastleVariation,
        (ushort)MusicSequence.LevelWater,
        (ushort)MusicSequence.LevelWaterVariation,
        (ushort)MusicSequence.LevelHot,
        (ushort)MusicSequence.LevelHotVariation,
        (ushort)MusicSequence.LevelBossKoopa,
        (ushort)MusicSequence.LevelBossKoopaVariation,
        (ushort)MusicSequence.LevelSnow,
        (ushort)MusicSequence.LevelSnowVariation,
        (ushort)MusicSequence.LevelSlide,
        (ushort)MusicSequence.LevelSlideVariation,
        (ushort)MusicSequence.LevelSpooky,
        (ushort)MusicSequence.LevelSpookyVariation,
        (ushort)MusicSequence.EventPowerup,
        (ushort)MusicSequence.EventPowerupVariation,
        (ushort)MusicSequence.EventMetalCap,
        (ushort)MusicSequence.EventMetalCapVariation,
        (ushort)MusicSequence.LevelKoopaRoad,
        (ushort)MusicSequence.LevelKoopaRoadVariation,
        (ushort)MusicSequence.LevelBossKoopaFinal,
        (ushort)MusicSequence.LevelBossKoopaFinalVariation,
        (ushort)MusicSequence.MenuFileSelect,
        (ushort)MusicSequence.MenuFileSelectVariation,
        (ushort)MusicSequence.EventCutsceneCredits,
        (ushort)MusicSequence.EventCutsceneCreditsVariation
    };

    // audio_defines.h
    /// <summary>
    /// Represents the playback status of a sound.
    /// </summary>
    [Flags]
    public enum SoundPlaybackStatus : uint
    {
        Stopped = 0,
        Starting = 1,
        Waiting = Starting, // Alias for Starting
        Playing = 2
    }

    /// <summary>
    /// Constructs a 32-bit sound argument used for triggering audio playback.
    ///
    /// Bit layout:
    /// - Bits 31–28 (bank):       Sound bank (custom classification, not the audio bank)
    /// - Bits 27–24 (playFlags):  Playback bitflags (e.g., looping, spatialization)
    /// - Bits 23–16 (soundID):    Sound ID to play
    /// - Bits 15–8  (priority):   Priority level (higher value = higher priority)
    /// - Bits 7–4   (flags2):     Additional flags (custom-defined usage)
    /// - Bits 3–0   (status):     Sound status (typically set to SOUND_STATUS_STARTING)
    /// </summary>
    /// <param name="bank">Bits 31–28. Sound bank (custom classification, not the audio bank)</param>
    /// <param name="playFlags">Bits 27–24. Playback bitflags (e.g., loop, spatial)</param>
    /// <param name="soundID">Bits 23–16. Sound ID to play</param>
    /// <param name="priority">Bits 15–8. Priority of the sound (higher = more important)</param>
    /// <param name="flags2">Bits 7–4. Additional flags (custom usage)</param>
    /// <returns>A 32-bit unsigned integer encoding the sound parameters</returns>
    private static uint SoundArgLoad(uint bank, uint playFlags, uint soundID, uint priority, uint flags2)
        => bank << 28 | playFlags << 24 | soundID << 16 | priority << 8 | flags2 << 4 | (uint)SoundPlaybackStatus.Starting;

#endregion

#region Sounds

    [Flags]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum Sounds
    {
        // SoundBitsKeys
        SOUND_GENERAL_COIN,
        SOUND_GENERAL_COIN_WATER,
        SOUND_GENERAL_COIN_SPURT,
        SOUND_GENERAL_COIN_SPURT_2,
        SOUND_GENERAL_COIN_SPURT_EU,
        SOUND_GENERAL_COIN_DROP,
        SOUND_GENERAL_RED_COIN,
        SOUND_MENU_COIN_ITS_A_ME_MARIO,
        SOUND_MENU_COLLECT_RED_COIN,

        // Action
        Action_TerrainJump,
        Action_TerrainLanding,
        Action_TerrainStep,
        Action_TerrainBodyHitGround,
        Action_TerrainStepTiptoe,
        Action_TerrainStuckInGround,
        Action_TerrainHeavyLanding,
        Action_MetalJump,
        Action_MetalLanding,
        Action_MetalStep,
        Action_MetalHeavyLanding,
        Action_ClapHandsCold,
        Action_HangingStep,
        Action_QuicksandStep,
        Action_MetalStepTiptoe,
        Action_Unknown430,
        Action_Unknown431,
        Action_Unknown432,
        Action_Swim,
        Action_Unknown434,
        Action_Throw,
        Action_KeySwish,
        Action_Spin,
        Action_Twirl,
        Action_ClimbUpTree,
        Action_ClimbDownTree,
        Action_Unk3C,
        Action_Unknown43D,
        Action_Unknown43E,
        Action_PatBack,
        Action_BrushHair,
        Action_ClimbUpPole,
        Action_MetalBonk,
        Action_UnstuckFromGround,
        Action_Hit,
        Action_Hit2,
        Action_Hit3,
        Action_Bonk,
        Action_ShrinkIntoBbh,
        Action_SwimFast,
        Action_MetalJumpWater,
        Action_MetalLandWater,
        Action_MetalStepWater,
        Action_Unk53,
        Action_Unk54,
        Action_Unk55,
        Action_FlyingFast,
        Action_Teleport,
        Action_Unknown458,
        Action_BounceOffObject,
        Action_SideFlipUnk,
        Action_ReadSign,
        Action_Unknown45C,
        Action_Unk5D,
        Action_IntroUnk45E,
        Action_IntroUnk45F,

        // Moving
        Moving_TerrainSlide,
        Moving_TerrainRidingShell,
        Moving_LavaBurn,
        Moving_SlideDownPole,
        Moving_SlideDownTree,
        Moving_QuicksandDeath,
        Moving_Shocked,
        Moving_Flying,
        Moving_AlmostDrowning,
        Moving_AimCannon,
        Moving_Unk1A,
        Moving_RidingShellLava,

        // Mario
        Mario_YahWahHoo,
        Mario_Hoohoo,
        Mario_Yahoo,
        Mario_Uh,
        Mario_Hrmm,
        Mario_Wah2,
        Mario_Whoa,
        Mario_Eeuh,
        Mario_Attacked,
        Mario_Ooof,
        Mario_Ooof2,
        Mario_HereWeGo,
        Mario_Yawning,
        Mario_Snoring1,
        Mario_Snoring2,
        Mario_WaaaOooW,
        Mario_Haha,
        Mario_Haha2,
        Mario_Uh2,
        Mario_Uh2_2,
        Mario_OnFire,
        Mario_Dying,
        Mario_PantingCold,
        Mario_Panting,
        Mario_Coughing1,
        Mario_Coughing2,
        Mario_Coughing3,
        Mario_PunchYah,
        Mario_PunchHoo,
        Mario_MamaMia,
        Mario_OkeyDokey,
        Mario_GroundPoundWah,
        Mario_Drowning,
        Mario_PunchWah,
        Mario_YahooWahaYippee,
        Mario_Doh,
        Mario_GameOver,
        Mario_Hello,
        Mario_PressStartToPlay,
        Mario_TwirlBounce,
        Mario_Snoring3,
        Mario_SoLongaBowser,
        Mario_ImaTired,

        // Peach
        Peach_DearMario,
        Peach_Mario,
        Peach_PowerOfTheStars,
        Peach_ThanksToYou,
        Peach_ThankYouMario,
        Peach_SomethingSpecial,
        Peach_BakeACake,
        Peach_ForMario,
        Peach_Mario2,

        // General
        General_ActivateCapSwitch,
        General_FlameOut,
        General_OpenWoodDoor,
        General_CloseWoodDoor,
        General_OpenIronDoor,
        General_CloseIronDoor,
        General_Bubbles,
        General_MovingWater,
        General_SwishWater,
        General_QuietBubble,
        General_VolcanoExplosion,
        General_QuietBubble2,
        General_CastleTrapOpen,
        General_WallExplosion,
        General_Coin,
        General_CoinWater,
        General_ShortStar,
        General_BigClock,
        General_LoudPound,
        General_LoudPound2,
        General_ShortPound1,
        General_ShortPound2,
        General_ShortPound3,
        General_ShortPound4,
        General_ShortPound5,
        General_ShortPound6,
        General_OpenChest,
        General_ClamShell1,
        General_BoxLanding,
        General_BoxLanding_2,
        General_Unknown1,
        General_Unknown1_2,
        General_ClamShell2,
        General_ClamShell3,
        General_PaintingEject,
        General_LevelSelectChange,
        General_Platform,
        General_DonutPlatformExplosion,
        General_BowserBombExplosion,
        General_CoinSpurt,
        General_CoinSpurt_2,
        General_CoinSpurtEu,
        General_Explosion6,
        General_Unk32,
        General_BoatTilt1,
        General_BoatTilt2,
        General_CoinDrop,
        General_Unknown3LowPrio,
        General_Unknown3,
        General_Unknown3_2,
        General_PendulumSwing,
        General_ChainChomp1,
        General_ChainChomp2,
        General_DoorTurnKey,
        General_MovingInSand,
        General_Unknown4LowPrio,
        General_Unknown4,
        General_MovingPlatformSwitch,
        General_CageOpen,
        General_QuietPound1LowPrio,
        General_QuietPound1,
        General_BreakBox,
        General_DoorInsertKey,
        General_QuietPound2,
        General_BigPound,
        General_Unk45,
        General_Unk46LowPrio,
        General_Unk46,
        General_CannonUp,
        General_GrindelRoll,
        General_Explosion7,
        General_ShakeCoffin,
        General_RaceGunShot,
        General_StarDoorOpen,
        General_StarDoorClose,
        General_PoundRock,
        General_StarAppears,
        General_Collect1Up,
        General_ButtonPressLowPrio,
        General_ButtonPress,
        General_ButtonPress2LowPrio,
        General_ButtonPress2,
        General_ElevatorMove,
        General_ElevatorMove2,
        General_SwishAir,
        General_SwishAir2,
        General_HauntedChair,
        General_SoftLanding,
        General_HauntedChairMove,
        General_BowserPlatform,
        General_BowserPlatform2,
        General_HeartSpin,
        General_PoundWoodPost,
        General_WaterLevelTrig,
        General_SwitchDoorOpen,
        General_RedCoin,
        General_BirdsFlyAway,
        General_MetalPound,
        General_Boing1,
        General_Boing2LowPrio,
        General_Boing2,
        General_YoshiWalk,
        General_EnemyAlert1,
        General_YoshiTalk,
        General_Splattering,
        General_Boing3,
        General_GrandStar,
        General_GrandStarJump,
        General_BoatRock,
        General_VanishSfx,

        // Environment
        Environment_Waterfall1,
        Environment_Waterfall2,
        Environment_Elevator1,
        Environment_Droning1,
        Environment_Droning2,
        Environment_Wind1,
        Environment_MovingSandSnow,
        Environment_Unk07,
        Environment_Elevator2,
        Environment_Water,
        Environment_Unknown2,
        Environment_BoatRocking1,
        Environment_Elevator3,
        Environment_Elevator4,
        Environment_Elevator4_2,
        Environment_Movingsand,
        Environment_MerryGoRoundCreaking,
        Environment_Wind2,
        Environment_Unk12,
        Environment_Sliding,
        Environment_Star,
        Environment_Unknown4,
        Environment_WaterDrain,
        Environment_MetalBoxPush,
        Environment_SinkQuicksand,

        // Object
        Object_SushiSharkWaterSound,
        Object_MriShoot,
        Object_BabyPenguinWalk,
        Object_BowserWalk,
        Object_BowserTailPickup,
        Object_BowserDefeated,
        Object_BowserSpinning,
        Object_BowserInhaling,
        Object_BigPenguinWalk,
        Object_BooBounceTop,
        Object_BooLaughShort,
        Object_Thwomp,
        Object_Cannon1,
        Object_Cannon2,
        Object_Cannon3,
        Object_JumpWalkWater,
        Object_Unknown2,
        Object_MriDeath,
        Object_Pounding1,
        Object_Pounding1HighPrio,
        Object_WhompLowPrio,
        Object_KingBobomb,
        Object_BullyMetal,
        Object_BullyExplode,
        Object_BullyExplode_2,
        Object_PoundingCannon,
        Object_BullyWalk,
        Object_Unknown3,
        Object_Unknown4,
        Object_BabyPenguinDive,
        Object_GoombaWalk,
        Object_UkikiChatterLong,
        Object_MontyMoleAttack,
        Object_EvilLakituThrow,
        Object_Unk23,
        Object_DyingEnemy1,
        Object_Cannon4,
        Object_DyingEnemy2,
        Object_BobombWalk,
        Object_SomethingLanding,
        Object_DivingInWater,
        Object_SnowSand1,
        Object_SnowSand2,
        Object_DefaultDeath,
        Object_BigPenguinYell,
        Object_WaterBombBouncing,
        Object_GoombaAlert,
        Object_WigglerJump,
        Object_Stomped,
        Object_Unknown6,
        Object_DivingIntoWater,
        Object_PiranhaPlantShrink,
        Object_KoopaTheQuickWalk,
        Object_KoopaWalk,
        Object_BullyWalking,
        Object_Dorrie,
        Object_BowserLaugh,
        Object_UkikiChatterShort,
        Object_UkikiChatterIdle,
        Object_UkikiStepDefault,
        Object_UkikiStepLeaves,
        Object_KoopaTalk,
        Object_KoopaDamage,
        Object_Klepto1,
        Object_Klepto2,
        Object_KingBobombTalk,
        Object_KingBobombJump,
        Object_KingWhompDeath,
        Object_BooLaughLong,
        Object_Eel,
        Object_Eel_2,
        Object_EyerokShowEye,
        Object_MrBlizzardAlert,
        Object_SnufitShoot,
        Object_SkeeterWalk,
        Object_WalkingWater,
        Object_BirdChirp3,
        Object_PiranhaPlantAppear,
        Object_FlameBlown,
        Object_MadPianoChomping,
        Object_BobombBuddyTalk,
        Object_SpinyUnk59,
        Object_WigglerHighPitch,
        Object_HeavehoTossed,
        Object_WigglerDeath,
        Object_BowserIntroLaugh,
        Object_EnemyDeathHigh,
        Object_EnemyDeathLow,
        Object_SwoopDeath,
        Object_KoopaFlyguyDeath,
        Object_PokeyDeath,
        Object_SnowmanBounce,
        Object_SnowmanExplode,
        Object_PoundingLoud,
        Object_MipsRabbit,
        Object_MipsRabbitWater,
        Object_EyerokExplode,
        Object_ChuckyaDeath,
        Object_WigglerTalk,
        Object_WigglerAttacked,
        Object_WigglerLowPitch,
        Object_SnufitSkeeterDeath,
        Object_BubbaChomp,
        Object_EnemyDefeatShrink,

        // Air
        Air_BowserSpitFire,
        Air_Unk01,
        Air_LakituFly,
        Air_LakituFlyHighPrio,
        Air_AmpBuzz,
        Air_BlowFire,
        Air_BlowWind,
        Air_RoughSlide,
        Air_HeavehoMove,
        Air_Unk07,
        Air_BobombLitFuse,
        Air_HowlingWind,
        Air_ChuckyaMove,
        Air_PeachTwinkle,
        Air_CastleOutdoorsAmbient,

        // Menu
        Menu_ChangeSelect,
        Menu_ReversePause,
        Menu_Pause,
        Menu_PauseHighPrio,
        Menu_Pause2,
        Menu_MessageAppear,
        Menu_MessageDisappear,
        Menu_CameraZoomIn,
        Menu_CameraZoomOut,
        Menu_PinchMarioFace,
        Menu_LetGoMarioFace,
        Menu_HandAppear,
        Menu_HandDisappear,
        Menu_Unk0C,
        Menu_PowerMeter,
        Menu_CameraBuzz,
        Menu_CameraTurn,
        Menu_Unk10,
        Menu_ClickFileSelect,
        Menu_MessageNextPage,
        Menu_CoinItsAMeMario,
        Menu_YoshiGainLives,
        Menu_EnterPipe,
        Menu_ExitPipe,
        Menu_BowserLaugh,
        Menu_EnterHole,
        Menu_ClickChangeView,
        Menu_CameraUnused1,
        Menu_CameraUnused2,
        Menu_MarioCastleWarp,
        Menu_StarSound,
        Menu_ThankYouPlayingMyGame,
        Menu_ReadASign,
        Menu_ExitASign,
        Menu_MarioCastleWarp2,
        Menu_StarSoundOkeyDokey,
        Menu_StarSoundLetsAGo,
        Menu_CollectRedCoin,
        Menu_CollectRedCoin0,
        Menu_CollectRedCoin1,
        Menu_CollectRedCoin2,
        Menu_CollectRedCoin3,
        Menu_CollectRedCoin4,
        Menu_CollectRedCoin5,
        Menu_CollectRedCoin6,
        Menu_CollectRedCoin7,
        Menu_CollectSecret,

        // General2
        General2_BobombExplosion,
        General2_PurpleSwitch,
        General2_RotatingBlockClick,
        General2_SpindelRoll,
        General2_PyramidTopSpin,
        General2_PyramidTopExplosion,
        General2_BirdChirp2,
        General2_SwitchTickFast,
        General2_SwitchTickSlow,
        General2_StarAppears,
        General2_RotatingBlockAlert,
        General2_BowserExplode,
        General2_BowserKey,
        General2_OneUpAppear,
        General2_RightAnswer,

        // Object2
        Object2_BowserRoar,
        Object2_PiranhaPlantBite,
        Object2_PiranhaPlantDying,
        Object2_BowserPuzzlePieceMove,
        Object2_BullyAttacked,
        Object2_KingBobombDamage,
        Object2_ScuttlebugWalk,
        Object2_ScuttlebugAlert,
        Object2_BabyPenguinYell,
        Object2_Swoop,
        Object2_BirdChirp1,
        Object2_LargeBullyAttacked,
        Object2_EyerokSoundShort,
        Object2_WhompSoundShort,
        Object2_EyerokSoundLong,
        Object2_BowserTeleport,
        Object2_MontyMoleAppear,
        Object2_BossDialogGrunt,
        Object2_MriSpinning
    }

    public static readonly Dictionary<Sounds, uint> SoundBank = new Dictionary<Sounds, uint>
    {
        // SoundBitsKeys
        { Sounds.SOUND_GENERAL_COIN, SoundArgLoad(3, 8, 0x11, 0x80, 8) },
        { Sounds.SOUND_GENERAL_COIN_WATER, SoundArgLoad(3, 8, 0x12, 0x80, 8) },
        { Sounds.SOUND_GENERAL_COIN_SPURT, SoundArgLoad(3, 0, 0x30, 0x00, 8) },
        { Sounds.SOUND_GENERAL_COIN_SPURT_2, SoundArgLoad(3, 8, 0x30, 0x00, 8) },
        { Sounds.SOUND_GENERAL_COIN_SPURT_EU, SoundArgLoad(3, 8, 0x30, 0x20, 8) },
        { Sounds.SOUND_GENERAL_COIN_DROP, SoundArgLoad(3, 0, 0x36, 0x40, 8) },
        { Sounds.SOUND_GENERAL_RED_COIN, SoundArgLoad(3, 0, 0x68, 0x90, 8) },
        { Sounds.SOUND_MENU_COIN_ITS_A_ME_MARIO, SoundArgLoad(7, 0, 0x14, 0x00, 8) },
        { Sounds.SOUND_MENU_COLLECT_RED_COIN, SoundArgLoad(7, 8, 0x28, 0x90, 8) },

        // Action
        { Sounds.Action_TerrainJump, SoundArgLoad(0, 4, 0x00, 0x80, 8) },
        { Sounds.Action_TerrainLanding, SoundArgLoad(0, 4, 0x08, 0x80, 8) },
        { Sounds.Action_TerrainStep, SoundArgLoad(0, 6, 0x10, 0x80, 8) },
        { Sounds.Action_TerrainBodyHitGround, SoundArgLoad(0, 4, 0x18, 0x80, 8) },
        { Sounds.Action_TerrainStepTiptoe, SoundArgLoad(0, 6, 0x20, 0x80, 8) },
        { Sounds.Action_TerrainStuckInGround, SoundArgLoad(0, 4, 0x48, 0x80, 8) },
        { Sounds.Action_TerrainHeavyLanding, SoundArgLoad(0, 4, 0x60, 0x80, 8) },
        { Sounds.Action_MetalJump, SoundArgLoad(0, 4, 0x28, 0x90, 8) },
        { Sounds.Action_MetalLanding, SoundArgLoad(0, 4, 0x29, 0x90, 8) },
        { Sounds.Action_MetalStep, SoundArgLoad(0, 4, 0x2A, 0x90, 8) },
        { Sounds.Action_MetalHeavyLanding, SoundArgLoad(0, 4, 0x2B, 0x90, 8) },
        { Sounds.Action_ClapHandsCold, SoundArgLoad(0, 6, 0x2C, 0x00, 8) },
        { Sounds.Action_HangingStep, SoundArgLoad(0, 4, 0x2D, 0xA0, 8) },
        { Sounds.Action_QuicksandStep, SoundArgLoad(0, 4, 0x2E, 0x00, 8) },
        { Sounds.Action_MetalStepTiptoe, SoundArgLoad(0, 4, 0x2F, 0x90, 8) },
        { Sounds.Action_Unknown430, SoundArgLoad(0, 4, 0x30, 0xC0, 8) },
        { Sounds.Action_Unknown431, SoundArgLoad(0, 4, 0x31, 0x60, 8) },
        { Sounds.Action_Unknown432, SoundArgLoad(0, 4, 0x32, 0x80, 8) },
        { Sounds.Action_Swim, SoundArgLoad(0, 4, 0x33, 0x80, 8) },
        { Sounds.Action_Unknown434, SoundArgLoad(0, 4, 0x34, 0x80, 8) },
        { Sounds.Action_Throw, SoundArgLoad(0, 4, 0x35, 0x80, 8) },
        { Sounds.Action_KeySwish, SoundArgLoad(0, 4, 0x36, 0x80, 8) },
        { Sounds.Action_Spin, SoundArgLoad(0, 4, 0x37, 0x80, 8) },
        { Sounds.Action_Twirl, SoundArgLoad(0, 4, 0x38, 0x80, 8) },
        { Sounds.Action_ClimbUpTree, SoundArgLoad(0, 4, 0x3A, 0x80, 8) },
        { Sounds.Action_ClimbDownTree, 0x003 },
        { Sounds.Action_Unk3C, 0x003 },
        { Sounds.Action_Unknown43D, SoundArgLoad(0, 4, 0x3D, 0x80, 8) },
        { Sounds.Action_Unknown43E, SoundArgLoad(0, 4, 0x3E, 0x80, 8) },
        { Sounds.Action_PatBack, SoundArgLoad(0, 4, 0x3F, 0x80, 8) },
        { Sounds.Action_BrushHair, SoundArgLoad(0, 4, 0x40, 0x80, 8) },
        { Sounds.Action_ClimbUpPole, SoundArgLoad(0, 4, 0x41, 0x80, 8) },
        { Sounds.Action_MetalBonk, SoundArgLoad(0, 4, 0x42, 0x80, 8) },
        { Sounds.Action_UnstuckFromGround, SoundArgLoad(0, 4, 0x43, 0x80, 8) },
        { Sounds.Action_Hit, SoundArgLoad(0, 4, 0x44, 0xC0, 8) },
        { Sounds.Action_Hit2, SoundArgLoad(0, 4, 0x44, 0xB0, 8) },
        { Sounds.Action_Hit3, SoundArgLoad(0, 4, 0x44, 0xA0, 8) },
        { Sounds.Action_Bonk, SoundArgLoad(0, 4, 0x45, 0xA0, 8) },
        { Sounds.Action_ShrinkIntoBbh, SoundArgLoad(0, 4, 0x46, 0xA0, 8) },
        { Sounds.Action_SwimFast, SoundArgLoad(0, 4, 0x47, 0xA0, 8) },
        { Sounds.Action_MetalJumpWater, SoundArgLoad(0, 4, 0x50, 0x90, 8) },
        { Sounds.Action_MetalLandWater, SoundArgLoad(0, 4, 0x51, 0x90, 8) },
        { Sounds.Action_MetalStepWater, SoundArgLoad(0, 4, 0x52, 0x90, 8) },
        { Sounds.Action_Unk53, 0x005 },
        { Sounds.Action_Unk54, 0x005 },
        { Sounds.Action_Unk55, 0x005 },
        { Sounds.Action_FlyingFast, SoundArgLoad(0, 4, 0x56, 0x80, 8) },
        { Sounds.Action_Teleport, SoundArgLoad(0, 4, 0x57, 0xC0, 8) },
        { Sounds.Action_Unknown458, SoundArgLoad(0, 4, 0x58, 0xA0, 8) },
        { Sounds.Action_BounceOffObject, SoundArgLoad(0, 4, 0x59, 0xB0, 8) },
        { Sounds.Action_SideFlipUnk, SoundArgLoad(0, 4, 0x5A, 0x80, 8) },
        { Sounds.Action_ReadSign, SoundArgLoad(0, 4, 0x5B, 0xFF, 8) },
        { Sounds.Action_Unknown45C, SoundArgLoad(0, 4, 0x5C, 0x80, 8) },
        { Sounds.Action_Unk5D, 0x005 },
        { Sounds.Action_IntroUnk45E, SoundArgLoad(0, 4, 0x5E, 0x80, 8) },
        { Sounds.Action_IntroUnk45F, SoundArgLoad(0, 4, 0x5F, 0x80, 8) },

        // Moving
        { Sounds.Moving_TerrainSlide, SoundArgLoad(1, 4, 0x00, 0x00, 0) },
        { Sounds.Moving_TerrainRidingShell, SoundArgLoad(1, 4, 0x20, 0x00, 0) },
        { Sounds.Moving_LavaBurn, SoundArgLoad(1, 4, 0x10, 0x00, 0) },
        { Sounds.Moving_SlideDownPole, SoundArgLoad(1, 4, 0x11, 0x00, 0) },
        { Sounds.Moving_SlideDownTree, SoundArgLoad(1, 4, 0x12, 0x80, 0) },
        { Sounds.Moving_QuicksandDeath, SoundArgLoad(1, 4, 0x14, 0x00, 0) },
        { Sounds.Moving_Shocked, SoundArgLoad(1, 4, 0x16, 0x00, 0) },
        { Sounds.Moving_Flying, SoundArgLoad(1, 4, 0x17, 0x00, 0) },
        { Sounds.Moving_AlmostDrowning, SoundArgLoad(1, 0xC, 0x18, 0x00, 0) },
        { Sounds.Moving_AimCannon, SoundArgLoad(1, 0xD, 0x19, 0x20, 0) },
        { Sounds.Moving_Unk1A, 0x101A },
        { Sounds.Moving_RidingShellLava, SoundArgLoad(1, 4, 0x28, 0x00, 0) },

        // Mario
        { Sounds.Mario_YahWahHoo, SoundArgLoad(2, 4, 0x00, 0x80, 8) },
        { Sounds.Mario_Hoohoo, SoundArgLoad(2, 4, 0x03, 0x80, 8) },
        { Sounds.Mario_Yahoo, SoundArgLoad(2, 4, 0x04, 0x80, 8) },
        { Sounds.Mario_Uh, SoundArgLoad(2, 4, 0x05, 0x80, 8) },
        { Sounds.Mario_Hrmm, SoundArgLoad(2, 4, 0x06, 0x80, 8) },
        { Sounds.Mario_Wah2, SoundArgLoad(2, 4, 0x07, 0x80, 8) },
        { Sounds.Mario_Whoa, SoundArgLoad(2, 4, 0x08, 0xC0, 8) },
        { Sounds.Mario_Eeuh, SoundArgLoad(2, 4, 0x09, 0x80, 8) },
        { Sounds.Mario_Attacked, SoundArgLoad(2, 4, 0x0A, 0xFF, 8) },
        { Sounds.Mario_Ooof, SoundArgLoad(2, 4, 0x0B, 0x80, 8) },
        { Sounds.Mario_Ooof2, SoundArgLoad(2, 4, 0x0B, 0xD0, 8) },
        { Sounds.Mario_HereWeGo, SoundArgLoad(2, 4, 0x0C, 0x80, 8) },
        { Sounds.Mario_Yawning, SoundArgLoad(2, 4, 0x0D, 0x80, 8) },
        { Sounds.Mario_Snoring1, SoundArgLoad(2, 4, 0x0E, 0x80, 8) },
        { Sounds.Mario_Snoring2, SoundArgLoad(2, 4, 0x0F, 0x80, 8) },
        { Sounds.Mario_WaaaOooW, SoundArgLoad(2, 4, 0x10, 0xC0, 8) },
        { Sounds.Mario_Haha, SoundArgLoad(2, 4, 0x11, 0x80, 8) },
        { Sounds.Mario_Haha2, SoundArgLoad(2, 4, 0x11, 0xF0, 8) },
        { Sounds.Mario_Uh2, SoundArgLoad(2, 4, 0x13, 0xD0, 8) },
        { Sounds.Mario_Uh2_2, SoundArgLoad(2, 4, 0x13, 0x80, 8) },
        { Sounds.Mario_OnFire, SoundArgLoad(2, 4, 0x14, 0xA0, 8) },
        { Sounds.Mario_Dying, SoundArgLoad(2, 4, 0x15, 0xFF, 8) },
        { Sounds.Mario_PantingCold, SoundArgLoad(2, 4, 0x16, 0x80, 8) },
        { Sounds.Mario_Panting, SoundArgLoad(2, 4, 0x18, 0x80, 8) },
        { Sounds.Mario_Coughing1, SoundArgLoad(2, 4, 0x1B, 0x80, 8) },
        { Sounds.Mario_Coughing2, SoundArgLoad(2, 4, 0x1C, 0x80, 8) },
        { Sounds.Mario_Coughing3, SoundArgLoad(2, 4, 0x1D, 0x80, 8) },
        { Sounds.Mario_PunchYah, SoundArgLoad(2, 4, 0x1E, 0x80, 8) },
        { Sounds.Mario_PunchHoo, SoundArgLoad(2, 4, 0x1F, 0x80, 8) },
        { Sounds.Mario_MamaMia, SoundArgLoad(2, 4, 0x20, 0x80, 8) },
        { Sounds.Mario_OkeyDokey, 0x202 },
        { Sounds.Mario_GroundPoundWah, SoundArgLoad(2, 4, 0x22, 0x80, 8) },
        { Sounds.Mario_Drowning, SoundArgLoad(2, 4, 0x23, 0xF0, 8) },
        { Sounds.Mario_PunchWah, SoundArgLoad(2, 4, 0x24, 0x80, 8) },
        { Sounds.Mario_YahooWahaYippee, SoundArgLoad(2, 4, 0x2B, 0x80, 8) },
        { Sounds.Mario_Doh, SoundArgLoad(2, 4, 0x30, 0x80, 8) },
        { Sounds.Mario_GameOver, SoundArgLoad(2, 4, 0x31, 0xFF, 8) },
        { Sounds.Mario_Hello, SoundArgLoad(2, 4, 0x32, 0xFF, 8) },
        { Sounds.Mario_PressStartToPlay, SoundArgLoad(2, 4, 0x33, 0xFF, 0xA) },
        { Sounds.Mario_TwirlBounce, SoundArgLoad(2, 4, 0x34, 0x80, 8) },
        { Sounds.Mario_Snoring3, SoundArgLoad(2, 4, 0x35, 0xFF, 8) },
        { Sounds.Mario_SoLongaBowser, SoundArgLoad(2, 4, 0x36, 0x80, 8) },
        { Sounds.Mario_ImaTired, SoundArgLoad(2, 4, 0x37, 0x80, 8) },

        // Peach
        { Sounds.Peach_DearMario, SoundArgLoad(2, 4, 0x28, 0xFF, 8) },
        { Sounds.Peach_Mario, SoundArgLoad(2, 4, 0x38, 0xFF, 8) },
        { Sounds.Peach_PowerOfTheStars, SoundArgLoad(2, 4, 0x39, 0xFF, 8) },
        { Sounds.Peach_ThanksToYou, SoundArgLoad(2, 4, 0x3A, 0xFF, 8) },
        { Sounds.Peach_ThankYouMario, SoundArgLoad(2, 4, 0x3B, 0xFF, 8) },
        { Sounds.Peach_SomethingSpecial, SoundArgLoad(2, 4, 0x3C, 0xFF, 8) },
        { Sounds.Peach_BakeACake, SoundArgLoad(2, 4, 0x3D, 0xFF, 8) },
        { Sounds.Peach_ForMario, SoundArgLoad(2, 4, 0x3E, 0xFF, 8) },
        { Sounds.Peach_Mario2, SoundArgLoad(2, 4, 0x3F, 0xFF, 8) },

        // General
        { Sounds.General_ActivateCapSwitch, SoundArgLoad(3, 0, 0x00, 0x80, 8) },
        { Sounds.General_FlameOut, SoundArgLoad(3, 0, 0x03, 0x80, 8) },
        { Sounds.General_OpenWoodDoor, SoundArgLoad(3, 0, 0x04, 0xC0, 8) },
        { Sounds.General_CloseWoodDoor, SoundArgLoad(3, 0, 0x05, 0xC0, 8) },
        { Sounds.General_OpenIronDoor, SoundArgLoad(3, 0, 0x06, 0xC0, 8) },
        { Sounds.General_CloseIronDoor, SoundArgLoad(3, 0, 0x07, 0xC0, 8) },
        { Sounds.General_Bubbles, 0x300 },
        { Sounds.General_MovingWater, SoundArgLoad(3, 0, 0x09, 0x00, 8) },
        { Sounds.General_SwishWater, SoundArgLoad(3, 0, 0x0A, 0x00, 8) },
        { Sounds.General_QuietBubble, SoundArgLoad(3, 0, 0x0B, 0x00, 8) },
        { Sounds.General_VolcanoExplosion, SoundArgLoad(3, 0, 0x0C, 0x80, 8) },
        { Sounds.General_QuietBubble2, SoundArgLoad(3, 0, 0x0D, 0x00, 8) },
        { Sounds.General_CastleTrapOpen, SoundArgLoad(3, 0, 0x0E, 0x80, 8) },
        { Sounds.General_WallExplosion, SoundArgLoad(3, 0, 0x0F, 0x00, 8) },
        { Sounds.General_Coin, SoundArgLoad(3, 8, 0x11, 0x80, 8) },
        { Sounds.General_CoinWater, SoundArgLoad(3, 8, 0x12, 0x80, 8) },
        { Sounds.General_ShortStar, SoundArgLoad(3, 0, 0x16, 0x00, 9) },
        { Sounds.General_BigClock, SoundArgLoad(3, 0, 0x17, 0x00, 8) },
        { Sounds.General_LoudPound, 0x3018 },
        { Sounds.General_LoudPound2, 0x301 },
        { Sounds.General_ShortPound1, 0x301 },
        { Sounds.General_ShortPound2, 0x301 },
        { Sounds.General_ShortPound3, 0x301 },
        { Sounds.General_ShortPound4, 0x301 },
        { Sounds.General_ShortPound5, 0x301 },
        { Sounds.General_ShortPound6, 0x301 },
        { Sounds.General_OpenChest, SoundArgLoad(3, 1, 0x20, 0x80, 8) },
        { Sounds.General_ClamShell1, SoundArgLoad(3, 1, 0x22, 0x80, 8) },
        { Sounds.General_BoxLanding, SoundArgLoad(3, 0, 0x24, 0x00, 8) },
        { Sounds.General_BoxLanding_2, SoundArgLoad(3, 2, 0x24, 0x00, 8) },
        { Sounds.General_Unknown1, SoundArgLoad(3, 0, 0x25, 0x00, 8) },
        { Sounds.General_Unknown1_2, SoundArgLoad(3, 2, 0x25, 0x00, 8) },
        { Sounds.General_ClamShell2, SoundArgLoad(3, 0, 0x26, 0x40, 8) },
        { Sounds.General_ClamShell3, SoundArgLoad(3, 0, 0x27, 0x40, 8) },
        { Sounds.General_PaintingEject, SoundArgLoad(3, 8, 0x28, 0x00, 8) },
        { Sounds.General_LevelSelectChange, SoundArgLoad(3, 0, 0x2B, 0x00, 8) },
        { Sounds.General_Platform, SoundArgLoad(3, 0, 0x2D, 0x80, 8) },
        { Sounds.General_DonutPlatformExplosion, SoundArgLoad(3, 0, 0x2E, 0x20, 8) },
        { Sounds.General_BowserBombExplosion, SoundArgLoad(3, 1, 0x2F, 0x00, 8) },
        { Sounds.General_CoinSpurt, SoundArgLoad(3, 0, 0x30, 0x00, 8) },
        { Sounds.General_CoinSpurt_2, SoundArgLoad(3, 8, 0x30, 0x00, 8) },
        { Sounds.General_CoinSpurtEu, SoundArgLoad(3, 8, 0x30, 0x20, 8) },
        { Sounds.General_Explosion6, 0x303 },
        { Sounds.General_Unk32, 0x303 },
        { Sounds.General_BoatTilt1, SoundArgLoad(3, 0, 0x34, 0x40, 8) },
        { Sounds.General_BoatTilt2, SoundArgLoad(3, 0, 0x35, 0x40, 8) },
        { Sounds.General_CoinDrop, SoundArgLoad(3, 0, 0x36, 0x40, 8) },
        { Sounds.General_Unknown3LowPrio, SoundArgLoad(3, 0, 0x37, 0x00, 8) },
        { Sounds.General_Unknown3, SoundArgLoad(3, 0, 0x37, 0x80, 8) },
        { Sounds.General_Unknown3_2, SoundArgLoad(3, 8, 0x37, 0x80, 8) },
        { Sounds.General_PendulumSwing, SoundArgLoad(3, 0, 0x38, 0x00, 8) },
        { Sounds.General_ChainChomp1, SoundArgLoad(3, 0, 0x39, 0x00, 8) },
        { Sounds.General_ChainChomp2, SoundArgLoad(3, 0, 0x3A, 0x00, 8) },
        { Sounds.General_DoorTurnKey, SoundArgLoad(3, 0, 0x3B, 0x00, 8) },
        { Sounds.General_MovingInSand, SoundArgLoad(3, 0, 0x3C, 0x00, 8) },
        { Sounds.General_Unknown4LowPrio, SoundArgLoad(3, 0, 0x3D, 0x00, 8) },
        { Sounds.General_Unknown4, SoundArgLoad(3, 0, 0x3D, 0x80, 8) },
        { Sounds.General_MovingPlatformSwitch, SoundArgLoad(3, 0, 0x3E, 0x00, 8) },
        { Sounds.General_CageOpen, SoundArgLoad(3, 0, 0x3F, 0xA0, 8) },
        { Sounds.General_QuietPound1LowPrio, SoundArgLoad(3, 0, 0x40, 0x00, 8) },
        { Sounds.General_QuietPound1, SoundArgLoad(3, 0, 0x40, 0x40, 8) },
        { Sounds.General_BreakBox, SoundArgLoad(3, 0, 0x41, 0xC0, 8) },
        { Sounds.General_DoorInsertKey, SoundArgLoad(3, 0, 0x42, 0x00, 8) },
        { Sounds.General_QuietPound2, SoundArgLoad(3, 0, 0x43, 0x00, 8) },
        { Sounds.General_BigPound, SoundArgLoad(3, 0, 0x44, 0x00, 8) },
        { Sounds.General_Unk45, SoundArgLoad(3, 0, 0x45, 0x00, 8) },
        { Sounds.General_Unk46LowPrio, SoundArgLoad(3, 0, 0x46, 0x00, 8) },
        { Sounds.General_Unk46, SoundArgLoad(3, 0, 0x46, 0x80, 8) },
        { Sounds.General_CannonUp, SoundArgLoad(3, 0, 0x47, 0x80, 8) },
        { Sounds.General_GrindelRoll, SoundArgLoad(3, 0, 0x48, 0x00, 8) },
        { Sounds.General_Explosion7, 0x304 },
        { Sounds.General_ShakeCoffin, 0x304 },
        { Sounds.General_RaceGunShot, SoundArgLoad(3, 1, 0x4D, 0x40, 8) },
        { Sounds.General_StarDoorOpen, SoundArgLoad(3, 0, 0x4E, 0xC0, 8) },
        { Sounds.General_StarDoorClose, SoundArgLoad(3, 0, 0x4F, 0xC0, 8) },
        { Sounds.General_PoundRock, SoundArgLoad(3, 0, 0x56, 0x00, 8) },
        { Sounds.General_StarAppears, SoundArgLoad(3, 0, 0x57, 0xFF, 9) },
        { Sounds.General_Collect1Up, SoundArgLoad(3, 0, 0x58, 0xFF, 8) },
        { Sounds.General_ButtonPressLowPrio, SoundArgLoad(3, 0, 0x5A, 0x00, 8) },
        { Sounds.General_ButtonPress, SoundArgLoad(3, 0, 0x5A, 0x40, 8) },
        { Sounds.General_ButtonPress2LowPrio, SoundArgLoad(3, 1, 0x5A, 0x00, 8) },
        { Sounds.General_ButtonPress2, SoundArgLoad(3, 1, 0x5A, 0x40, 8) },
        { Sounds.General_ElevatorMove, SoundArgLoad(3, 0, 0x5B, 0x00, 8) },
        { Sounds.General_ElevatorMove2, SoundArgLoad(3, 1, 0x5B, 0x00, 8) },
        { Sounds.General_SwishAir, SoundArgLoad(3, 0, 0x5C, 0x00, 8) },
        { Sounds.General_SwishAir2, SoundArgLoad(3, 1, 0x5C, 0x00, 8) },
        { Sounds.General_HauntedChair, SoundArgLoad(3, 0, 0x5D, 0x00, 8) },
        { Sounds.General_SoftLanding, SoundArgLoad(3, 0, 0x5E, 0x00, 8) },
        { Sounds.General_HauntedChairMove, SoundArgLoad(3, 0, 0x5F, 0x00, 8) },
        { Sounds.General_BowserPlatform, SoundArgLoad(3, 0, 0x62, 0x80, 8) },
        { Sounds.General_BowserPlatform2, SoundArgLoad(3, 1, 0x62, 0x80, 8) },
        { Sounds.General_HeartSpin, SoundArgLoad(3, 0, 0x64, 0xC0, 8) },
        { Sounds.General_PoundWoodPost, SoundArgLoad(3, 0, 0x65, 0xC0, 8) },
        { Sounds.General_WaterLevelTrig, SoundArgLoad(3, 0, 0x66, 0x80, 8) },
        { Sounds.General_SwitchDoorOpen, SoundArgLoad(3, 0, 0x67, 0xA0, 8) },
        { Sounds.General_RedCoin, SoundArgLoad(3, 0, 0x68, 0x90, 8) },
        { Sounds.General_BirdsFlyAway, SoundArgLoad(3, 0, 0x69, 0x00, 8) },
        { Sounds.General_MetalPound, SoundArgLoad(3, 0, 0x6B, 0x80, 8) },
        { Sounds.General_Boing1, SoundArgLoad(3, 0, 0x6C, 0x40, 8) },
        { Sounds.General_Boing2LowPrio, SoundArgLoad(3, 0, 0x6D, 0x20, 8) },
        { Sounds.General_Boing2, SoundArgLoad(3, 0, 0x6D, 0x40, 8) },
        { Sounds.General_YoshiWalk, SoundArgLoad(3, 0, 0x6E, 0x20, 8) },
        { Sounds.General_EnemyAlert1, SoundArgLoad(3, 0, 0x6F, 0x30, 8) },
        { Sounds.General_YoshiTalk, SoundArgLoad(3, 0, 0x70, 0x30, 8) },
        { Sounds.General_Splattering, SoundArgLoad(3, 0, 0x71, 0x30, 8) },
        { Sounds.General_Boing3, 0x307 },
        { Sounds.General_GrandStar, SoundArgLoad(3, 0, 0x73, 0x00, 8) },
        { Sounds.General_GrandStarJump, SoundArgLoad(3, 0, 0x74, 0x00, 8) },
        { Sounds.General_BoatRock, SoundArgLoad(3, 0, 0x75, 0x00, 8) },
        { Sounds.General_VanishSfx, SoundArgLoad(3, 0, 0x76, 0x20, 8) },

        // Environment
        { Sounds.Environment_Waterfall1, SoundArgLoad(4, 0, 0x00, 0x00, 0) },
        { Sounds.Environment_Waterfall2, SoundArgLoad(4, 0, 0x01, 0x00, 0) },
        { Sounds.Environment_Elevator1, SoundArgLoad(4, 0, 0x02, 0x00, 0) },
        { Sounds.Environment_Droning1, SoundArgLoad(4, 1, 0x03, 0x00, 0) },
        { Sounds.Environment_Droning2, SoundArgLoad(4, 0, 0x04, 0x00, 0) },
        { Sounds.Environment_Wind1, SoundArgLoad(4, 0, 0x05, 0x00, 0) },
        { Sounds.Environment_MovingSandSnow, 0x400 },
        { Sounds.Environment_Unk07, 0x400 },
        { Sounds.Environment_Elevator2, SoundArgLoad(4, 0, 0x08, 0x00, 0) },
        { Sounds.Environment_Water, SoundArgLoad(4, 0, 0x09, 0x00, 0) },
        { Sounds.Environment_Unknown2, SoundArgLoad(4, 0, 0x0A, 0x00, 0) },
        { Sounds.Environment_BoatRocking1, SoundArgLoad(4, 0, 0x0B, 0x00, 0) },
        { Sounds.Environment_Elevator3, SoundArgLoad(4, 0, 0x0C, 0x00, 0) },
        { Sounds.Environment_Elevator4, SoundArgLoad(4, 0, 0x0D, 0x00, 0) },
        { Sounds.Environment_Elevator4_2, SoundArgLoad(4, 1, 0x0D, 0x00, 0) },
        { Sounds.Environment_Movingsand, SoundArgLoad(4, 0, 0x0E, 0x00, 0) },
        { Sounds.Environment_MerryGoRoundCreaking, SoundArgLoad(4, 0, 0x0F, 0x40, 0) },
        { Sounds.Environment_Wind2, SoundArgLoad(4, 0, 0x10, 0x80, 0) },
        { Sounds.Environment_Unk12, 0x401 },
        { Sounds.Environment_Sliding, SoundArgLoad(4, 0, 0x13, 0x00, 0) },
        { Sounds.Environment_Star, SoundArgLoad(4, 0, 0x14, 0x00, 1) },
        { Sounds.Environment_Unknown4, SoundArgLoad(4, 1, 0x15, 0x00, 0) },
        { Sounds.Environment_WaterDrain, SoundArgLoad(4, 1, 0x16, 0x00, 0) },
        { Sounds.Environment_MetalBoxPush, SoundArgLoad(4, 0, 0x17, 0x80, 0) },
        { Sounds.Environment_SinkQuicksand, SoundArgLoad(4, 0, 0x18, 0x80, 0) },

        // Object
        { Sounds.Object_SushiSharkWaterSound, SoundArgLoad(5, 0, 0x00, 0x80, 8) },
        { Sounds.Object_MriShoot, SoundArgLoad(5, 0, 0x01, 0x00, 8) },
        { Sounds.Object_BabyPenguinWalk, SoundArgLoad(5, 0, 0x02, 0x00, 8) },
        { Sounds.Object_BowserWalk, SoundArgLoad(5, 0, 0x03, 0x00, 8) },
        { Sounds.Object_BowserTailPickup, SoundArgLoad(5, 0, 0x05, 0x00, 8) },
        { Sounds.Object_BowserDefeated, SoundArgLoad(5, 0, 0x06, 0x00, 8) },
        { Sounds.Object_BowserSpinning, SoundArgLoad(5, 0, 0x07, 0x00, 8) },
        { Sounds.Object_BowserInhaling, SoundArgLoad(5, 0, 0x08, 0x00, 8) },
        { Sounds.Object_BigPenguinWalk, SoundArgLoad(5, 0, 0x09, 0x80, 8) },
        { Sounds.Object_BooBounceTop, SoundArgLoad(5, 0, 0x0A, 0x00, 8) },
        { Sounds.Object_BooLaughShort, SoundArgLoad(5, 0, 0x0B, 0x00, 8) },
        { Sounds.Object_Thwomp, SoundArgLoad(5, 0, 0x0C, 0xA0, 8) },
        { Sounds.Object_Cannon1, SoundArgLoad(5, 0, 0x0D, 0xF0, 8) },
        { Sounds.Object_Cannon2, SoundArgLoad(5, 0, 0x0E, 0xF0, 8) },
        { Sounds.Object_Cannon3, SoundArgLoad(5, 0, 0x0F, 0xF0, 8) },
        { Sounds.Object_JumpWalkWater, 0x501 },
        { Sounds.Object_Unknown2, SoundArgLoad(5, 0, 0x13, 0x00, 8) },
        { Sounds.Object_MriDeath, SoundArgLoad(5, 0, 0x14, 0x00, 8) },
        { Sounds.Object_Pounding1, SoundArgLoad(5, 0, 0x15, 0x50, 8) },
        { Sounds.Object_Pounding1HighPrio, SoundArgLoad(5, 0, 0x15, 0x80, 8) },
        { Sounds.Object_WhompLowPrio, SoundArgLoad(5, 0, 0x16, 0x60, 8) },
        { Sounds.Object_KingBobomb, SoundArgLoad(5, 0, 0x16, 0x80, 8) },
        { Sounds.Object_BullyMetal, SoundArgLoad(5, 0, 0x17, 0x80, 8) },
        { Sounds.Object_BullyExplode, SoundArgLoad(5, 0, 0x18, 0xA0, 8) },
        { Sounds.Object_BullyExplode_2, SoundArgLoad(5, 1, 0x18, 0xA0, 8) },
        { Sounds.Object_PoundingCannon, SoundArgLoad(5, 0, 0x1A, 0x50, 8) },
        { Sounds.Object_BullyWalk, SoundArgLoad(5, 0, 0x1B, 0x30, 8) },
        { Sounds.Object_Unknown3, SoundArgLoad(5, 0, 0x1D, 0x80, 8) },
        { Sounds.Object_Unknown4, SoundArgLoad(5, 0, 0x1E, 0xA0, 8) },
        { Sounds.Object_BabyPenguinDive, SoundArgLoad(5, 0, 0x1F, 0x40, 8) },
        { Sounds.Object_GoombaWalk, SoundArgLoad(5, 0, 0x20, 0x00, 8) },
        { Sounds.Object_UkikiChatterLong, SoundArgLoad(5, 0, 0x21, 0x00, 8) },
        { Sounds.Object_MontyMoleAttack, SoundArgLoad(5, 0, 0x22, 0x00, 8) },
        { Sounds.Object_EvilLakituThrow, SoundArgLoad(5, 0, 0x22, 0x20, 8) },
        { Sounds.Object_Unk23, 0x502 },
        { Sounds.Object_DyingEnemy1, SoundArgLoad(5, 0, 0x24, 0x40, 8) },
        { Sounds.Object_Cannon4, SoundArgLoad(5, 0, 0x25, 0x40, 8) },
        { Sounds.Object_DyingEnemy2, 0x502 },
        { Sounds.Object_BobombWalk, SoundArgLoad(5, 0, 0x27, 0x00, 8) },
        { Sounds.Object_SomethingLanding, SoundArgLoad(5, 0, 0x28, 0x80, 8) },
        { Sounds.Object_DivingInWater, SoundArgLoad(5, 0, 0x29, 0xA0, 8) },
        { Sounds.Object_SnowSand1, SoundArgLoad(5, 0, 0x2A, 0x00, 8) },
        { Sounds.Object_SnowSand2, SoundArgLoad(5, 0, 0x2B, 0x00, 8) },
        { Sounds.Object_DefaultDeath, SoundArgLoad(5, 0, 0x2C, 0x80, 8) },
        { Sounds.Object_BigPenguinYell, SoundArgLoad(5, 0, 0x2D, 0x00, 8) },
        { Sounds.Object_WaterBombBouncing, SoundArgLoad(5, 0, 0x2E, 0x80, 8) },
        { Sounds.Object_GoombaAlert, SoundArgLoad(5, 0, 0x2F, 0x00, 8) },
        { Sounds.Object_WigglerJump, SoundArgLoad(5, 0, 0x2F, 0x60, 8) },
        { Sounds.Object_Stomped, SoundArgLoad(5, 0, 0x30, 0x80, 8) },
        { Sounds.Object_Unknown6, SoundArgLoad(5, 0, 0x31, 0x00, 8) },
        { Sounds.Object_DivingIntoWater, SoundArgLoad(5, 0, 0x32, 0x40, 8) },
        { Sounds.Object_PiranhaPlantShrink, SoundArgLoad(5, 0, 0x33, 0x40, 8) },
        { Sounds.Object_KoopaTheQuickWalk, SoundArgLoad(5, 0, 0x34, 0x20, 8) },
        { Sounds.Object_KoopaWalk, SoundArgLoad(5, 0, 0x35, 0x00, 8) },
        { Sounds.Object_BullyWalking, SoundArgLoad(5, 0, 0x36, 0x60, 8) },
        { Sounds.Object_Dorrie, SoundArgLoad(5, 0, 0x37, 0x60, 8) },
        { Sounds.Object_BowserLaugh, SoundArgLoad(5, 0, 0x38, 0x80, 8) },
        { Sounds.Object_UkikiChatterShort, SoundArgLoad(5, 0, 0x39, 0x00, 8) },
        { Sounds.Object_UkikiChatterIdle, SoundArgLoad(5, 0, 0x3A, 0x00, 8) },
        { Sounds.Object_UkikiStepDefault, SoundArgLoad(5, 0, 0x3B, 0x00, 8) },
        { Sounds.Object_UkikiStepLeaves, SoundArgLoad(5, 0, 0x3C, 0x00, 8) },
        { Sounds.Object_KoopaTalk, SoundArgLoad(5, 0, 0x3D, 0xA0, 8) },
        { Sounds.Object_KoopaDamage, SoundArgLoad(5, 0, 0x3E, 0xA0, 8) },
        { Sounds.Object_Klepto1, SoundArgLoad(5, 0, 0x3F, 0x40, 8) },
        { Sounds.Object_Klepto2, SoundArgLoad(5, 0, 0x40, 0x60, 8) },
        { Sounds.Object_KingBobombTalk, SoundArgLoad(5, 0, 0x41, 0x00, 8) },
        { Sounds.Object_KingBobombJump, SoundArgLoad(5, 0, 0x46, 0x80, 8) },
        { Sounds.Object_KingWhompDeath, SoundArgLoad(5, 1, 0x47, 0xC0, 8) },
        { Sounds.Object_BooLaughLong, SoundArgLoad(5, 0, 0x48, 0x00, 8) },
        { Sounds.Object_Eel, SoundArgLoad(5, 0, 0x4A, 0x00, 8) },
        { Sounds.Object_Eel_2, SoundArgLoad(5, 2, 0x4A, 0x00, 8) },
        { Sounds.Object_EyerokShowEye, SoundArgLoad(5, 2, 0x4B, 0x00, 8) },
        { Sounds.Object_MrBlizzardAlert, SoundArgLoad(5, 0, 0x4C, 0x00, 8) },
        { Sounds.Object_SnufitShoot, SoundArgLoad(5, 0, 0x4D, 0x00, 8) },
        { Sounds.Object_SkeeterWalk, SoundArgLoad(5, 0, 0x4E, 0x00, 8) },
        { Sounds.Object_WalkingWater, SoundArgLoad(5, 0, 0x4F, 0x00, 8) },
        { Sounds.Object_BirdChirp3, SoundArgLoad(5, 0, 0x51, 0x40, 0) },
        { Sounds.Object_PiranhaPlantAppear, SoundArgLoad(5, 0, 0x54, 0x20, 8) },
        { Sounds.Object_FlameBlown, SoundArgLoad(5, 0, 0x55, 0x80, 8) },
        { Sounds.Object_MadPianoChomping, SoundArgLoad(5, 2, 0x56, 0x40, 8) },
        { Sounds.Object_BobombBuddyTalk, SoundArgLoad(5, 0, 0x58, 0x40, 8) },
        { Sounds.Object_SpinyUnk59, SoundArgLoad(5, 0, 0x59, 0x10, 8) },
        { Sounds.Object_WigglerHighPitch, SoundArgLoad(5, 0, 0x5C, 0x40, 8) },
        { Sounds.Object_HeavehoTossed, SoundArgLoad(5, 0, 0x5D, 0x40, 8) },
        { Sounds.Object_WigglerDeath, 0x505 },
        { Sounds.Object_BowserIntroLaugh, SoundArgLoad(5, 0, 0x5F, 0x80, 9) },
        { Sounds.Object_EnemyDeathHigh, SoundArgLoad(5, 0, 0x60, 0xB0, 8) },
        { Sounds.Object_EnemyDeathLow, SoundArgLoad(5, 0, 0x61, 0xB0, 8) },
        { Sounds.Object_SwoopDeath, SoundArgLoad(5, 0, 0x62, 0xB0, 8) },
        { Sounds.Object_KoopaFlyguyDeath, SoundArgLoad(5, 0, 0x63, 0xB0, 8) },
        { Sounds.Object_PokeyDeath, SoundArgLoad(5, 0, 0x63, 0xC0, 8) },
        { Sounds.Object_SnowmanBounce, SoundArgLoad(5, 0, 0x64, 0xC0, 8) },
        { Sounds.Object_SnowmanExplode, SoundArgLoad(5, 0, 0x65, 0xD0, 8) },
        { Sounds.Object_PoundingLoud, SoundArgLoad(5, 0, 0x68, 0x40, 8) },
        { Sounds.Object_MipsRabbit, SoundArgLoad(5, 0, 0x6A, 0x00, 8) },
        { Sounds.Object_MipsRabbitWater, SoundArgLoad(5, 0, 0x6C, 0x00, 8) },
        { Sounds.Object_EyerokExplode, SoundArgLoad(5, 0, 0x6D, 0x00, 8) },
        { Sounds.Object_ChuckyaDeath, SoundArgLoad(5, 1, 0x6E, 0x00, 8) },
        { Sounds.Object_WigglerTalk, SoundArgLoad(5, 0, 0x6F, 0x00, 8) },
        { Sounds.Object_WigglerAttacked, SoundArgLoad(5, 0, 0x70, 0x60, 8) },
        { Sounds.Object_WigglerLowPitch, SoundArgLoad(5, 0, 0x71, 0x20, 8) },
        { Sounds.Object_SnufitSkeeterDeath, SoundArgLoad(5, 0, 0x72, 0xC0, 8) },
        { Sounds.Object_BubbaChomp, SoundArgLoad(5, 0, 0x73, 0x40, 8) },
        { Sounds.Object_EnemyDefeatShrink, SoundArgLoad(5, 0, 0x74, 0x40, 8) },

        // Air
        { Sounds.Air_BowserSpitFire, SoundArgLoad(6, 0, 0x00, 0x00, 0) },
        { Sounds.Air_Unk01, 0x6001 },
        { Sounds.Air_LakituFly, SoundArgLoad(6, 0, 0x02, 0x80, 0) },
        { Sounds.Air_LakituFlyHighPrio, SoundArgLoad(6, 0, 0x02, 0xFF, 0) },
        { Sounds.Air_AmpBuzz, SoundArgLoad(6, 0, 0x03, 0x40, 0) },
        { Sounds.Air_BlowFire, SoundArgLoad(6, 0, 0x04, 0x80, 0) },
        { Sounds.Air_BlowWind, SoundArgLoad(6, 0, 0x04, 0x40, 0) },
        { Sounds.Air_RoughSlide, SoundArgLoad(6, 0, 0x05, 0x00, 0) },
        { Sounds.Air_HeavehoMove, SoundArgLoad(6, 0, 0x06, 0x40, 0) },
        { Sounds.Air_Unk07, 0x6007 },
        { Sounds.Air_BobombLitFuse, SoundArgLoad(6, 0, 0x08, 0x60, 0) },
        { Sounds.Air_HowlingWind, SoundArgLoad(6, 0, 0x09, 0x80, 0) },
        { Sounds.Air_ChuckyaMove, SoundArgLoad(6, 0, 0x0A, 0x40, 0) },
        { Sounds.Air_PeachTwinkle, SoundArgLoad(6, 0, 0x0B, 0x40, 0) },
        { Sounds.Air_CastleOutdoorsAmbient, SoundArgLoad(6, 0, 0x10, 0x40, 0) },

        // Menu
        { Sounds.Menu_ChangeSelect, SoundArgLoad(7, 0, 0x00, 0xF8, 8) },
        { Sounds.Menu_ReversePause, 0x700 },
        { Sounds.Menu_Pause, SoundArgLoad(7, 0, 0x02, 0xF0, 8) },
        { Sounds.Menu_PauseHighPrio, SoundArgLoad(7, 0, 0x02, 0xFF, 8) },
        { Sounds.Menu_Pause2, SoundArgLoad(7, 0, 0x03, 0xFF, 8) },
        { Sounds.Menu_MessageAppear, SoundArgLoad(7, 0, 0x04, 0x00, 8) },
        { Sounds.Menu_MessageDisappear, SoundArgLoad(7, 0, 0x05, 0x00, 8) },
        { Sounds.Menu_CameraZoomIn, SoundArgLoad(7, 0, 0x06, 0x00, 8) },
        { Sounds.Menu_CameraZoomOut, SoundArgLoad(7, 0, 0x07, 0x00, 8) },
        { Sounds.Menu_PinchMarioFace, SoundArgLoad(7, 0, 0x08, 0x00, 8) },
        { Sounds.Menu_LetGoMarioFace, SoundArgLoad(7, 0, 0x09, 0x00, 8) },
        { Sounds.Menu_HandAppear, SoundArgLoad(7, 0, 0x0A, 0x00, 8) },
        { Sounds.Menu_HandDisappear, SoundArgLoad(7, 0, 0x0B, 0x00, 8) },
        { Sounds.Menu_Unk0C, SoundArgLoad(7, 0, 0x0C, 0x00, 8) },
        { Sounds.Menu_PowerMeter, SoundArgLoad(7, 0, 0x0D, 0x00, 8) },
        { Sounds.Menu_CameraBuzz, SoundArgLoad(7, 0, 0x0E, 0x00, 8) },
        { Sounds.Menu_CameraTurn, SoundArgLoad(7, 0, 0x0F, 0x00, 8) },
        { Sounds.Menu_Unk10, 0x701 },
        { Sounds.Menu_ClickFileSelect, SoundArgLoad(7, 0, 0x11, 0x00, 8) },
        { Sounds.Menu_MessageNextPage, SoundArgLoad(7, 0, 0x13, 0x00, 8) },
        { Sounds.Menu_CoinItsAMeMario, SoundArgLoad(7, 0, 0x14, 0x00, 8) },
        { Sounds.Menu_YoshiGainLives, SoundArgLoad(7, 0, 0x15, 0x00, 8) },
        { Sounds.Menu_EnterPipe, SoundArgLoad(7, 0, 0x16, 0xA0, 8) },
        { Sounds.Menu_ExitPipe, SoundArgLoad(7, 0, 0x17, 0xA0, 8) },
        { Sounds.Menu_BowserLaugh, SoundArgLoad(7, 0, 0x18, 0x80, 8) },
        { Sounds.Menu_EnterHole, SoundArgLoad(7, 1, 0x19, 0x80, 8) },
        { Sounds.Menu_ClickChangeView, SoundArgLoad(7, 0, 0x1A, 0x80, 8) },
        { Sounds.Menu_CameraUnused1, 0x701 },
        { Sounds.Menu_CameraUnused2, 0x701 },
        { Sounds.Menu_MarioCastleWarp, SoundArgLoad(7, 0, 0x1D, 0xB0, 8) },
        { Sounds.Menu_StarSound, SoundArgLoad(7, 0, 0x1E, 0xFF, 8) },
        { Sounds.Menu_ThankYouPlayingMyGame, SoundArgLoad(7, 0, 0x1F, 0xFF, 8) },
        { Sounds.Menu_ReadASign, 0x702 },
        { Sounds.Menu_ExitASign, 0x702 },
        { Sounds.Menu_MarioCastleWarp2, SoundArgLoad(7, 0, 0x22, 0x20, 8) },
        { Sounds.Menu_StarSoundOkeyDokey, SoundArgLoad(7, 0, 0x23, 0xFF, 8) },
        { Sounds.Menu_StarSoundLetsAGo, SoundArgLoad(7, 0, 0x24, 0xFF, 8) },
        { Sounds.Menu_CollectRedCoin, SoundArgLoad(7, 8, 0x28, 0x90, 8) },
        { Sounds.Menu_CollectRedCoin0, SoundArgLoad(7, 8, 0x28 + 0, 0x90, 8) },
        { Sounds.Menu_CollectRedCoin1, SoundArgLoad(7, 8, 0x28 + 1, 0x90, 8) },
        { Sounds.Menu_CollectRedCoin2, SoundArgLoad(7, 8, 0x28 + 2, 0x90, 8) },
        { Sounds.Menu_CollectRedCoin3, SoundArgLoad(7, 8, 0x28 + 3, 0x90, 8) },
        { Sounds.Menu_CollectRedCoin4, SoundArgLoad(7, 8, 0x28 + 4, 0x90, 8) },
        { Sounds.Menu_CollectRedCoin5, SoundArgLoad(7, 8, 0x28 + 5, 0x90, 8) },
        { Sounds.Menu_CollectRedCoin6, SoundArgLoad(7, 8, 0x28 + 6, 0x90, 8) },
        { Sounds.Menu_CollectRedCoin7, SoundArgLoad(7, 8, 0x28 + 7, 0x90, 8) },
        { Sounds.Menu_CollectSecret, SoundArgLoad(7, 0, 0x30, 0x20, 8) },

        // General2
        { Sounds.General2_BobombExplosion, SoundArgLoad(8, 0, 0x2E, 0x20, 8) },
        { Sounds.General2_PurpleSwitch, SoundArgLoad(8, 0, 0x3E, 0xC0, 8) },
        { Sounds.General2_RotatingBlockClick, SoundArgLoad(8, 0, 0x40, 0x00, 8) },
        { Sounds.General2_SpindelRoll, SoundArgLoad(8, 0, 0x48, 0x20, 8) },
        { Sounds.General2_PyramidTopSpin, SoundArgLoad(8, 1, 0x4B, 0xE0, 8) },
        { Sounds.General2_PyramidTopExplosion, SoundArgLoad(8, 1, 0x4C, 0xF0, 8) },
        { Sounds.General2_BirdChirp2, SoundArgLoad(8, 0, 0x50, 0x40, 0) },
        { Sounds.General2_SwitchTickFast, SoundArgLoad(8, 0, 0x54, 0xF0, 1) },
        { Sounds.General2_SwitchTickSlow, SoundArgLoad(8, 0, 0x55, 0xF0, 1) },
        { Sounds.General2_StarAppears, SoundArgLoad(8, 0, 0x57, 0xFF, 9) },
        { Sounds.General2_RotatingBlockAlert, SoundArgLoad(8, 0, 0x59, 0x00, 8) },
        { Sounds.General2_BowserExplode, SoundArgLoad(8, 0, 0x60, 0x00, 8) },
        { Sounds.General2_BowserKey, SoundArgLoad(8, 0, 0x61, 0x00, 8) },
        { Sounds.General2_OneUpAppear, SoundArgLoad(8, 0, 0x63, 0xD0, 8) },
        { Sounds.General2_RightAnswer, SoundArgLoad(8, 0, 0x6A, 0xA0, 8) },

        // Object2
        { Sounds.Object2_BowserRoar, SoundArgLoad(9, 0, 0x04, 0x00, 8) },
        { Sounds.Object2_PiranhaPlantBite, SoundArgLoad(9, 0, 0x10, 0x50, 8) },
        { Sounds.Object2_PiranhaPlantDying, SoundArgLoad(9, 0, 0x11, 0x60, 8) },
        { Sounds.Object2_BowserPuzzlePieceMove, SoundArgLoad(9, 0, 0x19, 0x20, 8) },
        { Sounds.Object2_BullyAttacked, SoundArgLoad(9, 0, 0x1C, 0x00, 8) },
        { Sounds.Object2_KingBobombDamage, SoundArgLoad(9, 1, 0x42, 0x40, 8) },
        { Sounds.Object2_ScuttlebugWalk, SoundArgLoad(9, 0, 0x43, 0x40, 8) },
        { Sounds.Object2_ScuttlebugAlert, SoundArgLoad(9, 0, 0x44, 0x40, 8) },
        { Sounds.Object2_BabyPenguinYell, SoundArgLoad(9, 0, 0x45, 0x00, 8) },
        { Sounds.Object2_Swoop, SoundArgLoad(9, 0, 0x49, 0x00, 8) },
        { Sounds.Object2_BirdChirp1, SoundArgLoad(9, 0, 0x52, 0x40, 0) },
        { Sounds.Object2_LargeBullyAttacked, SoundArgLoad(9, 0, 0x57, 0x00, 8) },
        { Sounds.Object2_EyerokSoundShort, SoundArgLoad(9, 3, 0x5A, 0x00, 8) },
        { Sounds.Object2_WhompSoundShort, SoundArgLoad(9, 3, 0x5A, 0xC0, 8) },
        { Sounds.Object2_EyerokSoundLong, SoundArgLoad(9, 2, 0x5B, 0x00, 8) },
        { Sounds.Object2_BowserTeleport, SoundArgLoad(9, 0, 0x66, 0x80, 8) },
        { Sounds.Object2_MontyMoleAppear, SoundArgLoad(9, 0, 0x67, 0x80, 8) },
        { Sounds.Object2_BossDialogGrunt, SoundArgLoad(9, 0, 0x69, 0x40, 8) },
        { Sounds.Object2_MriSpinning, SoundArgLoad(9, 0, 0x6B, 0x00, 8) },
    };

#endregion

    // mario_animation_ids.h
    /// <summary>
    /// Represents all of Mario's animation IDs.
    /// </summary>
    public enum MarioAnimationId : int
    {
        SlowLedgeGrab,                  // 0x00 
        FallOverBackwards,              // 0x01 
        BackwardAirKb,                  // 0x02 
        DyingOnBack,                    // 0x03 
        Backflip,                       // 0x04 
        ClimbUpPole,                    // 0x05 
        GrabPoleShort,                  // 0x06 
        GrabPoleSwingPart1,             // 0x07 
        GrabPoleSwingPart2,             // 0x08 
        HandstandIdle,                  // 0x09 
        HandstandJump,                  // 0x0A 
        StartHandstand,                 // 0x0B 
        ReturnFromHandstand,            // 0x0C 
        IdleOnPole,                     // 0x0D 
        APose,                          // 0x0E 
        SkidOnGround,                   // 0x0F 
        StopSkid,                       // 0x10 
        CrouchFromFastLongjump,         // 0x11 
        CrouchFromSlowLongjump,         // 0x12 
        FastLongjump,                   // 0x13 
        SlowLongjump,                   // 0x14 
        AirborneOnStomach,              // 0x15 
        WalkWithLightObj,               // 0x16 
        RunWithLightObj,                // 0x17 
        SlowWalkWithLightObj,           // 0x18 
        ShiveringWarmingHand,           // 0x19 
        ShiveringReturnToIdle,          // 0x1A 
        Shivering,                      // 0x1B 
        ClimbDownLedge,                 // 0x1C 
        CreditsWaving,                  // 0x1D 
        CreditsLookUp,                  // 0x1E 
        CreditsReturnFromLookUp,        // 0x1F 
        CreditsRaiseHand,               // 0x20 
        CreditsLowerHand,               // 0x21 
        CreditsTakeOffCap,              // 0x22 
        CreditsStartWalkLookUp,         // 0x23 
        CreditsLookBackThenRun,         // 0x24 
        FinalBowserRaiseHandSpin,       // 0x25 
        FinalBowserWingCapTakeOff,      // 0x26 
        CreditsPeaceSign,               // 0x27 
        StandUpFromLavaBoost,           // 0x28 
        FireLavaBurn,                   // 0x29 
        WingCapFly,                     // 0x2A 
        HangOnOwl,                      // 0x2B 
        LandOnStomach,                  // 0x2C 
        AirForwardKb,                   // 0x2D 
        DyingOnStomach,                 // 0x2E 
        Suffocating,                    // 0x2F 
        Coughing,                       // 0x30 
        ThrowCatchKey,                  // 0x31 
        DyingFallOver,                  // 0x32 
        IdleOnLedge,                    // 0x33 
        FastLedgeGrab,                  // 0x34 
        HangOnCeiling,                  // 0x35 
        PutCapOn,                       // 0x36 
        TakeCapOffThenOn,               // 0x37 
        QuicklyPutCapOn,                // 0x38  // unused
        HeadStuckInGround,              // 0x39 
        GroundPoundLanding,             // 0x3A 
        TripleJumpGroundPound,          // 0x3B 
        StartGroundPound,               // 0x3C 
        GroundPound,                    // 0x3D 
        BottomStuckInGround,            // 0x3E 
        IdleWithLightObj,               // 0x3F 
        JumpLandWithLightObj,           // 0x40 
        JumpWithLightObj,               // 0x41 
        FallLandWithLightObj,           // 0x42 
        FallWithLightObj,               // 0x43 
        FallFromSlidingWithLightObj,    // 0x44 
        SlidingOnBottomWithLightObj,    // 0x45 
        StandUpFromSlidingWithLightObj, // 0x46 
        RidingShell,                    // 0x47 
        Walking,                        // 0x48 
        ForwardFlip,                    // 0x49  // unused
        JumpRidingShell,                // 0x4A 
        LandFromDoubleJump,             // 0x4B 
        DoubleJumpFall,                 // 0x4C 
        SingleJump,                     // 0x4D 
        LandFromSingleJump,             // 0x4E 
        AirKick,                        // 0x4F 
        DoubleJumpRise,                 // 0x50 
        StartForwardSpinning,           // 0x51  // unused
        ThrowLightObject,               // 0x52 
        FallFromSlideKick,              // 0x53 
        BendKneesRidingShell,           // 0x54  // unused
        LegsStuckInGround,              // 0x55 
        GeneralFall,                    // 0x56 
        GeneralLand,                    // 0x57 
        BeingGrabbed,                   // 0x58 
        GrabHeavyObject,                // 0x59 
        SlowLandFromDive,               // 0x5A 
        FlyFromCannon,                  // 0x5B 
        MoveOnWireNetRight,             // 0x5C 
        MoveOnWireNetLeft,              // 0x5D 
        MissingCap,                     // 0x5E 
        PullDoorWalkIn,                 // 0x5F 
        PushDoorWalkIn,                 // 0x60 
        UnlockDoor,                     // 0x61 
        StartReachPocket,               // 0x62  // unused, reaching keys maybe?
        ReachPocket,                    // 0x63  // unused
        StopReachPocket,                // 0x64  // unused
        GroundThrow,                    // 0x65 
        GroundKick,                     // 0x66 
        FirstPunch,                     // 0x67 
        SecondPunch,                    // 0x68 
        FirstPunchFast,                 // 0x69 
        SecondPunchFast,                // 0x6A 
        PickUpLightObj,                 // 0x6B 
        Pushing,                        // 0x6C 
        StartRidingShell,               // 0x6D 
        PlaceLightObj,                  // 0x6E 
        ForwardSpinning,                // 0x6F 
        BackwardSpinning,               // 0x70 
        Breakdance,                     // 0x71 
        Running,                        // 0x72 
        RunningUnused,                  // 0x73  // unused duplicate, originally part 2?
        SoftBackKb,                     // 0x74 
        SoftFrontKb,                    // 0x75 
        DyingInQuicksand,               // 0x76 
        IdleInQuicksand,                // 0x77 
        MoveInQuicksand,                // 0x78 
        Electrocution,                  // 0x79 
        Shocked,                        // 0x7A 
        BackwardKb,                     // 0x7B 
        ForwardKb,                      // 0x7C 
        IdleHeavyObj,                   // 0x7D 
        StandAgainstWall,               // 0x7E 
        SidestepLeft,                   // 0x7F 
        SidestepRight,                  // 0x80 
        StartSleepIdle,                 // 0x81 
        StartSleepScratch,              // 0x82 
        StartSleepYawn,                 // 0x83 
        StartSleepSitting,              // 0x84 
        SleepIdle,                      // 0x85 
        SleepStartLying,                // 0x86 
        SleepLying,                     // 0x87 
        Dive,                           // 0x88 
        SlideDive,                      // 0x89 
        GroundBonk,                     // 0x8A 
        StopSlideLightObj,              // 0x8B 
        SlideKick,                      // 0x8C 
        CrouchFromSlideKick,            // 0x8D 
        SlideMotionless,                // 0x8E  // unused
        StopSlide,                      // 0x8F 
        FallFromSlide,                  // 0x90 
        Slide,                          // 0x91 
        Tiptoe,                         // 0x92 
        TwirlLand,                      // 0x93 
        Twirl,                          // 0x94 
        StartTwirl,                     // 0x95 
        StopCrouching,                  // 0x96 
        StartCrouching,                 // 0x97 
        Crouching,                      // 0x98 
        Crawling,                       // 0x99 
        StopCrawling,                   // 0x9A 
        StartCrawling,                  // 0x9B 
        SummonStar,                     // 0x9C 
        ReturnStarApproachDoor,         // 0x9D 
        BackwardsWaterKb,               // 0x9E 
        SwimWithObjPart1,               // 0x9F 
        SwimWithObjPart2,               // 0xA0 
        FlutterkickWithObj,             // 0xA1 
        WaterActionEndWithObj,          // 0xA2  // either swimming or flutterkicking
        StopGrabObjWater,               // 0xA3 
        WaterIdleWithObj,               // 0xA4 
        DrowningPart1,                  // 0xA5 
        DrowningPart2,                  // 0xA6 
        WaterDying,                     // 0xA7 
        WaterForwardKb,                 // 0xA8 
        FallFromWater,                  // 0xA9 
        SwimPart1,                      // 0xAA 
        SwimPart2,                      // 0xAB 
        Flutterkick,                    // 0xAC 
        WaterActionEnd,                 // 0xAD  // either swimming or flutterkicking
        WaterPickUpObj,                 // 0xAE 
        WaterGrabObjPart2,              // 0xAF 
        WaterGrabObjPart1,              // 0xB0 
        WaterThrowObj,                  // 0xB1 
        WaterIdle,                      // 0xB2 
        WaterStarDance,                 // 0xB3 
        ReturnFromWaterStarDance,       // 0xB4 
        GrabBowser,                     // 0xB5 
        SwingingBowser,                 // 0xB6 
        ReleaseBowser,                  // 0xB7 
        HoldingBowser,                  // 0xB8 
        HeavyThrow,                     // 0xB9 
        WalkPanting,                    // 0xBA 
        WalkWithHeavyObj,               // 0xBB 
        TurningPart1,                   // 0xBC 
        TurningPart2,                   // 0xBD 
        SlideflipLand,                  // 0xBE 
        Slideflip,                      // 0xBF 
        TripleJumpLand,                 // 0xC0 
        TripleJump,                     // 0xC1 
        FirstPerson,                    // 0xC2 
        IdleHeadLeft,                   // 0xC3 
        IdleHeadRight,                  // 0xC4 
        IdleHeadCenter,                 // 0xC5 
        HandstandLeft,                  // 0xC6 
        HandstandRight,                 // 0xC7 
        WakeFromSleep,                  // 0xC8 
        WakeFromLying,                  // 0xC9 
        StartTiptoe,                    // 0xCA 
        Slidejump,                      // 0xCB  // pole jump and wall kick
        StartWallkick,                  // 0xCC 
        StarDance,                      // 0xCD 
        ReturnFromStarDance,            // 0xCE 
        ForwardSpinningFlip,            // 0xCF 
        TripleJumpFly                   // 0xD0
    }

    // sm64.h
    /// <summary>
    /// Flags representing Mario's current state, powers, and caps.
    /// </summary>
    [Flags]
    public enum StateFlag : uint
    {
        NormalCap = 1 << 0,          // 0x00000001
        VanishCap = 1 << 1,          // 0x00000002
        MetalCap = 1 << 2,           // 0x00000004
        WingCap = 1 << 3,            // 0x00000008
        CapOnHead = 1 << 4,          // 0x00000010
        CapInHand = 1 << 5,          // 0x00000020
        MetalShock = 1 << 6,         // 0x00000040
        Teleporting = 1 << 7,        // 0x00000080
        Unknown08 = 1 << 8,          // 0x00000100
        Unknown13 = 1 << 13,         // 0x00002000
        ActionSoundPlayed = 1 << 16, // 0x00010000
        MarioSoundPlayed = 1 << 17,  // 0x00020000
        Unknown18 = 1 << 18,         // 0x00040000
        Punching = 1 << 20,          // 0x00100000
        Kicking = 1 << 21,           // 0x00200000
        Tripping = 1 << 22,          // 0x00400000
        Unknown25 = 1 << 25,         // 0x02000000
        Unknown30 = 1 << 30,         // 0x40000000
        Unknown31 = 0x80000000       // 1 << 31
    }

    public const StateFlag SpecialCaps = StateFlag.VanishCap | StateFlag.MetalCap | StateFlag.WingCap;
    public const StateFlag Caps = StateFlag.NormalCap | SpecialCaps;

    [Flags]
    public enum MarioCapType : uint
    {
        NormalCap = StateFlag.NormalCap,
        VanishCap = StateFlag.VanishCap,
        MetalCap = StateFlag.MetalCap,
        WingCap = StateFlag.WingCap
    }

    public const uint ActionGroupMask = 0x000001C0;

    /// <summary>
    /// High-level categorization of Mario's actions.
    /// </summary>
    /// [Flags]
    public enum ActionGroup : uint
    {
        Stationary = 0U,    // 0x00000000
        Moving = 1 << 6,    // 0x00000040
        Airborne = 2 << 6,  // 0x00000080
        Submerged = 3 << 6, // 0x000000C0
        Cutscene = 4 << 6,  // 0x00000100
        Automatic = 5 << 6, // 0x00000140
        Object = 6 << 6,    // 0x00000180
    }

    /// <summary>
    /// Flags that describe the properties of Mario's current action and represents all of Mario's possible actions
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [Flags]
    public enum ActionFlag : uint
    {
        // Base Mario Actions
        Stationary = 1 << 9,               // 0x00000200
        Moving = 1 << 10,                  // 0x00000400
        Air = 1 << 11,                     // 0x00000800
        Intangible = 1 << 12,              // 0x00001000
        Swimming = 1 << 13,                // 0x00002000
        MetalWater = 1 << 14,              // 0x00004000
        ShortHitbox = 1 << 15,             // 0x00008000
        RidingShell = 1 << 16,             // 0x00010000
        Invulnerable = 1 << 17,            // 0x00020000
        ButtOrStomachSlide = 1 << 18,      // 0x00040000
        Diving = 1 << 19,                  // 0x00080000
        OnPole = 1 << 20,                  // 0x00100000
        Mario_Hanging = 1 << 21,           // 0x00200000
        Mario_Idle = 1 << 22,              // 0x00400000
        Attacking = 1 << 23,               // 0x00800000
        AllowVerticalWindAction = 1 << 24, // 0x01000000
        ControlJumpHeight = 1 << 25,       // 0x02000000
        AllowFirstPerson = 1 << 26,        // 0x04000000
        PauseExit = 1 << 27,               // 0x08000000
        SwimmingOrFlying = 1 << 28,        // 0x10000000
        WaterOrText = 1 << 29,             // 0x20000000
        Mario_Throwing = 0x80000000,       // (1 << 31)

        // This starts All of Mario's possible Actions
        Uninitialized = 0x00000000, // (0x000)

        // group 0x000: stationary actions
        Idle = 0x0C400201,                 // (0x001 | ActionFlag.Stationary | ActionFlag.Mario_Idle | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        StartSleeping = 0x0C400202,        // (0x002 | ActionFlag.Stationary | ActionFlag.Mario_Idle | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        Sleeping = 0x0C000203,             // (0x003 | ActionFlag.Stationary | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        WakingUp = 0x0C000204,             // (0x004 | ActionFlag.Stationary | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        Panting = 0x0C400205,              // (0x005 | ActionFlag.Stationary | ActionFlag.Mario_Idle | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        HoldPantingUnused = 0x08000206,    // (0x006 | ActionFlag.Stationary | ActionFlag.PauseExit)
        HoldIdle = 0x08000207,             // (0x007 | ActionFlag.Stationary | ActionFlag.PauseExit)
        HoldHeavyIdle = 0x08000208,        // (0x008 | ActionFlag.Stationary | ActionFlag.PauseExit)
        StandingAgainstWall = 0x0C400209,  // (0x009 | ActionFlag.Stationary | ActionFlag.Mario_Idle | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        Coughing = 0x0C40020A,             // (0x00A | ActionFlag.Stationary | ActionFlag.Mario_Idle | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        Shivering = 0x0C40020B,            // (0x00B | ActionFlag.Stationary | ActionFlag.Mario_Idle | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        InQuicksand = 0x0002020D,          // (0x00D | ActionFlag.Stationary | ActionFlag.Invulnerable)
        Unknown_0002020E = 0x0002020E,     // (0x00E | ActionFlag.Stationary | ActionFlag.Invulnerable)
        Crouching = 0x0C008220,            // (0x020 | ActionFlag.Stationary | ActionFlag.ShortHitbox | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        StartCrouching = 0x0C008221,       // (0x021 | ActionFlag.Stationary | ActionFlag.ShortHitbox | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        StopCrouching = 0x0C008222,        // (0x022 | ActionFlag.Stationary | ActionFlag.ShortHitbox | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        StartCrawling = 0x0C008223,        // (0x023 | ActionFlag.Stationary | ActionFlag.ShortHitbox | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        StopCrawling = 0x0C008224,         // (0x024 | ActionFlag.Stationary | ActionFlag.ShortHitbox | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        SlideKickSlideStop = 0x08000225,   // (0x025 | ActionFlag.Stationary | ActionFlag.PauseExit)
        ShockwaveBounce = 0x00020226,      // (0x026 | ActionFlag.Stationary | ActionFlag.Invulnerable)
        FirstPerson = 0x0C000227,          // (0x027 | ActionFlag.Stationary | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        BackflipLandStop = 0x0800022F,     // (0x02F | ActionFlag.Stationary | ActionFlag.PauseExit)
        JumpLandStop = 0x0C000230,         // (0x030 | ActionFlag.Stationary | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        DoubleJumpLandStop = 0x0C000231,   // (0x031 | ActionFlag.Stationary | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        FreefallLandStop = 0x0C000232,     // (0x032 | ActionFlag.Stationary | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        SideFlipLandStop = 0x0C000233,     // (0x033 | ActionFlag.Stationary | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        HoldJumpLandStop = 0x08000234,     // (0x034 | ActionFlag.Stationary | ActionFlag.PauseExit)
        HoldFreefallLandStop = 0x08000235, // (0x035 | ActionFlag.Stationary | ActionFlag.PauseExit)
        AirThrowLand = 0x80000A36,         // (0x036 | ActionFlag.Stationary | ActionFlag.Air | ActionFlag.Throwing)
        TwirlLand = 0x18800238,            // (0x038 | ActionFlag.Stationary | ActionFlag.Attacking | ActionFlag.PauseExit | ActionFlag.SwimmingOrFlying)
        LavaBoostLand = 0x08000239,        // (0x039 | ActionFlag.Stationary | ActionFlag.PauseExit)
        TripleJumpLandStop = 0x0800023A,   // (0x03A | ActionFlag.Stationary | ActionFlag.PauseExit)
        LongJumpLandStop = 0x0800023B,     // (0x03B | ActionFlag.Stationary | ActionFlag.PauseExit)
        GroundPoundLand = 0x0080023C,      // (0x03C | ActionFlag.Stationary | ActionFlag.Attacking)
        BrakingStop = 0x0C00023D,          // (0x03D | ActionFlag.Stationary | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        ButtSlideStop = 0x0C00023E,        // (0x03E | ActionFlag.Stationary | ActionFlag.AllowFirstPerson | ActionFlag.PauseExit)
        HoldButtSlideStop = 0x0800043F,    // (0x03F | ActionFlag.Moving | ActionFlag.PauseExit)

        // group 0x040: moving (ground) actions
        Walking = 0x04000440,               // (0x040 | ActionFlag.Moving | ActionFlag.AllowFirstPerson)
        HoldWalking = 0x00000442,           // (0x042 | ActionFlag.Moving)
        TurningAround = 0x00000443,         // (0x043 | ActionFlag.Moving)
        FinishTurningAround = 0x00000444,   // (0x044 | ActionFlag.Moving)
        Braking = 0x04000445,               // (0x045 | ActionFlag.Moving | ActionFlag.AllowFirstPerson)
        RidingShellGround = 0x20810446,     // (0x046 | ActionFlag.Moving | ActionFlag.RidingShell | ActionFlag.Attacking | ActionFlag.WaterOrText)
        HoldHeavyWalking = 0x00000447,      // (0x047 | ActionFlag.Moving)
        Crawling = 0x04008448,              // (0x048 | ActionFlag.Moving | ActionFlag.ShortHitbox | ActionFlag.AllowFirstPerson)
        BurningGround = 0x00020449,         // (0x049 | ActionFlag.Moving | ActionFlag.Invulnerable)
        Decelerating = 0x0400044A,          // (0x04A | ActionFlag.Moving | ActionFlag.AllowFirstPerson)
        HoldDecelerating = 0x0000044B,      // (0x04B | ActionFlag.Moving)
        BeginSliding = 0x00000050,          // (0x050)
        HoldBeginSliding = 0x00000051,      // (0x051)
        ButtSlide = 0x00840452,             // (0x052 | ActionFlag.Moving | ActionFlag.ButtOrStomachSlide | ActionFlag.Attacking)
        StomachSlide = 0x008C0453,          // (0x053 | ActionFlag.Moving | ActionFlag.ButtOrStomachSlide | ActionFlag.Diving | ActionFlag.Attacking)
        HoldButtSlide = 0x00840454,         // (0x054 | ActionFlag.Moving | ActionFlag.ButtOrStomachSlide | ActionFlag.Attacking)
        HoldStomachSlide = 0x008C0455,      // (0x055 | ActionFlag.Moving | ActionFlag.ButtOrStomachSlide | ActionFlag.Diving | ActionFlag.Attacking)
        DiveSlide = 0x00880456,             // (0x056 | ActionFlag.Moving | ActionFlag.Diving | ActionFlag.Attacking)
        MovePunching = 0x00800457,          // (0x057 | ActionFlag.Moving | ActionFlag.Attacking)
        CrouchSlide = 0x04808459,           // (0x059 | ActionFlag.Moving | ActionFlag.ShortHitbox | ActionFlag.Attacking | ActionFlag.AllowFirstPerson)
        SlideKickSlide = 0x0080045A,        // (0x05A | ActionFlag.Moving | ActionFlag.Attacking)
        HardBackwardGroundKb = 0x00020460,  // (0x060 | ActionFlag.Moving | ActionFlag.Invulnerable)
        HardForwardGroundKb = 0x00020461,   // (0x061 | ActionFlag.Moving | ActionFlag.Invulnerable)
        BackwardGroundKb = 0x00020462,      // (0x062 | ActionFlag.Moving | ActionFlag.Invulnerable)
        ForwardGroundKb = 0x00020463,       // (0x063 | ActionFlag.Moving | ActionFlag.Invulnerable)
        SoftBackwardGroundKb = 0x00020464,  // (0x064 | ActionFlag.Moving | ActionFlag.Invulnerable)
        SoftForwardGroundKb = 0x00020465,   // (0x065 | ActionFlag.Moving | ActionFlag.Invulnerable)
        GroundBonk = 0x00020466,            // (0x066 | ActionFlag.Moving | ActionFlag.Invulnerable)
        DeathExitLand = 0x00020467,         // (0x067 | ActionFlag.Moving | ActionFlag.Invulnerable)
        JumpLand = 0x04000470,              // (0x070 | ActionFlag.Moving | ActionFlag.AllowFirstPerson)
        FreefallLand = 0x04000471,          // (0x071 | ActionFlag.Moving | ActionFlag.AllowFirstPerson)
        DoubleJumpLand = 0x04000472,        // (0x072 | ActionFlag.Moving | ActionFlag.AllowFirstPerson)
        SideFlipLand = 0x04000473,          // (0x073 | ActionFlag.Moving | ActionFlag.AllowFirstPerson)
        HoldJumpLand = 0x00000474,          // (0x074 | ActionFlag.Moving)
        HoldFreefallLand = 0x00000475,      // (0x075 | ActionFlag.Moving)
        QuicksandJumpLand = 0x00000476,     // (0x076 | ActionFlag.Moving)
        HoldQuicksandJumpLand = 0x00000477, // (0x077 | ActionFlag.Moving)
        TripleJumpLand = 0x04000478,        // (0x078 | ActionFlag.Moving | ActionFlag.AllowFirstPerson)
        LongJumpLand = 0x00000479,          // (0x079 | ActionFlag.Moving)
        BackflipLand = 0x0400047A,          // (0x07A | ActionFlag.Moving | ActionFlag.AllowFirstPerson)

        // group 0x080: airborne actions
        Jump = 0x03000880,              // (0x080 | ActionFlag.Air | ActionFlag.AllowVerticalWindAction | ActionFlag.ControlJumpHeight)
        DoubleJump = 0x03000881,        // (0x081 | ActionFlag.Air | ActionFlag.AllowVerticalWindAction | ActionFlag.ControlJumpHeight)
        TripleJump = 0x01000882,        // (0x082 | ActionFlag.Air | ActionFlag.AllowVerticalWindAction)
        Backflip = 0x01000883,          // (0x083 | ActionFlag.Air | ActionFlag.AllowVerticalWindAction)
        SteepJump = 0x03000885,         // (0x085 | ActionFlag.Air | ActionFlag.AllowVerticalWindAction | ActionFlag.ControlJumpHeight)
        WallKickAir = 0x03000886,       // (0x086 | ActionFlag.Air | ActionFlag.AllowVerticalWindAction | ActionFlag.ControlJumpHeight)
        SideFlip = 0x01000887,          // (0x087 | ActionFlag.Air | ActionFlag.AllowVerticalWindAction)
        LongJump = 0x03000888,          // (0x088 | ActionFlag.Air | ActionFlag.AllowVerticalWindAction | ActionFlag.ControlJumpHeight)
        WaterJump = 0x01000889,         // (0x089 | ActionFlag.Air | ActionFlag.AllowVerticalWindAction)
        Dive = 0x0188088A,              // (0x08A | ActionFlag.Air | ActionFlag.Diving | ActionFlag.Attacking | ActionFlag.AllowVerticalWindAction)
        Freefall = 0x0100088C,          // (0x08C | ActionFlag.Air | ActionFlag.AllowVerticalWindAction)
        TopOfPoleJump = 0x0300088D,     // (0x08D | ActionFlag.Air | ActionFlag.AllowVerticalWindAction | ActionFlag.ControlJumpHeight)
        ButtSlideAir = 0x0300088E,      // (0x08E | ActionFlag.Air | ActionFlag.AllowVerticalWindAction | ActionFlag.ControlJumpHeight)
        FlyingTripleJump = 0x03000894,  // (0x094 | ActionFlag.Air | ActionFlag.AllowVerticalWindAction | ActionFlag.ControlJumpHeight)
        ShotFromCannon = 0x00880898,    // (0x098 | ActionFlag.Air | ActionFlag.Diving | ActionFlag.Attacking)
        Flying = 0x10880899,            // (0x099 | ActionFlag.Air | ActionFlag.Diving | ActionFlag.Attacking | ActionFlag.SwimmingOrFlying)
        RidingShellJump = 0x0281089A,   // (0x09A | ActionFlag.Air | ActionFlag.RidingShell | ActionFlag.Attacking | ActionFlag.ControlJumpHeight)
        RidingShellFall = 0x0081089B,   // (0x09B | ActionFlag.Air | ActionFlag.RidingShell | ActionFlag.Attacking)
        VerticalWind = 0x1008089C,      // (0x09C | ActionFlag.Air | ActionFlag.Diving | ActionFlag.SwimmingOrFlying)
        HoldJump = 0x030008A0,          // (0x0A0 | ActionFlag.Air | ActionFlag.AllowVerticalWindAction | ActionFlag.ControlJumpHeight)
        HoldFreefall = 0x010008A1,      // (0x0A1 | ActionFlag.Air | ActionFlag.AllowVerticalWindAction)
        HoldButtSlideAir = 0x010008A2,  // (0x0A2 | ActionFlag.Air | ActionFlag.AllowVerticalWindAction)
        HoldWaterJump = 0x010008A3,     // (0x0A3 | ActionFlag.Air | ActionFlag.AllowVerticalWindAction)
        Twirling = 0x108008A4,          // (0x0A4 | ActionFlag.Air | ActionFlag.Attacking | ActionFlag.SwimmingOrFlying)
        ForwardRollout = 0x010008A6,    // (0x0A6 | ActionFlag.Air | ActionFlag.AllowVerticalWindAction)
        AirHitWall = 0x000008A7,        // (0x0A7 | ActionFlag.Air)
        RidingHoot = 0x000004A8,        // (0x0A8 | ActionFlag.Moving)
        GroundPound = 0x008008A9,       // (0x0A9 | ActionFlag.Air | ActionFlag.Attacking)
        SlideKick = 0x018008AA,         // (0x0AA | ActionFlag.Air | ActionFlag.Attacking | ActionFlag.AllowVerticalWindAction)
        AirThrow = 0x830008AB,          // (0x0AB | ActionFlag.Air | ActionFlag.AllowVerticalWindAction | ActionFlag.ControlJumpHeight | ActionFlag.Throwing)
        JumpKick = 0x018008AC,          // (0x0AC | ActionFlag.Air | ActionFlag.Attacking | ActionFlag.AllowVerticalWindAction)
        BackwardRollout = 0x010008AD,   // (0x0AD | ActionFlag.Air | ActionFlag.AllowVerticalWindAction)
        CrazyBoxBounce = 0x000008AE,    // (0x0AE | ActionFlag.Air)
        SpecialTripleJump = 0x030008AF, // (0x0AF | ActionFlag.Air | ActionFlag.AllowVerticalWindAction | ActionFlag.ControlJumpHeight)
        BackwardAirKb = 0x010208B0,     // (0x0B0 | ActionFlag.Air | ActionFlag.Invulnerable | ActionFlag.AllowVerticalWindAction)
        ForwardAirKb = 0x010208B1,      // (0x0B1 | ActionFlag.Air | ActionFlag.Invulnerable | ActionFlag.AllowVerticalWindAction)
        HardForwardAirKb = 0x010208B2,  // (0x0B2 | ActionFlag.Air | ActionFlag.Invulnerable | ActionFlag.AllowVerticalWindAction)
        HardBackwardAirKb = 0x010208B3, // (0x0B3 | ActionFlag.Air | ActionFlag.Invulnerable | ActionFlag.AllowVerticalWindAction)
        BurningJump = 0x010208B4,       // (0x0B4 | ActionFlag.Air | ActionFlag.Invulnerable | ActionFlag.AllowVerticalWindAction)
        BurningFall = 0x010208B5,       // (0x0B5 | ActionFlag.Air | ActionFlag.Invulnerable | ActionFlag.AllowVerticalWindAction)
        SoftBonk = 0x010208B6,          // (0x0B6 | ActionFlag.Air | ActionFlag.Invulnerable | ActionFlag.AllowVerticalWindAction)
        LavaBoost = 0x010208B7,         // (0x0B7 | ActionFlag.Air | ActionFlag.Invulnerable | ActionFlag.AllowVerticalWindAction)
        GettingBlown = 0x010208B8,      // (0x0B8 | ActionFlag.Air | ActionFlag.Invulnerable | ActionFlag.AllowVerticalWindAction)
        ThrownForward = 0x010208BD,     // (0x0BD | ActionFlag.Air | ActionFlag.Invulnerable | ActionFlag.AllowVerticalWindAction)
        ThrownBackward = 0x010208BE,    // (0x0BE | ActionFlag.Air | ActionFlag.Invulnerable | ActionFlag.AllowVerticalWindAction)

        // group 0x0C0: submerged actions
        WaterIdle = 0x380022C0,              // (0x0C0 | ActionFlag.Stationary | ActionFlag.Swimming | ActionFlag.PauseExit | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        HoldWaterIdle = 0x380022C1,          // (0x0C1 | ActionFlag.Stationary | ActionFlag.Swimming | ActionFlag.PauseExit | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        WaterActionEnd = 0x300022C2,         // (0x0C2 | ActionFlag.Stationary | ActionFlag.Swimming | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        HoldWaterActionEnd = 0x300022C3,     // (0x0C3 | ActionFlag.Stationary | ActionFlag.Swimming | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        Drowning = 0x300032C4,               // (0x0C4 | ActionFlag.Stationary | ActionFlag.Intangible | ActionFlag.Swimming | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        BackwardWaterKb = 0x300222C5,        // (0x0C5 | ActionFlag.Stationary | ActionFlag.Swimming | ActionFlag.Invulnerable | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        ForwardWaterKb = 0x300222C6,         // (0x0C6 | ActionFlag.Stationary | ActionFlag.Swimming | ActionFlag.Invulnerable | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        WaterDeath = 0x300032C7,             // (0x0C7 | ActionFlag.Stationary | ActionFlag.Intangible | ActionFlag.Swimming | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        WaterShocked = 0x300222C8,           // (0x0C8 | ActionFlag.Stationary | ActionFlag.Swimming | ActionFlag.Invulnerable | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        Breaststroke = 0x300024D0,           // (0x0D0 | ActionFlag.Moving | ActionFlag.Swimming | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        SwimmingEnd = 0x300024D1,            // (0x0D1 | ActionFlag.Moving | ActionFlag.Swimming | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        FlutterKick = 0x300024D2,            // (0x0D2 | ActionFlag.Moving | ActionFlag.Swimming | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        HoldBreaststroke = 0x300024D3,       // (0x0D3 | ActionFlag.Moving | ActionFlag.Swimming | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        HoldSwimmingEnd = 0x300024D4,        // (0x0D4 | ActionFlag.Moving | ActionFlag.Swimming | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        HoldFlutterKick = 0x300024D5,        // (0x0D5 | ActionFlag.Moving | ActionFlag.Swimming | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        WaterShellSwimming = 0x300024D6,     // (0x0D6 | ActionFlag.Moving | ActionFlag.Swimming | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        WaterThrow = 0x300024E0,             // (0x0E0 | ActionFlag.Moving | ActionFlag.Swimming | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        WaterPunch = 0x300024E1,             // (0x0E1 | ActionFlag.Moving | ActionFlag.Swimming | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        WaterPlunge = 0x300022E2,            // (0x0E2 | ActionFlag.Stationary | ActionFlag.Swimming | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        CaughtInWhirlpool = 0x300222E3,      // (0x0E3 | ActionFlag.Stationary | ActionFlag.Swimming | ActionFlag.Invulnerable | ActionFlag.SwimmingOrFlying | ActionFlag.WaterOrText)
        MetalWaterStanding = 0x080042F0,     // (0x0F0 | ActionFlag.Stationary | ActionFlag.MetalWater | ActionFlag.PauseExit)
        HoldMetalWaterStanding = 0x080042F1, // (0x0F1 | ActionFlag.Stationary | ActionFlag.MetalWater | ActionFlag.PauseExit)
        MetalWaterWalking = 0x000044F2,      // (0x0F2 | ActionFlag.Moving | ActionFlag.MetalWater)
        HoldMetalWaterWalking = 0x000044F3,  // (0x0F3 | ActionFlag.Moving | ActionFlag.MetalWater)
        MetalWaterFalling = 0x000042F4,      // (0x0F4 | ActionFlag.Stationary | ActionFlag.MetalWater)
        HoldMetalWaterFalling = 0x000042F5,  // (0x0F5 | ActionFlag.Stationary | ActionFlag.MetalWater)
        MetalWaterFallLand = 0x000042F6,     // (0x0F6 | ActionFlag.Stationary | ActionFlag.MetalWater)
        HoldMetalWaterFallLand = 0x000042F7, // (0x0F7 | ActionFlag.Stationary | ActionFlag.MetalWater)
        MetalWaterJump = 0x000044F8,         // (0x0F8 | ActionFlag.Moving | ActionFlag.MetalWater)
        HoldMetalWaterJump = 0x000044F9,     // (0x0F9 | ActionFlag.Moving | ActionFlag.MetalWater)
        MetalWaterJumpLand = 0x000044FA,     // (0x0FA | ActionFlag.Moving | ActionFlag.MetalWater)
        HoldMetalWaterJumpLand = 0x000044FB, // (0x0FB | ActionFlag.Moving | ActionFlag.MetalWater)

        // group 0x100: cutscene actions
        Disappeared = 0x00001300,            // (0x100 | ActionFlag.Stationary | ActionFlag.Intangible)
        IntroCutscene = 0x04001301,          // (0x101 | ActionFlag.Stationary | ActionFlag.Intangible | ActionFlag.AllowFirstPerson)
        StarDanceExit = 0x00001302,          // (0x102 | ActionFlag.Stationary | ActionFlag.Intangible)
        StarDanceWater = 0x00001303,         // (0x103 | ActionFlag.Stationary | ActionFlag.Intangible)
        FallAfterStarGrab = 0x00001904,      // (0x104 | ActionFlag.Air | ActionFlag.Intangible)
        ReadingAutomaticDialog = 0x20001305, // (0x105 | ActionFlag.Stationary | ActionFlag.Intangible | ActionFlag.WaterOrText)
        ReadingNpcDialog = 0x20001306,       // (0x106 | ActionFlag.Stationary | ActionFlag.Intangible | ActionFlag.WaterOrText)
        StarDanceNoExit = 0x00001307,        // (0x107 | ActionFlag.Stationary | ActionFlag.Intangible)
        ReadingSign = 0x00001308,            // (0x108 | ActionFlag.Stationary | ActionFlag.Intangible)
        JumboStarCutscene = 0x00001909,      // (0x109 | ActionFlag.Air | ActionFlag.Intangible)
        WaitingForDialog = 0x0000130A,       // (0x10A | ActionFlag.Stationary | ActionFlag.Intangible)
        DebugFreeMove = 0x0000130F,          // (0x10F | ActionFlag.Stationary | ActionFlag.Intangible)
        StandingDeath = 0x00021311,          // (0x111 | ActionFlag.Stationary | ActionFlag.Intangible | ActionFlag.Invulnerable)
        QuicksandDeath = 0x00021312,         // (0x112 | ActionFlag.Stationary | ActionFlag.Intangible | ActionFlag.Invulnerable)
        Electrocution = 0x00021313,          // (0x113 | ActionFlag.Stationary | ActionFlag.Intangible | ActionFlag.Invulnerable)
        Suffocation = 0x00021314,            // (0x114 | ActionFlag.Stationary | ActionFlag.Intangible | ActionFlag.Invulnerable)
        DeathOnStomach = 0x00021315,         // (0x115 | ActionFlag.Stationary | ActionFlag.Intangible | ActionFlag.Invulnerable)
        DeathOnBack = 0x00021316,            // (0x116 | ActionFlag.Stationary | ActionFlag.Intangible | ActionFlag.Invulnerable)
        EatenByBubba = 0x00021317,           // (0x117 | ActionFlag.Stationary | ActionFlag.Intangible | ActionFlag.Invulnerable)
        EndPeachCutscene = 0x00001918,       // (0x118 | ActionFlag.Air | ActionFlag.Intangible)
        CreditsCutscene = 0x00001319,        // (0x119 | ActionFlag.Stationary | ActionFlag.Intangible)
        EndWavingCutscene = 0x0000131A,      // (0x11A | ActionFlag.Stationary | ActionFlag.Intangible)
        PullingDoor = 0x00001320,            // (0x120 | ActionFlag.Stationary | ActionFlag.Intangible)
        PushingDoor = 0x00001321,            // (0x121 | ActionFlag.Stationary | ActionFlag.Intangible)
        WarpDoorSpawn = 0x00001322,          // (0x122 | ActionFlag.Stationary | ActionFlag.Intangible)
        EmergeFromPipe = 0x00001923,         // (0x123 | ActionFlag.Air | ActionFlag.Intangible)
        SpawnSpinAirborne = 0x00001924,      // (0x124 | ActionFlag.Air | ActionFlag.Intangible)
        SpawnSpinLanding = 0x00001325,       // (0x125 | ActionFlag.Stationary | ActionFlag.Intangible)
        ExitAirborne = 0x00001926,           // (0x126 | ActionFlag.Air | ActionFlag.Intangible)
        ExitLandSaveDialog = 0x00001327,     // (0x127 | ActionFlag.Stationary | ActionFlag.Intangible)
        DeathExit = 0x00001928,              // (0x128 | ActionFlag.Air | ActionFlag.Intangible)
        UnusedDeathExit = 0x00001929,        // (0x129 | ActionFlag.Air | ActionFlag.Intangible)
        FallingDeathExit = 0x0000192A,       // (0x12A | ActionFlag.Air | ActionFlag.Intangible)
        SpecialExitAirborne = 0x0000192B,    // (0x12B | ActionFlag.Air | ActionFlag.Intangible)
        SpecialDeathExit = 0x0000192C,       // (0x12C | ActionFlag.Air | ActionFlag.Intangible)
        FallingExitAirborne = 0x0000192D,    // (0x12D | ActionFlag.Air | ActionFlag.Intangible)
        UnlockingKeyDoor = 0x0000132E,       // (0x12E | ActionFlag.Stationary | ActionFlag.Intangible)
        UnlockingStarDoor = 0x0000132F,      // (0x12F | ActionFlag.Stationary | ActionFlag.Intangible)
        EnteringStarDoor = 0x00001331,       // (0x131 | ActionFlag.Stationary | ActionFlag.Intangible)
        SpawnNoSpinAirborne = 0x00001932,    // (0x132 | ActionFlag.Air | ActionFlag.Intangible)
        SpawnNoSpinLanding = 0x00001333,     // (0x133 | ActionFlag.Stationary | ActionFlag.Intangible)
        BbhEnterJump = 0x00001934,           // (0x134 | ActionFlag.Air | ActionFlag.Intangible)
        BbhEnterSpin = 0x00001535,           // (0x135 | ActionFlag.Moving | ActionFlag.Intangible)
        TeleportFadeOut = 0x00001336,        // (0x136 | ActionFlag.Stationary | ActionFlag.Intangible)
        TeleportFadeIn = 0x00001337,         // (0x137 | ActionFlag.Stationary | ActionFlag.Intangible)
        Shocked = 0x00020338,                // (0x138 | ActionFlag.Stationary | ActionFlag.Invulnerable)
        Squished = 0x00020339,               // (0x139 | ActionFlag.Stationary | ActionFlag.Invulnerable)
        HeadStuckInGround = 0x0002033A,      // (0x13A | ActionFlag.Stationary | ActionFlag.Invulnerable)
        ButtStuckInGround = 0x0002033B,      // (0x13B | ActionFlag.Stationary | ActionFlag.Invulnerable)
        FeetStuckInGround = 0x0002033C,      // (0x13C | ActionFlag.Stationary | ActionFlag.Invulnerable)
        PuttingOnCap = 0x0000133D,           // (0x13D | ActionFlag.Stationary | ActionFlag.Intangible)

        // group 0x140: "automatic" actions
        HoldingPole = 0x08100340,         // (0x140 | ActionFlag.Stationary | ActionFlag.OnPole | ActionFlag.PauseExit)
        GrabPoleSlow = 0x00100341,        // (0x141 | ActionFlag.Stationary | ActionFlag.OnPole)
        GrabPoleFast = 0x00100342,        // (0x142 | ActionFlag.Stationary | ActionFlag.OnPole)
        ClimbingPole = 0x00100343,        // (0x143 | ActionFlag.Stationary | ActionFlag.OnPole)
        TopOfPoleTransition = 0x00100344, // (0x144 | ActionFlag.Stationary | ActionFlag.OnPole)
        TopOfPole = 0x00100345,           // (0x145 | ActionFlag.Stationary | ActionFlag.OnPole)
        StartHanging = 0x08200348,        // (0x148 | ActionFlag.Stationary | ActionFlag.Hanging | ActionFlag.PauseExit)
        Hanging = 0x00200349,             // (0x149 | ActionFlag.Stationary | ActionFlag.Hanging)
        HangMoving = 0x0020054A,          // (0x14A | ActionFlag.Moving | ActionFlag.Hanging)
        LedgeGrab = 0x0800034B,           // (0x14B | ActionFlag.Stationary | ActionFlag.PauseExit)
        LedgeClimbSlow1 = 0x0000054C,     // (0x14C | ActionFlag.Moving)
        LedgeClimbSlow2 = 0x0000054D,     // (0x14D | ActionFlag.Moving)
        LedgeClimbDown = 0x0000054E,      // (0x14E | ActionFlag.Moving)
        LedgeClimbFast = 0x0000054F,      // (0x14F | ActionFlag.Moving)
        Grabbed = 0x00020370,             // (0x170 | ActionFlag.Stationary | ActionFlag.Invulnerable)
        InCannon = 0x00001371,            // (0x171 | ActionFlag.Stationary | ActionFlag.Intangible)
        TornadoTwirling = 0x10020372,     // (0x172 | ActionFlag.Stationary | ActionFlag.Invulnerable | ActionFlag.SwimmingOrFlying)

        // group 0x180: object actions
        Punching = 0x00800380,         // (0x180 | ActionFlag.Stationary | ActionFlag.Attacking)
        PickingUp = 0x00000383,        // (0x183 | ActionFlag.Stationary)
        DivePickingUp = 0x00000385,    // (0x185 | ActionFlag.Stationary)
        StomachSlideStop = 0x00000386, // (0x186 | ActionFlag.Stationary)
        PlacingDown = 0x00000387,      // (0x187 | ActionFlag.Stationary)
        Throwing = 0x80000588,         // (0x188 | ActionFlag.Moving | ActionFlag.Throwing)
        HeavyThrow = 0x80000589,       // (0x189 | ActionFlag.Moving | ActionFlag.Throwing)
        PickingUpBowser = 0x00000390,  // (0x190 | ActionFlag.Stationary)
        HoldingBowser = 0x00000391,    // (0x191 | ActionFlag.Stationary)
        ReleasingBowser = 0x00000392   // (0x192 | ActionFlag.Stationary)
    }
}