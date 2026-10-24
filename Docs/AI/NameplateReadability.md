# Rival nameplate readability — 2026-10-23

The preceding iteration was verified progress: mission HUD readability, the full e17 campaign result, native mission coverage and final browser checks across nine languages. Its close-pack bomb screenshots retained a separate, visible problem: overlapping racer names.

## Evidence and correction

The baseline browser probe on the MissionHud executable recorded overlapping names at 1280×720 and 640×360, labels extending beyond the viewport, and seven-pixel text at the smaller size. There were 177 repeated frame-level violations, not 177 distinct defects. The controlled Unity test also failed when five cars projected into a dense group, both centrally and at the left edge; pause itself worked.

World health bars remain attached to their cars. Names now use compact dark plates measured from their translated text, with at least 12 screen pixels. Placement prioritizes the bomb carrier, then nearer cars, reserves the HUD and health bars, and tries nearby rows/columns. Displaced plates have leader lines rendered behind all names. A short delay before returning to the default position reduces rearrangement as a pack moves apart. The carrier's name uses the existing gold accent. Off-screen/wrecked cars do not receive floating names; far cars retain their health bars. If there is no room, lower-priority names may yield while health bars remain.

The new regression requires all five names and the carrier name to remain present in the controlled pack. The final native test covers centre, both screen edges and pause/resume, so hiding names is not sufficient to pass. This fixture deliberately parks cars and fixes the camera; it is not evidence of a driving manoeuvre or physical bomb collision. The existing racer-name and player-HUD scenarios also pass. A first compile attempt used an obsolete Unity identifier API; it was replaced with the existing game CarId before the after-test passed.

No physics, weapon, reward, serialized definition or save-schema change is included. Native tests use an isolated project and preferences. Browser tests use webtest/NVIDIA Vulkan and AgentSink.

## Final validation

The original controlled regression failed before the fix and passed afterward. The final three PlayMode scenarios (nameplates, racer names and player HUD), all 369 EditMode tests and the 49.2-second solo physical build gate passed. All 284 C# files match the isolated build source.

Final WebGL probes passed Russian desktop, Russian mobile and Chinese mobile at both 1280×720 and 640×360. Each probe checks moving-pack geometry, physical text size, HUD exclusion, a visible carrier name, and pause; mobile probes use actual touch input. No captured exception/localization/glyph errors or layout findings. Screenshots were visually reviewed. Browser observations require at least four visible names during movement; the controlled native fixture separately requires all five clustered rivals.

The full e18 Easy race completed first with driving-only autopilot, one death, four bomb passes and four resets (two AI recoveries). The results board records **2:17.328** for the player; the debug world clock at Results is 159.302 seconds and includes post-finish waiting, so it is not the player's finish time. Reward: 260 coins. A fresh browser context retained **4,218 coins / 24 races**, upgrades, loadout and campaign state. This is automated driving coverage, not a claim of manual victory or live advertising SDK verification.

The verified archive is `Builds/Nameplates-2026-10-23/`, including before/after evidence, final probes, campaign/reload evidence and exact source hashes. Remaining observed candidates: the small-screen bomb instruction is still tiny, and the starting pack contains duplicate base bot names. Neither is represented as fixed by this iteration.
