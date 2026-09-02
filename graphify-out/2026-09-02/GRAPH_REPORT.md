# Graph Report - CarsGame  (2026-09-02)

## Corpus Check
- 33 files · ~28,760 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 653 nodes · 824 edges · 76 communities (32 shown, 44 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 6 edges (avg confidence: 0.86)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `1e49f997`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- com.unity.modules.audio
- Projectile
- CarController
- com.unity.modules.uielements
- com.unity.test-framework
- com.unity.modules.unitywebrequest
- com.unity.burst
- com.unity.render-pipelines.core
- com.unity.modules.physics
- ReadmeEditor
- com.unity.modules.imgui
- CarHealth
- com.unity.modules.jsonserialize
- com.unity.modules.imageconversion
- CarCannon
- Procedural Car Builder
- Unity Project Context
- dependencies
- com.unity.ai.navigation
- dependencies
- com.unity.collab-proxy
- com.unity.modules.physics2d
- com.unity.modules.physicscore2d
- RacingGame
- CarCameraFollow
- Rigidbody Vehicle Control
- com.unity.modules.accessibility
- com.unity.modules.tilemap
- com.unity.modules.wind
- Target Platforms
- com.unity.ai.assistant
- com.unity.ai.navigation
- com.unity.collab-proxy
- com.unity.ide.rider
- com.unity.ide.visualstudio
- com.unity.inputsystem
- com.unity.modules.accessibility
- com.unity.modules.adaptiveperformance
- com.unity.modules.terrainphysics
- com.unity.modules.tilemap
- com.unity.modules.umbra
- com.unity.modules.unityanalytics
- com.unity.modules.unitywebrequesttexture
- com.unity.modules.vectorgraphics
- com.unity.modules.vehicles
- com.unity.modules.video
- com.unity.modules.wind
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
- TargetCar
- com.unity.modules.ai

## God Nodes (most connected - your core abstractions)
1. `CarController` - 28 edges
2. `CarHealth` - 22 edges
3. `com.unity.modules.jsonserialize` - 17 edges
4. `CarCannon` - 16 edges
5. `OpponentDriver` - 14 edges
6. `ReadmeEditor` - 14 edges
7. `com.unity.modules.unitywebrequest` - 13 edges
8. `com.unity.modules.physics` - 13 edges
9. `com.unity.modules.uielements` - 12 edges
10. `com.unity.modules.audio` - 12 edges

## Surprising Connections (you probably didn't know these)
- `Unity 6000.5.9f1` --semantically_similar_to--> `Unity 6000.5.9f1 Revision b57deb96f08d`  [INFERRED] [semantically similar]
  Docs/AI/UnityProjectContext.md → ProjectSettings/ProjectVersion.txt
- `Unity Project Context` --references--> `Unity Project Version Record`  [EXTRACTED]
  Docs/AI/UnityProjectContext.md → ProjectSettings/ProjectVersion.txt
- `OpponentDriver` --references--> `CarController`  [EXTRACTED]
  Assets/Scripts/OpponentDriver.cs → Assets/Scripts/CarController.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Confirmed Unity Environment** — docs_ai_unityprojectcontext_unity_6000_5_9f1, docs_ai_unityprojectcontext_urp_17_5_0, docs_ai_unityprojectcontext_input_system_1_20_0 [EXTRACTED 1.00]
- **Gameplay Architecture Patterns** — docs_ai_unityprojectcontext_scene_composition, docs_ai_unityprojectcontext_rigidbody_vehicle_control, docs_ai_unityprojectcontext_track_markings_route, docs_ai_unityprojectcontext_procedural_car_generation [EXTRACTED 1.00]

## Communities (76 total, 44 thin omitted)

### Community 0 - "com.unity.modules.audio"
Cohesion: 0.07
Nodes (30): dependencies, depth, source, version, dependencies, depth, source, version (+22 more)

### Community 1 - "Projectile"
Cohesion: 0.18
Nodes (9): bool, Collision, float, GameObject, Material, Rigidbody, Vector3, Projectile (+1 more)

### Community 2 - "CarController"
Cohesion: 0.08
Nodes (14): bool, float, GameObject, Quaternion, Rigidbody, Transform, Vector3, CarController (+6 more)

### Community 3 - "com.unity.modules.uielements"
Cohesion: 0.04
Nodes (48): com.unity.2d.sprite, com.unity.nuget.mono-cecil, com.unity.nuget.newtonsoft-json, dependencies, depth, source, version, dependencies (+40 more)

### Community 4 - "com.unity.test-framework"
Cohesion: 0.06
Nodes (32): com.unity.ext.nunit, com.unity.test-framework.performance, dependencies, dependencies, depth, source, version, dependencies (+24 more)

### Community 5 - "com.unity.modules.unitywebrequest"
Cohesion: 0.06
Nodes (34): dependencies, depth, source, version, dependencies, depth, source, version (+26 more)

### Community 6 - "com.unity.burst"
Cohesion: 0.07
Nodes (30): com.unity.burst, com.unity.collections, com.unity.mathematics, dependencies, depth, source, url, version (+22 more)

### Community 7 - "com.unity.render-pipelines.core"
Cohesion: 0.07
Nodes (29): com.unity.render-pipelines.core, com.unity.render-pipelines.universal-config, com.unity.searcher, com.unity.shadergraph, depth, source, version, dependencies (+21 more)

### Community 8 - "com.unity.modules.physics"
Cohesion: 0.12
Nodes (16): dependencies, depth, source, version, dependencies, depth, source, version (+8 more)

### Community 9 - "ReadmeEditor"
Cohesion: 0.11
Nodes (14): bool, float, string, ReadmeEditor, bool, string, Readme, Section (+6 more)

### Community 10 - "com.unity.modules.imgui"
Cohesion: 0.05
Nodes (41): com.unity.modules.hierarchycore, dependencies, depth, source, version, dependencies, depth, source (+33 more)

### Community 11 - "CarHealth"
Cohesion: 0.09
Nodes (13): bool, Collision, float, int, Light, Material, Rigidbody, Transform (+5 more)

### Community 12 - "com.unity.modules.jsonserialize"
Cohesion: 0.10
Nodes (22): com.unity.modules.subsystems, dependencies, depth, source, version, dependencies, depth, source (+14 more)

### Community 13 - "com.unity.modules.imageconversion"
Cohesion: 0.06
Nodes (36): com.unity.dt.app-ui, dependencies, depth, source, url, version, dependencies, depth (+28 more)

### Community 14 - "CarCannon"
Cohesion: 0.18
Nodes (9): bool, float, Light, Material, Rigidbody, Transform, Vector3, CarCannon (+1 more)

### Community 15 - "Procedural Car Builder"
Cohesion: 0.30
Nodes (6): GameObject, Material, Transform, Vector3, ProceduralCarBuilder, Mesh

### Community 16 - "Unity Project Context"
Cohesion: 0.17
Nodes (13): Unity AI Navigation Package, CarsGame Project, Single-Scene Driving and Combat Prototype, Unity Input System 1.20.0, Official Unity AI/MCP Package, Procedural Car Mesh Generation, Runtime and Play Mode Automation, Unity 6000.5.9f1 (+5 more)

### Community 17 - "dependencies"
Cohesion: 0.17
Nodes (11): com.unity.ai.inference, com.unity.modules.cloth, com.unity.modules.unitywebrequestwww, com.unity.modules.xr, dependencies, com.unity.ai.inference, com.unity.modules.cloth, com.unity.modules.particlesystem (+3 more)

### Community 18 - "com.unity.ai.navigation"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.ai.navigation

### Community 19 - "dependencies"
Cohesion: 0.29
Nodes (6): dependencies, depth, source, version, dependencies, com.unity.modules.umbra

### Community 20 - "com.unity.collab-proxy"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.collab-proxy

### Community 21 - "com.unity.modules.physics2d"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, version, com.unity.modules.physics2d, com.unity.modules.physics2d

### Community 22 - "com.unity.modules.physicscore2d"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, version, com.unity.modules.physicscore2d, com.unity.modules.physicscore2d

### Community 24 - "CarCameraFollow"
Cohesion: 0.25
Nodes (6): float, Rigidbody, Transform, Vector3, CarCameraFollow, Camera

### Community 25 - "Rigidbody Vehicle Control"
Cohesion: 0.40
Nodes (5): Assembly-CSharp, Rigidbody Vehicle Control, SampleScene, MonoBehaviour Scene Composition, Track Markings Opponent Route

### Community 26 - "com.unity.modules.accessibility"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.accessibility

### Community 27 - "com.unity.modules.tilemap"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.tilemap

### Community 28 - "com.unity.modules.wind"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.wind

### Community 29 - "Target Platforms"
Cohesion: 0.67
Nodes (3): Linux Standalone Editor Target, Mobile Platform Settings, Target Platforms

### Community 73 - "TargetCar"
Cohesion: 0.25
Nodes (6): bool, float, Quaternion, Rigidbody, Vector3, TargetCar

### Community 74 - "com.unity.modules.ai"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, version, com.unity.modules.ai, com.unity.modules.ai

## Ambiguous Edges - Review These
- `Official Unity AI/MCP Package` → `Runtime and Play Mode Automation`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to
- `Linux Standalone Editor Target` → `Target Platforms`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to
- `Mobile Platform Settings` → `Target Platforms`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to

## Knowledge Gaps
- **325 isolated node(s):** `com.unity.ai.assistant`, `com.unity.ai.inference`, `com.unity.ai.navigation`, `com.unity.collab-proxy`, `com.unity.ide.rider` (+320 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **44 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `Official Unity AI/MCP Package` and `Runtime and Play Mode Automation`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **What is the exact relationship between `Linux Standalone Editor Target` and `Target Platforms`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **What is the exact relationship between `Mobile Platform Settings` and `Target Platforms`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `dependencies` connect `dependencies` to `com.unity.modules.audio`, `com.unity.modules.uielements`, `com.unity.test-framework`, `com.unity.modules.unitywebrequest`, `com.unity.burst`, `com.unity.render-pipelines.core`, `com.unity.modules.physics`, `com.unity.modules.ai`, `com.unity.modules.imgui`, `com.unity.modules.jsonserialize`, `com.unity.modules.imageconversion`, `com.unity.ai.navigation`, `com.unity.collab-proxy`, `com.unity.modules.physics2d`, `com.unity.modules.physicscore2d`, `com.unity.modules.accessibility`, `com.unity.modules.tilemap`, `com.unity.modules.wind`?**
  _High betweenness centrality (0.291) - this node is a cross-community bridge._
- **Why does `dependencies` connect `dependencies` to `com.unity.ai.assistant`, `com.unity.ai.navigation`, `com.unity.collab-proxy`, `com.unity.ide.rider`, `com.unity.ide.visualstudio`, `com.unity.inputsystem`, `com.unity.modules.accessibility`, `com.unity.modules.adaptiveperformance`, `com.unity.modules.terrainphysics`, `com.unity.modules.tilemap`, `com.unity.modules.umbra`, `com.unity.modules.unityanalytics`, `com.unity.modules.unitywebrequesttexture`, `com.unity.modules.vectorgraphics`, `com.unity.modules.vehicles`, `com.unity.modules.video`, `com.unity.modules.wind`, `com.unity.multiplayer.center`, `com.unity.pipeline`, `com.unity.render-pipelines.universal`, `com.unity.timeline`, `com.unity.visualscripting`, `com.unity.modules.ai`, `com.unity.modules.androidjni`, `com.unity.modules.animation`, `com.unity.modules.assetbundle`, `com.unity.modules.audio`, `com.unity.modules.director`, `com.unity.modules.imageconversion`, `com.unity.modules.imgui`, `com.unity.modules.jsonserialize`, `com.unity.modules.physics`, `com.unity.modules.physics2d`, `com.unity.modules.physicscore2d`, `com.unity.modules.screencapture`, `com.unity.modules.terrain`, `com.unity.modules.ui`, `com.unity.modules.uielements`, `com.unity.modules.unitywebrequest`, `com.unity.modules.unitywebrequestassetbundle`, `com.unity.modules.unitywebrequestaudio`, `com.unity.test-framework`, `com.unity.ugui`?**
  _High betweenness centrality (0.022) - this node is a cross-community bridge._
- **Why does `com.unity.modules.jsonserialize` connect `com.unity.modules.jsonserialize` to `com.unity.modules.uielements`, `com.unity.test-framework`, `com.unity.modules.unitywebrequest`, `com.unity.burst`, `com.unity.modules.imgui`, `com.unity.modules.imageconversion`, `dependencies`?**
  _High betweenness centrality (0.015) - this node is a cross-community bridge._
- **What connects `com.unity.ai.assistant`, `com.unity.ai.inference`, `com.unity.ai.navigation` to the rest of the system?**
  _325 weakly-connected nodes found - possible documentation gaps or missing edges._