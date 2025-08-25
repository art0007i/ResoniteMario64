# ResoniteMario64

A [BepisLoader](https://github.com/ResoniteModding/BepisLoader) mod
for [Resonite](https://resonite.com/) that allows you to spawn in and control mario from Super Mario 64.

Made possible by [libsm64](https://github.com/libsm64/libsm64).
I also used [CVRSuperMario64](https://github.com/kafeijao/Kafe_CVR_Mods/tree/master/CVRSuperMario64) as a reference for
creating this.

### Public Folder

- resrec:///U-NepuShiro/R-065B56789441685C8048321B0F9D5AAA43F9624971A9223CD6B5D4FE311FB1E2

### Castle Grounds World

- resrec:///U-NepuShiro/R-d4f4561f-8083-43cc-9cfc-f94bf8fc17ef

## Installation

1. Install [BepisLoader](https://github.com/ResoniteModding/BepisLoader).
2. Place [ResoniteMario64.dll](https://github.com/art0007i/ResoniteMario64/releases/latest/download/ResoniteMario64.dll)
   into your `BepInEx\plugins` folder. This folder should be at
   `C:\Program Files (x86)\Steam\steamapps\common\Resonite\BepInEx\plugins` for a default install. You can create it if it's
   missing, or if you launch the game once with BepisLoader installed it will create the folder for you.
3. Find `Super Mario 64 [US].z64` with hash `20b854b239203baf6c961b850a4a51a2` rename it to `baserom.us.z64` then place
      it next to the `ResoniteMario64.dll`.
4. Start the game. If you want to verify that the mod is working you can check your Resonite logs.

## Available Collider Types

Below are the types of colliders you can give to an object

<details>
  <summary>Click to Expand</summary>

### StaticCollider

- Tag: `"SM64 StaticCollider"` or `"SM64 Collider"`
- Collider must be enabled and active.
- Collider can be one of: Type `Static`, Type `Active`, or `CharacterCollider == true`

---

### DynamicCollider

- Tag: `"SM64 DynamicCollider"`
- Collider must be enabled and active.
- Collider type can **not** be a trigger.

---

### Interactable

- Tag: `"SM64 Interactable"`
- Collider must be enabled.

---

### WaterBox

- Tag: `"SM64 WaterBox"`
- Collider must be enabled and active.

---

### Teleporter

- Soon™️

---

</details>

## Available Collider Enums

Below are the enums you can use as strings, along with a brief description of what each enum represents.

These are used in the Tag Field on the Slot of which contains a Valid Collider. Formatted like so `SurfaceType_Grass,TerrainType_Default,Force_16.0` or `InteractableType_GoldCoin`

<details>
  <summary>Click to Expand</summary>

### TerrainType_(Enum)

Defines the type of terrain for collision and environmental behavior.

| Enum Name | Description                             |
|-----------|-----------------------------------------|
| Grass     | Standard grassy terrain                 |
| Stone     | Rocky or stone terrain                  |
| Snow      | Snow-covered terrain                    |
| Sand      | Sandy terrain                           |
| Spooky    | Wood terrain                            |
| Water     | Water surfaces                          |
| Slide     | Slide surfaces (icy or slippery slopes) |

---

### SurfaceType_(Enum)

Specifies various surface properties affecting player interaction, camera behavior, and environmental effects.

| Enum Name              | Description / Usage                           |
|------------------------|-----------------------------------------------|
| Default                | Normal environment surface                    |
| Burning                | Lava or damaging hot surface                  |
| Hangable               | Ceiling surfaces that can be climbed          |
| Slow                   | Surfaces that slow Mario down (unused)        |
| DeathPlane             | Instant death floor                           |
| CloseCamera            | Areas that force close camera behavior        |
| Water                  | Water surfaces (non-flowing)                  |
| FlowingWater           | Flowing water surfaces                        |
| Intangible             | Non-solid, intangible surfaces                |
| VerySlippery           | Very slippery surfaces, like slides           |
| Slippery               | Slippery surfaces                             |
| NotSlippery            | Non-slippery, climbable surfaces              |
| TtmVines               | Vines in Tall, Tall Mountain                  |
| MgrMusic               | Triggers Merry-Go-Round music                 |
| ShallowQuicksand       | Shallow quicksand                             |
| DeepQuicksand          | Deep quicksand (lethal)                       |
| InstantQuicksand       | Instant death quicksand                       |
| DeepMovingQuicksand    | Flowing deep quicksand                        |
| ShallowMovingQuicksand | Flowing shallow quicksand                     |
| Quicksand              | Moving quicksand                              |
| MovingQuicksand        | Flowing quicksand                             |
| WallMisc               | Walls, camera adjusters, warp pipes           |
| NoiseDefault           | Floor with noise texture                      |
| NoiseSlippery          | Slippery floor with noise                     |
| HorizontalWind         | Surfaces with horizontal wind effects         |
| InstantMovingQuicksand | Flowing instant death quicksand               |
| Ice                    | Slippery ice surfaces                         |
| Hard                   | Hard floor that causes fall damage            |
| TimerStart             | Timer start area (Peach’s secret slide)       |
| TimerEnd               | Timer end area (Peach’s secret slide)         |
| HardSlippery           | Hard and slippery floor                       |
| HardVerySlippery       | Hard and very slippery floor                  |
| HardNotSlippery        | Hard and non-slippery floor                   |
| VerticalWind           | Areas with vertical wind and death below      |
| BossFightCamera        | Wide camera for boss fights                   |
| CameraFreeRoam         | Free roam camera surfaces                     |
| Thi3Wallkick           | Surface for wall kicks in Tall, Tall Mountain |
| Camera8Dir             | Surfaces enabling far camera                  |
| CameraMiddle           | Camera returns to middle position             |
| CameraRotateRight      | Camera rotates right                          |
| CameraRotateLeft       | Camera rotates left                           |
| CameraBoundary         | Limits camera movement                        |
| NoiseVerySlippery73    | Unused very slippery floor with noise         |
| NoiseVerySlippery74    | Unused very slippery floor with noise         |
| NoiseVerySlippery      | Very slippery floor with noise                |
| NoCamCollision         | Surface with no camera collision              |
| NoCamCollision77       | Unused no camera collision surface            |
| NoCamColVerySlippery   | No cam collision, very slippery with noise    |
| NoCamColSlippery       | No cam collision, slippery with noise         |
| Switch                 | Surface for switches and Dorrie               |
| VanishCapWalls         | Walls passable only with Vanish Cap           |
| Trapdoor               | Bowser’s trapdoor surface                     |

---

### InteractableType_(Enum)

Represents different interactable objects and items within the game.

| Enum Name | Description                 |
|-----------|-----------------------------|
| None      | No interactable             |
| GoldCoin  | Standard coin               |
| RedCoin   | Red coin                    |
| BlueCoin  | Blue coin                   |
| Star      | Power star                  |
| NormalCap | Normal Mario cap            |
| VanishCap | Vanish Cap                  |
| MetalCap  | Metal Cap                   |
| WingCap   | Wing Cap                    |
| Damage    | Damage-causing interactable |

You can extend these by appending a number to represent different variations or specific events/damage types. For
example:

- `Damage0`, `Damage1`, `Damage2`, etc., can represent different damage strengths.
- `RedCoin0`, `RedCoin1`, `RedCoin2`, etc., can play the different red coin sounds.

---

### Force_(speed.angle)

You can specify forces applied from colliders:

- **speed**: The magnitude of the force applied (0–255).
- **angle**: The direction of the force encoded as an 8-bit value (0–255), representing an angle in degrees scaled to 256 units per full rotation (360°).


| Speed | Angle | Description                              | Encoded force (hex) |
|-------|-------|------------------------------------------|---------------------|
| 16    | 0     | Low force forward (0°)                   | 0x1000              |
| 64    | 64    | Medium force to the right (~90°)         | 0x4040              |
| 128   | 128   | Strong force backward (~180°)            | 0x8080              |
| 255   | 255   | Maximum force nearly full circle (~359°) | 0xFFFF              |

---

</details>

## Available World Variables

Below are enums that you can use to play music when spawning a mario, or set specific global values for the World

<details>
<summary>Click to Expand</summary>

### SM64Music

Enumerates music sequences used for different game events, levels, menus, and cutscenes. Variations represent alternate
versions of the same music sequence.

| Enum Name                | Variation                         | Description / Usage Location                   |
|--------------------------|-----------------------------------|------------------------------------------------|
| SoundPlayer              | SoundPlayerVariation              | Basic sound player                             |
| EventCutsceneCollectStar | EventCutsceneCollectStarVariation | Star collection cutscene                       |
| MenuTitleScreen          | MenuTitleScreenVariation          | Title screen menu music                        |
| LevelGrass               | LevelGrassVariation               | Bob-omb Battlefield and similar grassy levels  |
| LevelInsideCastle        | LevelInsideCastleVariation        | Inside Peach's Castle                          |
| LevelWater               | LevelWaterVariation               | Water-themed levels                            |
| LevelHot                 | LevelHotVariation                 | Hot/Lava levels like Lethal Lava Land          |
| LevelBossKoopa           | LevelBossKoopaVariation           | Koopa boss fights                              |
| LevelSnow                | LevelSnowVariation                | Snow levels like Cool, Cool Mountain           |
| LevelSlide               | LevelSlideVariation               | Slide levels (e.g., Cool, Cool Mountain slide) |
| LevelSpooky              | LevelSpookyVariation              | Spooky levels like Big Boo's Haunt             |
| EventPiranhaPlant        | EventPiranhaPlantVariation        | Piranha Plant events                           |
| LevelUnderground         | LevelUndergroundVariation         | Underground levels                             |
| MenuStarSelect           | MenuStarSelectVariation           | Star selection screen                          |
| EventPowerup             | EventPowerupVariation             | Power-up collection music                      |
| EventMetalCap            | EventMetalCapVariation            | Metal Cap music                                |
| EventKoopaMessage        | EventKoopaMessageVariation        | Koopa messages                                 |
| LevelKoopaRoad           | LevelKoopaRoadVariation           | Koopa Road level                               |
| EventHighScore           | EventHighScoreVariation           | High score music                               |
| EventMerryGoRound        | EventMerryGoRoundVariation        | Merry-Go-Round event                           |
| EventRace                | EventRaceVariation                | Racing events                                  |
| EventCutsceneStarSpawn   | EventCutsceneStarSpawnVariation   | Star spawn cutscene                            |
| EventBoss                | EventBossVariation                | Boss battle music                              |
| EventCutsceneCollectKey  | EventCutsceneCollectKeyVariation  | Key collection cutscene                        |
| EventEndlessStairs       | EventEndlessStairsVariation       | Endless stairs area                            |
| LevelBossKoopaFinal      | LevelBossKoopaFinalVariation      | Final Koopa boss battle                        |
| EventCutsceneCredits     | EventCutsceneCreditsVariation     | End credits music                              |
| EventSolvePuzzle         | EventSolvePuzzleVariation         | Puzzle solving events                          |
| EventToadMessage         | EventToadMessageVariation         | Toad message scenes                            |
| EventPeachMessage        | EventPeachMessageVariation        | Peach message scenes                           |
| EventCutsceneIntro       | EventCutsceneIntroVariation       | Intro cutscene                                 |
| EventCutsceneVictory     | EventCutsceneVictoryVariation     | Victory cutscene                               |
| EventCutsceneEnding      | EventCutsceneEndingVariation      | Ending cutscene                                |
| MenuFileSelect           | MenuFileSelectVariation           | File select screen                             |
| EventCutsceneLakitu      | EventCutsceneLakituVariation      | Lakitu cutscene                                |
| None                     |                                   | No music / default                             |

---

### Scale

The global scale factor applied to all Mario characters when they first spawn in the world. This controls how large or small every player's Mario appears initially.

---

### WaterLevel

The global water height level within the world. This determines the vertical position of the water surface that affects gameplay, such as swimming or drowning mechanics.

---

### GasLevel

The global gas height level within the world. This represents the vertical position of a gas layer that may affect the player, such as causing damage or impairing movement if entered.

---
</details>