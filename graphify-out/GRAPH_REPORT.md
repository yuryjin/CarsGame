# Graph Report - CarsGame  (2026-09-01)

## Corpus Check
- 15 files · ~8,417 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 644 nodes · 817 edges · 71 communities (28 shown, 43 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS · INFERRED: 3 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `e2c51a39`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- com.unity.modules.unitywebrequest
- CarController
- com.unity.modules.imgui
- com.unity.nuget.mono-cecil
- com.unity.modules.physics2d
- com.unity.modules.physics
- com.unity.modules.jsonserialize
- com.unity.modules.audio
- com.unity.burst
- com.unity.test-framework
- com.unity.modules.uielements
- ReadmeEditor
- CarHealth
- com.unity.modules.imageconversion
- CarCannon
- ProceduralCarBuilder
- com.unity.dt.app-ui
- com.unity.modules.ai
- Unity Project Context
- com.unity.modules.ui
- com.unity.modules.physicscore2d
- dependencies
- com.unity.render-pipelines.core
- dependencies
- com.unity.ugui
- com.unity.visualscripting
- com.unity.collab-proxy
- com.unity.ai.assistant
- com.unity.modules.accessibility
- com.unity.modules.wind
- com.unity.modules.ai
- com.unity.modules.androidjni
- com.unity.ai.navigation
- com.unity.collab-proxy
- com.unity.ide.rider
- com.unity.ide.visualstudio
- com.unity.inputsystem
- com.unity.modules.accessibility
- com.unity.modules.adaptiveperformance
- com.unity.modules.animation
- com.unity.modules.terrainphysics
- com.unity.modules.tilemap
- com.unity.modules.umbra
- com.unity.modules.unityanalytics
- com.unity.modules.unitywebrequesttexture
- com.unity.modules.assetbundle
- com.unity.modules.vectorgraphics
- com.unity.modules.vehicles
- com.unity.modules.video
- com.unity.modules.wind
- com.unity.modules.audio
- com.unity.multiplayer.center
- com.unity.pipeline
- com.unity.render-pipelines.universal
- com.unity.timeline
- com.unity.visualscripting
- com.unity.modules.director
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

## God Nodes (most connected - your core abstractions)
1. `CarController` - 28 edges
2. `CarHealth` - 22 edges
3. `com.unity.modules.jsonserialize` - 17 edges
4. `CarCannon` - 16 edges
5. `OpponentDriver` - 14 edges
6. `ReadmeEditor` - 14 edges
7. `Unity Project Context` - 14 edges
8. `com.unity.modules.unitywebrequest` - 13 edges
9. `com.unity.modules.physics` - 13 edges
10. `com.unity.modules.uielements` - 12 edges

## Surprising Connections (you probably didn't know these)
- `OpponentDriver` --references--> `CarController`  [EXTRACTED]
  Assets/Scripts/OpponentDriver.cs → Assets/Scripts/CarController.cs

## Import Cycles
- None detected.

## Communities (71 total, 43 thin omitted)

### Community 0 - "com.unity.modules.unitywebrequest"
Cohesion: 0.05
Nodes (45): dependencies, depth, source, version, dependencies, depth, source, version (+37 more)

### Community 1 - "CarController"
Cohesion: 0.06
Nodes (20): bool, float, GameObject, Quaternion, Rigidbody, Transform, Vector3, CarController (+12 more)

### Community 2 - "com.unity.modules.imgui"
Cohesion: 0.08
Nodes (26): com.unity.ext.nunit, com.unity.modules.hierarchycore, dependencies, depth, source, version, dependencies, depth (+18 more)

### Community 3 - "com.unity.nuget.mono-cecil"
Cohesion: 0.05
Nodes (38): com.unity.2d.sprite, com.unity.nuget.mono-cecil, com.unity.nuget.newtonsoft-json, dependencies, depth, source, version, dependencies (+30 more)

### Community 4 - "com.unity.modules.physics2d"
Cohesion: 0.20
Nodes (10): depth, source, version, dependencies, depth, source, version, com.unity.modules.physics2d (+2 more)

### Community 5 - "com.unity.modules.physics"
Cohesion: 0.12
Nodes (16): dependencies, depth, source, version, dependencies, depth, source, version (+8 more)

### Community 6 - "com.unity.modules.jsonserialize"
Cohesion: 0.08
Nodes (27): com.unity.modules.subsystems, dependencies, depth, source, version, dependencies, depth, source (+19 more)

### Community 7 - "com.unity.modules.audio"
Cohesion: 0.07
Nodes (30): dependencies, depth, source, version, dependencies, depth, source, version (+22 more)

### Community 8 - "com.unity.burst"
Cohesion: 0.08
Nodes (25): com.unity.burst, com.unity.mathematics, dependencies, depth, source, url, version, dependencies (+17 more)

### Community 9 - "com.unity.test-framework"
Cohesion: 0.09
Nodes (24): com.unity.collections, com.unity.test-framework.performance, dependencies, depth, source, version, dependencies, depth (+16 more)

### Community 10 - "com.unity.modules.uielements"
Cohesion: 0.12
Nodes (16): dependencies, depth, source, url, version, depth, source, version (+8 more)

### Community 11 - "ReadmeEditor"
Cohesion: 0.11
Nodes (14): bool, float, string, ReadmeEditor, bool, string, Readme, Section (+6 more)

### Community 12 - "CarHealth"
Cohesion: 0.09
Nodes (13): bool, Collision, float, int, Light, Material, Rigidbody, Transform (+5 more)

### Community 14 - "CarCannon"
Cohesion: 0.06
Nodes (25): float, Rigidbody, Transform, Vector3, CarCameraFollow, bool, float, Light (+17 more)

### Community 15 - "ProceduralCarBuilder"
Cohesion: 0.30
Nodes (6): GameObject, Material, Transform, Vector3, ProceduralCarBuilder, Mesh

### Community 16 - "com.unity.dt.app-ui"
Cohesion: 0.11
Nodes (19): com.unity.dt.app-ui, dependencies, depth, source, url, version, dependencies, depth (+11 more)

### Community 17 - "com.unity.modules.ai"
Cohesion: 0.17
Nodes (12): dependencies, depth, source, url, version, dependencies, depth, source (+4 more)

### Community 18 - "Unity Project Context"
Cohesion: 0.13
Nodes (14): Architecture, Assembly Boundaries, Available Unity Tooling, Coding Conventions, Confirmed Environment, Directory Structure, Important Constraints, Important Packages And Frameworks (+6 more)

### Community 19 - "com.unity.modules.ui"
Cohesion: 0.18
Nodes (11): dependencies, depth, source, version, dependencies, depth, source, version (+3 more)

### Community 20 - "com.unity.modules.physicscore2d"
Cohesion: 0.29
Nodes (7): dependencies, dependencies, depth, source, version, com.unity.modules.physicscore2d, com.unity.modules.physicscore2d

### Community 21 - "dependencies"
Cohesion: 0.17
Nodes (11): com.unity.ai.inference, com.unity.modules.cloth, com.unity.modules.unitywebrequestwww, com.unity.modules.xr, dependencies, com.unity.ai.inference, com.unity.modules.cloth, com.unity.modules.particlesystem (+3 more)

### Community 22 - "com.unity.render-pipelines.core"
Cohesion: 0.07
Nodes (29): com.unity.render-pipelines.core, com.unity.render-pipelines.universal-config, com.unity.searcher, com.unity.shadergraph, depth, source, version, dependencies (+21 more)

### Community 23 - "dependencies"
Cohesion: 0.29
Nodes (6): dependencies, depth, source, version, dependencies, com.unity.modules.umbra

### Community 24 - "com.unity.ugui"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, version, com.unity.ugui, com.unity.ugui

### Community 25 - "com.unity.visualscripting"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.visualscripting

### Community 26 - "com.unity.collab-proxy"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.collab-proxy

### Community 28 - "com.unity.modules.accessibility"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.accessibility

### Community 29 - "com.unity.modules.wind"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.wind

## Knowledge Gaps
- **327 isolated node(s):** `com.unity.ai.assistant`, `com.unity.ai.inference`, `com.unity.ai.navigation`, `com.unity.collab-proxy`, `com.unity.ide.rider` (+322 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **43 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `dependencies` connect `dependencies` to `com.unity.modules.unitywebrequest`, `com.unity.modules.imgui`, `com.unity.nuget.mono-cecil`, `com.unity.modules.physics2d`, `com.unity.modules.physics`, `com.unity.modules.jsonserialize`, `com.unity.modules.audio`, `com.unity.burst`, `com.unity.test-framework`, `com.unity.modules.uielements`, `com.unity.dt.app-ui`, `com.unity.modules.ai`, `com.unity.modules.ui`, `com.unity.modules.physicscore2d`, `com.unity.render-pipelines.core`, `com.unity.ugui`, `com.unity.visualscripting`, `com.unity.collab-proxy`, `com.unity.modules.accessibility`, `com.unity.modules.wind`?**
  _High betweenness centrality (0.299) - this node is a cross-community bridge._
- **Why does `dependencies` connect `dependencies` to `com.unity.modules.imageconversion`, `com.unity.ai.assistant`, `com.unity.modules.ai`, `com.unity.modules.androidjni`, `com.unity.ai.navigation`, `com.unity.collab-proxy`, `com.unity.ide.rider`, `com.unity.ide.visualstudio`, `com.unity.inputsystem`, `com.unity.modules.accessibility`, `com.unity.modules.adaptiveperformance`, `com.unity.modules.animation`, `com.unity.modules.terrainphysics`, `com.unity.modules.tilemap`, `com.unity.modules.umbra`, `com.unity.modules.unityanalytics`, `com.unity.modules.unitywebrequesttexture`, `com.unity.modules.assetbundle`, `com.unity.modules.vectorgraphics`, `com.unity.modules.vehicles`, `com.unity.modules.video`, `com.unity.modules.wind`, `com.unity.modules.audio`, `com.unity.multiplayer.center`, `com.unity.pipeline`, `com.unity.render-pipelines.universal`, `com.unity.timeline`, `com.unity.visualscripting`, `com.unity.modules.director`, `com.unity.modules.imgui`, `com.unity.modules.jsonserialize`, `com.unity.modules.physics`, `com.unity.modules.physics2d`, `com.unity.modules.physicscore2d`, `com.unity.modules.screencapture`, `com.unity.modules.terrain`, `com.unity.modules.ui`, `com.unity.modules.uielements`, `com.unity.modules.unitywebrequest`, `com.unity.modules.unitywebrequestassetbundle`, `com.unity.modules.unitywebrequestaudio`, `com.unity.test-framework`, `com.unity.ugui`?**
  _High betweenness centrality (0.022) - this node is a cross-community bridge._
- **Why does `com.unity.modules.jsonserialize` connect `com.unity.modules.jsonserialize` to `com.unity.modules.imgui`, `com.unity.nuget.mono-cecil`, `com.unity.burst`, `com.unity.test-framework`, `com.unity.dt.app-ui`, `dependencies`, `com.unity.visualscripting`?**
  _High betweenness centrality (0.015) - this node is a cross-community bridge._
- **What connects `com.unity.ai.assistant`, `com.unity.ai.inference`, `com.unity.ai.navigation` to the rest of the system?**
  _327 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `com.unity.modules.unitywebrequest` be split into smaller, more focused modules?**
  _Cohesion score 0.048484848484848485 - nodes in this community are weakly interconnected._
- **Should `CarController` be split into smaller, more focused modules?**
  _Cohesion score 0.06292517006802721 - nodes in this community are weakly interconnected._
- **Should `com.unity.modules.imgui` be split into smaller, more focused modules?**
  _Cohesion score 0.07692307692307693 - nodes in this community are weakly interconnected._