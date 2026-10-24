# Bounded AI recovery investigation — 2026-10-23

Observed: e10 reverse/night browser run stalled near distance 137 m for roughly 80 seconds with throttle applied and no AI recovery, then recovered and finished fourth. Expected: a genuinely blocked driver attempts reverse and recovers after the existing bounded deadline. This also affects opponents; it is not a difficulty-tuning request.

Baseline: Unity 6000.5.9f1, URP, SampleScene, WebGL NVIDIA Vulkan through webtest. Current base commit e424ab8 plus preserved working changes; last verified build `Builds/FinishConsistency-2026-10-23/`. AiDriver has no existing worktree diff. The first e10 run stalled; a second random-seed run did not show the same long stall. Exact original seed is unavailable. Evidence: `/tmp/cars-consistency/campaign-before/`, particularly e10 samples and driving screenshot. Latest saved mock profile: 2,406 coins / 15 races / e11 unlocked.

The previous goal turn was verified progress: finish accounting/time fixes, failing-to-passing regressions, builds, natural races and profile reloads.

| Hypothesis | Supporting evidence | Contradiction / limit | Prediction and experiment | Status |
|---|---|---|---|---|
| Brief projected forward-speed spikes repeatedly clear the stall timer | AiDriver clears stuckTime on any along speed >2 m/s; observed position stayed within a few metres | Per-tick projected speed was not recorded in the original browser run | Actual driver sampling over bounded centimeter motion fails to reset; stationary and sustained-travel controls distinguish the cause | Controlled reproduction confirms the mechanism; original collision is not a seeded replay |
| Frozen status prevents recovery | The driver intentionally excludes frozen time | No freeze badge in the captured stalled frame; an 80-second continuous freeze is not established | Explicit frozen control must suppress recovery, but unfrozen jitter should not | Testing |
| Recovery commands are lost after a race transition | Stall happened after e9→e10 | AI ResetCount itself stayed zero, so no reset had been requested; later recovery worked | Inspect generated ResetEdge in the real driver, before vehicle command application | Weakened |

A controlled PlayMode fixture moves the rigidbody by 8 cm once per second, projects it through actual RaceProgress/LapCounter, and calls actual AiDriver.Sample. It does not claim to replay the original barrier collision. It compares stopped, sustained travel, frozen and bounded-jitter cases without changing production code.

## Controlled reproduction and correction

The first fixture failed its moving control because changing only Rigidbody.position did not update the Transform used by projection in the same frame. The fixture now writes the transform and calls Physics.SyncTransforms. No production recovery edit was made until all controls were valid.

Valid before-fix evidence (`/tmp/cars-ai-recovery/before.xml`): stopped requested reset on tick 401; moving travelled 79.920 m and never reset; frozen never reset; jitter remained in a 0.080 m span with 4.000 m/s forward spikes and **never reset in 1,000 samples**. The final assertion failed as intended. This proves that bounded jitter can defeat the old recovery deadline, while not proving every detail of the original random-seed barrier collision.

The correction advances a recovery checkpoint only after two net forward metres, using signed spline distance across the seam. Reverse motion cannot lower the checkpoint. Initial grace, reverse attempt and six-second deadline remain. Freeze suspends the stall timer/reverse manoeuvre; the separate overturned/fallen rule is unchanged; teleport clears the previous deadline and buffered commands. Slow sustained travel at 1 m/s, seam crossing, freeze/thaw and teleport are added regression controls.

Debug snapshots now include the selected race seed, AI stuck time/reversing state and freeze state. These are on-demand diagnostics, without per-frame logging. The expanded five-scenario PlayMode suite passed, including the final slow-travel/no-reverse assertions. Subsequent physical races, EditMode and WebGL validation are recorded below. A session seed aids diagnosis but does not alone reproduce all prior RNG consumption across multiple races.

After-fix controls: stopped and 8 cm jitter reset at tick 401; moving at 4 m/s, moving at 1 m/s and seam crossing never reset or reversed; frozen never reset; freezing an existing recovery for 400 samples delayed the reset to tick 801; teleport on tick 250 cleared reversing immediately and reset on tick 550 after a fresh six-second deadline.


## Physical-harness setup issue

The first e10 measuring run never spawned cars: the isolated QA save had not unlocked e10 and GameFlow correctly rejected it (zero ticks, empty field). This is not a driving failure. Its failed XML/JSON are retained as `harness-locked-before.*`.

RaceHarness now clears the prerequisite only on its cloned measuring event, asserts that Countdown actually starts, and restores the saved profile and destroys the clone in teardown. It does not edit event assets or unlock the browser journey. The repeat uses e10 Easy, seed 12345, one lap, and logs the 110–160 m zone where the original browser stall appeared.


## Fixed-seed physical comparison

Both runs use the corrected measuring harness and e10 Easy / seed 12345 / one lap. The comparison run changes only AiDriver to its pre-iteration source in the isolated copy; the built WebGL artifact remains corrected. The copy is restored afterward.

| Driver | Old AI finish (s) / deaths / AI resets | New AI finish (s) / deaths / AI resets |
|---|---|---|
| AI 1 | 94.82 / 2 / 0 | 74.04 / 0 / 0 |
| AI 2 | 83.46 / 0 / 1 | 69.08 / 0 / 1 |
| AI 3 | 68.68 / 0 / 1 | 52.72 / 0 / 0 |
| Player under driving-only AI | 59.64 / 0 / 0 | 81.68 / 1 / 2 |

All four finished in both runs. Outcomes are mixed for individual cars: the field completed earlier and suffered fewer total deaths with the correction, while the player seat was slower and died once. This combat sample is not a general lap-time or balance improvement claim. The targeted guarantee is bounded recovery under jitter. The standard solo e1 Normal / seed 12345 remains 49.2 s with no death or automatic recovery.


The separate manual-input regression also passed: deliberate braking and idle input do not trigger recovery; blocked acceleration for a newcomer recovers once and remains latched. This run restored the final source to the isolated copy and verified it against the workspace. The six PlayMode scenarios and all 369 EditMode tests are now passing.


## Final browser validation and archive

`Builds/AiRecovery-2026-10-23/` contains the final ZIP (SHA256 `6e4a03d2fa35b44656dd539404ef27d93813c47ba2fdd98318047cf7c8ff9737`) and all evidence. Full ordinary-progression browser runs used driving-only autopilot through webtest/NVIDIA Vulkan, without finish or unlock cheats:

- e10: first, 3:24.558, zero deaths, one non-AI reset, 240 coins. AI reset count zero; maximum sampled stuck time 1.28 s.
- e11: third, 3:13.149, two deaths, five total resets including two AI resets, 128 coins. Telemetry at race times 108–112 s and 136–141 s shows reverse attempts, AI reset increments, deadline cleared and subsequent travel. Maximum sampled stuck time 5.58 s; the browser samples about once per second, not every tick.
- Fresh-context reloads verified 2,406 coins / 15 races before and 2,774 / 17 afterward, with upgrades/loadout retained and e12 unlocked. No captured exception, missing-localization or missing-glyph errors.

The browser recoveries support the real-simulation integration; the original first-e10 random collision remains an unrecorded exact trajectory. No difficulty or payout-rate change is inferred from these bot placements. Next work can continue campaign coverage and direct combat input; no blocking condition remains.
