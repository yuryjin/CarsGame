# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Project root: `/home/user/projects/games/Unity/newgames/CarsGame`
- Small single-scene driving/combat prototype with a procedural circuit, one player car, and three AI opponents.
- Last analyzed: 2026-09-01
- Last analyzed commit: `e2c51a3` (working tree also contains uncommitted gameplay and scene changes)

## Confirmed Environment

- Unity version: 6000.5.9f1
- Render pipeline: Universal Render Pipeline 17.5.0
- Input system: Unity Input System 1.20.0; new input backend active
- Target platforms: unresolved; current editor target is Linux Standalone and mobile settings also exist

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Rendering | URP | Confirmed | `Packages/manifest.json`, `ProjectSettings/GraphicsSettings.asset` |
| Input | Unity Input System | Confirmed | `Packages/manifest.json`, `Assets/Scripts/CarController.cs` |
| Tests | Unity Test Framework installed; no first-party tests found | Confirmed | `Packages/manifest.json`, repository scan |
| Navigation | AI Navigation package installed but not used by gameplay code | Confirmed | `Packages/manifest.json`, repository scan |
| Networking | No first-party multiplayer implementation | Confirmed | repository scan |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/Scripts` | Runtime vehicle, combat, health, camera, and opponent logic | Confirmed | first-party scripts |
| `Assets/Scenes` | Main gameplay scene | Confirmed | `SampleScene.unity` |
| `Assets/Settings` | URP renderer and volume assets | Confirmed | asset inventory |

## Assembly Boundaries

| Assembly | Responsibility | Key references | Notes |
| --- | --- | --- | --- |
| `Assembly-CSharp` | All first-party runtime gameplay | UnityEngine, Input System | No first-party asmdefs |

## Scenes And Startup Flow

- Build scenes: `Assets/Scenes/SampleScene.unity`
- Likely startup scene: `SampleScene`
- Scene loading flow: direct single-scene startup; no scene-loading code found

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Scene composition | MonoBehaviours and explicit serialized references own gameplay | Confirmed | `SampleScene.unity`, `Assets/Scripts` |
| Vehicle control | Shared Rigidbody-based `CarController`; player or AI supplies input | Confirmed | `CarController.cs`, `OpponentDriver.cs` |
| Track path | Ordered children of `Track_Markings` form the opponent route | Confirmed | `SampleScene.unity`, `OpponentDriver.cs` |
| Visual generation | Car meshes can be built procedurally | Confirmed | `ProceduralCarBuilder.cs` |

## Coding Conventions

- Namespace style: `RacingGame`
- Serialized fields: mostly public Inspector fields; private runtime state
- Async: none in first-party runtime code
- Comments/docs: short gameplay and physics rationale near non-obvious behavior

## Testing And Validation

- EditMode tests: none found
- PlayMode tests: none found
- CI/build validation: none found
- Active Unity Editor successfully compiled `Assembly-CSharp` on 2026-09-01

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| Official Unity AI/MCP package | available | `com.unity.ai.assistant` 2.19.0-pre.1, active relay logs |
| Unity MCP tools in this Codex thread | unavailable | no Unity MCP capabilities exposed to the session |
| Unity Editor compilation | available | `Logs/Editor.log`, `Library/ScriptAssemblies/Assembly-CSharp.dll` |
| Runtime/play-mode automation | unverified | no Unity MCP capability exposed to the session |

## Important Constraints

- `SampleScene.unity` contains large embedded procedural meshes; keep scene edits narrowly targeted.
- Preserve existing uncommitted gameplay and scene work.
- Do not add another Unity MCP provider while the official package is present.

## Unknowns And Confidence

- Final supported build platforms and performance targets are unknown.
- Runtime opponent handling has not yet been observed through an exposed Play Mode tool.

## Source Files Inspected

- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/EditorBuildSettings.asset`
- `ProjectSettings/GraphicsSettings.asset`
- `ProjectSettings/ProjectSettings.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `Assets/Scenes/SampleScene.unity`
- `Assets/Scripts/*.cs`
- `Logs/Editor.log`

<!-- unity-onboarding:generated:end -->
