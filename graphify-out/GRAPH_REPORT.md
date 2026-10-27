# Graph Report - CarsGame  (2026-10-27)

## Targeted update — 27 October 2026

Only 12 arena-related C# files, ArenaMode implementation semantics and the appended October 27 journal evidence section were refreshed. Existing journal nodes/edges are preserved. The rest of the existing corpus and all prior node IDs are retained; this is not a full-repository freshness claim. The prototype implements normal bridge bombs, a 240 m basin and existing kill scoring. Nuclear events, five-share scoring, Ugar, raids and traps remain design targets. The documented native checks passed: PlayMode 2/2 and EditMode 51/51, with exact source-play3/source-edit2 SIM manifests; localization imported 494 keys × 9 languages and CJK746 with no missing characters. Visual review found and repaired uninitialized MissionArt materials; PlayMode 2/2 and EditMode 51/51 were repeated after that fix. The final visual-only mesa mesh correction subsequently passed desert-final.xml 1/1, with source-final recording 1187 runtime inputs matching the workspace and arena-overview.png reviewed without magenta materials. The 2/2 and 51/51 results precede that mesh-only correction. No WebGL, physical-device or real SDK acceptance is claimed.

The report token counter covers external extraction calls (none); host-agent semantic reasoning tokens were not separately metered. Existing community IDs are retained to avoid unrelated graph churn. HTML uses the aggregated community view because the graph exceeds 5,000 nodes.

## Corpus Check
- 472 files · ~432,593 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 6819 nodes · 13167 edges · 422 communities (320 shown, 102 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 1046 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `d4deb895`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- CarLivery
- BodyDef
- CarWeapons
- WastelandTrack
- PlatformGlue
- DebugBus
- .BodyRow
- HudScreen
- CarController
- .Spawn
- MeshChunkWriter
- MenuScreens
- MissionDirector
- YandexNative
- .BakeProps
- План рефакторинга CarsGame
- GhostRecorder
- StarRingTrack
- BossCrawler
- CarCannon
- DesertMap
- cars-platform.js
- com.unity.modules.unitywebrequest
- HunyuanSampleCarBaker
- StandalonePlatform
- .ResetAll
- DailyRace
- ChasmLayout
- RaceParticipant
- KillFeedBuffer
- AiGunner
- .FireVoice
- PlatformRuntime
- dependencies
- MissionStrikes
- TrooperUnit
- .ApplyTo
- .Advance
- RaceManager
- com.unity.modules.jsonserialize
- .Build
- .Panel
- SpecialWeaponAttack
- NetProbe
- com.unity.test-framework
- TrackPath
- MissionHud
- EffectPool
- MenuCameraDirector
- GameFlow
- WorldHealthBars
- com.unity.render-pipelines.core
- MineFieldAndChargeTests
- MineVisual
- RacingGame.Tests
- RaceHarness
- conform_car.py
- UnityMusicOutput
- SkyMathTests
- PickupSpawner
- SpaceTurret
- ProgressData
- .BothDirectionsHaveDriveableShortcutValidLapsAndRepair
- MissionUnit
- MissionPlayTests
- RaceSpawner
- .FromJson
- CarBodyPlayTests
- com.unity.nuget.mono-cecil
- Continuing improvement session — 2026-10-23
- DailyChallenge
- ArenaDriver
- RacingGame
- EventRulesTests
- RaceProgress
- WeaponRig
- CarsGame: мидкор-слой. Черновик v1 (для адверсариальной проверки)
- Task
- .LoadAsync
- ArenaMode.md
- TrackSpline
- MusicDirector
- SkyDirector
- CarHealth
- UiButtonFx
- com.unity.modules.imgui
- com.unity.modules.animation
- PlayerEffects
- .CupSuggestsUnlockingTheNextRaceBeforeFarmingOldStars
- MinesPlayTests
- PlayerDriverInput
- RankEntry
- .Build
- MissionDriver
- PlatformContractsTests
- TwistPlayTests
- make_placeholder_music.py
- RaceDirector
- EditorMockPlatform
- PerfectStart
- AiCorneringPlayTests
- RacerNamesPlayTests
- OilHazardPool
- Projectile
- AbyssTrack
- Итерации
- AiRecoveryPlayTests
- Мультиплеер
- Result
- MineField
- UniqueRivalNamesPlayTests
- Интерфейс — бриф для Figma AI
- DifficultyTier
- TrackProfile
- WebMusicOutput
- SfxPlayer
- SpeedometerGraphic
- .ApplyStatus
- MineHazard
- NameplatePlayTests
- com.unity.mathematics
- CarsGame — журнал выполнения плана
- CarStatus
- .StartPractice
- .GrantTrial
- TouchPreferencesPlayTests
- AiDriver
- CrashProbe
- .GuidanceMatchesDistinctCombatBehaviors
- .ApplyHit
- .Material
- 1. Хронология фактов
- SaveService
- WastelandLayout
- .SecondsToTicks
- RaceDirectorPoolTests
- CarPaint
- HotBombPlayTests
- com.unity.modules.audio
- com.unity.modules.imageconversion
- com.unity.modules.physics2d
- PlayerExperienceTests
- RacingGame.Core.Sim
- RaceEventDefinition
- OilHazardTests
- PropModelLibrary
- .LoadoutTouchControlsGoalsAndHelpFollowRaceRules
- ReadmeEditor
- CarsGame: мидкор-слой. Этапы и промпты для агента
- com.unity.modules.uielements
- make_sfx.py
- OnboardingHints
- .Parse
- DriveCommand
- StarRingLayout
- TrackFork
- ArsenalExpansionTests
- .BuildSleekSportsCar
- PlayerPrefsSaveStore
- CarPaintTests
- .EnterMenu
- RecoveryIntentPlayTests
- 2026-09-02 — M0 (частично), M1, M2, M3 (код)
- Линзы этого пакета
- .Range
- .Pick
- ISimulated
- ArenaModeTests
- RuntimeSystemsTests
- LapCounterTests
- PropModelPlayTests
- Линзы этого пакета
- Линзы этого пакета
- Линзы этого пакета
- Линзы этого пакета
- Линзы этого пакета
- SaveService.cs
- KillTally
- SfxSynth
- Pickup
- HunyuanCarPlayTests
- com.unity.modules.ui
- Unity Project Context
- hunyuan_shape.py
- CarsGame Claude context
- ReportTests
- Investigation around 23:30
- BarrierTools
- TouchControlsPresenter
- FakeOutput
- MusicPlayer
- SpeedFx
- MonoBehaviour
- CarDamageSmoke
- DriftCharge
- GameplayImprovementsTests
- dependencies
- 2026-09-16 — план вне мидкора (S0–S10), этапы S1–S7 написаны офлайн
- 2026-09-24 — шесть улучшений для игрока (сессии carsgame-e5/-da/-ec/-4c)
- com.unity.modules.ai
- com.unity.ugui
- SkyPresetFactory
- .Get
- RacingGame.Core.Combat
- GhostRecord
- PerfectStartPausePlayTests
- .ArenaSupportsSpawningPursuitAndUnoccupiedRecovery
- DriftPlayTests
- GrassReversePlayTests
- Режим «Арена»: скоростной бой на дюне
- Линзы этого пакета
- common.sh
- RacingGame.EditorTools
- EngineAudio
- PropLayout.cs
- SpecialWeaponTargetsPlayTests
- .Merge
- TrackSplineTests
- Арт-направление: «Ржавый карнавал»
- Миссия «Левиафан»: вшестером против крепости в пустыне
- 2026-09-23 — S8–S10 (без ИИ), звёзды, пакетный QA без моста (сессия carsgame-8d)
- Ревью CarsGame (билд WebGL, 2026-10-23)
- .Resolve
- WebZipPostprocessor
- .FillCubemap
- LocImporter
- GhostRecordTests
- GhostPlaybackPlayTests
- .Hostile
- .ToJson
- make_reference.py
- HitInfo
- PauseSettingsPlayTests
- .FailedSavingExplainsRecoveryWithoutInterruptingDrivingOrClearingUserPause
- CarCameraFollow
- CombatFeedbackPlayTests
- InputActionAssetTests
- Readme
- Weapons — visual brief for Figma
- hy_python.sh
- PauseReason
- .EveryRecordedBodyRetainsItsVisibleGeometryWithoutJoiningTheRace
- .RecordsCaptureAppliedTrialAndCompareOnlyMatchingConfigurations
- ResultStarsPlayTests
- AiRecoveryTests
- PropModelLibraryTests
- com.unity.profiling.core
- Contextual driving help — 2026-10-23
- Gameplay improvements — 2026-09-24
- play.mjs
- Карточка игры в консоли Яндекс Игр (черновик)
- make_prop_reference.py
- WallRideEntry
- RaceResultsTests
- SpeedometerTests
- BoostGatePlayTests
- .BridgeBoostWallsRamAndCollapseWorkInOwnEvent
- Bounded AI recovery investigation — 2026-10-23
- Player experience improvements
- Racer name consistency — 2026-10-23
- CG-RISK-003 — устройства, производительность и память
- com.unity.collab-proxy
- H
- CameraRecoveryPlayTests
- SfxVoicePicker
- BoostGate
- Arsenal usability — 2026-10-23
- Образец: машина из Hunyuan3D-2 по контракту MidcoreDesign §G (2026-09-24)
- Bomb HUD readability — 2026-10-23
- Mission HUD readability — 2026-10-23
- codex-gpt-6-astra-max.md
- cursor-grok-4.6.md
- opencode-big-pickle.md
- opencode-mimo-v2.5-free.md
- opencode-nemotron-3-ultra.md
- Rigidbody Vehicle Control
- Q: Проследи
- Q: Как продолжать развивать игру?
- com.unity.modules.accessibility
- com.unity.modules.umbra
- CameraFeedbackPlayTests
- Расширение арсенала: 40 новых видов
- Rival nameplate readability — 2026-10-23
- Distinct rival names — 2026-10-23
- GameFlow.cs
- art/README.md
- combat-help.mjs
- profile-reload.mjs
- Garage showcase and test drive
- Target Platforms
- comfy_server.sh
- bomb-readability.mjs
- finish-transition.mjs
- help-mobile.mjs
- mission-readability.mjs
- mobile-readability.mjs
- nameplates.mjs
- player-experience.mjs
- race.sh
- race-soak.mjs
- racer-names.mjs
- results-layout.mjs
- touch-driving.mjs
- com.unity.ai.assistant
- com.unity.ai.navigation
- com.unity.ide.rider
- com.unity.ide.visualstudio
- com.unity.inputsystem
- com.unity.modules.accessibility
- com.unity.modules.androidjni
- com.unity.modules.terrainphysics
- com.unity.modules.tilemap
- com.unity.modules.umbra
- com.unity.modules.unityanalytics
- com.unity.modules.unitywebrequesttexture
- com.unity.modules.unitywebrequestwww
- com.unity.modules.vectorgraphics
- com.unity.modules.vehicles
- com.unity.modules.video
- com.unity.modules.xr
- com.unity.multiplayer.center
- com.unity.pipeline
- com.unity.render-pipelines.universal
- com.unity.timeline
- com.unity.visualscripting
- scout.md
- com.unity.modules.ai
- com.unity.modules.animation
- com.unity.modules.assetbundle
- com.unity.modules.audio
- com.unity.modules.director
- com.unity.modules.imageconversion
- com.unity.modules.imgui
- com.unity.modules.jsonserialize
- com.unity.modules.particlesystem
- com.unity.modules.physics
- com.unity.modules.physics2d
- com.unity.modules.physicscore2d
- com.unity.modules.screencapture
- com.unity.modules.terrain
- com.unity.modules.ui
- com.unity.modules.uielements
- com.unity.modules.unitywebrequest
- com.unity.modules.unitywebrequestassetbundle
- com.unity.modules.unitywebrequestaudio
- com.unity.test-framework
- com.unity.ugui
- SFX_SOURCES.md
- build-web.sh
- loc-bake.sh
- music-check.mjs
- EventDiscoveryPlayTests
- LocalizationRecoveryPlayTests
- RewardPresentationPlayTests
- .AddPose
- LapCounter
- Loc
- ResourceLifetimePlayTests
- Iterative polish, 2026-10-24
- .VolumeControlsStayIndependentAndPreserveAnExplicitEightyPercent
- FlowPausePlayTests
- CameraImpulse
- RacingGame.Platform
- SaveMergeInvariantTests
- MissionResultsPlayTests
- .ApplyWeapons
- .MutingEffectsImmediatelySilencesAnOngoingSkid
- .InterruptedLanguageSwitchKeepsWorkingMenusAndCanRetry
- Проверки CarsGame
- GhostCar
- .GetSlot
- Envelope
- ghost-replay.mjs
- local-storage.mjs
- .LongFramesCannotCountLessRaceTimeThanThePhysicsTheyAdvance
- .SpeedAndCornerEffectsCanBeDisabledAndRestoredWithoutChangingShakePreference
- boot-network.mjs
- endurance.mjs
- locale-lifetime.mjs
- vitals-readability.mjs
- Ghost replay — 2026-10-25
- boot-recovery.mjs
- UiInteractionTests
- auth-save.mjs
- drift-feedback.mjs
- pause-boundaries.mjs
- pause-settings.mjs
- practice.mjs
- test-drive-layout.mjs
- test_sim_sync.py
- weapon-labels.mjs
- audio-duck.mjs
- audio-settings.mjs
- combat-feedback.mjs
- daily-rollover.mjs
- event-discovery.mjs
- garage-purchases.mjs
- hybrid-input.mjs
- platform-save.mjs
- record-readability.mjs
- result-stars.mjs
- reward-details.mjs
- safe-area.mjs
- save-recovery.mjs
- touch-launch.mjs
- touch-preferences.mjs
- Grandstand-2026-10-25/README.md
- body-matrix.sh
- edit-tests.sh
- locale-recovery.mjs
- play-tests.sh
- practice-matrix.sh
- visual-quality.mjs

## God Nodes (most connected - your core abstractions)
1. `RacingGame.Tests` - 131 edges
2. `RacingGame` - 127 edges
3. `CarController` - 119 edges
4. `HudScreen` - 111 edges
5. `GameFlow` - 87 edges
6. `MeshChunkWriter` - 68 edges
7. `MenuScreens` - 64 edges
8. `WastelandTrack` - 64 edges
9. `RaceManager` - 64 edges
10. `MenuScreens` - 61 edges

## Surprising Connections (you probably didn't know these)
- `ArenaDriver` --implements--> `Arena bots: bridge runs and blast escape`  [EXTRACTED]
  Assets/CarsGame/Game/AI/ArenaDriver.cs → Docs/AI/ArenaMode.md
- `Fast loops around the canyon (быстрые петли)` --conceptually_related_to--> `ArenaDriver`  [INFERRED]
  Docs/AI/ArenaMode.md → Assets/CarsGame/Game/AI/ArenaDriver.cs
- `MissionStrikes` --implements--> `Shared MissionStrikes warning presentation`  [EXTRACTED]
  Assets/CarsGame/Game/Mission/MissionStrikes.cs → Docs/AI/ArenaMode.md
- `Bridge bomb (бомба с моста)` --references--> `AbyssTrack`  [EXTRACTED]
  Docs/AI/ArenaMode.md → Assets/CarsGame/Game/Track/AbyssTrack.cs
- `Cloverleaf canyon dune map («клевер»)` --references--> `AbyssTrack`  [EXTRACTED]
  Docs/AI/ArenaMode.md → Assets/CarsGame/Game/Track/AbyssTrack.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Confirmed Unity Environment** — docs_ai_unityprojectcontext_unity_6000_5_9f1, docs_ai_unityprojectcontext_urp_17_5_0, docs_ai_unityprojectcontext_input_system_1_20_0 [EXTRACTED 1.00]
- **Match pressure escalation instead of shrinking zone** — docs_ai_arenamode_npc_plane_raids, docs_ai_arenamode_npc_turrets, docs_ai_arenamode_sandstorm_finale, docs_ai_arenamode_final_double_points [EXTRACTED 1.00]
- **Shared bomb system (bridge, nuke, air raids)** — docs_ai_arenamode_bridge_bomb, docs_ai_arenamode_nuke_event, docs_ai_arenamode_npc_plane_raids, docs_ai_arenamode_shadow_sectors [EXTRACTED 1.00]
- **Gameplay Architecture Patterns** — docs_ai_unityprojectcontext_scene_composition, docs_ai_unityprojectcontext_rigidbody_vehicle_control, docs_ai_unityprojectcontext_track_markings_route, docs_ai_unityprojectcontext_procedural_car_generation [EXTRACTED 1.00]
- **Ugar multiplier built on drift and speed FX** — docs_ai_arenamode_ugar_multiplier, assets_carsgame_core_sim_driftcharge_racinggame_core_sim_driftcharge, assets_carsgame_game_vehicle_carcontroller_racinggame_carcontroller_stepdrift, assets_carsgame_game_ui_speedfx_racinggame_speedfx [INFERRED 0.85]

## Communities (422 total, 102 thin omitted)

### Community 0 - "CarLivery"
Cohesion: 0.05
Nodes (37): Cosmetics, LiveryDef, LiveryPattern, PaintDef, UnlockKind, LiveryClipper, Region, PlayerCosmetics (+29 more)

### Community 1 - "BodyDef"
Cohesion: 0.06
Nodes (43): BodyDef, BodyKit, BodyShape, BodyStats, GaragePreview, BodyMesh, Slot, CarBody (+35 more)

### Community 2 - "CarWeapons"
Cohesion: 0.07
Nodes (13): CarWeapons, SecondaryWeaponType, CarWeaponsTests, bool, float, int, Rigidbody, CarStatus (+5 more)

### Community 3 - "WastelandTrack"
Cohesion: 0.10
Nodes (19): EdgePiece, Ring, WastelandTrack, bool, Color32, Dictionary, float, GameObject (+11 more)

### Community 4 - "PlatformGlue"
Cohesion: 0.10
Nodes (8): AdPacer, PlatformGlue, AdPacerTests, float, bool, Task, Test, RacingGame.Core.Ads

### Community 5 - "DebugBus"
Cohesion: 0.08
Nodes (32): Command, DebugBus, GhostView, InputSnapshot, Perf, ReplaySnapshot, ResourceCounts, Snapshot (+24 more)

### Community 6 - ".BodyRow"
Cohesion: 0.11
Nodes (11): BodyClass, CarBodies, CarBodiesTests, Func, IList, List, IList, RectTransform (+3 more)

### Community 7 - "HudScreen"
Cohesion: 0.04
Nodes (24): bool, Button, CanvasGroup, Color, float, Image, int, IReadOnlyList (+16 more)

### Community 8 - "CarController"
Cohesion: 0.05
Nodes (16): IRespawnPoseProvider, Quaternion, Vector3, AudioSource, bool, Collision, float, GameObject (+8 more)

### Community 9 - ".Spawn"
Cohesion: 0.17
Nodes (8): GhostSpawnTests, Color, GameObject, GameObject, Mesh, IEnumerator, UnityTearDown, UnityTest

### Community 10 - "MeshChunkWriter"
Cohesion: 0.06
Nodes (22): LowPoly, MeshChunkWriter, Color32, Matrix4x4, Vector2, Vector3, Color32, int (+14 more)

### Community 11 - "MenuScreens"
Cohesion: 0.04
Nodes (50): WeaponKind, ILeaderboards, MenuScreens, CosmeticCell, Action, Func, Task, Color (+42 more)

### Community 12 - "MissionDirector"
Cohesion: 0.08
Nodes (12): MissionDirector, Outcome, Random, Color, Dictionary, float, GameObject, int (+4 more)

### Community 13 - "YandexNative"
Cohesion: 0.12
Nodes (4): YandexNative, DllImport, RacingGame.Platform.Yandex, IntPtr

### Community 14 - ".BakeProps"
Cohesion: 0.09
Nodes (22): EnvironmentBaker, Chunk, PropBaker, SceneConsolidator, Action, GameObject, Material, MenuItem (+14 more)

### Community 15 - "План рефакторинга CarsGame"
Cohesion: 0.05
Nodes (39): 1. Цель и исходное состояние, 2. Приоритетные наблюдения, 3. Границы разделения, 4. SOLID, KISS и DRY в этом проекте, 5. Бизнес-требования и тесты, 6. DOTS / ECS: ограниченный эксперимент, 7. Порядок небольших изменений, 8. Границы обзора (+31 more)

### Community 16 - "GhostRecorder"
Cohesion: 0.22
Nodes (4): GhostRecorder, double, int, Transform

### Community 17 - "StarRingTrack"
Cohesion: 0.10
Nodes (14): Side, Ring, StarRingTrack, bool, Collider, Color32, float, GameObject (+6 more)

### Community 18 - "BossCrawler"
Cohesion: 0.11
Nodes (11): BossCrawler, BossRole, Dictionary, float, int, List, Material, Mesh (+3 more)

### Community 19 - "CarCannon"
Cohesion: 0.09
Nodes (18): CarCannon, PrimaryWeaponType, PrimaryWeaponCatalog, Profile, WeaponMode, Profile, bool, float (+10 more)

### Community 20 - "DesertMap"
Cohesion: 0.13
Nodes (11): DesertMap, Color32, float, int, IReadOnlyList, List, Material, Quaternion (+3 more)

### Community 21 - "cars-platform.js"
Cohesion: 0.14
Nodes (34): attach(), boot(), byteLength(), cmd(), debugState(), detectDevice(), drainOnce(), env() (+26 more)

### Community 22 - "com.unity.modules.unitywebrequest"
Cohesion: 0.06
Nodes (37): dependencies, depth, source, url, version, dependencies, depth, source (+29 more)

### Community 23 - "HunyuanSampleCarBaker"
Cohesion: 0.07
Nodes (21): CarArtModelPostprocessor, HunyuanSampleCarBaker, PropModelLibraryBaker, AssetPostprocessor, string, Component, Dictionary, GameObject (+13 more)

### Community 24 - "StandalonePlatform"
Cohesion: 0.09
Nodes (19): AdResult, DeviceKind, IAds, IPayments, IPlatformServices, LeaderboardEntry, PlatformFeature, PlatformKind (+11 more)

### Community 25 - ".ResetAll"
Cohesion: 0.05
Nodes (26): DriftReadabilityPlayTests, AntialiasingPreview, CameraBodySurvey, GhostPreview, TearDown, SetUp, float, GameFlow (+18 more)

### Community 26 - "DailyRace"
Cohesion: 0.08
Nodes (12): DailyRace, DailyRaceTests, Func, int, string, Func, RaceEventDefinition, SetUp (+4 more)

### Community 27 - "ChasmLayout"
Cohesion: 0.14
Nodes (13): ChasmLayout, CliffMeshGen, Matrix4x4, Vector2, Vector3, Action, bool, Color32 (+5 more)

### Community 28 - "RaceParticipant"
Cohesion: 0.09
Nodes (11): HotBombRace, RaceParticipant, bool, float, int, bool, Collision, float (+3 more)

### Community 29 - "KillFeedBuffer"
Cohesion: 0.11
Nodes (15): KillEvent, KillFeedBuffer, KillFeedEntry, KillFeedTests, int, bool, float, int (+7 more)

### Community 30 - "AiGunner"
Cohesion: 0.07
Nodes (20): AiGunner, BotAim, BotAimDecision, AiCombatAndInputTests, bool, float, int, bool (+12 more)

### Community 31 - ".FireVoice"
Cohesion: 0.17
Nodes (9): Voice, WeaponSounds, WeaponSoundsTests, AudioClip, float, string, Vector3, Test (+1 more)

### Community 32 - "PlatformRuntime"
Cohesion: 0.09
Nodes (14): Pending, PlatformRuntime, PlatformRuntimeTests, Dictionary, float, int, List, string (+6 more)

### Community 33 - "dependencies"
Cohesion: 0.07
Nodes (33): dependencies, depth, source, version, dependencies, depth, source, version (+25 more)

### Community 34 - "MissionStrikes"
Cohesion: 0.16
Nodes (10): float, GameObject, int, List, Transform, Vector3, Stack, Strike (+2 more)

### Community 35 - "TrooperUnit"
Cohesion: 0.09
Nodes (18): DroneUnit, GroundTurretUnit, MissionLayers, PlaneUnit, TrooperUnit, UnitBody, Action, List (+10 more)

### Community 36 - ".ApplyTo"
Cohesion: 0.08
Nodes (22): BuildCatalog, CarBuild, Economy, Effects, RacePayout, UpgradeCatalog, UpgradeKind, WeaponCatalog (+14 more)

### Community 37 - ".Advance"
Cohesion: 0.22
Nodes (5): RaceClock, RaceClockTests, SetUp, TearDown, Test

### Community 38 - "RaceManager"
Cohesion: 0.12
Nodes (9): RaceManager, RaceState, bool, Dictionary, double, float, int, IReadOnlyList (+1 more)

### Community 39 - "com.unity.modules.jsonserialize"
Cohesion: 0.06
Nodes (32): dependencies, depth, source, version, dependencies, depth, source, version (+24 more)

### Community 40 - ".Build"
Cohesion: 0.13
Nodes (13): PropNoise, TerrainMeshGen, TerrainSettings, EnvironmentGenTests, int, Color32, float, int (+5 more)

### Community 41 - ".Panel"
Cohesion: 0.15
Nodes (13): UiFactory, Transform, Canvas, Color, Dictionary, Image, RectTransform, Sprite (+5 more)

### Community 42 - "SpecialWeaponAttack"
Cohesion: 0.26
Nodes (6): SpecialWeaponAttack, float, GameObject, int, Profile, Vector3

### Community 43 - "NetProbe"
Cohesion: 0.09
Nodes (15): NetProbe, NetProtocol, RelayServer, bool, double, float, int, RuntimeInitializeOnLoadMethod (+7 more)

### Community 44 - "com.unity.test-framework"
Cohesion: 0.07
Nodes (31): depth, source, url, version, dependencies, depth, source, version (+23 more)

### Community 45 - "TrackPath"
Cohesion: 0.12
Nodes (16): LaneLure, TrackPath, StarRingPlayTests, bool, float, Func, Quaternion, Vector3 (+8 more)

### Community 46 - "MissionHud"
Cohesion: 0.11
Nodes (16): MissionHud, bool, Camera, Color, float, GameObject, HashSet, Image (+8 more)

### Community 47 - "EffectPool"
Cohesion: 0.12
Nodes (16): EffectPool, Flash, bool, Color, float, int, Light, List (+8 more)

### Community 48 - "MenuCameraDirector"
Cohesion: 0.15
Nodes (9): MenuCameraDirector, Shot, bool, Camera, float, int, Quaternion, Vector3 (+1 more)

### Community 49 - "GameFlow"
Cohesion: 0.04
Nodes (35): GameFlow, PracticeLesson, GhostSession, RaceOverlays, bool, Color, GameObject, IEnumerator (+27 more)

### Community 50 - "WorldHealthBars"
Cohesion: 0.11
Nodes (18): Entry, WorldHealthBars, bool, Canvas, Color, float, HashSet, Image (+10 more)

### Community 51 - "com.unity.render-pipelines.core"
Cohesion: 0.07
Nodes (29): depth, source, version, dependencies, depth, source, version, dependencies (+21 more)

### Community 52 - "MineFieldAndChargeTests"
Cohesion: 0.11
Nodes (14): FixedSite, MineFieldAndChargeTests, CarHealth, CarWeapons, GameObject, List, MineHazard, MineKind (+6 more)

### Community 53 - "MineVisual"
Cohesion: 0.08
Nodes (25): MineKind, MineSkin, MineSpawn, MineVisual, bool, float, GameObject, int (+17 more)

### Community 54 - "RacingGame.Tests"
Cohesion: 0.07
Nodes (6): AbyssTrackPlayTests, RacingGame.Core.Economy, RacingGame.Tests, RacingGame.Core.Ghost, RacingGame.Core.Platform, RacingGame.Core.Save

### Community 55 - "RaceHarness"
Cohesion: 0.08
Nodes (24): CarReport, RaceHarness, Report, WallImpactCounter, RaceMatrix, AiDriver, bool, Collision (+16 more)

### Community 56 - "conform_car.py"
Cohesion: 0.11
Nodes (15): apply_mod(), bbox(), color_layer(), decimate(), set_colors(), transform(), paint_faces(), fit() (+7 more)

### Community 57 - "UnityMusicOutput"
Cohesion: 0.13
Nodes (13): Channel, UnityMusicOutput, AudioClip, AudioSource, bool, Dictionary, float, HashSet (+5 more)

### Community 58 - "SkyMathTests"
Cohesion: 0.26
Nodes (4): SkyMathTests, Color, Test, Vector3

### Community 59 - "PickupSpawner"
Cohesion: 0.24
Nodes (5): PickupSpawner, bool, float, int, List

### Community 60 - "SpaceTurret"
Cohesion: 0.13
Nodes (12): Phase, SpaceTurret, float, GameObject, int, IReadOnlyList, LineRenderer, List (+4 more)

### Community 61 - "ProgressData"
Cohesion: 0.12
Nodes (19): RaceCup, DailyClaim, DailyData, DeviceLedger, EventBest, ProgressData, SaveData, SettingsData (+11 more)

### Community 62 - ".BothDirectionsHaveDriveableShortcutValidLapsAndRepair"
Cohesion: 0.13
Nodes (11): TrackForkPlayTests, Collider, float, IEnumerator, RaceManager, SetUp, TearDown, Timeout (+3 more)

### Community 63 - "MissionUnit"
Cohesion: 0.10
Nodes (11): MissionUnit, UnitKind, GameObject, bool, Color, float, GameObject, int (+3 more)

### Community 64 - "MissionPlayTests"
Cohesion: 0.10
Nodes (14): MissionPlayTests, Camera, Dictionary, float, GameObject, IEnumerator, List, LogType (+6 more)

### Community 65 - "RaceSpawner"
Cohesion: 0.07
Nodes (21): AiDriverProfile, RivalRole, IBattleSite, Color, float, string, GameObject, Quaternion (+13 more)

### Community 66 - ".FromJson"
Cohesion: 0.23
Nodes (5): MusicDirectorTests, MusicDirector, Test, TestCase, FakeOutput

### Community 67 - "CarBodyPlayTests"
Cohesion: 0.12
Nodes (17): CarBodyPlayTests, Run, bool, float, GameFlow, GameObject, IEnumerator, int (+9 more)

### Community 68 - "com.unity.nuget.mono-cecil"
Cohesion: 0.08
Nodes (26): dependencies, depth, source, url, version, dependencies, depth, source (+18 more)

### Community 69 - "Continuing improvement session — 2026-10-23"
Cohesion: 0.08
Nodes (25): Completed readability / recovery validation, Continuing improvement session — 2026-10-23, Current iteration: small-screen readability, Final regression underway, Findings from the first long browser run, Finish accounting completed, Finish transition completed, Follow-up: recommendations and repeatable journeys (+17 more)

### Community 70 - "DailyChallenge"
Cohesion: 0.07
Nodes (26): DailyChallenge, DailySpec, RaceTwist, DailyChallengeTests, DailyRolloverPlayTests, bool, float, ICollection (+18 more)

### Community 71 - "ArenaDriver"
Cohesion: 0.09
Nodes (9): float, float, List, Material, Pose, Quaternion, Vector3, ArenaDriver (+1 more)

### Community 72 - "RacingGame"
Cohesion: 0.05
Nodes (11): ChasmSettings, GhostVisualMesh, WastelandTests, SpaceTrackPlayTests, WastelandPlayTests, float, Dictionary, RacingGame.World.Gen (+3 more)

### Community 73 - "EventRulesTests"
Cohesion: 0.13
Nodes (12): EventRules, EventRulesTests, float, int, GameObject, List, RaceEventDefinition, RaceManager (+4 more)

### Community 74 - "RaceProgress"
Cohesion: 0.13
Nodes (10): RaceProgress, RaceProgressTests, float, int, GameObject, RaceProgress, SetUp, TearDown (+2 more)

### Community 75 - "WeaponRig"
Cohesion: 0.13
Nodes (14): WeaponRig, ArsenalPlayTests, float, int, LineRenderer, Profile, Transform, Vector3 (+6 more)

### Community 76 - "CarsGame: мидкор-слой. Черновик v1 (для адверсариальной проверки)"
Cohesion: 0.08
Nodes (24): 0. Факты о текущем коде (проверены по репозиторию 2026-09-10), 10. Прокачка, 11. Порядок работ и критерии, 12. Арт-пайплайн для новых машин (кратко), 13. Открытые вопросы для рецензентов, 1. Цели и ограничения, 2. Система статусов (фундамент), 3. Слоты оружия и ввод (+16 more)

### Community 77 - "Task"
Cohesion: 0.14
Nodes (5): IAsyncSessions, ICloudSave, IReadOnlyList, Task, RuntimeInitializeOnLoadMethod

### Community 78 - ".LoadAsync"
Cohesion: 0.17
Nodes (8): MockCloudSave, MockLocalStore, SaveServiceTests, bool, int, string, Task, Test

### Community 79 - "ArenaMode.md"
Cohesion: 0.01
Nodes (120): RaceRanking, EventMode, IComparer, ICarInputSource, AiDriverProfile, AiGunner, bool, int (+112 more)

### Community 80 - "TrackSpline"
Cohesion: 0.21
Nodes (9): TrackSpline, PropLayout, float, Vector3, Transform, IList, List, d (+1 more)

### Community 81 - "MusicDirector"
Cohesion: 0.13
Nodes (7): IMusicOutput, MusicDirector, FlowState, bool, float, int, string

### Community 82 - "SkyDirector"
Cohesion: 0.13
Nodes (13): SkyDirector, SkyPreset, Color, Cubemap, int, Light, Material, bool (+5 more)

### Community 83 - "CarHealth"
Cohesion: 0.11
Nodes (10): CarHealth, bool, Collision, Color, float, GameObject, int, MaterialPropertyBlock (+2 more)

### Community 84 - "UiButtonFx"
Cohesion: 0.07
Nodes (21): Press, TouchButton, UiButtonFx, int, List, PointerEventData, Touchscreen, bool (+13 more)

### Community 85 - "com.unity.modules.imgui"
Cohesion: 0.08
Nodes (24): dependencies, depth, source, version, dependencies, depth, source, url (+16 more)

### Community 86 - "com.unity.modules.animation"
Cohesion: 0.09
Nodes (24): dependencies, depth, source, version, dependencies, depth, source, version (+16 more)

### Community 87 - "PlayerEffects"
Cohesion: 0.15
Nodes (8): PlayerEffects, bool, Collision, EmissionModule, float, int, Light, ParticleSystem

### Community 88 - ".CupSuggestsUnlockingTheNextRaceBeforeFarmingOldStars"
Cohesion: 0.20
Nodes (8): CupProgressionPlayTests, Button, GameFlow, IEnumerator, string, TearDown, Timeout, UnityTest

### Community 89 - "MinesPlayTests"
Cohesion: 0.13
Nodes (14): MinesPlayTests, float, GameFlow, IEnumerator, int, MineHazard, RaceDirector, RaceEventDefinition (+6 more)

### Community 90 - "PlayerDriverInput"
Cohesion: 0.05
Nodes (27): EdgeLatch, PlayerDriverInput, ScriptedDriverInput, EdgeLatchTests, InputPausePlayTests, int, bool, float (+19 more)

### Community 91 - "RankEntry"
Cohesion: 0.22
Nodes (7): RankEntry, RaceRankingTests, bool, double, int, List, Test

### Community 92 - ".Build"
Cohesion: 0.32
Nodes (5): MountainRingGen, Color32, Mesh, Vector2, Vector3

### Community 93 - "MissionDriver"
Cohesion: 0.18
Nodes (6): MissionDriver, Vector3, float, int, Vector3, Side

### Community 95 - "TwistPlayTests"
Cohesion: 0.18
Nodes (12): TwistPlayTests, Button, Component, float, GameFlow, IEnumerator, RaceEventDefinition, RaceManager (+4 more)

### Community 96 - "make_placeholder_music.py"
Cohesion: 0.20
Nodes (21): circ_reverb(), env(), fft_filter(), hat(), kick(), main(), note_hz(), pad() (+13 more)

### Community 97 - "RaceDirector"
Cohesion: 0.12
Nodes (7): RaceDirector, GameObject, int, IReadOnlyList, List, Transform, Vector3

### Community 98 - "EditorMockPlatform"
Cohesion: 0.17
Nodes (8): EditorMockPlatform, bool, Dictionary, float, IReadOnlyList, Random, string, Task

### Community 99 - "PerfectStart"
Cohesion: 0.18
Nodes (5): PerfectStart, bool, Color, double, float

### Community 100 - "AiCorneringPlayTests"
Cohesion: 0.08
Nodes (24): AiCorneringPlayTests, GamepadNavigationPlayTests, float, GameFlow, IEnumerator, RaceEventDefinition, RaceManager, SetUp (+16 more)

### Community 101 - "RacerNamesPlayTests"
Cohesion: 0.13
Nodes (10): RacerNamesPlayTests, float, GameFlow, List, LogType, RaceEventDefinition, RaceManager, SetUp (+2 more)

### Community 102 - "OilHazardPool"
Cohesion: 0.15
Nodes (10): OilHazardPool, OilSlick, bool, float, GameObject, int, IReadOnlyList, Material (+2 more)

### Community 103 - "Projectile"
Cohesion: 0.12
Nodes (13): Projectile, bool, Collider, Collision, float, GameObject, int, List (+5 more)

### Community 104 - "AbyssTrack"
Cohesion: 0.10
Nodes (14): AbyssTrack, SpaceTrack, AbyssTrackTests, float, GameObject, IEnumerable, int, List (+6 more)

### Community 105 - "Итерации"
Cohesion: 0.05
Nodes (41): 1 — результаты гонки, 2 — повторяемое обучение, 3 — независимая громкость (в работе), 4 — условия рекордов (в работе), CarsGame — цикл улучшений 25 октября 2026, Итерации, Итерация 10 — настройки без выхода из гонки, Итерация 11 — позднее выравнивание в ускоряющей площадке (+33 more)

### Community 106 - "AiRecoveryPlayTests"
Cohesion: 0.10
Nodes (16): AiRecoveryPlayTests, Observation, AiDriver, bool, float, GameFlow, IEnumerator, int (+8 more)

### Community 107 - "Мультиплеер"
Cohesion: 0.10
Nodes (20): 1. Суть, 2.1. Реального мультиплеера в SDK нет, 2.2. CSP хостинга: сейчас только отчёт, 2.3. Правила, которые меняют дизайн сетевой игры, 2.4. Проверка игрока на своём сервере, 2.5. Прочее, 2. Что говорит Яндекс (проверено 2026-09-25), 3. Варианты (+12 more)

### Community 108 - "Result"
Cohesion: 0.15
Nodes (11): IServerClock, Result, ResultCategory, EntryList, YandexPlatform, Task, IReadOnlyList, PlatformRuntime (+3 more)

### Community 109 - "MineField"
Cohesion: 0.19
Nodes (9): float, int, List, Pose, Vector3, IMineFieldSite, MineField, Slot (+1 more)

### Community 110 - "UniqueRivalNamesPlayTests"
Cohesion: 0.11
Nodes (15): UniqueRivalNamesPlayTests, AiDriverProfile, DifficultyTier, float, GameFlow, IEnumerator, List, RaceEventDefinition (+7 more)

### Community 111 - "Интерфейс — бриф для Figma AI"
Cohesion: 0.10
Nodes (19): 10. Результаты, 11. HUD гонки (ПК), 12. Состояния HUD, 13. Пауза, 14. Мобильный HUD, 15. Арена CARNAGE, 16. Загрузочный экран, 17. Логотип, иконка, обложка (+11 more)

### Community 112 - "DifficultyTier"
Cohesion: 0.25
Nodes (6): DifficultyTier, RaceSetup, float, int, string, float

### Community 113 - "TrackProfile"
Cohesion: 0.19
Nodes (6): Settings, TrackProfile, TrackProfileTests, float, Vector3, Test

### Community 114 - "WebMusicOutput"
Cohesion: 0.20
Nodes (3): MusicFiles, WebMusicOutput, DllImport

### Community 115 - "SfxPlayer"
Cohesion: 0.20
Nodes (7): SfxPlayer, AudioClip, AudioSource, float, int, Vector3, AudioListener

### Community 116 - "SpeedometerGraphic"
Cohesion: 0.16
Nodes (12): SpeedometerGraphic, WeaponWheelGraphic, Color, float, Vector2, VertexHelper, bool, Color (+4 more)

### Community 117 - ".ApplyStatus"
Cohesion: 0.28
Nodes (5): CarStatusTests, CarStatus, GameObject, TearDown, Test

### Community 118 - "MineHazard"
Cohesion: 0.11
Nodes (14): MineHazard, MineHazardTests, bool, float, GameObject, int, CarController, CarHealth (+6 more)

### Community 119 - "NameplatePlayTests"
Cohesion: 0.14
Nodes (14): NameplatePlayTests, float, GameFlow, IEnumerator, List, RaceEventDefinition, Rect, RectTransform (+6 more)

### Community 120 - "com.unity.mathematics"
Cohesion: 0.11
Nodes (19): dependencies, depth, source, version, dependencies, depth, source, url (+11 more)

### Community 121 - "CarsGame — журнал выполнения плана"
Cohesion: 0.11
Nodes (19): 2026-09-10 — мидкор-план и адверсариальная проверка, 2026-09-18 — 2026-09-21 — работа вне журнала (восстановлено по коммитам), 2026-09-21 — проверка в редакторе (мост работает), 2026-09-24 — поворотная турель (сессия carsgame-7b), 2026-09-24 — смена основного оружия в гонке: 1 = пушка, 2 = пулемёт, 2026-09-24 — фикс ИИ и шпилька старт/финиш (сессия carsgame-35/-76), 2026-09-25 — звук, шаг 1: пул голосов SFX, 2026-09-25 — звук, шаг 2: музыка (+11 more)

### Community 122 - "CarStatus"
Cohesion: 0.12
Nodes (7): StatusEffect, StatusKind, CarStatus, float, int, float, int

### Community 123 - ".StartPractice"
Cohesion: 0.19
Nodes (15): PracticePlayTests, PracticeMatrix, Button, float, GameFlow, IEnumerator, MenuScreens, RaceManager (+7 more)

### Community 124 - ".GrantTrial"
Cohesion: 0.11
Nodes (15): RewardedOffers, RewardedOffersTests, GaragePlayTests, bool, SetUp, TearDown, Test, float (+7 more)

### Community 125 - "TouchPreferencesPlayTests"
Cohesion: 0.13
Nodes (16): TouchPreferencesPlayTests, BackgroundBehavior, Bounds, Button, EditorInputBehaviorInPlayMode, float, GameFlow, IEnumerator (+8 more)

### Community 126 - "AiDriver"
Cohesion: 0.13
Nodes (6): AiDriver, bool, float, int, RaycastHit, Vector3

### Community 127 - "CrashProbe"
Cohesion: 0.15
Nodes (10): CrashProbe, S, TurnTest, bool, Collision, float, int, Rigidbody (+2 more)

### Community 128 - ".GuidanceMatchesDistinctCombatBehaviors"
Cohesion: 0.17
Nodes (7): ArsenalGroup, WeaponGuide, WeaponGuideTests, ArsenalGroup, PrimaryWeaponType, Test, TestCase

### Community 129 - ".ApplyHit"
Cohesion: 0.16
Nodes (9): HitResult, CarHealthHitTests, bool, float, CarHealth, CarStatus, GameObject, TearDown (+1 more)

### Community 130 - ".Material"
Cohesion: 0.19
Nodes (9): WeaponArt, Color, Dictionary, Material, Mesh, PrimitiveType, Transform, Vector3 (+1 more)

### Community 131 - "1. Хронология фактов"
Cohesion: 0.11
Nodes (17): 1. Хронология фактов, 2. Таблица «действие → отклик», 3. Числа, замеченные в интерфейсе, 4. Непонятное / что пришлось угадывать (факты без оценки), 5. Сбои, ошибки, задержки, 6. Скриншоты (evidence/shots), Запуск 10 — телефон 812×375 (эмуляция, без включения тач-режима), Запуск 11 — английский интерфейс, бездействие на «Сложно», разбитие машины (+9 more)

### Community 132 - "SaveService"
Cohesion: 0.10
Nodes (17): ILocalSaveStore, SaveService, SaveCompatibilityPlayTests, Store, bool, double, float, Func (+9 more)

### Community 133 - "WastelandLayout"
Cohesion: 0.10
Nodes (11): Kind, Segment, WastelandLayout, Dictionary, float, int, List, string (+3 more)

### Community 134 - ".SecondsToTicks"
Cohesion: 0.18
Nodes (9): BossPart, Action, Collider, Func, GameObject, LineRenderer, Vector3, Material (+1 more)

### Community 135 - "RaceDirectorPoolTests"
Cohesion: 0.10
Nodes (13): RaceDirectorPoolTests, SetUp, SetUp, SetUp, SetUp, CarController, CarHealth, CarStatus (+5 more)

### Community 136 - "CarPaint"
Cohesion: 0.21
Nodes (7): CarPaint, Color, float, int, List, MaterialPropertyBlock, string

### Community 137 - "HotBombPlayTests"
Cohesion: 0.17
Nodes (11): HotBombPlayTests, float, GameFlow, IEnumerator, RaceEventDefinition, RaceManager, RaceParticipant, SetUp (+3 more)

### Community 138 - "com.unity.modules.audio"
Cohesion: 0.12
Nodes (17): dependencies, depth, source, version, dependencies, depth, source, version (+9 more)

### Community 139 - "com.unity.modules.imageconversion"
Cohesion: 0.12
Nodes (17): dependencies, depth, source, version, dependencies, depth, source, version (+9 more)

### Community 140 - "com.unity.modules.physics2d"
Cohesion: 0.12
Nodes (17): dependencies, depth, source, version, dependencies, depth, source, version (+9 more)

### Community 141 - "PlayerExperienceTests"
Cohesion: 0.13
Nodes (8): RaceCups, PlayerExperienceTests, RaceCupBoundaryTests, Test, TestCase, Test, TestCase, SkillGoal

### Community 142 - "RacingGame.Core.Sim"
Cohesion: 0.17
Nodes (5): RubberBand, RubberBandTests, float, Test, RacingGame.Core.Sim

### Community 143 - "RaceEventDefinition"
Cohesion: 0.08
Nodes (19): FinishGoal, RaceEventDefinition, SkillGoal, RaceResultEntry, RaceResults, StarGoalText, StarGoalsTests, bool (+11 more)

### Community 144 - "OilHazardTests"
Cohesion: 0.12
Nodes (10): OilHazardTests, SetUp, CarController, CarHealth, CarStatus, GameObject, RaceDirector, SetUp (+2 more)

### Community 145 - "PropModelLibrary"
Cohesion: 0.24
Nodes (10): Entry, PropModelLibrary, bool, Dictionary, int, List, Material, Mesh (+2 more)

### Community 146 - ".LoadoutTouchControlsGoalsAndHelpFollowRaceRules"
Cohesion: 0.13
Nodes (11): PlayerExperiencePlayTests, Button, float, GameFlow, IEnumerator, RaceEventDefinition, SetUp, string (+3 more)

### Community 147 - "ReadmeEditor"
Cohesion: 0.18
Nodes (7): ReadmeEditor, bool, float, string, Editor, GUIContent, GUIStyle

### Community 148 - "CarsGame: мидкор-слой. Этапы и промпты для агента"
Cohesion: 0.12
Nodes (15): CarsGame: мидкор-слой. Этапы и промпты для агента, После MVP (короткие промпты, по одному этапу каждый), Преамбула (вставлять в начало каждого промпта), Этап 0. Тики в CarHealth, единый результат попадания, потолки пулов (P0a, ~4 дня), Этап 10. Реальный SDK Яндекса и память WebGL (P4b, ~3 дня), Этап 11. Плейтест-резерв (5–7 дней, без нового контента), Этап 1. Замер базовой производительности WebGL (P0b, ~2 дня), Этап 2. Система статусов и щит (P1a, ~3 дня) (+7 more)

### Community 149 - "com.unity.modules.uielements"
Cohesion: 0.12
Nodes (16): dependencies, depth, source, url, version, depth, source, version (+8 more)

### Community 150 - "make_sfx.py"
Cohesion: 0.23
Nodes (15): build(), buzz(), fade(), fetch(), load(), lowpass(), main(), mix() (+7 more)

### Community 151 - "OnboardingHints"
Cohesion: 0.12
Nodes (11): Hint, HintTracker, OnboardingHints, HintTrackerTests, float, bool, Color, float (+3 more)

### Community 152 - ".Parse"
Cohesion: 0.32
Nodes (4): SaveMigrator, SaveParseStatus, SaveMigratorTests, Test

### Community 153 - "DriveCommand"
Cohesion: 0.09
Nodes (15): DriveCommand, ICarInputSource, OpponentDriver, DriveCommandTests, bool, float, int, float (+7 more)

### Community 154 - "StarRingLayout"
Cohesion: 0.10
Nodes (15): Kind, Segment, StarRingLayout, StarRingTests, Dictionary, float, List, string (+7 more)

### Community 155 - "TrackFork"
Cohesion: 0.12
Nodes (11): TrackFork, Collider, Dictionary, float, Func, int, List, Material (+3 more)

### Community 156 - "ArsenalExpansionTests"
Cohesion: 0.15
Nodes (9): ArsenalExpansionTests, CarHealth, CarStatus, GameObject, RaceDirector, SetUp, TearDown, Test (+1 more)

### Community 157 - ".BuildSleekSportsCar"
Cohesion: 0.16
Nodes (10): RaceCarPrefabBaker, ProceduralCarBuilder, MenuItem, string, Transform, GameObject, Material, Mesh (+2 more)

### Community 158 - "PlayerPrefsSaveStore"
Cohesion: 0.15
Nodes (10): PlayerPrefsSaveStore, ReadResult, PlayerPrefsSaveStoreTests, Action, bool, string, SetUp, string (+2 more)

### Community 159 - "CarPaintTests"
Cohesion: 0.29
Nodes (7): CarPaintTests, Color, GameObject, Material, MeshRenderer, Renderer, Test

### Community 160 - ".EnterMenu"
Cohesion: 0.09
Nodes (20): FinishTransitionPlayTests, Button, float, GameFlow, IEnumerator, RaceEventDefinition, SetUp, string (+12 more)

### Community 161 - "RecoveryIntentPlayTests"
Cohesion: 0.13
Nodes (11): RecoveryIntentPlayTests, float, GameFlow, IEnumerator, int, RaceEventDefinition, SetUp, string (+3 more)

### Community 162 - "2026-09-02 — M0 (частично), M1, M2, M3 (код)"
Cohesion: 0.13
Nodes (15): 2026-09-02/03 (продолжение) — M7–M9 сделаны, WebGL-модуль установлен, 2026-09-02 — M0 (частично), M1, M2, M3 (код), 2026-09-02 (вечер) — мост разблокировался сам, M1–M3 проверены в редакторе, M4 сделан, 2026-09-02 (ночь) — Codex-ревью M4 закрыто, M5 написан, Codex-ревью M5 закрыто (6 находок), Codex-ревью M6 закрыто (8 находок), Codex-ревью последних правок (10 находок) — применено, M5 проверен в Play-mode, M6 (бой) сделан (+7 more)

### Community 163 - "Линзы этого пакета"
Cohesion: 0.13
Nodes (14): L36 · Соревнование · Competition, L37 · Сотрудничество · Cooperation, L38 · Соревнование и сотрудничество · Competition vs Cooperation, L40 · Награда · Reward, L41 · Наказание · Punishment, L42 · Простота и сложность · Simplicity / Complexity, L43 · Элегантность · Elegance, L46 · Экономика · Economy (+6 more)

### Community 164 - ".Range"
Cohesion: 0.26
Nodes (6): RngStreams, Stream, RngStreamsTests, Random, Test, Stream

### Community 165 - ".Pick"
Cohesion: 0.31
Nodes (3): SfxVoicePickerTests, object, Test

### Community 166 - "ISimulated"
Cohesion: 0.15
Nodes (5): ISimulated, SimulationDriver, bool, float, List

### Community 168 - "RuntimeSystemsTests"
Cohesion: 0.24
Nodes (5): Probe, RuntimeSystemsTests, Action, Test, ISimulated

### Community 170 - "PropModelPlayTests"
Cohesion: 0.15
Nodes (9): PropModelPlayTests, GameObject, IEnumerator, PrimaryWeaponType, RenderTexture, SetUp, TearDown, Transform (+1 more)

### Community 171 - "Линзы этого пакета"
Cohesion: 0.14
Nodes (13): L01 · Главное переживание · Essential Experience, L02 · Неожиданность · Surprise, L03 · Веселье · Fun, L04 · Любопытство · Curiosity, L05 · Внутренняя ценность · Endogenous Value, L06 · Решение задач · Problem Solving, L15 · Игрушка · The Toy, L17 · Удовольствие · Pleasure (+5 more)

### Community 172 - "Линзы этого пакета"
Cohesion: 0.14
Nodes (13): L16 · Игрок · The Player, L18 · Поток · Flow, L19 · Потребности · Needs, L20 · Суждение · Judgment, L45 · Воображение · Imagination, L62 · Кривая интереса · Interest Curve, L63 · Врождённый интерес · Inherent Interest, L65 · Проекция · Projection (+5 more)

### Community 173 - "Линзы этого пакета"
Cohesion: 0.14
Nodes (13): L21 · Функциональное пространство · Functional Space, L22 · Динамическое состояние · Dynamic State, L23 · Эмерджентность · Emergence, L24 · Действие · Action, L25 · Цели · Goals, L26 · Правила · Rules, L27 · Навык · Skill, L39 · Время · Time (+5 more)

### Community 174 - "Линзы этого пакета"
Cohesion: 0.14
Nodes (13): L28 · Ожидаемая ценность · Expected Value, L29 · Случайность · Chance, L30 · Справедливость · Fairness, L31 · Вызов · Challenge, L32 · Осмысленный выбор · Meaningful Choices, L33 · Треугольник · Triangularity, L34 · Навык против случая · Skill vs Chance, L35 · Голова и руки · Head and Hands (+5 more)

### Community 175 - "Линзы этого пакета"
Cohesion: 0.14
Nodes (13): L53 · Управление · Control, L54 · Физический интерфейс · Physical Interface, L55 · Виртуальный интерфейс · Virtual Interface, L56 · Прозрачность · Transparency, L57 · Обратная связь · Feedback, L58 · Сочность · Juiciness, L59 · Каналы и измерения · Channels and Dimensions, L60 · Режимы · Modes (+5 more)

### Community 177 - "KillTally"
Cohesion: 0.26
Nodes (4): KillTally, KillTallyTests, int, Test

### Community 178 - "SfxSynth"
Cohesion: 0.28
Nodes (6): SfxSynth, AudioClip, Func, int, Random, string

### Community 179 - "Pickup"
Cohesion: 0.26
Nodes (6): Pickup, PickupKind, float, Material, Transform, Vector3

### Community 180 - "HunyuanCarPlayTests"
Cohesion: 0.23
Nodes (7): HunyuanCarPlayTests, float, IEnumerator, SetUp, TearDown, Timeout, UnityTest

### Community 181 - "com.unity.modules.ui"
Cohesion: 0.15
Nodes (13): dependencies, depth, source, version, dependencies, depth, source, version (+5 more)

### Community 182 - "Unity Project Context"
Cohesion: 0.17
Nodes (13): Unity AI Navigation Package, CarsGame Project, Single-Scene Driving and Combat Prototype, Unity Input System 1.20.0, Official Unity AI/MCP Package, Procedural Car Mesh Generation, Runtime and Play Mode Automation, Unity 6000.5.9f1 (+5 more)

### Community 183 - "hunyuan_shape.py"
Cohesion: 0.23
Nodes (11): denoise(), encode(), free_mib(), _hop_in(), _hop_out(), staged(), stream(), to() (+3 more)

### Community 184 - "CarsGame Claude context"
Cohesion: 0.27
Nodes (19): CG-RISK-007 — единое доказательство финальной интеграции, CG-RISK-009 — воспроизводимость окружения QA, CG-RISK-010 — достоверность EditMode gate, CG-RISK-006 — актуальность изолированной QA-копии, CG-RISK-001 — отпускание сенсорного управления, Assets/CarsGame/Game/Input/TouchButton.cs, Docs/AI/ImprovementCycle-2026-10-25.md, Tools/qa/freeze-source.py (+11 more)

### Community 185 - "ReportTests"
Cohesion: 0.18
Nodes (4): ReportTests, RunnerTests, result_xml(), Fast runner regression checks, independent of a Unity installation.

### Community 186 - "Investigation around 23:30"
Cohesion: 0.09
Nodes (21): 03:53 — физические материалы: отдельная подтверждённая утечка, 04:00 — candidate18 собран, 04:03 — дополнительное наложение в тест-драйве, 04:15 — поздний случай Wasteland в дополнительных200заездах, 04:22 — сборка20 готова, сравнение Wasteland продолжается, 04:32 — текущие проверки20, 04:51 — браузер20 и завершённая матрица Wasteland, 05:02 — сохранение естественного входа в wallride (+13 more)

### Community 187 - "BarrierTools"
Cohesion: 0.29
Nodes (3): BarrierTools, MenuItem, Vector2

### Community 188 - "TouchControlsPresenter"
Cohesion: 0.13
Nodes (10): TouchControlsPresenter, bool, Button, Canvas, float, RectTransform, string, TextMeshProUGUI (+2 more)

### Community 189 - "FakeOutput"
Cohesion: 0.10
Nodes (7): FakeOutput, Output, bool, float, List, int, IMusicOutput

### Community 191 - "SpeedFx"
Cohesion: 0.17
Nodes (8): SpeedFx, Canvas, Color, float, Image, int, RectTransform, TextMeshProUGUI

### Community 192 - "MonoBehaviour"
Cohesion: 0.11
Nodes (10): WorldAssets, GravitySurface, TargetCar, Material, bool, float, Quaternion, Rigidbody (+2 more)

### Community 193 - "CarDamageSmoke"
Cohesion: 0.19
Nodes (6): CarDamageSmoke, EmissionModule, float, GameObject, int, ParticleSystem

### Community 194 - "DriftCharge"
Cohesion: 0.19
Nodes (6): DriftCharge, DriftChargeTests, bool, float, float, Test

### Community 196 - "dependencies"
Cohesion: 0.17
Nodes (11): dependencies, com.unity.collab-proxy, com.unity.localization, com.unity.modules.adaptiveperformance, com.unity.modules.cloth, com.unity.modules.wind, com.unity.collab-proxy, com.unity.localization (+3 more)

### Community 197 - "2026-09-16 — план вне мидкора (S0–S10), этапы S1–S7 написаны офлайн"
Cohesion: 0.17
Nodes (12): 2026-09-16 — план вне мидкора (S0–S10), этапы S1–S7 написаны офлайн, Codex-ревью (три прохода, все находки разобраны), S10 (частично) — документы релиза, S1 — релиз-блокирующие баги (код готов, Codex-ревью пройдено), S2 — тач-HUD и прицел, S3 — профиль скоростей и гоночная линия (за флагом), S5 — обратная связь боя, S6 — камера и звук машины (+4 more)

### Community 198 - "2026-09-24 — шесть улучшений для игрока (сессии carsgame-e5/-da/-ec/-4c)"
Cohesion: 0.17
Nodes (12): 2026-09-24 — шесть улучшений для игрока (сессии carsgame-e5/-da/-ec/-4c), Rewarded, Адверсариальное ревью (Codex gpt-6-astra max) — 15 находок, исправлены все, Заезд дня, Коллекция, Контент: 15 событий вместо 5, Машина из Hunyuan3D-2 (контракт §G), Ощущение скорости (+4 more)

### Community 199 - "com.unity.modules.ai"
Cohesion: 0.17
Nodes (12): dependencies, depth, source, url, version, dependencies, depth, source (+4 more)

### Community 200 - "com.unity.ugui"
Cohesion: 0.17
Nodes (12): dependencies, depth, source, version, dependencies, depth, source, url (+4 more)

### Community 201 - "SkyPresetFactory"
Cohesion: 0.23
Nodes (6): SkyPresetFactory, Action, Color, MenuItem, string, StringBuilder

### Community 202 - ".Get"
Cohesion: 0.20
Nodes (6): SfxLibrary, Collision, AudioClip, Dictionary, RuntimeInitializeOnLoadMethod, string

### Community 203 - "RacingGame.Core.Combat"
Cohesion: 0.11
Nodes (9): ProjectileKind, ProjectileSpawn, float, GameObject, int, Material, Transform, Vector3 (+1 more)

### Community 204 - "GhostRecord"
Cohesion: 0.17
Nodes (8): GhostRecord, GhostRecordCodec, List, string, float, int, Sample, UTF8Encoding

### Community 205 - "PerfectStartPausePlayTests"
Cohesion: 0.15
Nodes (14): PerfectStartPausePlayTests, BackgroundBehavior, EditorInputBehaviorInPlayMode, float, GameFlow, IEnumerator, Keyboard, PerfectStart (+6 more)

### Community 206 - ".ArenaSupportsSpawningPursuitAndUnoccupiedRecovery"
Cohesion: 0.20
Nodes (7): CarController, IEnumerator, Transform, UnityTearDown, UnityTest, Vector3, ArenaTests

### Community 207 - "DriftPlayTests"
Cohesion: 0.18
Nodes (7): DriftPlayTests, float, IEnumerator, SetUp, TearDown, Timeout, UnityTest

### Community 208 - "GrassReversePlayTests"
Cohesion: 0.18
Nodes (7): GrassReversePlayTests, float, IEnumerator, SetUp, TearDown, Timeout, UnityTest

### Community 209 - "Режим «Арена»: скоростной бой на дюне"
Cohesion: 0.11
Nodes (17): 10. Порядок работ, 11. Отклонено и почему, 1. Суть, 2. Карта, 3. Очки, 4. «Угар» — множитель за безбашенную езду, 6. Давление по ходу матча (вместо сужения зоны), 7. Что уже есть в коде (+9 more)

### Community 210 - "Линзы этого пакета"
Cohesion: 0.18
Nodes (10): L48 · Доступность · Accessibility, L49 · Видимый прогресс · Visible Progress, L50 · Параллельность · Parallelism, L51 · Пирамида · The Pyramid, L52 · Головоломка · The Puzzle, Как работать, Линзы этого пакета, Объект ревью (+2 more)

### Community 211 - "common.sh"
Cohesion: 0.31
Nodes (7): ensure_sim(), PYTHONPATH, common.sh script, sync_meta(), XDG_CONFIG_HOME, run_suite(), tests.sh script

### Community 212 - "RacingGame.EditorTools"
Cohesion: 0.09
Nodes (11): GrandstandGeometry, RuntimeWiring, FontAtlasBaker, OpenGameSceneOnStartup, Color32, float, MenuItem, MenuItem (+3 more)

### Community 213 - "EngineAudio"
Cohesion: 0.24
Nodes (6): EngineAudio, AudioClip, AudioSource, bool, float, int

### Community 214 - "PropLayout.cs"
Cohesion: 0.46
Nodes (7): GrandstandPlacement, LampPlacement, TireStackPlacement, TreePlacement, float, int, Vector3

### Community 215 - "SpecialWeaponTargetsPlayTests"
Cohesion: 0.17
Nodes (13): SpecialWeaponTargetsPlayTests, CarController, float, GameFlow, GameObject, IEnumerator, RaceEventDefinition, RaceManager (+5 more)

### Community 216 - ".Merge"
Cohesion: 0.24
Nodes (4): DailyCosmeticsSaveTests, SaveMergeTests, Test, Test

### Community 218 - "Арт-направление: «Ржавый карнавал»"
Cohesion: 0.20
Nodes (9): 1. Рамка мира, 2. Границы содержания, 3. Палитра, 4. Формы, фактура, шрифт, 5. Машины, пилоты, ливреи, 6. Биомы и прогрессия, 7. Производственные ограничения, 8. Что это меняет в проекте (+1 more)

### Community 219 - "Миссия «Левиафан»: вшестером против крепости в пустыне"
Cohesion: 0.20
Nodes (9): 1. Суть, 2. Карта, 3. Босс «Ржавый Левиафан», 4. Охрана, 5. Союзники, 6. Технически, 7. Открытые вопросы, Замеры (MissionPlayTests, normal, бот на месте игрока, 2026-09-26) (+1 more)

### Community 220 - "2026-09-23 — S8–S10 (без ИИ), звёзды, пакетный QA без моста (сессия carsgame-8d)"
Cohesion: 0.20
Nodes (10): 2026-09-23 — S8–S10 (без ИИ), звёзды, пакетный QA без моста (сессия carsgame-8d), S10, S8 — первая сессия и результаты, S9 (без ИИ-части), Звёзды (работа 21.09, доведена), Инструменты (`Tools/qa/`, всё в пакетном Unity на копии проекта в `$CG_WORK`, по умолчанию `/tmp/cars-qa`), Окружение и координация, Проверки (+2 more)

### Community 221 - "Ревью CarsGame (билд WebGL, 2026-10-23)"
Cohesion: 0.20
Nodes (9): Главные находки, Общие замечания ревьюеров, По линзам, Ревью CarsGame (билд WebGL, 2026-10-23), Сводка по группам, Серьёзность: высокая (45), Серьёзность: низкая (66), Серьёзность: средняя (111) (+1 more)

### Community 222 - ".Resolve"
Cohesion: 0.19
Nodes (7): LanguageResolver, LanguageResolverTests, HashSet, ICollection, string, string, Test

### Community 223 - "WebZipPostprocessor"
Cohesion: 0.18
Nodes (7): BuildCommands, WebZipPostprocessor, BuildReport, MenuItem, BuildReport, long, IPostprocessBuildWithReport

### Community 224 - ".FillCubemap"
Cohesion: 0.15
Nodes (11): SkyMath, SkyParams, SkyReadabilityPreview, Color, Cubemap, float, Vector3, CubemapFace (+3 more)

### Community 225 - "LocImporter"
Cohesion: 0.29
Nodes (4): LocImporter, List, MenuItem, string

### Community 227 - "GhostPlaybackPlayTests"
Cohesion: 0.20
Nodes (11): GhostPlaybackPlayTests, float, GameFlow, GhostCar, IEnumerator, RaceManager, RaceParticipant, SetUp (+3 more)

### Community 228 - ".Hostile"
Cohesion: 0.22
Nodes (7): TargetFinder, Team, Teams, TeamTag, int, Vector3, GameObject

### Community 229 - ".ToJson"
Cohesion: 0.20
Nodes (6): RecordSetup, RecordSetupTests, bool, int, string, Test

### Community 231 - "HitInfo"
Cohesion: 0.25
Nodes (6): HitInfo, HitType, float, GameObject, int, Vector3

### Community 232 - "PauseSettingsPlayTests"
Cohesion: 0.12
Nodes (13): PauseSettingsPlayTests, BackgroundBehavior, Button, EditorInputBehaviorInPlayMode, float, GameFlow, IEnumerator, Keyboard (+5 more)

### Community 233 - ".FailedSavingExplainsRecoveryWithoutInterruptingDrivingOrClearingUserPause"
Cohesion: 0.19
Nodes (10): FailingStore, SaveRecoveryPlayTests, bool, Button, float, IEnumerator, string, Timeout (+2 more)

### Community 234 - "CarCameraFollow"
Cohesion: 0.22
Nodes (6): CarCameraFollow, Camera, float, Rigidbody, Transform, Vector3

### Community 235 - "CombatFeedbackPlayTests"
Cohesion: 0.13
Nodes (12): CombatFeedbackPlayTests, float, GameFlow, IEnumerator, List, MonoBehaviour, RaceEventDefinition, SetUp (+4 more)

### Community 236 - "InputActionAssetTests"
Cohesion: 0.36
Nodes (3): InputActionAssetTests, string, Test

### Community 237 - "Readme"
Cohesion: 0.29
Nodes (7): Readme, Section, bool, string, ScriptableObject, Section, Texture2D

### Community 238 - "Weapons — visual brief for Figma"
Cohesion: 0.25
Nodes (7): Page 1 — guns, Page 2 — melee rigs (arm extends from the car and strikes), Page 3 — beams, sprays and pulses, Page 4 — funny weapons (cartoon humour welcome), Page 5 — traps and specials, Shared style block (paste every time), Weapons — visual brief for Figma

### Community 239 - "hy_python.sh"
Cohesion: 0.25
Nodes (7): HF_HOME, HF_HUB_OFFLINE, HY3DGEN_MODELS, PYTHONPATH, hy_python.sh script, U2NET_HOME, XDG_CACHE_HOME

### Community 241 - ".EveryRecordedBodyRetainsItsVisibleGeometryWithoutJoiningTheRace"
Cohesion: 0.17
Nodes (11): GhostBodyVisualsPlayTests, GameFlow, GameObject, IEnumerator, List, MeshFilter, TearDown, Timeout (+3 more)

### Community 242 - ".RecordsCaptureAppliedTrialAndCompareOnlyMatchingConfigurations"
Cohesion: 0.14
Nodes (12): RecordSetupPlayTests, float, GameFlow, IEnumerator, int, RaceEventDefinition, RaceManager, SetUp (+4 more)

### Community 243 - "ResultStarsPlayTests"
Cohesion: 0.14
Nodes (11): ResultStarsPlayTests, float, GameFlow, IEnumerator, RaceEventDefinition, SetUp, string, TearDown (+3 more)

### Community 244 - "AiRecoveryTests"
Cohesion: 0.33
Nodes (4): AiRecoveryTests, IEnumerator, UnityTearDown, UnityTest

### Community 245 - "PropModelLibraryTests"
Cohesion: 0.38
Nodes (3): PropModelLibraryTests, Mesh, Test

### Community 246 - "com.unity.profiling.core"
Cohesion: 0.29
Nodes (7): dependencies, depth, source, url, version, com.unity.profiling.core, com.unity.profiling.core

### Community 247 - "Contextual driving help — 2026-10-23"
Cohesion: 0.29
Nodes (6): Changes, Contextual driving help — 2026-10-23, Final build and continuation run, Next observed issue, Observed problem, Validation

### Community 248 - "Gameplay improvements — 2026-09-24"
Cohesion: 0.29
Nodes (6): Checks, Gameplay improvements — 2026-09-24, Варианты будущих карт и режимов — 2026-09-24, «Горячая бомба» — 2026-09-25, Развилка на северном повороте — 2026-09-25, Режим «Арена»

### Community 249 - "play.mjs"
Cohesion: 0.33
Nodes (5): sleep(), st(), /home/user/gstack/node_modules/playwright-core/index.mjs, args, logs

### Community 250 - "Карточка игры в консоли Яндекс Игр (черновик)"
Cohesion: 0.29
Nodes (6): Заметки для модератора, Карточка игры в консоли Яндекс Игр (черновик), Короткое описание (до 60 символов), Медиа, Основное, Полное описание

### Community 251 - "make_prop_reference.py"
Cohesion: 0.43
Nodes (5): call(), gen(), render(), workflow(), Reference pictures for props (Tools/art/props_spec.json) from the local ComfyUI…

### Community 252 - "WallRideEntry"
Cohesion: 0.14
Nodes (12): Case, Report, WallRideEntry, Report, float, GameFlow, int, List (+4 more)

### Community 254 - "SpeedometerTests"
Cohesion: 0.33
Nodes (3): SpeedometerTests, Test, TestCase

### Community 255 - "BoostGatePlayTests"
Cohesion: 0.18
Nodes (9): BoostGatePlayTests, float, GameFlow, GameObject, IEnumerator, SetUp, TearDown, Timeout (+1 more)

### Community 256 - ".BridgeBoostWallsRamAndCollapseWorkInOwnEvent"
Cohesion: 0.50
Nodes (3): IEnumerator, Timeout, UnityTest

### Community 257 - "Bounded AI recovery investigation — 2026-10-23"
Cohesion: 0.33
Nodes (5): Bounded AI recovery investigation — 2026-10-23, Controlled reproduction and correction, Final browser validation and archive, Fixed-seed physical comparison, Physical-harness setup issue

### Community 258 - "Player experience improvements"
Cohesion: 0.33
Nodes (5): Compatibility and boundaries, Five-player playtest protocol, Player experience improvements, Player-facing changes, Verification

### Community 259 - "Racer name consistency — 2026-10-23"
Cohesion: 0.33
Nodes (5): Campaign baseline, Racer name consistency — 2026-10-23, Reproduction and change, Validation status, Verified archive and next coverage

### Community 260 - "CG-RISK-003 — устройства, производительность и память"
Cohesion: 0.33
Nodes (6): CG-RISK-003 — устройства, производительность и память, Assets/CarsGame/Tests/PlayMode/ResourceLifetimePlayTests.cs, Assets/Scenes/SampleScene.unity, Docs/AI/Endurance-2026-10-24.md, Tools/qa/endurance.mjs, Tools/qa/visual-quality.mjs

### Community 261 - "com.unity.collab-proxy"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.collab-proxy

### Community 263 - "CameraRecoveryPlayTests"
Cohesion: 0.21
Nodes (9): CameraRecoveryPlayTests, bool, float, GameFlow, IEnumerator, SetUp, TearDown, Timeout (+1 more)

### Community 264 - "SfxVoicePicker"
Cohesion: 0.40
Nodes (4): SfxVoicePicker, float, int, object

### Community 265 - "BoostGate"
Cohesion: 0.23
Nodes (7): BoostGate, Passage, bool, Collider, Dictionary, float, HashSet

### Community 266 - "Arsenal usability — 2026-10-23"
Cohesion: 0.40
Nodes (4): Arsenal usability — 2026-10-23, Implemented, Next observed issue, Validation

### Community 267 - "Образец: машина из Hunyuan3D-2 по контракту MidcoreDesign §G (2026-09-24)"
Cohesion: 0.40
Nodes (4): Исправлено по дороге, Не сделано, Образец: машина из Hunyuan3D-2 по контракту MidcoreDesign §G (2026-09-24), Результат

### Community 268 - "Bomb HUD readability — 2026-10-23"
Cohesion: 0.40
Nodes (4): Bomb HUD readability — 2026-10-23, Evidence and change, Final validation, Separate next investigation

### Community 269 - "Mission HUD readability — 2026-10-23"
Cohesion: 0.40
Nodes (4): Mission HUD readability — 2026-10-23, Observed problems and corrections, Verification, Verified archive

### Community 270 - "codex-gpt-6-astra-max.md"
Cohesion: 0.40
Nodes (4): 1. Список проблем, 2. Чего не хватает, 3. Что вырезать первым при 4–6 неделях на одного разработчика, 4. Ответы на открытые вопросы

### Community 271 - "cursor-grok-4.6.md"
Cohesion: 0.40
Nodes (4): 1. Проблемы, 2. Чего не хватает, 3. Что вырезать первым (4–6 недель, один разработчик), 4. Ответы на §13

### Community 272 - "opencode-big-pickle.md"
Cohesion: 0.40
Nodes (4): 1. Список проблем (по серьёзности), 2. Чего не хватает, 3. Что вырезать первым (при 4–6 неделях на одного разработчика), 4. Ответы на открытые вопросы (§13)

### Community 273 - "opencode-mimo-v2.5-free.md"
Cohesion: 0.40
Nodes (4): 1. Список проблем (по серьёзности), 2. Чего не хватает, 3. Что вырезать первым (при 4-6 неделях на 1 разработчика), 4. Ответы на открытые вопросы (§13)

### Community 274 - "opencode-nemotron-3-ultra.md"
Cohesion: 0.40
Nodes (4): 1. Список проблем (по серьёзности), 2. Чего не хватает (до 8 пунктов), 3. Что вырезать первым при 4–6 неделях на одного разработчика, 4. Ответы на 6 открытых вопросов (§13)

### Community 275 - "Rigidbody Vehicle Control"
Cohesion: 0.40
Nodes (5): Assembly-CSharp, Rigidbody Vehicle Control, SampleScene, MonoBehaviour Scene Composition, Track Markings Opponent Route

### Community 276 - "Q: Проследи"
Cohesion: 0.40
Nodes (4): Answer, Outcome, Q: Проследи, Source Nodes

### Community 277 - "Q: Как продолжать развивать игру?"
Cohesion: 0.40
Nodes (4): Answer, Outcome, Q: Как продолжать развивать игру?, Source Nodes

### Community 278 - "com.unity.modules.accessibility"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.accessibility

### Community 279 - "com.unity.modules.umbra"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.umbra

### Community 280 - "CameraFeedbackPlayTests"
Cohesion: 0.18
Nodes (8): CameraFeedbackPlayTests, bool, float, GameObject, IEnumerator, SetUp, TearDown, UnityTest

### Community 282 - "Расширение арсенала: 40 новых видов"
Cohesion: 0.50
Nodes (3): Поведение и ограничения, Проверка, Расширение арсенала: 40 новых видов

### Community 283 - "Rival nameplate readability — 2026-10-23"
Cohesion: 0.50
Nodes (3): Evidence and correction, Final validation, Rival nameplate readability — 2026-10-23

### Community 284 - "Distinct rival names — 2026-10-23"
Cohesion: 0.50
Nodes (3): Distinct rival names — 2026-10-23, Evidence and correction, Validation status

### Community 285 - "GameFlow.cs"
Cohesion: 0.25
Nodes (6): Известные ограничения, Проверка сборки на реальном SDK Яндекса, Список проверки, Что должен сделать пользователь в консоли разработчика, CG-RISK-002 — приёмка реального SDK, CG-RISK-008 — концентрация ответственностей

### Community 286 - "art/README.md"
Cohesion: 0.50
Nodes (3): Car art pipeline (Hunyuan3D-2 → Blender → Unity), Props: pickups and weapon heads (Z-Image reference → Hunyuan3D-2 → Blender → Unity), Size and performance budget (all future art)

### Community 287 - "combat-help.mjs"
Cohesion: 0.50
Nodes (3): errors, evidence, findings

### Community 288 - "profile-reload.mjs"
Cohesion: 0.50
Nodes (3): errors, journey, last

### Community 291 - "Target Platforms"
Cohesion: 0.67
Nodes (3): Linux Standalone Editor Target, Mobile Platform Settings, Target Platforms

### Community 354 - "EventDiscoveryPlayTests"
Cohesion: 0.21
Nodes (8): EventDiscoveryPlayTests, Button, GameFlow, IEnumerator, string, TearDown, Timeout, UnityTest

### Community 355 - "LocalizationRecoveryPlayTests"
Cohesion: 0.22
Nodes (10): LocalizationRecoveryPlayTests, RecoveringTable, AsyncOperationHandle, bool, float, int, Locale, string (+2 more)

### Community 356 - "RewardPresentationPlayTests"
Cohesion: 0.17
Nodes (9): RewardPresentationPlayTests, float, GameFlow, IEnumerator, SetUp, string, TearDown, Timeout (+1 more)

### Community 357 - ".AddPose"
Cohesion: 0.24
Nodes (6): Sample, byte, float, int, Quaternion, Vector3

### Community 358 - "LapCounter"
Cohesion: 0.33
Nodes (4): LapCounter, bool, float, int

### Community 359 - "Loc"
Cohesion: 0.24
Nodes (8): Loc, Action, AsyncOperationHandle, HashSet, int, Locale, string, StringTable

### Community 360 - "ResourceLifetimePlayTests"
Cohesion: 0.20
Nodes (11): Report, ResourceLifetimePlayTests, Sample, float, GameFlow, int, List, long (+3 more)

### Community 361 - "Iterative polish, 2026-10-24"
Cohesion: 0.17
Nodes (11): Baseline, Broader validation, Browser iteration, Candidate validation, Current work, Delivery summary, Final package checks, First evidence (+3 more)

### Community 362 - ".VolumeControlsStayIndependentAndPreserveAnExplicitEightyPercent"
Cohesion: 0.20
Nodes (8): AudioSettingsPlayTests, float, IEnumerator, string, TearDown, Timeout, UnityTest, Slider

### Community 363 - "FlowPausePlayTests"
Cohesion: 0.29
Nodes (7): FlowPausePlayTests, GameFlow, IEnumerator, RaceManager, TearDown, Timeout, UnityTest

### Community 364 - "CameraImpulse"
Cohesion: 0.27
Nodes (4): CameraImpulse, float, Quaternion, Vector3

### Community 365 - "RacingGame.Platform"
Cohesion: 0.22
Nodes (3): PlatformBootstrap, RacingGame.Platform, RacingGame.Core.Localization

### Community 366 - "SaveMergeInvariantTests"
Cohesion: 0.50
Nodes (3): SaveMergeInvariantTests, Test, TestCase

### Community 367 - "MissionResultsPlayTests"
Cohesion: 0.22
Nodes (6): MissionResultsPlayTests, float, GameFlow, SetUp, string, TearDown

### Community 369 - ".MutingEffectsImmediatelySilencesAnOngoingSkid"
Cohesion: 0.46
Nodes (4): AudioMixerPlayTests, IEnumerator, Timeout, UnityTest

### Community 370 - ".InterruptedLanguageSwitchKeepsWorkingMenusAndCanRetry"
Cohesion: 0.29
Nodes (5): Func, IEnumerator, Timeout, UnityTearDown, UnityTest

### Community 371 - "Проверки CarsGame"
Cohesion: 0.25
Nodes (7): Браузерные проверки, Когда запускать, Обычный запуск (Linux), Окружение и результаты, Проверка 25 октября 2026, Проверки CarsGame, Что добавлено в этом проходе

### Community 372 - "GhostCar"
Cohesion: 0.33
Nodes (4): GhostCar, bool, double, Material

### Community 373 - ".GetSlot"
Cohesion: 0.33
Nodes (4): StartGrid, float, Quaternion, Vector3

### Community 374 - "Envelope"
Cohesion: 0.33
Nodes (7): Envelope, LeaderboardEntryDto, SessionList, bool, int, long, string

### Community 375 - "ghost-replay.mjs"
Cohesion: 0.38
Nodes (5): decodeHeader(), fixture(), fnv(), errors, evidence

### Community 376 - "local-storage.mjs"
Cohesion: 0.29
Nodes (4): context, pointers, source, storage

### Community 377 - ".LongFramesCannotCountLessRaceTimeThanThePhysicsTheyAdvance"
Cohesion: 0.33
Nodes (4): RaceClockHitchPlayTests, IEnumerator, Timeout, UnityTest

### Community 378 - ".SpeedAndCornerEffectsCanBeDisabledAndRestoredWithoutChangingShakePreference"
Cohesion: 0.33
Nodes (4): SteadyCameraPlayTests, IEnumerator, Timeout, UnityTest

### Community 380 - "endurance.mjs"
Cohesion: 0.33
Nodes (4): events, report, seconds, tiers

### Community 381 - "locale-lifetime.mjs"
Cohesion: 0.33
Nodes (3): report, rounds, url

### Community 382 - "vitals-readability.mjs"
Cohesion: 0.33
Nodes (5): errors, evidence, failures, languages, minima

### Community 383 - "Ghost replay — 2026-10-25"
Cohesion: 0.40
Nodes (4): Evidence so far, Format, Ghost replay — 2026-10-25, Runtime behaviour

### Community 387 - "drift-feedback.mjs"
Cohesion: 0.50
Nodes (3): errors, evidence, languages

### Community 388 - "pause-boundaries.mjs"
Cohesion: 0.50
Nodes (3): errors, evidence, failures

### Community 389 - "pause-settings.mjs"
Cohesion: 0.50
Nodes (3): errors, evidence, launchHelp

### Community 390 - "practice.mjs"
Cohesion: 0.50
Nodes (3): errors, evidence, languages

### Community 391 - "test-drive-layout.mjs"
Cohesion: 0.50
Nodes (3): errors, evidence, languages

### Community 393 - "weapon-labels.mjs"
Cohesion: 0.50
Nodes (3): errors, evidence, failures

## Ambiguous Edges - Review These
- `Official Unity AI/MCP Package` → `Runtime and Play Mode Automation`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to
- `Linux Standalone Editor Target` → `Target Platforms`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to
- `Mobile Platform Settings` → `Target Platforms`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to

## Knowledge Gaps
- **949 isolated node(s):** `Slot`, `Outcome`, `Kind`, `Kind`, `Stream` (+944 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **102 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `Official Unity AI/MCP Package` and `Runtime and Play Mode Automation`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **What is the exact relationship between `Linux Standalone Editor Target` and `Target Platforms`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **What is the exact relationship between `Mobile Platform Settings` and `Target Platforms`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `RacingGame.Tests` connect `RacingGame.Tests` to `.GuidanceMatchesDistinctCombatBehaviors`, `UiInteractionTests`, `CarWeapons`, `PlatformGlue`, `.Spawn`, `RacingGame.Core.Sim`, `RaceEventDefinition`, `OilHazardTests`, `OnboardingHints`, `CameraFeedbackPlayTests`, `DriveCommand`, `DailyRace`, `StarRingLayout`, `ArsenalExpansionTests`, `.ResetAll`, `AiGunner`, `CarPaintTests`, `PlatformRuntime`, `.FireVoice`, `RecoveryIntentPlayTests`, `.Advance`, `.Pick`, `ArenaModeTests`, `PropModelPlayTests`, `SaveService.cs`, `KillTally`, `HunyuanCarPlayTests`, `RaceHarness`, `.BothDirectionsHaveDriveableShortcutValidLapsAndRepair`, `MissionPlayTests`, `DriftCharge`, `RacingGame`, `EventRulesTests`, `RaceProgress`, `RacingGame.Core.Combat`, `WeaponRig`, `.ArenaSupportsSpawningPursuitAndUnoccupiedRecovery`, `ArenaMode.md`, `DriftPlayTests`, `GrassReversePlayTests`, `SpecialWeaponTargetsPlayTests`, `TrackSplineTests`, `PlayerDriverInput`, `MinesPlayTests`, `EventDiscoveryPlayTests`, `LocalizationRecoveryPlayTests`, `AiCorneringPlayTests`, `RacerNamesPlayTests`, `AbyssTrack`, `AiRecoveryPlayTests`, `InputActionAssetTests`, `RacingGame.Platform`, `UniqueRivalNamesPlayTests`, `MissionResultsPlayTests`, `TrackProfile`, `.MutingEffectsImmediatelySilencesAnOngoingSkid`, `AiRecoveryTests`, `PropModelLibraryTests`, `MineHazard`, `.LongFramesCannotCountLessRaceTimeThanThePhysicsTheyAdvance`, `.SpeedAndCornerEffectsCanBeDisabledAndRestoredWithoutChangingShakePreference`, `WallRideEntry`, `RaceResultsTests`, `SpeedometerTests`, `BoostGatePlayTests`?**
  _High betweenness centrality (0.099) - this node is a cross-community bridge._
- **Why does `GameFlow` connect `GameFlow` to `WastelandTrack`, `DebugBus`, `HudScreen`, `CarController`, `MenuScreens`, `MissionDirector`, `RaceEventDefinition`, `GhostRecorder`, `StarRingTrack`, `.LoadoutTouchControlsGoalsAndHelpFollowRaceRules`, `TrackFork`, `RaceParticipant`, `GameFlow.cs`, `.EnterMenu`, `.ApplyTo`, `RaceManager`, `TrackPath`, `MenuCameraDirector`, `CarsGame Claude context`, `PickupSpawner`, `MusicPlayer`, `SpeedFx`, `MonoBehaviour`, `DailyChallenge`, `MusicDirector`, `SkyDirector`, `EngineAudio`, `RaceDirector`, `AbyssTrack`, `DifficultyTier`, `GhostCar`, `.StartPractice`?**
  _High betweenness centrality (0.096) - this node is a cross-community bridge._
- **Why does `RacingGame` connect `RacingGame` to `CarLivery`, `BodyDef`, `CarWeapons`, `HudScreen`, `CarController`, `MeshChunkWriter`, `MenuScreens`, `CarCannon`, `DailyRace`, `RaceParticipant`, `AiGunner`, `.FireVoice`, `MissionStrikes`, `TrooperUnit`, `.Advance`, `RaceManager`, `MissionHud`, `EffectPool`, `MenuCameraDirector`, `GameFlow`, `WorldHealthBars`, `MineVisual`, `RacingGame.Tests`, `PickupSpawner`, `MissionUnit`, `RaceSpawner`, `ArenaDriver`, `RaceProgress`, `WeaponRig`, `ArenaMode.md`, `UiButtonFx`, `PlayerEffects`, `PlayerDriverInput`, `MissionDriver`, `RaceDirector`, `PerfectStart`, `OilHazardPool`, `Projectile`, `MineField`, `DifficultyTier`, `WebMusicOutput`, `SfxPlayer`, `SpeedometerGraphic`, `MineHazard`, `CarStatus`, `.GrantTrial`, `AiDriver`, `CrashProbe`, `.GuidanceMatchesDistinctCombatBehaviors`, `.Material`, `CarPaint`, `RacingGame.Core.Sim`, `RaceEventDefinition`, `PropModelLibrary`, `OnboardingHints`, `DriveCommand`, `.BuildSleekSportsCar`, `ISimulated`, `SfxSynth`, `Pickup`, `TouchControlsPresenter`, `MusicPlayer`, `SpeedFx`, `MonoBehaviour`, `CarDamageSmoke`, `.Get`, `RacingGame.Core.Combat`, `EngineAudio`, `.Hostile`, `CarCameraFollow`, `PauseReason`, `RaceResultsTests`, `SfxVoicePicker`, `BoostGate`, `GameFlow.cs`, `CameraImpulse`, `RacingGame.Platform`, `.ApplyWeapons`, `GhostCar`, `.GetSlot`?**
  _High betweenness centrality (0.049) - this node is a cross-community bridge._
- **What connects `Slot`, `Outcome`, `Kind` to the rest of the system?**
  _949 weakly-connected nodes found - possible documentation gaps or missing edges._