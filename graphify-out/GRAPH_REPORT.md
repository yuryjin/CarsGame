# Graph Report - CarsGame  (2026-09-19)

## Corpus Check
- 141 files · ~94,147 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2264 nodes · 4069 edges · 173 communities (121 shown, 52 thin omitted)
- Extraction: 96% EXTRACTED · 4% INFERRED · 0% AMBIGUOUS · INFERRED: 177 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `fc140c70`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- com.unity.modules.audio
- RacingGame.World
- .Resolve
- Result
- com.unity.burst
- com.unity.modules.unitywebrequest
- com.unity.ext.nunit
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
- GhostRecord
- UiButtonFx
- RaceProgress
- Rigidbody Vehicle Control
- UpgradeCatalog
- StandalonePlatform
- DebugBus
- Target Platforms
- com.unity.ai.assistant
- com.unity.ai.navigation
- cars-platform.js
- com.unity.ide.rider
- com.unity.ide.visualstudio
- com.unity.inputsystem
- com.unity.modules.accessibility
- PlayerDriverInput
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
- YandexPlatform
- HudScreen
- MeshChunkWriter
- AiDriver
- RaceManager
- EffectPool
- .BuildSleekSportsCar
- EditorMockPlatform
- TrackSpline
- .Build
- SkyDirector
- CarCannon
- RacingGame.Core.Platform
- MenuScreens
- CarsGame: мидкор-слой. Этапы и промпты для агента
- RacingGame.World.Gen
- .Panel
- PlatformRuntime
- com.unity.nuget.newtonsoft-json
- .Rank
- CliffMeshGen
- WebZipPostprocessor
- EnvironmentBaker
- 2026-09-02 — M0 (частично), M1, M2, M3 (код)
- com.unity.modules.imageconversion
- MenuCameraDirector
- RaceParticipant
- .BakeProps
- TrackPath
- RaceSpawner
- SaveData
- SaveService
- Pickup
- RaceDirector
- DriveCommand
- PlayerEffects
- TouchControlsPresenter
- SimulationDriver
- com.unity.test-framework
- GhostCar
- WorldHealthBars
- com.unity.nuget.mono-cecil
- com.unity.modules.terrain
- PlatformGlue
- SfxPlayer
- RacingGame.Tests
- com.unity.modules.imgui
- .Spawn
- BarrierTools
- CarPaint
- TrackSplineTests
- PauseReason
- LocImporter
- FontAtlasBaker
- .Get
- RacingGame
- RaceResults
- MonoBehaviour
- codex-gpt-6-astra-max.md
- cursor-grok-4.6.md
- opencode-big-pickle.md
- opencode-mimo-v2.5-free.md
- .Consolidate
- .Build
- opencode-nemotron-3-ultra.md
- RngStreams
- RaceEventDefinition
- .GetSlot
- Envelope
- dependencies
- SkyMath
- GhostRecorder
- RaceClock
- ChasmLayout
- SpeedometerGraphic
- CarDamageSmoke
- com.unity.modules.unitywebrequestwww
- com.unity.modules.xr
- com.unity.modules.particlesystem
- RacingGame.Core.Save
- SfxSynth
- AdPacer
- SkyPresetFactory
- .Merge
- EngineAudio
- CarCameraFollow
- PropLayout.cs
- PickupSpawner
- .CreateOverlayCanvas
- EnvironmentGenTests
- Карточка игры в консоли Яндекс Игр (черновик)
- SpeedometerTests
- PlatformFeature
- .FromJson
- .Find
- Проверка сборки на реальном SDK Яндекса

## God Nodes (most connected - your core abstractions)
1. `CarController` - 59 edges
2. `RacingGame` - 56 edges
3. `HudScreen` - 53 edges
4. `GameFlow` - 48 edges
5. `MenuScreens` - 42 edges
6. `Result` - 39 edges
7. `RaceManager` - 38 edges
8. `YandexPlatform` - 34 edges
9. `CarHealth` - 33 edges
10. `RaceParticipant` - 30 edges

## Surprising Connections (you probably didn't know these)
- `Unity 6000.5.9f1` --semantically_similar_to--> `Unity 6000.5.9f1 Revision b57deb96f08d`  [INFERRED] [semantically similar]
  Docs/AI/UnityProjectContext.md → ProjectSettings/ProjectVersion.txt
- `Unity Project Context` --references--> `Unity Project Version Record`  [EXTRACTED]
  Docs/AI/UnityProjectContext.md → ProjectSettings/ProjectVersion.txt
- `PlatformGlue` --references--> `AdPacer`  [EXTRACTED]
  Assets/CarsGame/Game/Flow/PlatformGlue.cs → Assets/CarsGame/Core/Ads/AdPacer.cs
- `GhostCar` --references--> `GhostRecord`  [EXTRACTED]
  Assets/CarsGame/Game/Ghost/GhostCar.cs → Assets/CarsGame/Core/Ghost/GhostRecord.cs
- `GhostRecorder` --references--> `GhostRecord`  [EXTRACTED]
  Assets/CarsGame/Game/Ghost/GhostRecorder.cs → Assets/CarsGame/Core/Ghost/GhostRecord.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Confirmed Unity Environment** — docs_ai_unityprojectcontext_unity_6000_5_9f1, docs_ai_unityprojectcontext_urp_17_5_0, docs_ai_unityprojectcontext_input_system_1_20_0 [EXTRACTED 1.00]
- **Gameplay Architecture Patterns** — docs_ai_unityprojectcontext_scene_composition, docs_ai_unityprojectcontext_rigidbody_vehicle_control, docs_ai_unityprojectcontext_track_markings_route, docs_ai_unityprojectcontext_procedural_car_generation [EXTRACTED 1.00]

## Communities (173 total, 52 thin omitted)

### Community 0 - "com.unity.modules.audio"
Cohesion: 0.07
Nodes (30): dependencies, depth, source, version, dependencies, depth, source, version (+22 more)

### Community 2 - ".Resolve"
Cohesion: 0.14
Nodes (8): HashSet, string, LanguageResolver, string, Test, LanguageResolverTests, RacingGame.Core.Localization, ICollection

### Community 3 - "Result"
Cohesion: 0.23
Nodes (6): Result, ResultCategory, Task, IReadOnlyList, Task, Envelope

### Community 4 - "com.unity.burst"
Cohesion: 0.11
Nodes (20): com.unity.burst, com.unity.collections, com.unity.mathematics, dependencies, depth, source, url, version (+12 more)

### Community 5 - "com.unity.modules.unitywebrequest"
Cohesion: 0.06
Nodes (34): dependencies, depth, source, version, dependencies, depth, source, version (+26 more)

### Community 6 - "com.unity.ext.nunit"
Cohesion: 0.15
Nodes (13): com.unity.ext.nunit, dependencies, depth, source, version, dependencies, depth, source (+5 more)

### Community 7 - "com.unity.render-pipelines.core"
Cohesion: 0.07
Nodes (29): com.unity.render-pipelines.core, com.unity.render-pipelines.universal-config, com.unity.searcher, com.unity.shadergraph, depth, source, version, dependencies (+21 more)

### Community 8 - "com.unity.modules.physics"
Cohesion: 0.12
Nodes (16): dependencies, depth, source, version, dependencies, depth, source, version (+8 more)

### Community 9 - "ReadmeEditor"
Cohesion: 0.12
Nodes (13): bool, float, string, ReadmeEditor, bool, string, Readme, Section (+5 more)

### Community 10 - "com.unity.modules.uielements"
Cohesion: 0.12
Nodes (16): dependencies, depth, source, url, version, depth, source, version (+8 more)

### Community 11 - "CarHealth"
Cohesion: 0.10
Nodes (10): bool, Collision, Color, float, GameObject, int, MaterialPropertyBlock, Renderer (+2 more)

### Community 12 - "com.unity.modules.jsonserialize"
Cohesion: 0.06
Nodes (33): com.unity.modules.subsystems, dependencies, depth, source, version, dependencies, depth, source (+25 more)

### Community 13 - "com.unity.modules.ui"
Cohesion: 0.11
Nodes (18): com.unity.modules.hierarchycore, dependencies, depth, source, version, dependencies, depth, source (+10 more)

### Community 14 - "YandexNative"
Cohesion: 0.12
Nodes (4): YandexNative, RacingGame.Platform.Yandex, DllImport, IntPtr

### Community 15 - "CarController"
Cohesion: 0.08
Nodes (14): Quaternion, Vector3, IRespawnPoseProvider, AudioSource, bool, float, GameObject, int (+6 more)

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

### Community 22 - "GhostRecord"
Cohesion: 0.22
Nodes (7): int, List, string, GhostRecord, Test, GhostRecordTests, Sample

### Community 23 - "UiButtonFx"
Cohesion: 0.12
Nodes (13): bool, Button, float, Vector3, UiButtonFx, BaseEventData, IDeselectHandler, IPointerDownHandler (+5 more)

### Community 24 - "RaceProgress"
Cohesion: 0.07
Nodes (16): bool, float, int, LapCounter, float, Vector3, Settings, TrackProfile (+8 more)

### Community 25 - "Rigidbody Vehicle Control"
Cohesion: 0.40
Nodes (5): Assembly-CSharp, Rigidbody Vehicle Control, SampleScene, MonoBehaviour Scene Composition, Track Markings Opponent Route

### Community 26 - "UpgradeCatalog"
Cohesion: 0.11
Nodes (13): float, int, Economy, Effects, UpgradeCatalog, UpgradeKind, int, KillTally (+5 more)

### Community 27 - "StandalonePlatform"
Cohesion: 0.17
Nodes (6): bool, string, Task, StandalonePlatform, WebMockPlatform, SystemLanguage

### Community 28 - "DebugBus"
Cohesion: 0.15
Nodes (10): bool, float, int, long, string, Command, DebugBus, Perf (+2 more)

### Community 29 - "Target Platforms"
Cohesion: 0.67
Nodes (3): Linux Standalone Editor Target, Mobile Platform Settings, Target Platforms

### Community 32 - "cars-platform.js"
Cohesion: 0.13
Nodes (35): attach(), boot(), byteLength(), cmd(), debugState(), detectDevice(), drainOnce(), env() (+27 more)

### Community 37 - "PlayerDriverInput"
Cohesion: 0.11
Nodes (12): int, EdgeLatch, bool, float, InputAction, PlayerDriverInput, bool, float (+4 more)

### Community 46 - "GameFlow"
Cohesion: 0.13
Nodes (9): bool, Color, InputAction, int, List, string, FlowState, GameFlow (+1 more)

### Community 73 - "Q: Проследи"
Cohesion: 0.40
Nodes (4): Answer, Outcome, Q: Проследи, Source Nodes

### Community 74 - "YandexPlatform"
Cohesion: 0.10
Nodes (22): bool, int, IReadOnlyList, long, string, Task, AdResult, DeviceKind (+14 more)

### Community 75 - "HudScreen"
Cohesion: 0.07
Nodes (16): bool, Button, CanvasGroup, Color, float, GameObject, Image, int (+8 more)

### Community 76 - "MeshChunkWriter"
Cohesion: 0.19
Nodes (13): Color32, Matrix4x4, Vector2, Vector3, LowPoly, Color32, int, List (+5 more)

### Community 77 - "AiDriver"
Cohesion: 0.11
Nodes (12): bool, float, int, Vector3, AiDriver, float, AiDriverProfile, bool (+4 more)

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
Cohesion: 0.17
Nodes (8): bool, Dictionary, float, IReadOnlyList, Random, string, Task, EditorMockPlatform

### Community 82 - "TrackSpline"
Cohesion: 0.27
Nodes (6): float, Vector3, TrackSpline, d, IList, sign

### Community 83 - ".Build"
Cohesion: 0.15
Nodes (11): int, PropNoise, Color32, float, int, List, Mesh, Vector2 (+3 more)

### Community 84 - "SkyDirector"
Cohesion: 0.14
Nodes (13): Color, Cubemap, int, Light, Material, SkyDirector, bool, Color (+5 more)

### Community 85 - "CarCannon"
Cohesion: 0.16
Nodes (9): bool, Color, float, int, Material, Rigidbody, Transform, Vector3 (+1 more)

### Community 86 - "RacingGame.Core.Platform"
Cohesion: 0.17
Nodes (5): PlatformBootstrap, RacingGame.Platform, RacingGame.Core.Platform, RacingGame.Platform.Mock, RacingGame.Core.Ads

### Community 87 - "MenuScreens"
Cohesion: 0.13
Nodes (16): Action, bool, Button, Canvas, CanvasGroup, Color, float, int (+8 more)

### Community 88 - "CarsGame: мидкор-слой. Этапы и промпты для агента"
Cohesion: 0.12
Nodes (15): CarsGame: мидкор-слой. Этапы и промпты для агента, После MVP (короткие промпты, по одному этапу каждый), Преамбула (вставлять в начало каждого промпта), Этап 0. Тики в CarHealth, единый результат попадания, потолки пулов (P0a, ~4 дня), Этап 10. Реальный SDK Яндекса и память WebGL (P4b, ~3 дня), Этап 11. Плейтест-резерв (5–7 дней, без нового контента), Этап 1. Замер базовой производительности WebGL (P0b, ~2 дня), Этап 2. Система статусов и щит (P1a, ~3 дня) (+7 more)

### Community 89 - "RacingGame.World.Gen"
Cohesion: 0.15
Nodes (4): MenuItem, RuntimeWiring, RacingGame.World.Gen, RacingGame.EditorTools

### Community 90 - ".Panel"
Cohesion: 0.18
Nodes (14): Transform, Action, Button, Color, Image, RectTransform, Sprite, TextAlignmentOptions (+6 more)

### Community 91 - "PlatformRuntime"
Cohesion: 0.14
Nodes (9): Dictionary, float, int, List, string, Pending, PlatformRuntime, Preserve (+1 more)

### Community 92 - "com.unity.nuget.newtonsoft-json"
Cohesion: 0.11
Nodes (19): com.unity.nuget.newtonsoft-json, dependencies, depth, source, url, version, dependencies, depth (+11 more)

### Community 93 - ".Rank"
Cohesion: 0.23
Nodes (9): bool, double, int, List, RaceRanking, RankEntry, Test, RaceRankingTests (+1 more)

### Community 94 - "CliffMeshGen"
Cohesion: 0.23
Nodes (9): Action, bool, Color32, float, List, Mesh, Vector2, Vector3 (+1 more)

### Community 95 - "WebZipPostprocessor"
Cohesion: 0.19
Nodes (7): BuildReport, MenuItem, BuildCommands, BuildReport, long, WebZipPostprocessor, IPostprocessBuildWithReport

### Community 96 - "EnvironmentBaker"
Cohesion: 0.19
Nodes (9): Action, GameObject, Material, MenuItem, Mesh, string, Transform, Vector2 (+1 more)

### Community 97 - "2026-09-02 — M0 (частично), M1, M2, M3 (код)"
Cohesion: 0.07
Nodes (29): 2026-09-02/03 (продолжение) — M7–M9 сделаны, WebGL-модуль установлен, 2026-09-02 — M0 (частично), M1, M2, M3 (код), 2026-09-02 (вечер) — мост разблокировался сам, M1–M3 проверены в редакторе, M4 сделан, 2026-09-02 (ночь) — Codex-ревью M4 закрыто, M5 написан, 2026-09-10 — мидкор-план и адверсариальная проверка, 2026-09-16 — план вне мидкора (S0–S10), этапы S1–S7 написаны офлайн, CarsGame — журнал выполнения плана, Codex-ревью M5 закрыто (6 находок) (+21 more)

### Community 98 - "com.unity.modules.imageconversion"
Cohesion: 0.12
Nodes (17): dependencies, depth, source, version, dependencies, depth, source, version (+9 more)

### Community 99 - "MenuCameraDirector"
Cohesion: 0.15
Nodes (9): bool, Camera, float, int, Quaternion, Vector3, MenuCameraDirector, Shot (+1 more)

### Community 100 - "RaceParticipant"
Cohesion: 0.10
Nodes (10): ArenaLayout, bool, float, GameObject, int, Quaternion, string, Vector3 (+2 more)

### Community 101 - ".BakeProps"
Cohesion: 0.21
Nodes (9): Color32, float, List, Material, MenuItem, string, Vector3, Chunk (+1 more)

### Community 102 - "TrackPath"
Cohesion: 0.19
Nodes (6): bool, float, Quaternion, Vector3, TrackPath, Settings

### Community 103 - "RaceSpawner"
Cohesion: 0.16
Nodes (10): ArenaLayout, bool, Color, float, GameObject, int, IReadOnlyList, List (+2 more)

### Community 104 - "SaveData"
Cohesion: 0.17
Nodes (14): bool, double, float, int, List, long, string, DeviceLedger (+6 more)

### Community 105 - "SaveService"
Cohesion: 0.16
Nodes (7): bool, double, float, Func, string, Task, SaveService

### Community 106 - "Pickup"
Cohesion: 0.21
Nodes (7): Collider, float, Material, Transform, Vector3, Pickup, PickupKind

### Community 107 - "RaceDirector"
Cohesion: 0.06
Nodes (23): float, GameObject, int, Material, Vector3, ProjectileSpawn, GameObject, int (+15 more)

### Community 108 - "DriveCommand"
Cohesion: 0.12
Nodes (10): bool, float, int, DriveCommand, ICarInputSource, float, int, Transform (+2 more)

### Community 109 - "PlayerEffects"
Cohesion: 0.11
Nodes (12): float, Quaternion, Vector3, CameraImpulse, bool, Collision, EmissionModule, float (+4 more)

### Community 110 - "TouchControlsPresenter"
Cohesion: 0.27
Nodes (3): bool, Canvas, TouchControlsPresenter

### Community 111 - "SimulationDriver"
Cohesion: 0.18
Nodes (5): ISimulated, bool, float, List, SimulationDriver

### Community 112 - "com.unity.test-framework"
Cohesion: 0.11
Nodes (18): com.unity.test-framework.performance, dependencies, depth, source, url, version, depth, dependencies (+10 more)

### Community 113 - "GhostCar"
Cohesion: 0.22
Nodes (4): bool, double, GhostCar, RacingGame.Core.Ghost

### Community 114 - "WorldHealthBars"
Cohesion: 0.19
Nodes (8): Canvas, float, Image, List, RectTransform, Vector2, Entry, WorldHealthBars

### Community 115 - "com.unity.nuget.mono-cecil"
Cohesion: 0.11
Nodes (19): com.unity.2d.sprite, com.unity.nuget.mono-cecil, dependencies, depth, source, version, dependencies, depth (+11 more)

### Community 116 - "com.unity.modules.terrain"
Cohesion: 0.18
Nodes (11): dependencies, depth, source, version, dependencies, depth, source, version (+3 more)

### Community 117 - "PlatformGlue"
Cohesion: 0.18
Nodes (3): bool, Task, PlatformGlue

### Community 118 - "SfxPlayer"
Cohesion: 0.22
Nodes (6): AudioClip, AudioSource, float, int, Vector3, SfxPlayer

### Community 119 - "RacingGame.Tests"
Cohesion: 0.22
Nodes (3): PlayerProgression, RacingGame.Core.Economy, RacingGame.Tests

### Community 120 - "com.unity.modules.imgui"
Cohesion: 0.12
Nodes (17): dependencies, depth, source, version, dependencies, depth, source, version (+9 more)

### Community 121 - ".Spawn"
Cohesion: 0.22
Nodes (6): float, Vector3, Sample, Color, GameObject, byte

### Community 122 - "BarrierTools"
Cohesion: 0.26
Nodes (4): MenuItem, Transform, Vector2, BarrierTools

### Community 123 - "CarPaint"
Cohesion: 0.21
Nodes (7): Color, float, int, List, MaterialPropertyBlock, string, CarPaint

### Community 126 - "LocImporter"
Cohesion: 0.33
Nodes (4): List, MenuItem, string, LocImporter

### Community 127 - "FontAtlasBaker"
Cohesion: 0.40
Nodes (3): MenuItem, string, FontAtlasBaker

### Community 128 - ".Get"
Cohesion: 0.18
Nodes (6): GameObject, Dictionary, HashSet, int, string, Loc

### Community 130 - "RaceResults"
Cohesion: 0.33
Nodes (7): bool, double, int, List, string, RaceResultEntry, RaceResults

### Community 131 - "MonoBehaviour"
Cohesion: 0.14
Nodes (9): Material, WorldAssets, bool, float, Quaternion, Rigidbody, Vector3, TargetCar (+1 more)

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

### Community 136 - ".Consolidate"
Cohesion: 0.32
Nodes (4): Color32, MenuItem, SceneConsolidator, PhysicsMaterial

### Community 137 - ".Build"
Cohesion: 0.38
Nodes (5): Color32, Mesh, Vector2, Vector3, MountainRingGen

### Community 138 - "opencode-nemotron-3-ultra.md"
Cohesion: 0.40
Nodes (4): 1. Список проблем (по серьёзности), 2. Чего не хватает (до 8 пунктов), 3. Что вырезать первым при 4–6 неделях на одного разработчика, 4. Ответы на 6 открытых вопросов (§13)

### Community 140 - "RngStreams"
Cohesion: 0.38
Nodes (4): Random, RngStreams, Stream, Stream

### Community 141 - "RaceEventDefinition"
Cohesion: 0.13
Nodes (13): SaveService, float, int, string, DifficultyTier, bool, float, int (+5 more)

### Community 142 - ".GetSlot"
Cohesion: 0.33
Nodes (4): float, Quaternion, Vector3, StartGrid

### Community 143 - "Envelope"
Cohesion: 0.33
Nodes (7): bool, int, long, string, Envelope, LeaderboardEntryDto, SessionList

### Community 144 - "dependencies"
Cohesion: 0.10
Nodes (21): com.unity.addressables, com.unity.profiling.core, com.unity.scriptablebuildpipeline, dependencies, depth, source, url, version (+13 more)

### Community 145 - "SkyMath"
Cohesion: 0.23
Nodes (7): Color, Cubemap, float, Vector3, SkyMath, SkyParams, CubemapFace

### Community 146 - "GhostRecorder"
Cohesion: 0.18
Nodes (4): int, Transform, GhostRecorder, GhostSession

### Community 148 - "ChasmLayout"
Cohesion: 0.23
Nodes (6): float, Matrix4x4, Vector2, Vector3, ChasmLayout, ChasmSettings

### Community 149 - "SpeedometerGraphic"
Cohesion: 0.36
Nodes (6): Color, float, Vector2, SpeedometerGraphic, MaskableGraphic, VertexHelper

### Community 150 - "CarDamageSmoke"
Cohesion: 0.19
Nodes (6): EmissionModule, float, GameObject, int, ParticleSystem, CarDamageSmoke

### Community 156 - "RacingGame.Core.Save"
Cohesion: 0.18
Nodes (4): ILocalSaveStore, string, PlayerPrefsSaveStore, RacingGame.Core.Save

### Community 157 - "SfxSynth"
Cohesion: 0.32
Nodes (5): AudioClip, Func, int, Random, SfxSynth

### Community 158 - "AdPacer"
Cohesion: 0.35
Nodes (4): float, AdPacer, Test, AdPacerTests

### Community 159 - "SkyPresetFactory"
Cohesion: 0.25
Nodes (6): Action, Color, MenuItem, string, StringBuilder, SkyPresetFactory

### Community 161 - "EngineAudio"
Cohesion: 0.24
Nodes (6): AudioClip, AudioSource, bool, float, int, EngineAudio

### Community 162 - "CarCameraFollow"
Cohesion: 0.22
Nodes (6): Camera, float, Rigidbody, Transform, Vector3, CarCameraFollow

### Community 163 - "PropLayout.cs"
Cohesion: 0.38
Nodes (9): float, int, List, Vector3, GrandstandPlacement, LampPlacement, PropLayout, TireStackPlacement (+1 more)

### Community 164 - "PickupSpawner"
Cohesion: 0.28
Nodes (5): bool, float, int, List, PickupSpawner

### Community 165 - ".CreateOverlayCanvas"
Cohesion: 0.29
Nodes (3): Transform, Vector2, Canvas

### Community 167 - "Карточка игры в консоли Яндекс Игр (черновик)"
Cohesion: 0.29
Nodes (6): Заметки для модератора, Карточка игры в консоли Яндекс Игр (черновик), Короткое описание (до 60 символов), Медиа, Основное, Полное описание

### Community 168 - "SpeedometerTests"
Cohesion: 0.33
Nodes (3): Test, SpeedometerTests, TestCase

### Community 171 - ".Find"
Cohesion: 0.40
Nodes (3): int, Vector3, TargetFinder

### Community 172 - "Проверка сборки на реальном SDK Яндекса"
Cohesion: 0.40
Nodes (4): Известные ограничения, Проверка сборки на реальном SDK Яндекса, Список проверки, Что должен сделать пользователь в консоли разработчика

## Ambiguous Edges - Review These
- `Official Unity AI/MCP Package` → `Runtime and Play Mode Automation`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to
- `Linux Standalone Editor Target` → `Target Platforms`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to
- `Mobile Platform Settings` → `Target Platforms`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to

## Knowledge Gaps
- **429 isolated node(s):** `Stream`, `com.unity.ai.assistant`, `com.unity.ai.navigation`, `com.unity.collab-proxy`, `com.unity.ide.rider` (+424 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **52 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `Official Unity AI/MCP Package` and `Runtime and Play Mode Automation`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **What is the exact relationship between `Linux Standalone Editor Target` and `Target Platforms`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **What is the exact relationship between `Mobile Platform Settings` and `Target Platforms`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `TrackPath` connect `TrackPath` to `MenuCameraDirector`, `PickupSpawner`, `MonoBehaviour`, `RaceSpawner`, `AiDriver`, `GameFlow`, `.GetSlot`, `TrackSpline`, `RaceProgress`?**
  _High betweenness centrality (0.105) - this node is a cross-community bridge._
- **Why does `RacingGame` connect `RacingGame` to `RacingGame.World`, `.Resolve`, `RaceResults`, `MonoBehaviour`, `CarHealth`, `RaceEventDefinition`, `.GetSlot`, `CarController`, `RaceClock`, `SpeedometerGraphic`, `CarDamageSmoke`, `UiButtonFx`, `RaceProgress`, `SfxSynth`, `EngineAudio`, `CarCameraFollow`, `.Find`, `GameFlow`, `AiDriver`, `RaceManager`, `EffectPool`, `.BuildSleekSportsCar`, `CarCannon`, `RacingGame.Core.Platform`, `MenuCameraDirector`, `RaceSpawner`, `Pickup`, `RaceDirector`, `PlayerEffects`, `TouchControlsPresenter`, `SimulationDriver`, `GhostCar`, `WorldHealthBars`, `SfxPlayer`, `RacingGame.Tests`, `CarPaint`, `PauseReason`?**
  _High betweenness centrality (0.075) - this node is a cross-community bridge._
- **Why does `GameFlow` connect `GameFlow` to `.Get`, `EngineAudio`, `MenuCameraDirector`, `RaceParticipant`, `PickupSpawner`, `TrackPath`, `MonoBehaviour`, `HudScreen`, `RaceEventDefinition`, `RaceManager`, `CarController`, `GhostCar`, `GhostRecorder`, `SkyDirector`, `MenuScreens`, `DebugBus`, `PauseReason`?**
  _High betweenness centrality (0.072) - this node is a cross-community bridge._
- **What connects `Stream`, `com.unity.ai.assistant`, `com.unity.ai.navigation` to the rest of the system?**
  _429 weakly-connected nodes found - possible documentation gaps or missing edges._