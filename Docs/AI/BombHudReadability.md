# Bomb HUD readability — 2026-10-23

The preceding nameplate iteration was verified progress: its final browser race and fresh-context reload passed and the executable was archived. Screenshots exposed a separate small-screen problem in the live bomb instructions.

## Evidence and change

At 640×360, the existing mobile UI dump records 7.67 physical pixels for PassHint and 10.67 for CarrierTimer. The new layout preserves the existing wording and bomb rules, but sizes the title at a minimum of 14 screen pixels and the instruction at 12. The panel uses text's preferred height, allowing long translations to wrap without shrinking. Its width and placement scale independently of the old reference-canvas dimensions. Pause visibility and informational/non-interactive behavior remain unchanged. Visual review also identified the temporary teaching tip immediately below the old bomb panel. HudScreen now places that tip below the actual expanded bomb card in touch layout, preserving its height and restoring the original row outside bomb races.

No changes to simulation, weapons, rewards or saves. Test runs use an isolated Unity project/preferences, webtest/NVIDIA Vulkan and AgentSink. Browser probes import the separate QA profile and do not export it. The full race/reload workflow alone advances that QA profile.

## Final validation

The initial measurements are retained in `before.json`. The browser geometry probe failed on the preceding executable with 24 repeated unreadable-text observations at 812×375 and 640×360; 1280×720 passed. The first implementation corrected size but produced four repeated intersections with the teaching tip. That failed browser evidence and first executable are retained separately.

After correcting the teaching-tip placement, **369 EditMode tests, five PlayMode scenarios and the 49.2-second physical gate passed**. PlayMode covers bomb contact/pause/explosion/respawn/retry/exit, natural bot completion, dense nameplates, localized racer names and contextual player HUD. All 284 C# files match the final isolated build source.

Final browser probes passed desktop at 1280×720, 812×375 and 640×360; mobile checks visited all nine languages plus the platform-default option. They verify physical font sizes, full text bounds, non-overlap with HUD/teaching tips, expanded goals, real touch pause and restoration of the ordinary teaching-tip row after leaving a bomb race. Russian and French screenshots were visually reviewed. The separate mobile moving-nameplate regression also passed. Captured exception/localization/glyph errors and geometry findings were empty.

A full e18 Easy race on the final executable finished second in **2:37.289**, with one death, four bomb passes and five resets (one AI recovery), awarding 206 coins. The debug world clock at Results was 179.355 seconds, including post-finish waiting; it is not the player's finish time. Fresh-context reload retained **4,424 coins / 25 races**, upgrades, loadout and campaign state. Full-race driving was automated; separate UI probes used actual mouse/touch input. No manual-victory or live advertising-SDK claim.

The final executable and evidence are archived in `Builds/BombHud-2026-10-23/`, with source hashes and `validation.json`. This iteration is verified progress, with no active blocking condition.

## Separate next investigation

The previous e18 screenshots/standings contain two identically named Bolt rivals. RaceSpawner's suffix is based on profile-array traversal rather than occurrences of a base name. Difficulty_easy references the Rookie/Bolt profile twice in its four-entry array, confirming the duplicate-base-name path. This layout change does not address it.
