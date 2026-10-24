# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Root: `/home/user/projects/games/Unity/newgames/CarsGame`.
- Single-scene arcade driving/combat game with a campaign, special tracks, boss mission, daily race, garage, test drives, progression, ghosts and nine UI languages.
- Refreshed 2026-10-24 against base commit `35035e5` and the current polish changes.
- Current validation and before/after evidence: [Polish-2026-10-24.md](Polish-2026-10-24.md).
- AI recovery work predates this iteration: [AiRecoveryInvestigation.md](AiRecoveryInvestigation.md). Older pending items in [ContinuousPlaytest.md](ContinuousPlaytest.md) are chronological history, not necessarily outstanding defects.

## Environment And Build

| Area | Confirmed configuration |
| --- | --- |
| Unity | 6000.5.9f1 |
| Rendering | URP 17.5.0 |
| Input | Input System 1.20.0 |
| UI/localization | UGUI 2.5.0, TMP, Localization 1.5.12, Addressables |
| Tests | Unity Test Framework 1.7.0; dedicated EditMode and PlayMode assemblies |
| Product version | 0.2.0 |
| Shipping build path | WebGL with the Yandex template and SDK adapter; local mock SDK for QA |
| Scene | `Assets/Scenes/SampleScene.unity` is the enabled build scene |

`Tools/qa/build-web.sh` runs a solo driving gate, then `RacingGame.EditorTools.BuildCommands.BuildYandex`, producing a WebGL folder, ZIP and size report. See `Assets/CarsGame/Editor/Build/` for the build/postprocess implementation. Browser runs do not establish production Yandex moderation or real-phone compatibility.

## Code Layout And Assemblies

| Path / assembly | Responsibility |
| --- | --- |
| `Assets/CarsGame/Core` / `RacingGame.Core` | Save merging/migration, economy, event rules, track math and platform contracts |
| `Assets/CarsGame/Game` / `RacingGame.Game` | Vehicles, simulation, races/missions, camera, runtime menus/HUD, audio and flow |
| `Assets/CarsGame/Platform` / `RacingGame.Platform` | Mock/standalone/Yandex adapters and local save storage; depends on Core |
| `Assets/CarsGame/Editor` / `RacingGame.Editor` | Build tools, localization/font import and generated content tooling |
| `Assets/CarsGame/Net` / `RacingGame.Net` | Optional transport code, gated by `CARS_NET`; not evidence of shipped multiplayer |
| `Assets/CarsGame/Tests/{EditMode,PlayMode}` | Separate test assemblies |
| `Assets/WebGLTemplates/Yandex/TemplateData` | Browser SDK, mock SDK and music integration |
| `Assets/CarsGame/Localization/Source/strings.csv` | Localization source; generated tables live in `Assets/Localization/Tables` |
| `Tools/qa` | Native tests, browser interaction probes, builds and local server |

## Runtime Architecture

- `PlatformBootstrap`/`PlatformRuntime` select and initialize platform services. `PlatformGlue` loads/merges saves and initializes localization; `GameFlow` waits for readiness before the initial menu.
- `GameFlow` owns menu/race/results/test-drive transitions. `RaceManager` owns countdown, racing, finishing and results. Events combine scene references with `Resources/Events`; daily events are generated separately.
- `SimulationDriver` enforces a 0.02 s fixed step and orders simulation participants. `CarController` consumes player, AI or scripted input. `AiDriver` follows `TrackPath` and handles recovery.
- Menus and HUD are runtime UGUI/TMP hierarchies. Physical-pixel readability matters because menus and driving HUD use different scaling rules.
- `SaveService` writes a local mirror and coalesces cloud writes. Cloud data must be successfully read before writes are authorized; account generations reject stale loads. The JavaScript adapter serializes writes and prevents stale account acknowledgements from discarding a new account's queued payload.
- `PauseController` combines user, host, ad and visibility pause reasons. UI closing must preserve unrelated pause reasons.

## Testing And Tooling

- Current EditMode suite: 371 tests. Current PlayMode suite: 40 tests. Consult the iteration report for the final results rather than assuming a build implies runtime validation.
- Full PlayMode runs require graphics: garage previews use RenderTextures. `Tools/qa/play-tests.sh` runs under Xvfb/Vulkan; `CG_NOGRAPHICS=1` is only appropriate for compatible filtered tests.
- `Tools/qa/platform-save.mjs` exercises the browser save adapter in a Node VM without a browser or network.
- Use isolated copies via `Tools/qa/common.sh`; never launch batch Unity on the main project while its editor may be open. Each concurrent Unity process needs its own `SIM`; use separate `CG_QA_CONFIG_HOME` values for independent save state.
- QA defaults to an isolated `XDG_CONFIG_HOME` so tests cannot change the user's real PlayerPrefs.
- Every browser check must run through `webtest`, retain its NVIDIA Vulkan arguments and close the browser in `finally`. Keep all audio on `AgentSink`.
- The official Unity AI package is installed (`com.unity.ai.assistant` 2.19.0-pre.2), but Unity MCP tools are not exposed in this session. CLI automation is verified; do not install a second bridge just to work around this.

## Sources

`Packages/manifest.json`, `ProjectSettings/{ProjectVersion.txt,EditorBuildSettings.asset,ProjectSettings.asset}`, the first-party assembly definitions, `Game/Flow/{GameFlow,PlatformGlue}.cs`, `Game/Race/SimulationDriver.cs`, `Core/Save/SaveService.cs`, `TemplateData/cars-platform.js`, the build tools, test XML and QA reports.

<!-- unity-onboarding:generated:end -->
