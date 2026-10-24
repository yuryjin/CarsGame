# Bomb HUD readability — 2026-10-23

The preceding nameplate iteration was verified progress: its final browser race and fresh-context reload passed and the executable was archived. Screenshots exposed a separate small-screen problem in the live bomb instructions.

## Evidence and change

At 640×360, the existing mobile UI dump records 7.67 physical pixels for PassHint and 10.67 for CarrierTimer. The new layout preserves the existing wording and bomb rules, but sizes the title at a minimum of 14 screen pixels and the instruction at 12. The panel uses text's preferred height, allowing long translations to wrap without shrinking. Its width and placement scale independently of the old reference-canvas dimensions. Pause visibility and informational/non-interactive behavior remain unchanged. Visual review also identified the temporary teaching tip immediately below the old bomb panel. HudScreen now places that tip below the actual expanded bomb card in touch layout, preserving its height and restoring the original row outside bomb races.

No changes to simulation, weapons, rewards or saves. Test runs use an isolated Unity project/preferences, webtest/NVIDIA Vulkan and AgentSink. Browser probes import the separate QA profile and do not export it. The full race/reload workflow alone advances that QA profile.

## Validation status

Baseline screen measurements are saved in `/tmp/cars-bomb-hud/before.json`. The browser geometry probe failed on the preceding executable with 24 repeated unreadable-text findings at 812×375 and 640×360; 1280×720 passed that check. All other baseline assertions, including actual touch pause, completed. Native regression coverage includes HotBombPlayTests, nameplates, localized racer names and contextual player HUD. The first native pass completed 369 EditMode tests, five PlayMode scenarios and the 49.2-second physical gate. The first WebGL layout passed text readability but failed with four repeated overlaps between PassHint and the teaching tip at 812×375 and 640×360. After the placement correction, the same five PlayMode scenarios, 369 EditMode tests and 49.2-second physical gate passed again. The final WebGL build completed and all 284 C# files match its isolated source. Desktop probes passed 1280×720, 812×375 and 640×360. Mobile probes passed all nine languages plus platform default, expanded goals, real touch pause and switching back to an ordinary race. Captured error/finding arrays are empty. Russian and French screenshots were visually reviewed. Final browser scope is three landscape sizes, all nine languages plus the platform-default selector entry, expanded goals, real touch pause, nameplate regression and a full bomb race followed by reload. These checks are pending; no finished-build claim yet.

## Separate next investigation

The previous e18 screenshots/standings contain two identically named Bolt rivals. RaceSpawner's suffix is based on profile-array traversal rather than occurrences of a base name. Difficulty_easy references the Rookie/Bolt profile twice in its four-entry array, confirming the duplicate-base-name path. This layout change does not address it.
