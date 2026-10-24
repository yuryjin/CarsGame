# Player experience improvements

Implemented 2026-10-23. Scope: onboarding, weapon selection, HUD, drift feedback, event goals, and reasons to continue racing.

## Player-facing changes

- The first three events form the Driving School cup: combat, two drift boosts, then a complete bridge shortcut. Events e2/e3 now last two laps. Hints explain the current mechanic and acknowledge successful drift/shortcut/ceiling actions. A help screen in Pause repeats controls and goals.
- Garage loadout: choose two of the existing primary weapons, keep the existing purchased secondary slot, and swap using Q, the mouse wheel, keys 1/2, gamepad D-pad up/down, or a touch button. The free-arsenal toggle restores the full weapon wheel. The machine gun still requires ownership. Invalid or duplicate selections fall back to two usable weapons. Weapon cards describe their role and base stats; the existing garage test drive lets players try the equipped pair against a moving rival.
- Combat buttons follow the actual event rules. Hot Bomb hides fire, secondary, and swap; mines-only hides fire and swap. The controls hint explains the event instead of asking players to fire a disabled gun.
- Goals expand during countdown and collapse to a small tappable summary while racing. They can be expanded again. Start-of-race control hints are shorter; full help remains available from Pause.
- Drift meter ticks match the actual charge thresholds (0.6/1.4/2.4). The meter explains holding/releasing the brake, stage cues rise in pitch, and a short confirmation remains after a successful release. Existing speed lines and camera boost response are reused.
- Distinct second-star goals: e2 drift boosts, e3 completed shortcut, e18 bomb transfers, e19 ceiling boost, e20 clean final lap. e19's third star requires finishing without deaths or resets. Runtime counters are copied into the finish snapshot; later events cannot change earned stars. Existing saved stars remain earned.
- Three cups combine existing events. Each needs six stars and a recorded finish in all three events: Driving School (e1/e2/e3, Sun paint), Combat (e4/e5/e18, Twin Stripes), Extreme Routes (e17/e19/e20, Gold paint). Rewards reuse cosmetic ownership and its cloud union; no extra coin ledger or duplicate payouts. Results show a specific next event/goal and the associated cup reward.

## Compatibility and boundaries

- Save version 5 adds `reservePrimary` and `fullArsenal`. Version 4 saves retain their equipped primary, purchases, stars and money, and receive the shotgun as reserve. Latest loadout selection follows the newer cloud save. Cosmetics are granted again idempotently after merged progress is read.
- No new packages, scene wiring or prefab changes are required. Translations live in the existing nine-language CSV and generated string tables.
- The touch reset button and its hint use `R`, supported by the shipped font; the old reset-arrow character was missing from the CJK fallback atlas.
- The workspace also contains concurrent garage-preview/test-drive work. This change integrates with that existing `StartTestDrive` flow rather than creating a second practice mode.
- QA-only `DebugBus` snapshots include current equipment and skill counters. The `ui` command returns active button positions and text for browser tests. Existing debug-channel access restrictions remain in place.

## Verification

- EditMode: **345/345 passed**, including migration, cloud merge, weapon availability, skill-star conditions, clean-run rules and idempotent cup rewards (`Builds/PlayerExperience-2026-10-23/edit.xml`).
- Physical PlayMode scenarios passed: drift release, two Hot Bomb scenarios, Star Ring and the bridge in both directions. These exercise actual collisions and movement, including rejecting shortcut credit after falling/resetting (`Builds/PlayerExperience-2026-10-23/play-vulkan.xml`).
- The completed graphics run exposed a delayed goal-card collapse. The start event now refreshes the goals immediately, and layout changes preserve their expanded/collapsed state. The targeted UI regression then passed **1/1**, covering loadout selection, touch swap, goals, pause help and hidden combat controls in Hot Bomb (`Builds/PlayerExperience-2026-10-23/play-ui.xml`). Together with the five physical scenarios, all six selected PlayMode scenarios passed.
- Graphics checks use Vulkan on the RTX 4070 Ti. Browser checks run through `webtest`, assert an NVIDIA renderer and close Chromium in `finally`. Audio stays on `AgentSink`.
- Pre-build gate: a solo e1 lap on Normal completed in **48.4 seconds**, with zero deaths and zero automatic resets (`Builds/PlayerExperience-2026-10-23/race_prebuild.json`).
- WebGL build succeeded: `CarsGame-web-0.2.0.zip`, approximately **18 MB** (`Builds/PlayerExperience-2026-10-23/`).
- Browser smoke **passed** at 1280×720 and 812×375: loadout persistence after reload, the full-arsenal toggle, keyboard switching, goal expansion/collapse, cups, the next-goal result card, Hot Bomb, mines-only controls, touch weapon swap and pause help. It completes prerequisite events through the QA finish hook before opening the locked mines event; this is UI validation, not a balance playtest. Eight key screenshots and `evidence.json` are in `Builds/PlayerExperience-2026-10-23/browser/`. All screenshots were reviewed. No captured JavaScript/C# exceptions, missing localization keys or missing font characters; `errors.json` is empty.
- Browser reproduction: serve the WebGL output with `Tools/qa/serve.py`, then run `URL='http://127.0.0.1:8789/?sdk=mock' webtest -t 600 -- node Tools/qa/player-experience.mjs`. The harness uses an isolated browser context and closes it after the run.
- Automated checks are not a novice playtest. Physical gamepad ergonomics, human comprehension and subjective sound quality still need hands-on evaluation.

## Five-player playtest protocol

Use five people who have not played the game. Start each with a fresh profile; include both keyboard and touch users. Ask them to play the Driving School cup without coaching. Stop after three completed races or 15 minutes. Do not change difficulty or assist them mid-run; record where assistance was needed.

| Participant | Device | First meaningful shot | First successful drift boost | Understands shortcut risk | Starts next race unaided | Confusion / exit reason |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | | | | | | |
| 2 | | | | | | |
| 3 | | | | | | |
| 4 | | | | | | |
| 5 | | | | | | |

After playing, ask: “What gives you a drift boost?”, “Why did you lose your last race?”, and “What would you try next?”. Record their words. Prioritize the two most common obstacles; compare a later version with new participants. No human results have been collected by this implementation task.
