# Continuing improvement session — 2026-10-23

## Latest verified build — 06:09 UTC

`Builds/BombHud-2026-10-23/` makes the live bomb carrier/timer and instruction readable: minimum 14 and 12 physical pixels. The panel height follows translated text; the temporary teaching tip stays below it and returns to its normal row in ordinary races. Baseline readability failures and the first implementation's teaching-tip overlap are preserved as evidence.

**369 EditMode tests, five PlayMode scenarios and the 49.2-second physical gate passed.** Final browser checks passed three landscape sizes, all nine languages plus platform default, expanded goals, actual touch pause, ordinary-race restoration and moving rival-nameplate regression. No captured exception/localization/glyph errors or layout findings. See `BombHudReadability.md` for exact coverage and limits.

The full e18 Easy race finished second in **2:37.289**, with one death, four bomb passes, five resets (one AI recovery) and 206 coins. Fresh-context reload retained **4,424 coins / 25 races**, upgrades/loadout and campaign state. Full-race driving was automated. All 284 C# files match the source used for the final build. ZIP SHA256: `20b1f9349a319868c837997074d926600649e8b800c01662bcd4af4873f49961`.

Next: fix the confirmed duplicate rival-name path. Difficulty_easy includes the Bolt profile twice; RaceSpawner numbers by array traversal rather than actual repeated names. Preserve profile frequency and difficulty while disambiguating names. This iteration is verified progress; no active blocking condition exists.

## Previous verified build — 05:38 UTC

`Builds/Nameplates-2026-10-23/` fixes overlapping rival names in dense packs and tiny small-screen text. Compact plates use at least 12 physical pixels, avoid other plates/health bars/HUD, and prioritize the gold bomb-carrier name. Leader lines identify displaced plates; a short return delay reduces rearrangement.

The reproduced failure now passes. **369 EditMode tests, three final PlayMode scenarios and the 49.2-second physical gate passed.** Native geometry tests require all five rivals centrally, at both edges and after pause. Browser probes passed Russian desktop/mobile and Chinese mobile at 1280×720 and 640×360, including carrier identity, touch pause and visual review; no captured errors or layout findings.

The full e18 Easy run finished first in **2:17.328** (results board), with one death, four bomb passes, four resets (two AI recoveries) and 260 coins. The debug world clock includes waiting after the player's finish and reads 159.302 seconds at Results. Fresh-context reload retained **4,218 coins / 24 races**, upgrades/loadout and progression. Campaign driving was automated. All 284 C# files matched the build source. ZIP SHA256: `3756ebc02c363960c92ce5f9a6d2eb45edefe16034d31dcb1fa156bd1cf4fb88`.

See `NameplateReadability.md` for evidence and limits. Next observed candidates are the small-screen bomb instruction and duplicate bot names. This iteration is verified progress; no active blocking condition exists.

## Previous verified build — 04:59 UTC

`Builds/MissionHud-2026-10-23/` makes the mission HUD readable on small screens: boss name/core at least 14 pixels, route/count/distance at least 12. Expanded mission goals use the free upper-left corner; long Portuguese text gets enough height for two lines. The distance label has a dark background and its marker chooses free space around HUD panels and touch controls. Three browser review passes exposed and corrected actual layout problems; failed evidence is retained.

**369 EditMode tests, the final contextual-HUD PlayMode scenario and the 49.2-second physical gate passed.** An earlier full mission simulation in this iteration completed naturally with the mission driver/gunner bot, nine raids and no runtime errors. Final browser checks passed three desktop sizes and all nine languages with real touch firing, expanded goals, marker/label overlap checks and pause. See `MissionReadability.md` for exact scope; the browser UI probes are not a manual mission-victory claim.

The preceding-build e17 race finished first in **2:51.838**, without deaths, with 240 coins. Both preceding/final executable reloads retained **3,958 coins / 23 races**, upgrades/loadout and campaign progress. Captured browser exception/localization/glyph errors were empty. Source manifest: 1,356 files; all 283 C# files matched. ZIP SHA256: `308f8978a243e70d834089ca800e75b4310bfff0cf3b79f33a97ba8c6ae4240f`.

Next: investigate the already observed overlapping racer nameplates in close starting packs, then continue direct mission combat coverage. This iteration is verified progress; no active blocking condition exists.


## Previous verified build — 04:03 UTC

`Builds/RacerNames-2026-10-23/` fixes inconsistent racer names in death, kill and bomb-owner notifications. Literal names and already translated mission-unit names no longer become ellipses. Rich-text notification/feed names are escaped. The original regression fails before the change and passes afterward; **369 EditMode tests, six PlayMode scenarios and the 49.2-second solo build gate passed**.

The browser comparison reproduced «Бомба у Bolt 2» in the old build and verified «Бомба у Болт 2» in the new one during ordinary bomb play. The preceding e15 run finished second; the new-build e16 run finished second in **3:22.025**, with two deaths, five resets (one AI recovery), and 176 coins. Fresh-context reload confirmed **3,718 coins / 22 races and e17 unlocked**, preserving upgrades/loadout. Captured exception/localization/glyph errors were empty. Campaign driving is automated; controlled death and bomb-transfer stimuli in Unity are explicitly separate from browser input.

See `RacerNameConsistency.md` for reproduction, evidence and limits. Source manifest: 1,356 files; all 283 C# files matched the workspace. ZIP SHA256: `cae077e9857ff174b1cc8d929eb863db04832782e1dda38227ba9ef1d68279d5`. The next useful coverage is e17 and missions; overlapping distant nameplates in a close starting pack are also a concrete readability candidate. This iteration is verified progress, with no active blocker.


The user requested several hours of running, playing and improving the game. This session began at 2026-10-22 23:44 UTC (02:44 Istanbul), continuing the already active improvement goal. Work proceeds in tested iterations; elapsed time alone is not a success criterion.

## Previous verified build — 03:39 UTC

`Builds/DrivingHelp-2026-10-23/` contains contextual driving help, twelve new localized entries across all nine languages, updated static Chinese glyphs and browser evidence. ZIP SHA256: `f6466a4f00704eff7cf3386399bcab0ddf61100ca0c28491f8f4028327155516`. Basic controls remain visible in special events; the help follows compact/full/touch weapon selection and equipped secondary, and mine start hints identify the action. It no longer uses garage-specific instructions in a race.

**369 EditMode tests, three PlayMode scenarios, the 49.2-second solo build gate, actual desktop mine/loadout/wheel input, actual touch mine input and readable scrolling help across all nine languages passed.** The language selector includes a separate platform-default choice, so Ukrainian was verified in a supplemental run. No captured exception/localization/glyph errors.

The preceding-build campaign completed e12/e13 second, with no deaths, and preserved 3,126 coins / 19 races. On the new build, e14 completed first in 3:58.123 after one actual continue offer was accepted through the local mock SDK. Reward 240; reload confirmed **3,366 coins / 20 races and e15 unlocked**, with loadout/upgrades retained. Driving was automated; the separate input probes used keyboard/touch events. See `ContextualDrivingHelp.md` for evidence and limits.

The next observed inconsistency is the e14 death toast showing raw “Vega” while the Russian kill feed shows “Вега”. The captured continue screenshot and `GameFlow.OnWreck` provide a concrete next investigation. This iteration is verified progress; no active blocking condition exists.

## Previous verified build — 03:10 UTC

`Builds/AiRecovery-2026-10-23/` contains the new WebGL ZIP, exact build-source hashes, before/after regressions, **369 passing EditMode tests**, **six passing PlayMode scenarios**, fixed-seed physical comparison and full browser campaign/reload evidence. SHA256: `6e4a03d2fa35b44656dd539404ef27d93813c47ba2fdd98318047cf7c8ff9737`.

A controlled reproduction showed that 8 cm barrier-like jitter produced 4 m/s forward-speed spikes and prevented AI recovery indefinitely. The corrected driver clears the stall timer only after two net forward metres. It preserves the initial grace and six-second deadline, suspends the stall manoeuvre during freeze, and clears it on teleport. Slow travel and crossing the spline seam do not trigger reversing. Manual braking/idle input still do not trigger player recovery. The original random-seed collision was not replayed exactly; see `AiRecoveryInvestigation.md` for the narrower causal claim and controls.

On the new WebGL build, driving-only autopilot completed **e10 first in 3:24.558**, no deaths, 240 coins, and **e11 third in 3:13.149**, two deaths, 128 coins. e11 telemetry captured two actual AI recoveries after reverse attempts, followed by continued travel; sampled stall time stayed below the six-second deadline. Five total e11 resets include other recovery causes. Fresh browser contexts verified **2,406 / 15 → 2,774 coins / 17 races**, preserved upgrades/loadout and **e12 unlocked**. Captured exception/localization/glyph errors were empty. Browser tests used webtest/NVIDIA Vulkan and AgentSink.

The ordinary solo lap remains 49.2 s with no death or automatic recovery. A one-lap e10 Easy comparison with seed 12345 finished every car using both old and new AI; individual combat outcomes were mixed, so this is not a universal lap-time improvement claim. The measuring harness now explicitly bypasses the unlock gate only on its cloned event and restores its test save. Ordinary browser progression is unchanged.

Next useful play coverage: e12 onward on the preserved profile, and direct combat-input scenarios. Avoid changing difficulty based on a driving-only bot that does not fire. There is no active blocking condition; this iteration is verified progress.

## Previous verified build — 02:35 UTC

`Builds/FinishConsistency-2026-10-23/` contains the newest WebGL ZIP, source hashes, **369 passing EditMode tests**, **three main PlayMode scenarios plus one DNF/timed-Arena scenario**, original failing regressions, browser evidence and save-reload checks. Rewards now use the per-victim counts recorded at the finish line; survival agrees with recorded deaths. Race-time formatting floors complete milliseconds consistently with the HUD and handles minute boundaries, system culture and invalid values. The solo prebuild lap remains 49.2 s with zero deaths/automatic recoveries.

The preceding-build campaign finished e9 second and e10 fourth; e11 correctly stayed locked. The repeat e10 on the new build finished second and unlocked e11. Fresh browser contexts verified the profile before and after: **2,230 / 14 → 2,406 coins / 15 races**, with upgrades and loadout retained. Captured exception/localization/glyph errors were empty. Driving was automated; real input and UI checks are separately documented.

Earlier `Builds/FinishTransition-2026-10-23/` and `Builds/Playtest-2026-10-23/` archives contain the finish card, 20 mobile UI states, nine-language touch labels, actual two-finger input, results-footer, music and broader campaign evidence. Earlier sections below are a chronological record, including superseded pending statuses and failed probes.

At the 02:35 checkpoint the long first-e10 barrier stall remained unresolved; its absence in a repeat random-seed run was not proof of a fix. The subsequent recovery investigation and correction are documented above.

## Verified baseline

`Builds/ArsenalUsability-2026-10-23/` contains the prior WebGL build, 357 passing EditMode tests, four passing PlayMode scenarios, a 48.4-second solo physical lap and twelve browser state/screenshot records. Browser automation runs through `webtest` on NVIDIA Vulkan; audio remains on `AgentSink`. Tests use isolated project copies and isolated browser storage.

## Current iteration: small-screen readability

Observed at 812×375: arsenal descriptions, garage captions and driving help are too small, some around 8–10 pixels. The desktop reference canvas is the cause. The help overlay also shows the underlying pause controls through its surface.

Changes under validation:

- Menus have a minimum readable scale with compact headers and a scrolling garage on short viewports. Gameplay world canvases retain their coordinate system.
- Compact home layout keeps the Play action visible.
- Driving help keeps a fixed readable font in a scroll view, resets its scroll on reopening and hides the underlying pause controls. Pause/help sizes remain within the screen.
- Weapon names use a larger, single-line label rather than shrinking indefinitely.
- Preview/rotation arrows are larger. Inspecting a body on the scrolling layout returns to its showcase.
- Escape and the arsenal Back button use the same destination.
- Debug UI snapshots expose font pixel sizes, control bounds and text truncation for measurable browser assertions.

Validation pending: Unity tests/build, actual interactions at 812×375 and 640×360, nine-language settings, rotation/resizing back to desktop and the existing experience regression probe.

## Next play sessions

Run complete races through real simulation (no QA finish shortcut), check steering/braking/firing/reset input, then collect outcomes across beginner progression and special modes. Distinguish automated driving from direct input scenarios in reports. Investigate observed failures before changing balance. Preserve saves and unrelated concurrent changes.

## Findings from the first long browser run

- Actual keyboard fire changed ammo/reload state; Q changed the active weapon; R incremented recovery count.
- Holding the brake for 7.5 seconds at the start triggered an unwanted automatic recovery (0 → 1). The onboarding timer now requires acceleration intent without braking. A real-time PlayMode regression passed: braking and idle input do not reset, blocked acceleration recovers once, then stays latched until movement. The first version of that test incorrectly advanced simulation frames for a real-time timeout; it was corrected to use real-time waits.
- e1 Easy: natural finish #3, 2:41.068, six total resets including the deliberate manual reset; normal reward 150 and e2 unlocked.
- e2 Normal: natural finish #5, 3:07.895, seven resets; normal reward 80. e3 correctly remained locked because progression requires a top-three finish. The probe's assumption that any finish unlocks the next event was wrong; its attempted e3 start was rejected. Do not call the entire three-race probe a pass.
- The results recommendation after that e2 finish suggested farming e1 kills. A better recommendation should prioritize the top-three finish needed to unlock e3. This remains a follow-up task.
- At 812×375 the new garage and help are visibly readable. Help measured 16.9 px and fit completely. The probe incorrectly demanded scrolling even when all help text fit; its assertion now requires movement only when content exceeds the visible area. 640×360 and the complete language/resize sequence still need validation.
- TMP's optimized numeric SetText can leave the raw text property stale (the timer snapshot showed its initial string while the car moved). UI diagnostics now include parsed rendered text, and race-time snapshots expose the actual race clock. Use the authoritative clock for rotation/pause checks.

Current validation is rebuilding the combined readability/recovery changes and a dedicated orientation pause reason. That reason composes with user/ad pauses and is cleared when the menu component is disabled. No balance changes have been made.

## Follow-up: recommendations and repeatable journeys

The cup recommendation now prefers an available prerequisite for a locked cup race over farming stars in an earlier race. Unplayed open races still take priority. After an attempted race, the results goal names the top-three requirement when that opens more content. A PlayMode scenario is checking the browser-observed e1/e2 state, retry action, then e3 selection after unlocking, and finally star farming after all three races have a finish.

The next full browser journey uses Easy consistently, records locked progression as a placement requirement rather than a crash, buys the first armor upgrade through the garage, and can save its isolated IndexedDB/local storage state for later sessions. The driving-only QA autopilot does not have an AI gunner; its finishing position alone is not evidence that opponent difficulty is unfair.

Validation update: combined readability/recovery/orientation changes passed **358 EditMode tests and five PlayMode scenarios**, plus the solo prebuild lap (48.4 s, no deaths). The subsequent cup-recommendation change passed **two targeted PlayMode scenarios**, including the failed-podium retry / newly unlocked race / missing-star fallback sequence. Its separate WebGL build is now running. Browser mobile checks and the next natural journey are still queued/running through the machine-wide limiter; do not label those complete yet.

## Font and QA profile isolation

The nine-language browser check found U+6587 (文) missing from the Chinese selector's hard-coded autonym 中文. CSV coverage alone did not include it. `FontAtlasBaker.UiSymbols` now includes the autonym, and `loc-bake.sh` reads that same constant when subsetting the TTF. Its Unicode manifest is per-work-directory instead of a shared `/tmp` filename. The TTF expands from 705 to 706 code points; atlas/build verification is running.

On this Linux machine, project copies used the same `~/.config/unity3d/DefaultCompany/CarsGame/prefs` file. Unity's [PlayerPrefs documentation](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/PlayerPrefs.html) describes company/product-based storage. `Tools/qa/common.sh` now gives each QA work directory its own `XDG_CONFIG_HOME` (overridable with `CG_QA_CONFIG_HOME`). The next run explicitly checks that PlayerPrefs were created there. A snapshot of the existing Editor preferences was preserved at `/tmp/cars-profile-safety/editor-prefs-2026-10-23.xml`; it is not restored over any concurrent work. Earlier play scenarios restored their captured SaveData; the race harness naturally recorded its races in the then-shared Editor profile. Browser profiles have remained isolated throughout.

## Completed readability / recovery validation

- Final combined run: **358/358 EditMode and 6/6 PlayMode tests passed**, including recovery intent, progression recommendations, loadout/touch rules and garage scenarios.
- QA PlayerPrefs isolation is now verified on disk at `/tmp/cars-polished/config/unity3d/DefaultCompany/CarsGame/prefs`.
- Localization: 416 keys × nine languages, 706 TTF code points, 675 atlas characters, no missing glyphs during baking. Browser language verification is still in progress.
- WebGL build succeeded at `/tmp/cars-polished/build/`; the solo prebuild lap on the now-isolated default profile took **49.2 s**, with zero deaths and zero automatic recoveries. Earlier 48.4 s timings used the previous QA profile.
- The repeat natural journey (`/tmp/cars-readable-final/races-retry/`) passed all three races: e1/e2/e3 each finished second; each paid 176 coins. e1: one deliberate reset, no deaths; e2: two resets, one death; e3: one reset, no deaths. Armor was purchased through the garage for 150 coins after e1. Saved browser profile: `/tmp/cars-journey-state.json`. These were driving-only autopilot races after explicit real keyboard input checks.
- Special-mode journey is ongoing. e18 naturally finished third, with three bomb passes, one death, five recoveries and a normal reward of 148 coins. The driver is automated; do not interpret its recovery count as a human usability rating.

## Next iteration: results and help navigation

The actual e3 result screenshot revealed that initial keyboard focus scrolls the results overview to the Next action and clips the reward above the viewport. Main result actions now sit in a fixed footer outside the scroll area. The reward remains at the top on arrival; retry/next/garage stay reachable while reading goals and standings. PlayMode and browser regressions are in progress.

Escape previously toggled the user pause while driving help was open, resuming immediately. It now consumes the help layer first, preserving all pause reasons and returning focus to Resume. The gamepad Back button closes the same help layer. Closing via the on-screen button uses the same method.

## Natural special-mode and mobile findings

All three special races finished normally in the browser without finish/unlock shortcuts: e18 third (148 coins, three passes, one death), e19 third (128 coins, one death), e20 second (176 coins, three deaths). The e20 complete result phase arrived at race clock 233.8 s. The test timeout is now configurable, defaulting to 300 s for longer events; this run passed the original 240 s limit. Browser error capture was empty.

The polished mobile probe passed all **20 recorded states**, including Chinese/German arsenal labels, all nine language settings, 640×360 and 812×375 layouts, orientation clock suspension/resumption and retention of an existing user pause. The build and evidence are archived under `Builds/ReadabilityAndRecovery-2026-10-23/`.

A new probe uses actual CDP touch input without the debug force-touch command. It confirmed automatic touch controls and found undersized 640×360 targets: Brake 40 px, Swap 30 px, Reset/Pause 33.3 px and Goals 20.7 px. Baseline evidence: `/tmp/cars-polished/touch-baseline/`. Controls now reserve at least 44 pixels for those actions. The adjacent health/weapon panels move left to preserve spacing. Goal text uses a 12 px floor on touch layouts and expands to its measured text height. These changes are under validation.

The first results-footer regression found a second focus path: the ad button inside the scroll became the first selectable after the main actions moved out. Results now explicitly focus the main fixed action. The targeted PlayMode run then passed 2/2. The failing XML is retained as `/tmp/cars-results/initial-focus-failure.xml`.

## Night visibility and continued campaign

The first natural e4 run showed near-black asphalt and a barely visible player body in the darker track sections (`/tmp/cars-readable-final/combat-races/e4-driving.png`). The night preset now raises blue ambient fill and moonlight slightly, lowers contrast and vignette, and keeps the dark sky, stars and headlights. The matching editor preset factory has the same values, so regeneration preserves the change. Browser comparison is pending; no race-length, damage or opponent balance change has been made.

The long e4 run completed first, paid 240 coins and unlocked e5. It retained the authored five laps. The same saved profile is now continuing through e5/e6.

The touch target/UI changes passed the two targeted PlayMode scenarios and the 49.2 s solo prebuild lap. The WebGL build succeeded. A queued browser batch will check Escape-from-help, actual multi-touch steering/fire/pause behavior and music lifecycle. The subsequent HUD change also gives brief touch hints a 12 px floor with measured height, and hides hints while expanded goals are being read.

The old `music-check.mjs` and `browser-smoke.mjs` defaulted to SwiftShader and could leak the browser on failure. They now require the webtest environment, launch NVIDIA Vulkan, verify the renderer and close in `finally`. The music probe also parses string debug snapshots correctly when waiting for results.

## Results and real touch input verified

The results browser probe passed at 1280×720, 812×375 and 640×360: reward visible on arrival, all three footer actions visible and stationary during scrolling, Garage reachable, rewarded doubling adds exactly one extra normal reward and removes its action, Retry starts the same event. Evidence: `/tmp/cars-results/browser/`.

The new mobile batch passed its 20-state UI probe, including Escape returning from help to pause with an unchanged race clock. The actual CDP touch probe also passed: automatically enabled touch controls (no forced touch flag), minimum target sizes, expanded goal readability, two fingers steering/firing together, weapon swap, manual recovery and release of FIRE across pause/resume. Screenshots caught a visual issue the first assertion set missed: the last letter of the Russian swap label wrapped onto a second line. Label padding/wrapping and a line-count assertion now address it.

The e4/e5/e6 natural journey finished first/first/second, paid 240/240/176 coins and saved progression. **The overall probe failed**, correctly, because the shield status icon ◆ was absent from the font. Its results are retained under `/tmp/cars-readable-final/combat-races/`. The icon set is being baked, the unsupported frozen star is replaced with an already supported outlined star, and a static-atlas coverage regression covers all status symbols and 中文.

Music lifecycle checks all passed except the initial no-gesture assumption: headless Chrome allowed playback without an explicit blocking policy. The probe now sets a user-gesture-required policy for that case; the rerun is pending. No game audio logic was changed. The browser always closed despite the failed assertion.

## Final regression underway

The combined font bake succeeded with **678 static atlas characters**, 709 TTF code points and no missing requested glyphs. The new coverage regression and the existing suite passed **359/359 EditMode tests**. Isolated PlayerPrefs were verified again in the final QA directory.

The passive music probe passed after eliminating Playwright's synthetic user gesture from initial diagnostics. The initial blocked/suspended state, real gesture unlock, menu/race/finish tracks, tab hiding, advertising pause and resumption all passed. This was a test-harness correction; game audio code is unchanged. Evidence: `/tmp/cars-touch/music-passive.txt`.

Night before/after captures are under `/tmp/cars-night-before/shots/` and `/tmp/cars-night-after/shots/`. The darker sections are more legible while the sky and headlights retain their night appearance. URP logs an unsupported optional FSR pass; local package code shows its validity is separate from other postprocessing passes. No package or global render setting was altered. Mock SDK loading still produces its expected local `/sdk.js` 404.

The night captures also exposed a stale timer on countdown after a retry. `HudScreen.SetVisible(true)` now clears it immediately. Unity and browser regressions assert the displayed `0:00.000` before the new race starts. The final PlayMode/build/browser pass is still in progress.

## Repeat campaign on the combined build

The repeat e6 minefield race finished second in 190.7 s, paid 176 coins and recorded two deaths/two recoveries. e7 reversed finished second in 214.7 s, paid 176 coins and unlocked e8; two deaths/five recoveries. Both completed without the prior shield-glyph warnings. These are driving-only autopilot outcomes, not human performance measurements. The e8 high-damage race finished third in 198.1 s, paid 128 coins and unlocked e9; three deaths/four recoveries. All three full-distance races passed, with no captured exceptions, missing localization keys or missing glyphs. The isolated profile now has 1,966 coins and 12 completed races. Final UI/persistence probes passed; the saved profile reloaded with matching coins, race count, upgrades, loadout and e9 availability.

## Next iteration: visible finish and immediate results

The natural race recordings exposed an avoidable dead period after the player crosses the finish: input is disabled while `RaceManager.finishGraceSeconds` waits up to 20 seconds for the field, but the HUD has no participant-finish feedback. Keep the existing grace period available for spectators, show an explicit finish card with the recorded position/time, and offer a Results action gated on an actual player finish. This must preserve the frozen finish statistics and normal reward path, remain inaccessible before finishing, and never grant duplicate rewards.

Finish-transition implementation: a centered, readable finish card uses existing translations for the title, position and Results action. The personal timer freezes at the recorded crossing time; rivals keep racing until the existing timeout or the player's action. Touch driving controls disappear while the card is available, and pause/rotation hide it temporarily. A natural one-lap PlayMode test exercises the new flow. Its first compile attempt missed a test namespace import; the first runtime attempt incorrectly equated simulation frames with the real finish-grace clock. Both test-harness issues were corrected; passing verification is still pending.

Finish-transition validation update: **3/3 targeted PlayMode tests and 359/359 EditMode tests passed**. The new PlayMode scenario physically completes a one-lap circuit with a driving-only autopilot, keeps one rival stationary so the spectator phase remains available, then verifies clock freezing, hidden touch controls, pause/focus restoration, frozen finish statistics, single reward and retry reset. It uses no finish shortcut. The WebGL build and full-distance browser scenario are pending.

Follow-up candidates from code inspection (not browser-confirmed failures): `UiFactory.FormatTime` rounds seconds independently of minutes, so a value just below a minute boundary can format as `0:60.000`; the HUD floors milliseconds instead. Also, `GameFlow.OnFinished` reads live per-victim kill counts for coin calculation although `RaceManager.ReadResult` freezes the player's kill total at the line. Investigate boundary formatting and late-projectile rewards after the current finish transition is verified; do not infer a reproduced reward bug from this inspection alone.

## Finish transition completed

The final WebGL probe passed a full three-lap e7 race with no finish/unlock shortcut, showed the new card, froze the personal timer while race time advanced, hid driving controls, resized to 640×360, paused the grace period in portrait, returned from the pause menu and activated Results with Enter. It verified one normal reward and one completed race. Captured exception/localization/glyph errors were empty. Screenshots were inspected. The passing build and evidence are archived under `Builds/FinishTransition-2026-10-23/`; the source remains identical to its build manifest. The earlier failing test import and clock-assumption results remain in `/tmp/cars-finish/` for traceability.

## Next iteration: consistent finish accounting

The previous goal turn made verified progress: two archived WebGL iterations, completed runtime/UI changes, passing tests and natural browser evidence. Current work starts from that build and investigates the two recorded consistency candidates. Regression cases cover minute boundaries, invalid times and a comma-decimal system culture. The natural-finish scenario now injects deterministic score events before/after the line to compare final reward and survival with the frozen record. The first compile attempt used `Assert.Multiple`, unavailable in this project's NUnit version; it was replaced before reproduction. No production fix has been applied yet. A separate browser journey is running e9/e10/e11 on the preceding verified build.

Reproduction confirmed: four time cases failed (minute/hour boundaries, comma-decimal culture and out-of-range input). The finish test recorded one kill/zero deaths but received 290 coins instead of 260 and reported `Survived=false` after injected late events. Fixes now capture per-victim counts at the line, use that snapshot for payout, derive survival from recorded deaths, and format complete milliseconds with invariant separators. The initial fixed PlayMode set passed 3/3; all EditMode tests and the build are running. Additional coverage is being added for DNF and timed Arena, which must capture combat at race end rather than at a lap finish.

The preceding-build browser journey completed e9 second (219.1 s, no deaths, 176 coins) and e10 fourth (292.0 s, no deaths, 88 coins). e11 correctly stayed locked. The isolated saved profile now contains 2,230 coins and 14 races. e10 spent roughly 16–93 s at track distance 137 m with throttle applied and no recovery, then recovered once before 108 s. The screenshot shows the car pinned against a barrier; samples preserve throttle, steering and AI recovery count. This is a reproduced long AI stall, not evidence for changing opponent difficulty. Investigate recovery's instantaneous forward-speed reset, which may be cleared by barrier jitter; root cause is not yet proven.

The consistency build passed **369/369 EditMode tests**, three main PlayMode scenarios and the additional DNF/timed-Arena scenario. The zero-lap DNF keeps its combat tally but pays no coins; the timed Arena includes all kills at match end. The prebuild lap remains 49.2 s, zero deaths/automatic recoveries. Source hashes match the workspace C# files. The new browser context restored the old profile at 2,230 coins / 14 races; the repeat e10 run is underway.

Next AI investigation should reproduce the observed bounded barrier motion against the actual `AiDriver.Sample` recovery path. One candidate fixture feeds mostly stationary progress with brief forward-speed spikes, then checks whether recovery remains bounded; contrast that with sustained forward travel and freezing. Confirm the cause before choosing a net-progress window or changing recovery thresholds. Browser race seeds are currently random and were not recorded, so the long e10 stall cannot be called an exact seeded replay yet.

## Finish accounting completed

The new-build e10 run finished second, paid 176 coins, unlocked e11 and saved the 15th race. A fresh browser context matched coins, race count, loadout and upgrades and confirmed the e11 gate. All tests and browser checks for this iteration passed; `Builds/FinishConsistency-2026-10-23/` retains both the before-fix failures and the verified build. Continue with an actual-driver recovery fixture for the separately observed long e10 barrier stall.
