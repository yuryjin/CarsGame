# Graph Report - .  (2026-09-02)

## Corpus Check
- Corpus is ~8,417 words - fits in a single context window. You may not need a graph.

## Summary
- 650 nodes · 822 edges · 73 communities (30 shown, 43 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 6 edges (avg confidence: 0.86)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Unity Media Modules
- Camera Combat Targets
- Vehicle Driving AI
- AI Pipeline Dependencies
- Testing Inference Tooling
- Web Request Modules
- Input UI Multiplayer
- Universal Rendering Pipeline
- Physics Terrain Vehicles
- Readme Editor Tooling
- IDE NUnit Graphics
- Vehicle Health System
- JSON XR Subsystems
- Image Capture Networking
- Vehicle Cannon System
- Procedural Car Builder
- Project Environment Overview
- Manifest Package References
- AI Navigation Package
- Umbra Visibility Module
- Version Control Package
- 2D Physics Module
- 2D Physics Core
- Unity UI Package
- Visual Scripting Package
- Scene Gameplay Composition
- Accessibility Module
- Tilemap Module
- Wind Module
- Target Platform Settings
- AI Assistant Package
- AI Navigation Metadata
- Version Control Metadata
- Rider IDE Metadata
- Visual Studio Metadata
- Input System Metadata
- Accessibility Metadata
- Adaptive Performance Metadata
- Terrain Physics Metadata
- Tilemap Metadata
- Umbra Metadata
- Analytics Metadata
- Web Texture Metadata
- Vector Graphics Metadata
- Vehicle Physics Metadata
- Video Module Metadata
- Wind Module Metadata
- Multiplayer Center Metadata
- Unity Pipeline Metadata
- Universal Pipeline Metadata
- Timeline Metadata
- Visual Scripting Metadata
- AI Module Metadata
- Android JNI Metadata
- Animation Module Metadata
- Asset Bundle Metadata
- Audio Module Metadata
- Director Module Metadata
- Image Conversion Metadata
- IMGUI Module Metadata
- JSON Serialization Metadata
- Physics Module Metadata
- Physics2D Module Metadata
- Physics2D Core Metadata
- Screen Capture Metadata
- Terrain Module Metadata
- UI Module Metadata
- UI Elements Metadata
- Web Request Metadata
- Asset Bundle Web Metadata
- Audio Web Request Metadata
- Test Framework Metadata
- UGUI Package Metadata

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

## Communities (73 total, 43 thin omitted)

### Community 0 - "Unity Media Modules"
Cohesion: 0.04
Nodes (48): com.unity.modules.hierarchycore, dependencies, depth, source, version, dependencies, depth, source (+40 more)

### Community 1 - "Camera Combat Targets"
Cohesion: 0.05
Nodes (27): float, Rigidbody, Transform, Vector3, CarCameraFollow, float, Light, Material (+19 more)

### Community 2 - "Vehicle Driving AI"
Cohesion: 0.08
Nodes (14): bool, float, GameObject, Quaternion, Rigidbody, Transform, Vector3, CarController (+6 more)

### Community 3 - "AI Pipeline Dependencies"
Cohesion: 0.05
Nodes (39): com.unity.2d.sprite, com.unity.mathematics, com.unity.nuget.mono-cecil, com.unity.nuget.newtonsoft-json, dependencies, depth, source, version (+31 more)

### Community 4 - "Testing Inference Tooling"
Cohesion: 0.06
Nodes (37): com.unity.burst, com.unity.collections, com.unity.test-framework.performance, dependencies, depth, source, url, version (+29 more)

### Community 5 - "Web Request Modules"
Cohesion: 0.06
Nodes (34): dependencies, depth, source, version, dependencies, depth, source, version (+26 more)

### Community 6 - "Input UI Multiplayer"
Cohesion: 0.07
Nodes (29): com.unity.dt.app-ui, dependencies, depth, source, url, version, dependencies, depth (+21 more)

### Community 7 - "Universal Rendering Pipeline"
Cohesion: 0.07
Nodes (29): com.unity.render-pipelines.core, com.unity.render-pipelines.universal-config, com.unity.searcher, com.unity.shadergraph, depth, source, version, dependencies (+21 more)

### Community 8 - "Physics Terrain Vehicles"
Cohesion: 0.07
Nodes (27): dependencies, depth, source, version, dependencies, depth, source, version (+19 more)

### Community 9 - "Readme Editor Tooling"
Cohesion: 0.11
Nodes (14): bool, float, string, ReadmeEditor, bool, string, Readme, Section (+6 more)

### Community 10 - "IDE NUnit Graphics"
Cohesion: 0.08
Nodes (24): com.unity.ext.nunit, dependencies, depth, source, version, dependencies, depth, source (+16 more)

### Community 11 - "Vehicle Health System"
Cohesion: 0.14
Nodes (8): bool, Collision, int, Rigidbody, CarHealth, MaterialPropertyBlock, Rect, Renderer

### Community 12 - "JSON XR Subsystems"
Cohesion: 0.10
Nodes (22): com.unity.modules.subsystems, dependencies, depth, source, version, dependencies, depth, source (+14 more)

### Community 13 - "Image Capture Networking"
Cohesion: 0.12
Nodes (17): dependencies, depth, source, version, dependencies, depth, source, version (+9 more)

### Community 14 - "Vehicle Cannon System"
Cohesion: 0.18
Nodes (9): bool, float, Light, Material, Rigidbody, Transform, Vector3, CarCannon (+1 more)

### Community 15 - "Procedural Car Builder"
Cohesion: 0.30
Nodes (6): GameObject, Material, Transform, Vector3, ProceduralCarBuilder, Mesh

### Community 16 - "Project Environment Overview"
Cohesion: 0.17
Nodes (13): Unity AI Navigation Package, CarsGame Project, Single-Scene Driving and Combat Prototype, Unity Input System 1.20.0, Official Unity AI/MCP Package, Procedural Car Mesh Generation, Runtime and Play Mode Automation, Unity 6000.5.9f1 (+5 more)

### Community 17 - "Manifest Package References"
Cohesion: 0.17
Nodes (11): com.unity.ai.inference, com.unity.modules.cloth, com.unity.modules.unitywebrequestwww, com.unity.modules.xr, dependencies, com.unity.ai.inference, com.unity.modules.cloth, com.unity.modules.particlesystem (+3 more)

### Community 18 - "AI Navigation Package"
Cohesion: 0.17
Nodes (12): dependencies, depth, source, url, version, dependencies, depth, source (+4 more)

### Community 19 - "Umbra Visibility Module"
Cohesion: 0.29
Nodes (6): dependencies, depth, source, version, dependencies, com.unity.modules.umbra

### Community 20 - "Version Control Package"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.collab-proxy

### Community 21 - "2D Physics Module"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, version, com.unity.modules.physics2d, com.unity.modules.physics2d

### Community 22 - "2D Physics Core"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, version, com.unity.modules.physicscore2d, com.unity.modules.physicscore2d

### Community 23 - "Unity UI Package"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, version, com.unity.ugui, com.unity.ugui

### Community 24 - "Visual Scripting Package"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.visualscripting

### Community 25 - "Scene Gameplay Composition"
Cohesion: 0.40
Nodes (5): Assembly-CSharp, Rigidbody Vehicle Control, SampleScene, MonoBehaviour Scene Composition, Track Markings Opponent Route

### Community 26 - "Accessibility Module"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.accessibility

### Community 27 - "Tilemap Module"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.tilemap

### Community 28 - "Wind Module"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.wind

### Community 29 - "Target Platform Settings"
Cohesion: 0.67
Nodes (3): Linux Standalone Editor Target, Mobile Platform Settings, Target Platforms

## Ambiguous Edges - Review These
- `Target Platforms` → `Linux Standalone Editor Target`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to
- `Target Platforms` → `Mobile Platform Settings`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to
- `Official Unity AI/MCP Package` → `Runtime and Play Mode Automation`  [AMBIGUOUS]
  Docs/AI/UnityProjectContext.md · relation: conceptually_related_to

## Knowledge Gaps
- **325 isolated node(s):** `com.unity.ai.assistant`, `com.unity.ai.inference`, `com.unity.ai.navigation`, `com.unity.collab-proxy`, `com.unity.ide.rider` (+320 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **43 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `Target Platforms` and `Linux Standalone Editor Target`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **What is the exact relationship between `Target Platforms` and `Mobile Platform Settings`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **What is the exact relationship between `Official Unity AI/MCP Package` and `Runtime and Play Mode Automation`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `dependencies` connect `Umbra Visibility Module` to `Unity Media Modules`, `AI Pipeline Dependencies`, `Testing Inference Tooling`, `Web Request Modules`, `Input UI Multiplayer`, `Universal Rendering Pipeline`, `Physics Terrain Vehicles`, `IDE NUnit Graphics`, `JSON XR Subsystems`, `Image Capture Networking`, `AI Navigation Package`, `Version Control Package`, `2D Physics Module`, `2D Physics Core`, `Unity UI Package`, `Visual Scripting Package`, `Accessibility Module`, `Tilemap Module`, `Wind Module`?**
  _High betweenness centrality (0.294) - this node is a cross-community bridge._
- **Why does `dependencies` connect `Manifest Package References` to `AI Assistant Package`, `AI Navigation Metadata`, `Version Control Metadata`, `Rider IDE Metadata`, `Visual Studio Metadata`, `Input System Metadata`, `Accessibility Metadata`, `Adaptive Performance Metadata`, `Terrain Physics Metadata`, `Tilemap Metadata`, `Umbra Metadata`, `Analytics Metadata`, `Web Texture Metadata`, `Vector Graphics Metadata`, `Vehicle Physics Metadata`, `Video Module Metadata`, `Wind Module Metadata`, `Multiplayer Center Metadata`, `Unity Pipeline Metadata`, `Universal Pipeline Metadata`, `Timeline Metadata`, `Visual Scripting Metadata`, `AI Module Metadata`, `Android JNI Metadata`, `Animation Module Metadata`, `Asset Bundle Metadata`, `Audio Module Metadata`, `Director Module Metadata`, `Image Conversion Metadata`, `IMGUI Module Metadata`, `JSON Serialization Metadata`, `Physics Module Metadata`, `Physics2D Module Metadata`, `Physics2D Core Metadata`, `Screen Capture Metadata`, `Terrain Module Metadata`, `UI Module Metadata`, `UI Elements Metadata`, `Web Request Metadata`, `Asset Bundle Web Metadata`, `Audio Web Request Metadata`, `Test Framework Metadata`, `UGUI Package Metadata`?**
  _High betweenness centrality (0.022) - this node is a cross-community bridge._
- **Why does `com.unity.modules.jsonserialize` connect `JSON XR Subsystems` to `Unity Media Modules`, `AI Pipeline Dependencies`, `Testing Inference Tooling`, `Web Request Modules`, `Input UI Multiplayer`, `IDE NUnit Graphics`, `Umbra Visibility Module`, `Visual Scripting Package`?**
  _High betweenness centrality (0.015) - this node is a cross-community bridge._
- **What connects `com.unity.ai.assistant`, `com.unity.ai.inference`, `com.unity.ai.navigation` to the rest of the system?**
  _325 weakly-connected nodes found - possible documentation gaps or missing edges._