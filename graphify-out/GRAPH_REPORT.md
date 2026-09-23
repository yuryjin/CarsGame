# Graph Report - CarsGame  (2026-09-24)

## Corpus Check
- 187 files · ~118,044 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2793 nodes · 5100 edges · 209 communities (148 shown, 61 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 315 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `0bb7a2e7`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- com.unity.modules.audio
- OnboardingHints
- PlatformGlue
- MonoBehaviour
- com.unity.burst
- com.unity.modules.unitywebrequest
- com.unity.modules.imgui
- com.unity.render-pipelines.core
- com.unity.modules.physics
- ReadmeEditor
- com.unity.modules.uielements
- CarHealth
- com.unity.modules.jsonserialize
- com.unity.modules.ui
- YandexNative
- CarController
- Unity Project Context
- dependencies
- com.unity.modules.ai
- CarsGame: мидкор-слой. Черновик v1 (для адверсариальной проверки)
- dependencies
- com.unity.modules.physics2d
- GhostCar
- UiButtonFx
- RaceProgress
- Rigidbody Vehicle Control
- .Build
- YandexPlatform
- DebugBus
- Target Platforms
- com.unity.ai.assistant
- com.unity.ai.navigation
- cars-platform.js
- com.unity.ide.rider
- com.unity.ide.visualstudio
- com.unity.inputsystem
- com.unity.modules.accessibility
- ChasmLayout
- com.unity.modules.terrainphysics
- com.unity.modules.tilemap
- com.unity.modules.umbra
- com.unity.modules.unityanalytics
- com.unity.modules.unitywebrequesttexture
- com.unity.modules.vectorgraphics
- com.unity.modules.vehicles
- com.unity.modules.video
- GameFlow
- com.unity.multiplayer.center
- com.unity.pipeline
- com.unity.render-pipelines.universal
- com.unity.timeline
- com.unity.visualscripting
- com.unity.modules.ai
- com.unity.modules.androidjni
- com.unity.modules.animation
- com.unity.modules.assetbundle
- com.unity.modules.audio
- com.unity.modules.director
- com.unity.modules.imageconversion
- com.unity.modules.imgui
- com.unity.modules.jsonserialize
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
- Q: Проследи
- Result
- HudScreen
- MeshChunkWriter
- BarrierTools
- RaceManager
- EffectPool
- .BuildSleekSportsCar
- EditorMockPlatform
- TrackSpline
- AiCombatAndInputTests
- SkyDirector
- CarCannon
- RacingGame
- MenuScreens
- CarsGame: мидкор-слой. Этапы и промпты для агента
- SkyMath
- .Panel
- TrackProfile
- com.unity.nuget.newtonsoft-json
- .Rank
- CliffMeshGen
- WebZipPostprocessor
- EnvironmentBaker
- 2026-09-02 — M0 (частично), M1, M2, M3 (код)
- com.unity.modules.imageconversion
- MenuCameraDirector
- RaceParticipant
- RaceHarness
- TrackPath
- RaceSpawner
- PlatformRuntime
- Technical Design
- .SecondsToTicks
- Projectile
- AiDriver
- PlayerEffects
- ArenaLayout
- SimulationDriver
- com.unity.test-framework
- PerfectStart
- WorldHealthBars
- com.unity.nuget.mono-cecil
- RacingGame.Core.Sim
- PropLayout.cs
- SfxPlayer
- RacingGame.Tests
- com.unity.ugui
- AiGunner
- Envelope
- CarPaint
- TrackSplineTests
- RacingGame.Core.Platform
- CarWeaponsTests
- OpponentDriver
- .BuildGarage
- PlayerDriverInput
- RaceResultEntry
- TargetCar
- codex-gpt-6-astra-max.md
- cursor-grok-4.6.md
- opencode-big-pickle.md
- opencode-mimo-v2.5-free.md
- .ArenaSupportsSpawningPursuitAndUnoccupiedRecovery
- CarStatus
- opencode-nemotron-3-ultra.md
- RacingGame.Core.Save
- .ApplyHit
- RacingGame.EditorTools
- browser-smoke.mjs
- com.unity.modules.assetbundle
- .CountStars
- SaveServiceTests
- RaceClockTests
- MineHazardTests
- SpeedometerGraphic
- CarDamageSmoke
- com.unity.modules.unitywebrequestwww
- com.unity.modules.xr
- com.unity.modules.particlesystem
- PickupSpawner
- SfxSynth
- .Range
- SaveService
- SaveData
- .GetSlot
- TouchControlsPresenter
- .Parse
- PlatformContractsTests
- DriveCommand
- H
- Карточка игры в консоли Яндекс Игр (черновик)
- SpeedometerTests
- RaceResultsTests
- SkyPresetFactory
- Q: Как продолжать развивать игру?
- Проверка сборки на реальном SDK Яндекса
- common.sh
- .Merge
- MockCloudSave
- race.sh
- build-web.sh
- edit-tests.sh
- loc-bake.sh
- HitInfo
- PauseReason
- RacingGame.World.Gen
- RacingGame.World
- HitResult
- GhostRecord
- CarWeapons
- RaceDirectorPoolTests
- .ApplyTo
- RaceEventDefinition
- Pickup
- RaceDirector
- KillTally
- PropBaker
- MineHazard
- EdgeLatchTests
- EngineAudio
- ProjectileSpawn
- RankEntry
- GhostRecorder
- MineSpawn
- EnvironmentGenTests
- .RegisterCar
- .ClearAllStatuses
- InputActionAssetTests
- CameraImpulse
- ScriptedDriverInput
- IRespawnPoseProvider
- StatusEffect

## God Nodes (most connected - your core abstractions)
1. `CarController` - 71 edges
2. `RacingGame` - 68 edges
3. `HudScreen` - 65 edges
4. `MenuScreens` - 52 edges
5. `GameFlow` - 49 edges
6. `Result` - 41 edges
7. `RaceManager` - 41 edges
8. `CarHealth` - 35 edges
9. `YandexPlatform` - 34 edges
10. `RacingGame.Tests` - 34 edges

## Surprising Connections (you probably didn't know these)
- `Unity 6000.5.9f1` --semantically_similar_to--> `Unity 6000.5.9f1 Revision b57deb96f08d`  [INFERRED] [semantically similar]
  Docs/AI/UnityProjectContext.md → ProjectSettings/ProjectVersion.txt
- `Unity Project Context` --references--> `Unity Project Version Record`  [EXTRACTED]
  Docs/AI/UnityProjectContext.md → ProjectSettings/ProjectVersion.txt
- `HitInfo` --references--> `HitType`  [EXTRACTED]
  Assets/CarsGame/Core/Combat/HitInfo.cs → Assets/CarsGame/Core/Combat/HitType.cs
- `HitInfo` --references--> `StatusKind`  [EXTRACTED]
  Assets/CarsGame/Core/Combat/HitInfo.cs → Assets/CarsGame/Core/Combat/StatusKind.cs
- `StatusEffect` --references--> `StatusKind`  [EXTRACTED]
  Assets/CarsGame/Core/Combat/StatusEffect.cs → Assets/CarsGame/Core/Combat/StatusKind.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Confirmed Unity Environment** — docs_ai_unityprojectcontext_unity_6000_5_9f1, docs_ai_unityprojectcontext_urp_17_5_0, docs_ai_unityprojectcontext_input_system_1_20_0 [EXTRACTED 1.00]
- **Gameplay Architecture Patterns** — docs_ai_unityprojectcontext_scene_composition, docs_ai_unityprojectcontext_rigidbody_vehicle_control, docs_ai_unityprojectcontext_track_markings_route, docs_ai_unityprojectcontext_procedural_car_generation [EXTRACTED 1.00]

## Communities (209 total, 61 thin omitted)

### Community 0 - "com.unity.modules.audio"
Cohesion: 0.07
Nodes (30): dependencies, depth, source, version, dependencies, depth, source, version (+22 more)

### Community 1 - "OnboardingHints"
Cohesion: 0.13
Nodes (10): float, Hint, HintTracker, Color, float, GameObject, OnboardingHints, Test (+2 more)

### Community 2 - "PlatformGlue"
Cohesion: 0.05
Nodes (21): float, AdPacer, HashSet, string, LanguageResolver, bool, Task, PlatformGlue (+13 more)

### Community 3 - "MonoBehaviour"
Cohesion: 0.11
Nodes (13): bool, Collision, float, int, Rigidbody, string, CrashProbe, S (+5 more)

### Community 4 - "com.unity.burst"
Cohesion: 0.15
Nodes (13): com.unity.burst, com.unity.mathematics, dependencies, depth, source, url, version, dependencies (+5 more)

### Community 5 - "com.unity.modules.unitywebrequest"
Cohesion: 0.08
Nodes (28): dependencies, depth, source, version, dependencies, depth, source, version (+20 more)

### Community 6 - "com.unity.modules.imgui"
Cohesion: 0.08
Nodes (24): com.unity.ext.nunit, dependencies, depth, source, version, dependencies, depth, source (+16 more)

### Community 7 - "com.unity.render-pipelines.core"
Cohesion: 0.07
Nodes (30): com.unity.render-pipelines.core, com.unity.render-pipelines.universal-config, com.unity.searcher, com.unity.shadergraph, dependencies, depth, source, version (+22 more)

### Community 8 - "com.unity.modules.physics"
Cohesion: 0.07
Nodes (27): dependencies, depth, source, version, dependencies, depth, source, version (+19 more)

### Community 9 - "ReadmeEditor"
Cohesion: 0.11
Nodes (14): bool, float, string, ReadmeEditor, bool, string, Readme, Section (+6 more)

### Community 10 - "com.unity.modules.uielements"
Cohesion: 0.12
Nodes (16): dependencies, depth, source, url, version, depth, source, version (+8 more)

### Community 11 - "CarHealth"
Cohesion: 0.11
Nodes (10): bool, Collision, Color, float, GameObject, int, MaterialPropertyBlock, Renderer (+2 more)

### Community 12 - "com.unity.modules.jsonserialize"
Cohesion: 0.08
Nodes (27): com.unity.modules.subsystems, dependencies, depth, source, version, dependencies, depth, source (+19 more)

### Community 13 - "com.unity.modules.ui"
Cohesion: 0.11
Nodes (18): com.unity.modules.hierarchycore, dependencies, depth, source, version, dependencies, depth, source (+10 more)

### Community 14 - "YandexNative"
Cohesion: 0.10
Nodes (7): Task, YandexNative, IReadOnlyList, Task, DllImport, Envelope, IntPtr

### Community 15 - "CarController"
Cohesion: 0.09
Nodes (9): AudioSource, bool, float, int, Quaternion, Rigidbody, Vector3, CarController (+1 more)

### Community 16 - "Unity Project Context"
Cohesion: 0.17
Nodes (13): Unity AI Navigation Package, CarsGame Project, Single-Scene Driving and Combat Prototype, Unity Input System 1.20.0, Official Unity AI/MCP Package, Procedural Car Mesh Generation, Runtime and Play Mode Automation, Unity 6000.5.9f1 (+5 more)

### Community 17 - "dependencies"
Cohesion: 0.17
Nodes (11): com.unity.collab-proxy, com.unity.localization, com.unity.modules.adaptiveperformance, com.unity.modules.cloth, com.unity.modules.wind, dependencies, com.unity.collab-proxy, com.unity.localization (+3 more)

### Community 18 - "com.unity.modules.ai"
Cohesion: 0.17
Nodes (12): dependencies, depth, source, url, version, dependencies, depth, source (+4 more)

### Community 19 - "CarsGame: мидкор-слой. Черновик v1 (для адверсариальной проверки)"
Cohesion: 0.08
Nodes (24): 0. Факты о текущем коде (проверены по репозиторию 2026-09-10), 10. Прокачка, 11. Порядок работ и критерии, 12. Арт-пайплайн для новых машин (кратко), 13. Открытые вопросы для рецензентов, 1. Цели и ограничения, 2. Система статусов (фундамент), 3. Слоты оружия и ввод (+16 more)

### Community 20 - "dependencies"
Cohesion: 0.09
Nodes (22): dependencies, depth, source, url, version, dependencies, depth, source (+14 more)

### Community 21 - "com.unity.modules.physics2d"
Cohesion: 0.12
Nodes (17): dependencies, depth, source, version, dependencies, depth, source, version (+9 more)

### Community 22 - "GhostCar"
Cohesion: 0.12
Nodes (10): float, Vector3, Sample, bool, Color, double, GameObject, GhostCar (+2 more)

### Community 23 - "UiButtonFx"
Cohesion: 0.12
Nodes (13): bool, Button, float, Vector3, UiButtonFx, BaseEventData, IDeselectHandler, IPointerDownHandler (+5 more)

### Community 24 - "RaceProgress"
Cohesion: 0.11
Nodes (9): bool, float, int, LapCounter, float, int, RaceProgress, Test (+1 more)

### Community 25 - "Rigidbody Vehicle Control"
Cohesion: 0.40
Nodes (5): Assembly-CSharp, Rigidbody Vehicle Control, SampleScene, MonoBehaviour Scene Composition, Track Markings Opponent Route

### Community 26 - ".Build"
Cohesion: 0.20
Nodes (9): Color32, float, int, List, Mesh, Vector2, Vector3, TerrainMeshGen (+1 more)

### Community 27 - "YandexPlatform"
Cohesion: 0.12
Nodes (19): AdResult, DeviceKind, IAds, IAsyncSessions, ILeaderboards, IPayments, IPlatformServices, PlatformFeature (+11 more)

### Community 28 - "DebugBus"
Cohesion: 0.15
Nodes (10): bool, float, int, long, string, Command, DebugBus, Perf (+2 more)

### Community 29 - "Target Platforms"
Cohesion: 0.67
Nodes (3): Linux Standalone Editor Target, Mobile Platform Settings, Target Platforms

### Community 32 - "cars-platform.js"
Cohesion: 0.13
Nodes (35): attach(), boot(), byteLength(), cmd(), debugState(), detectDevice(), drainOnce(), env() (+27 more)

### Community 37 - "ChasmLayout"
Cohesion: 0.23
Nodes (6): float, Matrix4x4, Vector2, Vector3, ChasmLayout, ChasmSettings

### Community 46 - "GameFlow"
Cohesion: 0.10
Nodes (10): bool, Color, InputAction, int, List, SaveService, string, FlowState (+2 more)

### Community 73 - "Q: Проследи"
Cohesion: 0.40
Nodes (4): Answer, Outcome, Q: Проследи, Source Nodes

### Community 74 - "Result"
Cohesion: 0.11
Nodes (11): bool, int, IReadOnlyList, long, string, Task, ICloudSave, LeaderboardEntry (+3 more)

### Community 75 - "HudScreen"
Cohesion: 0.06
Nodes (19): GameObject, bool, Button, CanvasGroup, Color, float, GameObject, Image (+11 more)

### Community 76 - "MeshChunkWriter"
Cohesion: 0.17
Nodes (14): List, Color32, Matrix4x4, Vector2, Vector3, LowPoly, Color32, int (+6 more)

### Community 77 - "BarrierTools"
Cohesion: 0.26
Nodes (4): MenuItem, Transform, Vector2, BarrierTools

### Community 78 - "RaceManager"
Cohesion: 0.15
Nodes (9): bool, Dictionary, double, float, int, IReadOnlyList, List, RaceManager (+1 more)

### Community 79 - "EffectPool"
Cohesion: 0.12
Nodes (16): bool, Color, float, int, Light, List, Material, MaterialPropertyBlock (+8 more)

### Community 80 - ".BuildSleekSportsCar"
Cohesion: 0.16
Nodes (10): MenuItem, string, Transform, RaceCarPrefabBaker, GameObject, Material, Mesh, Transform (+2 more)

### Community 81 - "EditorMockPlatform"
Cohesion: 0.14
Nodes (9): ResultCategory, bool, Dictionary, float, IReadOnlyList, Random, string, Task (+1 more)

### Community 82 - "TrackSpline"
Cohesion: 0.27
Nodes (6): float, Vector3, TrackSpline, d, IList, sign

### Community 83 - "AiCombatAndInputTests"
Cohesion: 0.10
Nodes (13): int, Vector3, TargetFinder, AiGunner, CarCannon, CarController, CarStatus, CarWeapons (+5 more)

### Community 84 - "SkyDirector"
Cohesion: 0.14
Nodes (13): Color, Cubemap, int, Light, Material, SkyDirector, bool, Color (+5 more)

### Community 85 - "CarCannon"
Cohesion: 0.15
Nodes (9): bool, Color, float, int, Material, Rigidbody, Transform, Vector3 (+1 more)

### Community 87 - "MenuScreens"
Cohesion: 0.10
Nodes (20): float, int, string, DifficultyTier, Action, bool, Button, Canvas (+12 more)

### Community 88 - "CarsGame: мидкор-слой. Этапы и промпты для агента"
Cohesion: 0.12
Nodes (15): CarsGame: мидкор-слой. Этапы и промпты для агента, После MVP (короткие промпты, по одному этапу каждый), Преамбула (вставлять в начало каждого промпта), Этап 0. Тики в CarHealth, единый результат попадания, потолки пулов (P0a, ~4 дня), Этап 10. Реальный SDK Яндекса и память WebGL (P4b, ~3 дня), Этап 11. Плейтест-резерв (5–7 дней, без нового контента), Этап 1. Замер базовой производительности WebGL (P0b, ~2 дня), Этап 2. Система статусов и щит (P1a, ~3 дня) (+7 more)

### Community 89 - "SkyMath"
Cohesion: 0.24
Nodes (7): Color, Cubemap, float, Vector3, SkyMath, SkyParams, CubemapFace

### Community 90 - ".Panel"
Cohesion: 0.17
Nodes (14): Transform, Action, Button, Canvas, Color, Image, RectTransform, Sprite (+6 more)

### Community 91 - "TrackProfile"
Cohesion: 0.20
Nodes (6): float, Vector3, Settings, TrackProfile, Test, TrackProfileTests

### Community 92 - "com.unity.nuget.newtonsoft-json"
Cohesion: 0.15
Nodes (13): com.unity.nuget.newtonsoft-json, dependencies, depth, source, url, version, dependencies, depth (+5 more)

### Community 93 - ".Rank"
Cohesion: 0.44
Nodes (3): List, Test, RaceRankingTests

### Community 94 - "CliffMeshGen"
Cohesion: 0.26
Nodes (8): Action, bool, Color32, float, Mesh, Vector2, Vector3, CliffMeshGen

### Community 95 - "WebZipPostprocessor"
Cohesion: 0.13
Nodes (11): BuildReport, MenuItem, BuildCommands, BuildReport, long, WebZipPostprocessor, List, MenuItem (+3 more)

### Community 96 - "EnvironmentBaker"
Cohesion: 0.12
Nodes (14): Action, GameObject, Material, MenuItem, Mesh, string, Transform, Vector2 (+6 more)

### Community 97 - "2026-09-02 — M0 (частично), M1, M2, M3 (код)"
Cohesion: 0.05
Nodes (43): 2026-09-02/03 (продолжение) — M7–M9 сделаны, WebGL-модуль установлен, 2026-09-02 — M0 (частично), M1, M2, M3 (код), 2026-09-02 (вечер) — мост разблокировался сам, M1–M3 проверены в редакторе, M4 сделан, 2026-09-02 (ночь) — Codex-ревью M4 закрыто, M5 написан, 2026-09-10 — мидкор-план и адверсариальная проверка, 2026-09-16 — план вне мидкора (S0–S10), этапы S1–S7 написаны офлайн, 2026-09-18 — 2026-09-21 — работа вне журнала (восстановлено по коммитам), 2026-09-21 — проверка в редакторе (мост работает) (+35 more)

### Community 98 - "com.unity.modules.imageconversion"
Cohesion: 0.09
Nodes (23): dependencies, depth, source, version, dependencies, depth, source, version (+15 more)

### Community 99 - "MenuCameraDirector"
Cohesion: 0.09
Nodes (15): Camera, float, Rigidbody, Transform, Vector3, CarCameraFollow, bool, Camera (+7 more)

### Community 100 - "RaceParticipant"
Cohesion: 0.14
Nodes (7): bool, float, GameObject, int, string, RaceParticipant, Snapshot

### Community 101 - "RaceHarness"
Cohesion: 0.10
Nodes (18): AiDriver, bool, Collision, float, IEnumerator, int, List, RaceProgress (+10 more)

### Community 102 - "TrackPath"
Cohesion: 0.18
Nodes (6): bool, float, Quaternion, Vector3, TrackPath, Settings

### Community 103 - "RaceSpawner"
Cohesion: 0.17
Nodes (9): bool, Color, float, GameObject, int, IReadOnlyList, List, string (+1 more)

### Community 104 - "PlatformRuntime"
Cohesion: 0.13
Nodes (9): Dictionary, float, int, List, string, Pending, PlatformRuntime, Preserve (+1 more)

### Community 105 - "Technical Design"
Cohesion: 0.07
Nodes (26): Architecture Diagram, Components, Current Implementation, Delivery Steps, Edge Cases, File Structure, Functional Requirements, In Scope (+18 more)

### Community 106 - ".SecondsToTicks"
Cohesion: 0.25
Nodes (5): CarStatus, GameObject, TearDown, Test, CarStatusTests

### Community 107 - "Projectile"
Cohesion: 0.15
Nodes (11): bool, Collider, Collision, float, GameObject, int, List, Renderer (+3 more)

### Community 108 - "AiDriver"
Cohesion: 0.15
Nodes (6): bool, float, int, Vector3, AiDriver, RaycastHit

### Community 109 - "PlayerEffects"
Cohesion: 0.16
Nodes (8): bool, Collision, EmissionModule, float, int, Light, ParticleSystem, PlayerEffects

### Community 110 - "ArenaLayout"
Cohesion: 0.20
Nodes (7): float, Material, Quaternion, Vector3, ArenaLayout, Quaternion, Vector3

### Community 111 - "SimulationDriver"
Cohesion: 0.15
Nodes (5): ISimulated, bool, float, List, SimulationDriver

### Community 112 - "com.unity.test-framework"
Cohesion: 0.09
Nodes (24): com.unity.collections, com.unity.test-framework.performance, dependencies, depth, source, version, dependencies, depth (+16 more)

### Community 113 - "PerfectStart"
Cohesion: 0.18
Nodes (5): bool, Color, double, float, PerfectStart

### Community 114 - "WorldHealthBars"
Cohesion: 0.21
Nodes (8): Canvas, float, Image, List, RectTransform, Vector2, Entry, WorldHealthBars

### Community 115 - "com.unity.nuget.mono-cecil"
Cohesion: 0.11
Nodes (19): com.unity.2d.sprite, com.unity.nuget.mono-cecil, dependencies, depth, source, version, dependencies, depth (+11 more)

### Community 116 - "RacingGame.Core.Sim"
Cohesion: 0.18
Nodes (5): float, RubberBand, Test, RubberBandTests, RacingGame.Core.Sim

### Community 117 - "PropLayout.cs"
Cohesion: 0.38
Nodes (9): float, int, List, Vector3, GrandstandPlacement, LampPlacement, PropLayout, TireStackPlacement (+1 more)

### Community 118 - "SfxPlayer"
Cohesion: 0.22
Nodes (6): AudioClip, AudioSource, float, int, Vector3, SfxPlayer

### Community 119 - "RacingGame.Tests"
Cohesion: 0.16
Nodes (4): UiInteractionTests, RacingGame.Core.Economy, RacingGame.Tests, RacingGame.Core.Track

### Community 120 - "com.unity.ugui"
Cohesion: 0.17
Nodes (12): dependencies, depth, source, version, dependencies, depth, source, url (+4 more)

### Community 121 - "AiGunner"
Cohesion: 0.14
Nodes (8): float, AiDriverProfile, bool, float, int, AiGunner, float, ArenaDriver

### Community 122 - "Envelope"
Cohesion: 0.33
Nodes (7): bool, int, long, string, Envelope, LeaderboardEntryDto, SessionList

### Community 123 - "CarPaint"
Cohesion: 0.21
Nodes (7): Color, float, int, List, MaterialPropertyBlock, string, CarPaint

### Community 125 - "RacingGame.Core.Platform"
Cohesion: 0.18
Nodes (5): PlatformBootstrap, RacingGame.Platform, RacingGame.Platform.Yandex, RacingGame.Core.Platform, RacingGame.Platform.Mock

### Community 126 - "CarWeaponsTests"
Cohesion: 0.14
Nodes (7): CarStatus, CarWeapons, GameObject, SetUp, TearDown, Test, CarWeaponsTests

### Community 127 - "OpponentDriver"
Cohesion: 0.29
Nodes (5): float, int, Transform, Vector3, OpponentDriver

### Community 128 - ".BuildGarage"
Cohesion: 0.14
Nodes (12): float, int, Economy, Effects, UpgradeCatalog, UpgradeKind, WeaponCatalog, WeaponEffects (+4 more)

### Community 129 - "PlayerDriverInput"
Cohesion: 0.17
Nodes (7): int, EdgeLatch, bool, float, InputAction, PlayerDriverInput, InputActionMap

### Community 130 - "RaceResultEntry"
Cohesion: 0.33
Nodes (7): bool, double, int, List, string, RaceResultEntry, RaceResults

### Community 131 - "TargetCar"
Cohesion: 0.12
Nodes (8): GameObject, Transform, bool, float, Quaternion, Rigidbody, Vector3, TargetCar

### Community 132 - "codex-gpt-6-astra-max.md"
Cohesion: 0.40
Nodes (4): 1. Список проблем, 2. Чего не хватает, 3. Что вырезать первым при 4–6 неделях на одного разработчика, 4. Ответы на открытые вопросы

### Community 133 - "cursor-grok-4.6.md"
Cohesion: 0.40
Nodes (4): 1. Проблемы, 2. Чего не хватает, 3. Что вырезать первым (4–6 недель, один разработчик), 4. Ответы на §13

### Community 134 - "opencode-big-pickle.md"
Cohesion: 0.40
Nodes (4): 1. Список проблем (по серьёзности), 2. Чего не хватает, 3. Что вырезать первым (при 4–6 неделях на одного разработчика), 4. Ответы на открытые вопросы (§13)

### Community 135 - "opencode-mimo-v2.5-free.md"
Cohesion: 0.40
Nodes (4): 1. Список проблем (по серьёзности), 2. Чего не хватает, 3. Что вырезать первым (при 4-6 неделях на 1 разработчика), 4. Ответы на открытые вопросы (§13)

### Community 136 - ".ArenaSupportsSpawningPursuitAndUnoccupiedRecovery"
Cohesion: 0.22
Nodes (7): CarController, IEnumerator, Transform, UnityTest, Vector3, ArenaTests, UnityTearDown

### Community 137 - "CarStatus"
Cohesion: 0.18
Nodes (4): StatusKind, float, int, CarStatus

### Community 138 - "opencode-nemotron-3-ultra.md"
Cohesion: 0.40
Nodes (4): 1. Список проблем (по серьёзности), 2. Чего не хватает (до 8 пунктов), 3. Что вырезать первым при 4–6 неделях на одного разработчика, 4. Ответы на 6 открытых вопросов (§13)

### Community 140 - "RacingGame.Core.Save"
Cohesion: 0.17
Nodes (4): ILocalSaveStore, string, PlayerPrefsSaveStore, RacingGame.Core.Save

### Community 141 - ".ApplyHit"
Cohesion: 0.22
Nodes (6): CarHealth, CarStatus, GameObject, TearDown, Test, CarHealthHitTests

### Community 142 - "RacingGame.EditorTools"
Cohesion: 0.13
Nodes (6): MenuItem, RuntimeWiring, MenuItem, string, FontAtlasBaker, RacingGame.EditorTools

### Community 144 - "com.unity.modules.assetbundle"
Cohesion: 0.08
Nodes (27): com.unity.addressables, com.unity.profiling.core, com.unity.scriptablebuildpipeline, dependencies, depth, source, url, version (+19 more)

### Community 146 - "SaveServiceTests"
Cohesion: 0.32
Nodes (3): Task, Test, SaveServiceTests

### Community 147 - "RaceClockTests"
Cohesion: 0.20
Nodes (6): float, RaceClock, SetUp, TearDown, Test, RaceClockTests

### Community 148 - "MineHazardTests"
Cohesion: 0.15
Nodes (9): CarController, CarHealth, CarStatus, GameObject, MineHazard, RaceDirector, TearDown, Test (+1 more)

### Community 149 - "SpeedometerGraphic"
Cohesion: 0.41
Nodes (6): Color, float, Vector2, SpeedometerGraphic, MaskableGraphic, VertexHelper

### Community 150 - "CarDamageSmoke"
Cohesion: 0.21
Nodes (6): EmissionModule, float, GameObject, int, ParticleSystem, CarDamageSmoke

### Community 156 - "PickupSpawner"
Cohesion: 0.24
Nodes (5): bool, float, int, List, PickupSpawner

### Community 157 - "SfxSynth"
Cohesion: 0.32
Nodes (5): AudioClip, Func, int, Random, SfxSynth

### Community 158 - ".Range"
Cohesion: 0.26
Nodes (6): Random, RngStreams, Stream, Test, RngStreamsTests, Stream

### Community 159 - "SaveService"
Cohesion: 0.25
Nodes (7): bool, double, float, Func, string, Task, SaveService

### Community 160 - "SaveData"
Cohesion: 0.17
Nodes (14): bool, double, float, int, List, long, string, DeviceLedger (+6 more)

### Community 161 - ".GetSlot"
Cohesion: 0.33
Nodes (4): float, Quaternion, Vector3, StartGrid

### Community 162 - "TouchControlsPresenter"
Cohesion: 0.20
Nodes (5): bool, Canvas, Transform, Vector2, TouchControlsPresenter

### Community 163 - ".Parse"
Cohesion: 0.31
Nodes (4): SaveMigrator, SaveParseStatus, Test, SaveMigratorTests

### Community 165 - "DriveCommand"
Cohesion: 0.14
Nodes (7): bool, float, int, DriveCommand, ICarInputSource, Test, DriveCommandTests

### Community 167 - "Карточка игры в консоли Яндекс Игр (черновик)"
Cohesion: 0.29
Nodes (6): Заметки для модератора, Карточка игры в консоли Яндекс Игр (черновик), Короткое описание (до 60 символов), Медиа, Основное, Полное описание

### Community 168 - "SpeedometerTests"
Cohesion: 0.33
Nodes (3): Test, SpeedometerTests, TestCase

### Community 170 - "SkyPresetFactory"
Cohesion: 0.25
Nodes (6): Action, Color, MenuItem, string, StringBuilder, SkyPresetFactory

### Community 171 - "Q: Как продолжать развивать игру?"
Cohesion: 0.40
Nodes (4): Answer, Outcome, Q: Как продолжать развивать игру?, Source Nodes

### Community 172 - "Проверка сборки на реальном SDK Яндекса"
Cohesion: 0.40
Nodes (4): Известные ограничения, Проверка сборки на реальном SDK Яндекса, Список проверки, Что должен сделать пользователь в консоли разработчика

### Community 175 - "MockCloudSave"
Cohesion: 0.25
Nodes (5): bool, int, string, MockCloudSave, MockLocalStore

### Community 180 - "HitInfo"
Cohesion: 0.25
Nodes (6): float, GameObject, int, Vector3, HitInfo, HitType

### Community 182 - "RacingGame.World.Gen"
Cohesion: 0.15
Nodes (8): Color32, Mesh, Vector2, Vector3, MountainRingGen, int, PropNoise, RacingGame.World.Gen

### Community 184 - "HitResult"
Cohesion: 0.50
Nodes (3): bool, float, HitResult

### Community 185 - "GhostRecord"
Cohesion: 0.22
Nodes (7): int, List, string, GhostRecord, Test, GhostRecordTests, Sample

### Community 186 - "CarWeapons"
Cohesion: 0.18
Nodes (7): bool, float, int, Rigidbody, CarWeapons, PrimaryWeaponType, SecondaryWeaponType

### Community 187 - "RaceDirectorPoolTests"
Cohesion: 0.16
Nodes (8): CarController, CarHealth, CarStatus, GameObject, RaceDirector, TearDown, Test, RaceDirectorPoolTests

### Community 188 - ".ApplyTo"
Cohesion: 0.17
Nodes (7): PlayerProgression, GameObject, SetUp, TearDown, Test, PlayerProgressionTests, Effects

### Community 189 - "RaceEventDefinition"
Cohesion: 0.17
Nodes (8): bool, float, int, string, EventMode, RaceEventDefinition, RaceSetup, GhostSession

### Community 190 - "Pickup"
Cohesion: 0.22
Nodes (7): Collider, float, Material, Transform, Vector3, Pickup, PickupKind

### Community 191 - "RaceDirector"
Cohesion: 0.17
Nodes (6): GameObject, int, IReadOnlyList, List, Transform, RaceDirector

### Community 192 - "KillTally"
Cohesion: 0.29
Nodes (4): int, KillTally, Test, KillTallyTests

### Community 193 - "PropBaker"
Cohesion: 0.21
Nodes (8): Color32, float, List, MenuItem, string, Vector3, Chunk, PropBaker

### Community 194 - "MineHazard"
Cohesion: 0.21
Nodes (7): bool, float, GameObject, int, Renderer, Transform, MineHazard

### Community 196 - "EngineAudio"
Cohesion: 0.24
Nodes (6): AudioClip, AudioSource, bool, float, int, EngineAudio

### Community 197 - "ProjectileSpawn"
Cohesion: 0.20
Nodes (8): ProjectileKind, float, GameObject, int, Material, Transform, Vector3, ProjectileSpawn

### Community 198 - "RankEntry"
Cohesion: 0.36
Nodes (6): bool, double, int, RaceRanking, RankEntry, IComparer

### Community 199 - "GhostRecorder"
Cohesion: 0.25
Nodes (3): int, Transform, GhostRecorder

### Community 200 - "MineSpawn"
Cohesion: 0.25
Nodes (7): float, GameObject, int, Material, Quaternion, Vector3, MineSpawn

### Community 202 - ".RegisterCar"
Cohesion: 0.29
Nodes (3): SetUp, SetUp, SetUp

### Community 204 - "InputActionAssetTests"
Cohesion: 0.38
Nodes (3): string, Test, InputActionAssetTests

### Community 205 - "CameraImpulse"
Cohesion: 0.33
Nodes (4): float, Quaternion, Vector3, CameraImpulse

### Community 206 - "ScriptedDriverInput"
Cohesion: 0.33
Nodes (3): bool, float, ScriptedDriverInput

### Community 207 - "IRespawnPoseProvider"
Cohesion: 0.40
Nodes (3): Quaternion, Vector3, IRespawnPoseProvider

### Community 208 - "StatusEffect"
Cohesion: 0.50
Nodes (3): float, int, StatusEffect

## Ambiguous Edges - Review These
- `Official Unity AI/MCP Package` → `Runtime and Play Mode Automation`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to
- `Linux Standalone Editor Target` → `Target Platforms`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to
- `Mobile Platform Settings` → `Target Platforms`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to

## Knowledge Gaps
- **473 isolated node(s):** `Stream`, `com.unity.ai.assistant`, `com.unity.ai.navigation`, `com.unity.collab-proxy`, `com.unity.ide.rider` (+468 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **61 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `Official Unity AI/MCP Package` and `Runtime and Play Mode Automation`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **What is the exact relationship between `Linux Standalone Editor Target` and `Target Platforms`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **What is the exact relationship between `Mobile Platform Settings` and `Target Platforms`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `TrackPath` connect `TrackPath` to `.GetSlot`, `MenuCameraDirector`, `MonoBehaviour`, `RaceSpawner`, `AiDriver`, `GameFlow`, `TrackSpline`, `RaceProgress`, `TrackProfile`, `PickupSpawner`?**
  _High betweenness centrality (0.075) - this node is a cross-community bridge._
- **Why does `TrackSpline` connect `TrackSpline` to `ChasmLayout`, `TrackPath`, `EnvironmentGenTests`, `RacingGame.Tests`, `.Build`, `TrackProfile`, `TrackSplineTests`?**
  _High betweenness centrality (0.066) - this node is a cross-community bridge._
- **Why does `PlatformGlue` connect `PlatformGlue` to `MonoBehaviour`, `RaceManager`, `PauseReason`, `YandexPlatform`, `SaveService`?**
  _High betweenness centrality (0.057) - this node is a cross-community bridge._
- **What connects `Stream`, `com.unity.ai.assistant`, `com.unity.ai.navigation` to the rest of the system?**
  _473 weakly-connected nodes found - possible documentation gaps or missing edges._