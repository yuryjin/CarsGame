# Extended improvement session — 2026-10-24

User requested another approximately ten hours of improvement. Started 20:18 Istanbul; working window to approximately 06:18 on 25 October. Baseline `7cd2e5c`, clean tree; previous iteration's verified build is `Builds/Polish-2026-10-24/`.

Work and raw evidence: `/tmp/cars-endurance-20261024/`. Use isolated Unity copies, GPU-native tests where rendering is required, all browser runs through `webtest` with NVIDIA Vulkan, audio only through AgentSink. No publication or commits are implied.

## Work streams

1. Input and pause boundaries: reproduce commands buffered while menus are open, repeated transitions, touch/keyboard/gamepad consistency.
2. Long sessions: object/memory growth, race/garage/menu reuse, pause/ad/visibility interactions.
3. Driving and combat: varied tracks, difficulties and seeds; investigate repeatable failures before changing balance.
4. Presentation and player guidance: inspect actual play at desktop/mobile sizes; improve observed confusing or unreadable states.
5. Integrate, rerun affected regressions, build, archive source hashes and final evidence.

## Findings under investigation

- Input pause boundary: `PlayerDriverInput.Update` reads reset edges while paused, but simulation does not consume them until resumed. A real Input System regression will distinguish intentional held controls from stale one-shot commands. Confirmed by two failing PlayMode tests before the fix; both pass after clearing pending controls on pause and rejecting paused sampling.

## First iteration evidence

- Input: two before-fix failures reproduced stale reset commands. After-fix: both pass; fresh reset input still works once.
- Flow/clock: two more before-fix failures reproduced lost host/ad/hidden pause reasons on menu transitions and a race clock reset independently unpausing. Four combined pause tests pass after preserving external reasons and synchronizing the new race clock.
- Repeated garage/track lifecycle: eight rounds of four body previews plus e1/e16/e17/e19/e20 countdowns and menu returns. GameObjects stayed at 925, meshes at 252 and RenderTextures at 12, but materials grew by **871 every round**, from 1173 to 7270. Delta names: 845 ordinary TMP instances, six TMP fallback instances, twenty unnamed Lit materials. Total allocated native memory increased from 391,778,233 to 414,565,046 bytes (not an isolated whole-app memory benchmark).
- Installed TMP source creates a material instance for each outline setter and does not dispose that instance in TextMeshProUGUI.OnDestroy. UI styles now use a shared immutable material per font/outline width; cannon fallback materials use the existing shared WeaponArt cache. Same lifecycle measurement is rerunning.
- An independent, immutable baseline WebGL run is driving e16–e20 on Normal using the previously earned profile. A browser reproduction of pause input/visibility is queued behind that run.

Lifecycle rerun passed: all eight samples have **169 materials, 252 meshes, 12 RenderTextures and 925 GameObjects**. The final allocated-memory sample was 392,723,039 bytes versus 414,565,046 before, but total native/managed memory still includes editor/caching/GC effects; the strong result is removal of the deterministic +871-material-per-round leak. Full PlayMode and the first WebGL candidate are now running.

Next visual finding to reproduce: drift charge/release text uses the original 1920×1080 canvas sizing (18–24 reference units), which can shrink to 6–8 physical pixels at 640×360. Unlike the main HUD, SpeedFx has no physical-pixel minimum.

## Browser confirmations and next iteration

- Baseline e16–e20 Normal naturally completed all five events with the previously earned save. e16/e17 had frequent recoveries; the broader immutable baseline matrix is measuring all 20 events at all three tiers before changing driving behaviour. Driving-only autopilot is not a claim of human balance or combat proficiency.
- Browser pause baseline v2 reproduces stale R (resets 0→1 on resume) and hidden-tab transition dropping its pause. Candidate 1 passes all three checks, including overlapping user/visibility pause.
- Real keyboard drift baseline v2 reaches stage 1 and earns a boost, while charge text measures 8 px at 640×360. Native after-fix test passes with a 14 px minimum. Candidate 2 browser languages are in progress.
- Full initial PlayMode suite: 43/45. Two fixture issues repaired: resource comparison now compares actual progression rather than the asynchronous save timestamp; mine blast check stops unrelated AI inputs and revives its target after the autonomous combat phase. Targeted fixture rerun: 3/3 pass.
- Compact vital HUD baseline: lap labels measure 9.3 px at 640×360 and 10.7 px at 812×375 in keyboard and touch layouts. Health/ammo likewise shrink below a readable size. Iteration 3 introduces physical-pixel minimums and grows their cards; browser visual/layout validation pending.
- Reviewing the new pause input boundary revealed a possible perfect-start interaction: clearing cached throttle could turn a key held in pause into a fresh launch press. Added actual keyboard regression cases before deciding the correction.

## Integrated checks around 21:10

- Candidate 2: real keyboard drift charge/release passes at 640×360 in all nine languages, with NVIDIA Vulkan rendering.
- Full candidate-3 PlayMode run: **46/46 pass**. Warming from preceding tests changes the absolute lifetime baseline to 196 materials / 286 meshes / 12 RT / 934 GameObjects, but all eight resource samples remain constant.
- The perfect-start interaction was reproduced with real Input System input: held W while paused incorrectly awarded the launch bonus (1 of 2 cases failed; fresh post-resume press passed). RawThrottle now observes physical throttle independently of neutral paused driving commands. All six launch/input/flow pause checks pass after the correction.
- Added four track guidance messages across nine languages. Abyss announces disappearing barriers at lap 2 and narrowing at lap 3; Space explains the rings and unguarded road. CSV + Unity tables imported (436 keys), Chinese font subset rebuilt (700 baked chars, no missing glyphs). A first bake caught a HUD local-variable scope error; it was fixed and the second bake succeeded.
- Candidate 4 incorporates these changes plus a debug-only read-only `resources` snapshot. Enumeration happens only on explicit request, enabling long WebGL sessions to check resource lifetime without forced GC or restarts.
- `Tools/qa/endurance.mjs` prepared for uninterrupted natural races with driving-only autopilot, actual menu clicks, pause/visibility checks, resource snapshots, and a final save/reload comparison. Needs a short validation run before multi-hour use.

## Additional checks and extended measurements

- Integrated EditMode suite: **371/371 pass**.
- Track hint filtered run: Abyss/Space passed. PlayerExperience also enters the garage and failed because that invocation mistakenly used `CG_NOGRAPHICS=1` (RenderTexture.Create); this is an execution-mode error, not treated as a product failure. Full integrated GPU PlayMode rerun is active.
- Candidate 5 builds successfully. Its bomb/mission cards now reserve space for the enlarged touch vital cards. Final browser checks are queued under the machine-wide two-browser limit; other projects currently occupy both slots.
- Safe-area defect found in the WebGL shell: `viewport-fit=cover` combined with a full `100vw × 100vh` canvas ignores display cutouts/home indicators. Root now uses CSS safe-area insets and canvas fills that root. Chrome's installed CDP provides actual safe-area emulation; before/after and pointer-coordinate probe queued. This stylesheet change is newer than candidate 5.
- Immutable baseline first tier: 20 measurements, 19 player finishes, one OneLife DNF, zero simulation time limits. Normal and Hard are still running. `summarize-races.py` reports race outcomes separately from Unity process exit codes.
- A separate frozen-source physical matrix now measures all 20 bodies on e1/e16/e17/e19/e20, solo, normal tier. e1 is one lap; the other families use three laps so Abyss reaches its narrow stage. This is controlled driving coverage, not campaign/economy evidence.

At 21:26 the integrated GPU PlayMode suite passed **48/48**, including the new launch cases and special-track hints. The only runtime-source difference between candidate 5 and the next build is the safe-area stylesheet (verified across the 1169-file manifest), so candidate 6 reuses the successful native checks and rebuilds the WebGL shell without repeating the physical prebuild lap.

Body matrix startup repair: an overly broad rsync exclusion (`Search`) omitted the localization package's Editor/Search source folder. No races ran in those attempts. Stopped only our isolated runner, restored its complete PackageCache, and restarted in `body-matrix-v2`. The reusable matrix runner now stops at its first process failure so infrastructure errors cannot produce a misleading full matrix.

## Checks around 21:45

- Candidate 5 vital HUD: 48/54 language/size/input combinations passed; all six Chinese cases exposed an empty weapon name because its 1.45-em CJK line did not fit the title box. The next candidate reserves complete title/ammo lines. Desktop compact goals now also keep a 12-pixel minimum (before: 7.7–8.8 px).
- Extended lifetime measurement across **all nine languages**, eight rounds of four garage bodies and five generated event families: **2/2 tests passed** (including PlayerExperience). All eight samples: 172 materials, 266 meshes, 12 RT and 925 GameObjects. Native allocated bytes still rose from 389 MB to 416 MB; no claim of constant total memory. The first multilingual attempt raced the asynchronous locale load; the fixture now waits for Loc.Ready.
- Actual browser safe-area emulation passed before/after stylesheet comparison, inset pointer coordinates, portrait pause and both notch orientations.
- Short uninterrupted WebGL harness validated one natural e1 race, exact reward/race counting, garage transitions, resource snapshot and save/reload. A generic console warning reported a lost *probe* context while the game kept rendering; the harness now captures DOM canvas context-loss events and queries the actual game's isContextLost(). The repeat passed, retained the warning and observed no game-canvas loss. GPU was 49°C / 2.0 of 12.3 GB used; no kernel GPU-reset entry.
- Additional arsenal-label and ghost-appearance probes are underway before freezing the longer browser soak candidate.
