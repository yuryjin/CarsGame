# Mission HUD readability — 2026-10-23

The preceding goal turn was verified progress: consistent racer names, a failing/passing regression, a WebGL comparison and e16 campaign/reload coverage. This iteration completed the next numbered race and improved the mission HUD from measured browser failures.

## Observed problems and corrections

| Observed problem | Final behavior | Evidence |
| --- | --- | --- |
| At 640×360, the boss name/core were 8.7 pixels, route/distance 6.7 and enemy count 7.3. The 960×540 view also fell below readable thresholds. | Name/core retain at least 14 pixels; route/count/distance retain at least 12. Rows resize in screen pixels while world projection retains its original coordinate system. | `before/` records ten failures; `desktop/` and `browser/` pass. |
| The first larger card wrapped desktop `100%` and overlapped expanded touch goals. | Percentage width follows its font; goals occupy the vacated lap/position corner and reserve the actual boss-card width. | `first-layout/` retains both failures and its build hash. |
| Portuguese needs a longer boss name. | The full name occupies two lines at 14 pixels, within a 34.74-pixel label, without clipping or shrinking. | `second-layout/` records the fixed-height failure; `browser/mission-8-640.png` and its UI dump show the correction. |
| The distance could sit under the speedometer; saved UI rectangles confirmed it in English, Turkish, Chinese and German. | A dark-backed distance label stays inside the viewport. Its marker preserves horizontal direction and chooses the nearest free vertical space against active HUD/control rectangles. | `second-layout/` contains the old placements; the final geometry probe checks HUD labels, buttons, expanded goals and viewport edges. |

Informational graphics do not intercept driving input. No boss/combat rules, rewards, serialized assets or save schema changed. Overlapping distant racer nameplates are a separate investigation candidate and remain unchanged.

## Verification

- On the preceding RacerNames build, e17 finished first in 2:51.838, without deaths, with three resets and 240 coins. This used driving-only autopilot. Fresh-context reload retained 3,958 coins / 23 races and the same upgrades/loadout.
- The full existing Unity mission test, without forced victory, destroyed the fortress after it travelled 1,255 m. It recorded nine raids, 3,600 shots, 2,001 hits, 404 coins and no runtime errors, then restored the circuit and removed mission objects. The player seat used the mission driver and gunner bot. `mission-play.xml` covers this initial mission pass and the HUD scenario; it precedes the later layout refinements.
- After the final layout refinements, the contextual-HUD PlayMode scenario, all 369 EditMode tests, and the solo physical build gate passed. The gate remains 49.2 seconds with zero deaths/automatic recovery. WebGL built successfully.
- The final desktop probe used W/Ctrl and actual pointer interaction at 1280×720, 960×540 and 640×360. Minimum text size, label height, percentage wrapping, expanded-goal separation, marker overlap/bounds and pause checks passed.
- The final mobile probe visited the platform-default option plus all nine explicit languages. It used CDP touch events to fire, expand/collapse goals and pause each mission. Ten ammunition-consumption checks passed. The first locale covered all three landscape sizes; the remaining locales covered 640×360. The Portuguese title uses two lines; other information remains single-line. All marker/goal geometry checks passed, and clock/fortress route stayed unchanged on pause. Captured exception/localization/glyph errors were empty.
- Fresh-context reload on the final executable again retained 3,958 coins / 23 races, upgrades and both equipped weapons. Browser probes use separate contexts and never export their temporary mission/language changes into the campaign storage file.

This is not a manual browser victory or a fixed-seed physics comparison. The browser mission probes cover early play and UI; the complete mission outcome is covered by the Unity bot scenario. All browser runs use webtest/NVIDIA Vulkan and AgentSink. Unity uses an isolated project/preferences directory. No deployment or user-profile replacement occurred.

## Verified archive

`Builds/MissionHud-2026-10-23/` contains the ZIP, failing and passing browser evidence, test results, mission log, profile checks and exact build-source hashes. ZIP SHA256: `308f8978a243e70d834089ca800e75b4310bfff0cf3b79f33a97ba8c6ae4240f`. The final manifest contains 1,356 files; all 283 C# files matched the workspace.

The iteration is verified progress, with no active blocker. Next useful investigation: overlapping racer nameplates in the close-pack bomb screenshots from the preceding build, followed by more direct mission combat coverage. Preserve this verified build while evaluating further changes.
