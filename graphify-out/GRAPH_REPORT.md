# Graph Report - CarsGame  (2026-09-10)

## Corpus Check
- 126 files · ~76,682 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2014 nodes · 3498 edges · 152 communities (104 shown, 48 thin omitted)
- Extraction: 96% EXTRACTED · 4% INFERRED · 0% AMBIGUOUS · INFERRED: 135 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `3c5683bd`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- com.unity.modules.animation
- RacingGame.Tests
- PlatformGlue
- com.unity.nuget.mono-cecil
- com.unity.burst
- com.unity.modules.unitywebrequest
- com.unity.ext.nunit
- com.unity.render-pipelines.core
- com.unity.modules.physics
- ReadmeEditor
- com.unity.modules.uielements
- CarHealth
- com.unity.modules.jsonserialize
- com.unity.modules.audio
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
- YandexPlatform
- com.unity.modules.subsystems
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
- Result
- HudScreen
- MeshChunkWriter
- AiDriver
- RaceManager
- EffectPool
- .BuildSleekSportsCar
- EditorMockPlatform
- TrackPath
- .Build
- SkyDirector
- CarCannon
- RacingGame.Core.Platform
- MenuScreens
- CarsGame: мидкор-слой. Этапы и промпты для агента
- RacingGame.World.Gen
- .Text
- PlatformRuntime
- com.unity.nuget.newtonsoft-json
- RankEntry
- .Get
- WebZipPostprocessor
- .BakeProps
- 2026-09-02 — M0 (частично), M1, M2, M3 (код)
- com.unity.modules.imageconversion
- MenuCameraDirector
- RaceParticipant
- PropBaker
- AiGunner
- RaceSpawner
- SaveService
- Pickup
- RaceDirector
- DriveCommand
- PlayerEffects
- TouchControlsPresenter
- SimulationDriver
- .ResetCar
- GhostCar
- WorldHealthBars
- com.unity.mathematics
- com.unity.modules.terrain
- OpponentDriver
- SfxPlayer
- GameFlow.cs
- RaceEventDefinition
- .Spawn
- .Move
- CarPaint
- TrackSplineTests
- PauseReason
- LocImporter
- FontAtlasBaker
- Loc
- RacingGame
- RaceResults
- TargetCar
- codex-gpt-6-astra-max.md
- cursor-grok-4.6.md
- opencode-big-pickle.md
- opencode-mimo-v2.5-free.md
- .Consolidate
- .Build
- opencode-nemotron-3-ultra.md
- RngStreams
- DifficultyTier
- MonoBehaviour
- Envelope
- com.unity.test-framework
- GhostRecorder
- RaceClock
- com.unity.modules.unitywebrequestwww
- com.unity.modules.xr
- com.unity.modules.particlesystem

## God Nodes (most connected - your core abstractions)
1. `CarController` - 55 edges
2. `RacingGame` - 51 edges
3. `GameFlow` - 46 edges
4. `Result` - 39 edges
5. `RaceManager` - 37 edges
6. `MenuScreens` - 35 edges
7. `YandexPlatform` - 34 edges
8. `HudScreen` - 28 edges
9. `RaceParticipant` - 27 edges
10. `StandalonePlatform` - 27 edges

## Surprising Connections (you probably didn't know these)
- `Unity 6000.5.9f1` --semantically_similar_to--> `Unity 6000.5.9f1 Revision b57deb96f08d`  [INFERRED] [semantically similar]
  Docs/AI/UnityProjectContext.md → ProjectSettings/ProjectVersion.txt
- `Unity Project Context` --references--> `Unity Project Version Record`  [EXTRACTED]
  Docs/AI/UnityProjectContext.md → ProjectSettings/ProjectVersion.txt
- `GhostCar` --references--> `GhostRecord`  [EXTRACTED]
  Assets/CarsGame/Game/Ghost/GhostCar.cs → Assets/CarsGame/Core/Ghost/GhostRecord.cs
- `GhostRecorder` --references--> `GhostRecord`  [EXTRACTED]
  Assets/CarsGame/Game/Ghost/GhostRecorder.cs → Assets/CarsGame/Core/Ghost/GhostRecord.cs
- `EditorMockPlatform` --references--> `PlatformKind`  [EXTRACTED]
  Assets/CarsGame/Platform/Mock/EditorMockPlatform.cs → Assets/CarsGame/Core/Platform/Contracts.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Confirmed Unity Environment** — docs_ai_unityprojectcontext_unity_6000_5_9f1, docs_ai_unityprojectcontext_urp_17_5_0, docs_ai_unityprojectcontext_input_system_1_20_0 [EXTRACTED 1.00]
- **Gameplay Architecture Patterns** — docs_ai_unityprojectcontext_scene_composition, docs_ai_unityprojectcontext_rigidbody_vehicle_control, docs_ai_unityprojectcontext_track_markings_route, docs_ai_unityprojectcontext_procedural_car_generation [EXTRACTED 1.00]

## Communities (152 total, 48 thin omitted)

### Community 0 - "com.unity.modules.animation"
Cohesion: 0.09
Nodes (24): dependencies, depth, source, version, dependencies, depth, source, version (+16 more)

### Community 1 - "RacingGame.Tests"
Cohesion: 0.15
Nodes (3): RacingGame.Tests, RacingGame.World, RacingGame.Core.Track

### Community 2 - "PlatformGlue"
Cohesion: 0.07
Nodes (15): float, AdPacer, HashSet, string, LanguageResolver, bool, Task, PlatformGlue (+7 more)

### Community 3 - "com.unity.nuget.mono-cecil"
Cohesion: 0.15
Nodes (13): com.unity.nuget.mono-cecil, dependencies, depth, source, url, version, dependencies, depth (+5 more)

### Community 4 - "com.unity.burst"
Cohesion: 0.11
Nodes (20): com.unity.burst, com.unity.collections, com.unity.test-framework.performance, depth, source, url, version, dependencies (+12 more)

### Community 5 - "com.unity.modules.unitywebrequest"
Cohesion: 0.09
Nodes (23): dependencies, depth, source, version, dependencies, depth, source, version (+15 more)

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
Cohesion: 0.06
Nodes (34): com.unity.modules.hierarchycore, dependencies, depth, source, url, version, dependencies, depth (+26 more)

### Community 11 - "CarHealth"
Cohesion: 0.13
Nodes (9): bool, Collision, float, GameObject, int, MaterialPropertyBlock, Renderer, Rigidbody (+1 more)

### Community 12 - "com.unity.modules.jsonserialize"
Cohesion: 0.09
Nodes (22): dependencies, depth, source, version, dependencies, depth, source, version (+14 more)

### Community 13 - "com.unity.modules.audio"
Cohesion: 0.09
Nodes (23): dependencies, depth, source, version, dependencies, depth, source, version (+15 more)

### Community 14 - "YandexNative"
Cohesion: 0.10
Nodes (7): Task, YandexNative, IReadOnlyList, Task, DllImport, Envelope, IntPtr

### Community 15 - "CarController"
Cohesion: 0.10
Nodes (9): AudioSource, bool, float, GameObject, int, Rigidbody, Transform, CarController (+1 more)

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
Cohesion: 0.13
Nodes (9): bool, float, int, LapCounter, float, int, RaceProgress, Test (+1 more)

### Community 25 - "Rigidbody Vehicle Control"
Cohesion: 0.40
Nodes (5): Assembly-CSharp, Rigidbody Vehicle Control, SampleScene, MonoBehaviour Scene Composition, Track Markings Opponent Route

### Community 26 - "UpgradeCatalog"
Cohesion: 0.19
Nodes (9): float, int, Economy, Effects, UpgradeCatalog, UpgradeKind, Test, EconomyTests (+1 more)

### Community 27 - "YandexPlatform"
Cohesion: 0.12
Nodes (18): AdResult, DeviceKind, IAds, IAsyncSessions, ILeaderboards, IPayments, IPlatformServices, PlatformFeature (+10 more)

### Community 28 - "com.unity.modules.subsystems"
Cohesion: 0.12
Nodes (16): com.unity.modules.subsystems, dependencies, depth, source, version, dependencies, depth, source (+8 more)

### Community 29 - "Target Platforms"
Cohesion: 0.67
Nodes (3): Linux Standalone Editor Target, Mobile Platform Settings, Target Platforms

### Community 32 - "cars-platform.js"
Cohesion: 0.14
Nodes (32): attach(), boot(), byteLength(), detectDevice(), drainOnce(), env(), flushNow(), flushSave() (+24 more)

### Community 37 - "PlayerDriverInput"
Cohesion: 0.11
Nodes (12): int, EdgeLatch, bool, float, InputAction, PlayerDriverInput, bool, float (+4 more)

### Community 46 - "GameFlow"
Cohesion: 0.12
Nodes (10): AudioSource, bool, Color, InputAction, int, List, SaveService, string (+2 more)

### Community 73 - "Q: Проследи"
Cohesion: 0.40
Nodes (4): Answer, Outcome, Q: Проследи, Source Nodes

### Community 74 - "Result"
Cohesion: 0.11
Nodes (11): bool, int, IReadOnlyList, long, string, Task, ICloudSave, LeaderboardEntry (+3 more)

### Community 75 - "HudScreen"
Cohesion: 0.14
Nodes (7): float, GameObject, Image, RectTransform, StringBuilder, TextMeshProUGUI, HudScreen

### Community 76 - "MeshChunkWriter"
Cohesion: 0.20
Nodes (13): Color32, Matrix4x4, Vector2, Vector3, LowPoly, Color32, int, List (+5 more)

### Community 77 - "AiDriver"
Cohesion: 0.19
Nodes (6): bool, float, int, Vector3, AiDriver, RaycastHit

### Community 78 - "RaceManager"
Cohesion: 0.16
Nodes (8): bool, Dictionary, double, float, int, IReadOnlyList, List, RaceManager

### Community 79 - "EffectPool"
Cohesion: 0.13
Nodes (14): bool, Color, float, int, Light, List, Material, MaterialPropertyBlock (+6 more)

### Community 80 - ".BuildSleekSportsCar"
Cohesion: 0.16
Nodes (10): MenuItem, string, Transform, RaceCarPrefabBaker, GameObject, Material, Mesh, Transform (+2 more)

### Community 81 - "EditorMockPlatform"
Cohesion: 0.15
Nodes (9): ResultCategory, bool, Dictionary, float, IReadOnlyList, Random, string, Task (+1 more)

### Community 82 - "TrackPath"
Cohesion: 0.07
Nodes (27): float, Vector3, TrackSpline, float, int, List, Vector3, GrandstandPlacement (+19 more)

### Community 83 - ".Build"
Cohesion: 0.16
Nodes (11): int, PropNoise, Color32, float, int, List, Mesh, Vector2 (+3 more)

### Community 84 - "SkyDirector"
Cohesion: 0.06
Nodes (26): Action, Color, MenuItem, string, StringBuilder, SkyPresetFactory, Color, Cubemap (+18 more)

### Community 85 - "CarCannon"
Cohesion: 0.13
Nodes (9): bool, Color, float, int, Material, Rigidbody, Transform, Vector3 (+1 more)

### Community 86 - "RacingGame.Core.Platform"
Cohesion: 0.16
Nodes (6): WebMockPlatform, PlatformBootstrap, RacingGame.Platform, RacingGame.Platform.Yandex, RacingGame.Core.Platform, RacingGame.Platform.Mock

### Community 87 - "MenuScreens"
Cohesion: 0.12
Nodes (10): bool, Canvas, Dictionary, float, int, string, StringBuilder, TextMeshProUGUI (+2 more)

### Community 88 - "CarsGame: мидкор-слой. Этапы и промпты для агента"
Cohesion: 0.12
Nodes (15): CarsGame: мидкор-слой. Этапы и промпты для агента, После MVP (короткие промпты, по одному этапу каждый), Преамбула (вставлять в начало каждого промпта), Этап 0. Тики в CarHealth, единый результат попадания, потолки пулов (P0a, ~4 дня), Этап 10. Реальный SDK Яндекса и память WebGL (P4b, ~3 дня), Этап 11. Плейтест-резерв (5–7 дней, без нового контента), Этап 1. Замер базовой производительности WebGL (P0b, ~2 дня), Этап 2. Система статусов и щит (P1a, ~3 дня) (+7 more)

### Community 89 - "RacingGame.World.Gen"
Cohesion: 0.15
Nodes (4): MenuItem, RuntimeWiring, RacingGame.World.Gen, RacingGame.EditorTools

### Community 90 - ".Text"
Cohesion: 0.18
Nodes (12): Action, Button, Canvas, Color, Image, RectTransform, TextMeshProUGUI, Transform (+4 more)

### Community 91 - "PlatformRuntime"
Cohesion: 0.15
Nodes (9): Dictionary, float, int, List, string, Pending, PlatformRuntime, Preserve (+1 more)

### Community 92 - "com.unity.nuget.newtonsoft-json"
Cohesion: 0.11
Nodes (19): com.unity.addressables, com.unity.nuget.newtonsoft-json, depth, source, url, version, dependencies, depth (+11 more)

### Community 93 - "RankEntry"
Cohesion: 0.22
Nodes (9): bool, double, int, List, RaceRanking, RankEntry, Test, RaceRankingTests (+1 more)

### Community 95 - "WebZipPostprocessor"
Cohesion: 0.19
Nodes (7): BuildReport, MenuItem, BuildCommands, BuildReport, long, WebZipPostprocessor, IPostprocessBuildWithReport

### Community 96 - ".BakeProps"
Cohesion: 0.18
Nodes (10): Action, GameObject, Material, MenuItem, Mesh, string, Transform, Vector2 (+2 more)

### Community 97 - "2026-09-02 — M0 (частично), M1, M2, M3 (код)"
Cohesion: 0.11
Nodes (17): 2026-09-02/03 (продолжение) — M7–M9 сделаны, WebGL-модуль установлен, 2026-09-02 — M0 (частично), M1, M2, M3 (код), 2026-09-02 (вечер) — мост разблокировался сам, M1–M3 проверены в редакторе, M4 сделан, 2026-09-02 (ночь) — Codex-ревью M4 закрыто, M5 написан, 2026-09-10 — мидкор-план и адверсариальная проверка, CarsGame — журнал выполнения плана, Codex-ревью M5 закрыто (6 находок), Codex-ревью M6 закрыто (8 находок) (+9 more)

### Community 98 - "com.unity.modules.imageconversion"
Cohesion: 0.12
Nodes (17): dependencies, depth, source, version, dependencies, depth, source, version (+9 more)

### Community 99 - "MenuCameraDirector"
Cohesion: 0.07
Nodes (20): AudioClip, Func, int, Random, SfxSynth, Camera, float, Rigidbody (+12 more)

### Community 100 - "RaceParticipant"
Cohesion: 0.12
Nodes (8): bool, float, GameObject, int, Quaternion, string, Vector3, RaceParticipant

### Community 101 - "PropBaker"
Cohesion: 0.21
Nodes (8): Color32, float, List, MenuItem, string, Vector3, Chunk, PropBaker

### Community 102 - "AiGunner"
Cohesion: 0.24
Nodes (6): float, AiDriverProfile, bool, float, int, AiGunner

### Community 103 - "RaceSpawner"
Cohesion: 0.17
Nodes (9): bool, Color, float, GameObject, int, IReadOnlyList, List, string (+1 more)

### Community 104 - "SaveService"
Cohesion: 0.07
Nodes (29): bool, double, float, int, List, long, string, DeviceLedger (+21 more)

### Community 106 - "Pickup"
Cohesion: 0.21
Nodes (7): Collider, float, Material, Transform, Vector3, Pickup, PickupKind

### Community 107 - "RaceDirector"
Cohesion: 0.06
Nodes (23): float, GameObject, int, Material, Vector3, ProjectileSpawn, GameObject, int (+15 more)

### Community 108 - "DriveCommand"
Cohesion: 0.17
Nodes (5): bool, float, int, DriveCommand, ICarInputSource

### Community 109 - "PlayerEffects"
Cohesion: 0.13
Nodes (10): float, Quaternion, Vector3, CameraImpulse, bool, float, Light, PlayerEffects (+2 more)

### Community 110 - "TouchControlsPresenter"
Cohesion: 0.18
Nodes (5): bool, Canvas, Transform, Vector2, TouchControlsPresenter

### Community 111 - "SimulationDriver"
Cohesion: 0.18
Nodes (5): ISimulated, bool, float, List, SimulationDriver

### Community 112 - ".ResetCar"
Cohesion: 0.18
Nodes (5): Quaternion, Vector3, IRespawnPoseProvider, Quaternion, Vector3

### Community 113 - "GhostCar"
Cohesion: 0.22
Nodes (4): bool, double, GhostCar, RacingGame.Core.Ghost

### Community 114 - "WorldHealthBars"
Cohesion: 0.21
Nodes (8): Canvas, float, Image, List, RectTransform, Vector2, Entry, WorldHealthBars

### Community 115 - "com.unity.mathematics"
Cohesion: 0.11
Nodes (19): com.unity.2d.sprite, com.unity.mathematics, dependencies, depth, source, version, dependencies, depth (+11 more)

### Community 116 - "com.unity.modules.terrain"
Cohesion: 0.18
Nodes (11): dependencies, depth, source, version, dependencies, depth, source, version (+3 more)

### Community 117 - "OpponentDriver"
Cohesion: 0.33
Nodes (5): float, int, Transform, Vector3, OpponentDriver

### Community 118 - "SfxPlayer"
Cohesion: 0.22
Nodes (6): AudioClip, AudioSource, float, int, Vector3, SfxPlayer

### Community 119 - "GameFlow.cs"
Cohesion: 0.20
Nodes (4): FlowState, PlayerProgression, RacingGame.Core.Economy, RacingGame.Core.Localization

### Community 120 - "RaceEventDefinition"
Cohesion: 0.24
Nodes (6): bool, float, int, string, RaceEventDefinition, GhostSession

### Community 121 - ".Spawn"
Cohesion: 0.22
Nodes (6): float, Vector3, Sample, Color, GameObject, byte

### Community 123 - "CarPaint"
Cohesion: 0.27
Nodes (6): Color, int, List, MaterialPropertyBlock, string, CarPaint

### Community 126 - "LocImporter"
Cohesion: 0.33
Nodes (4): List, MenuItem, string, LocImporter

### Community 127 - "FontAtlasBaker"
Cohesion: 0.40
Nodes (3): MenuItem, string, FontAtlasBaker

### Community 128 - "Loc"
Cohesion: 0.33
Nodes (5): Dictionary, HashSet, int, string, Loc

### Community 129 - "RacingGame"
Cohesion: 0.31
Nodes (3): RaceState, RacingGame.Core.Sim, RacingGame

### Community 130 - "RaceResults"
Cohesion: 0.31
Nodes (7): bool, double, int, List, string, RaceResultEntry, RaceResults

### Community 131 - "TargetCar"
Cohesion: 0.22
Nodes (6): bool, float, Quaternion, Rigidbody, Vector3, TargetCar

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
Cohesion: 0.38
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

### Community 141 - "DifficultyTier"
Cohesion: 0.25
Nodes (6): float, int, string, DifficultyTier, RaceSetup, ScriptableObject

### Community 142 - "MonoBehaviour"
Cohesion: 0.17
Nodes (7): Material, WorldAssets, float, Quaternion, Vector3, StartGrid, MonoBehaviour

### Community 143 - "Envelope"
Cohesion: 0.33
Nodes (7): bool, int, long, string, Envelope, LeaderboardEntryDto, SessionList

### Community 144 - "com.unity.test-framework"
Cohesion: 0.07
Nodes (32): com.unity.profiling.core, com.unity.scriptablebuildpipeline, dependencies, dependencies, depth, source, url, version (+24 more)

### Community 146 - "GhostRecorder"
Cohesion: 0.33
Nodes (3): int, Transform, GhostRecorder

## Ambiguous Edges - Review These
- `Official Unity AI/MCP Package` → `Runtime and Play Mode Automation`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to
- `Linux Standalone Editor Target` → `Target Platforms`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to
- `Mobile Platform Settings` → `Target Platforms`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to

## Knowledge Gaps
- **410 isolated node(s):** `Stream`, `com.unity.ai.assistant`, `com.unity.ai.navigation`, `com.unity.collab-proxy`, `com.unity.ide.rider` (+405 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **48 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `Official Unity AI/MCP Package` and `Runtime and Play Mode Automation`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **What is the exact relationship between `Linux Standalone Editor Target` and `Target Platforms`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **What is the exact relationship between `Mobile Platform Settings` and `Target Platforms`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `TrackPath` connect `TrackPath` to `RacingGame.Tests`, `MenuCameraDirector`, `RaceSpawner`, `AiDriver`, `MonoBehaviour`, `GameFlow`, `RaceProgress`?**
  _High betweenness centrality (0.081) - this node is a cross-community bridge._
- **Why does `TrackSpline` connect `TrackPath` to `RacingGame.Tests`, `.Build`, `TrackSplineTests`?**
  _High betweenness centrality (0.069) - this node is a cross-community bridge._
- **Why does `GameFlow` connect `GameFlow` to `MenuCameraDirector`, `RaceParticipant`, `CarHealth`, `HudScreen`, `DifficultyTier`, `RaceManager`, `CarController`, `MonoBehaviour`, `GhostCar`, `GhostRecorder`, `TrackPath`, `SkyDirector`, `MenuScreens`, `GameFlow.cs`, `RaceEventDefinition`, `PauseReason`, `.Get`?**
  _High betweenness centrality (0.064) - this node is a cross-community bridge._
- **What connects `Stream`, `com.unity.ai.assistant`, `com.unity.ai.navigation` to the rest of the system?**
  _410 weakly-connected nodes found - possible documentation gaps or missing edges._