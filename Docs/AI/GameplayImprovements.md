# Gameplay improvements — 2026-09-24

Implemented in `Assets/CarsGame` using the existing vehicle, combat, save and UI systems; no new dependencies.

- **Driving:** service braking preserves tyre grip, steering is immediate while tactical commands retain reaction delay, traffic speed follows the car ahead, and stuck recovery ignores frozen cars. Low-speed steering assistance is reflected in the AI's curvature/speed model; the existing track's tight final hairpin is now reachable. `CarController.lowSpeedSteeringBoost` remains tunable (default 3).
- **Pickups:** deterministic sectors place health/ammo outside bends, nitro before bends, and items in the opposing lane. Narrow box triggers require an intentional lane choice.
- **Combat feedback:** resolved-hit events distinguish shields, damage and destruction. The HUD shows an incoming direction/source and an outgoing confirmation; shield hits have a separate sound.
- **Rivals:** named, coloured profiles equip racer, shooter and defender loadouts. Names appear above nearby cars. Machine-gun bursts respect the weapon's fire interval.
- **Goals:** events share descriptions between selection, live HUD and results. Third-star variants include no deaths, winning without repairs, and a time limit. Stats are captured at the finish line so later damage cannot remove earned stars. Existing earned stars remain valid.
- **Builds:** free balanced/speed/survival/firepower presets multiply purchased upgrades and equip only owned weapons. Purchases are retained; save migration and cloud merge preserve the selected build. Weapon setup occurs before damage/fire-rate upgrades.
- **Ghost fix found during validation:** copy baked meshes without instantiating a live car, avoiding race registration and RequireComponent removal errors.

## Checks

`GameplayImprovementsTests`, `AiRecoveryTests` and `GhostSpawnTests` cover tuning, ownership, saves, goals, hit outcomes, pickup placement, rival loadouts, recovery and ghost isolation.

Batch Unity runs on `/tmp/cars-qa/sim`, separate from the open editor. Artifacts use `/tmp/cars-qa/improvements`.

Initial two-lap solo baseline: 336.5 simulated seconds, 91 wall impacts, 9 automatic resets. After driving changes, the six-car two-lap seed-12345 run completed in 122.8 simulated seconds; the player finished in 99.3 seconds. All six cars finished, with one AI recovery and one automatic reset across the field. These are deterministic harness measurements, not a promise of identical human race times or zero combat collisions.

Full EditMode suite: **215 passed, 0 failed** (`edit.xml`), including the recovery, ghost-isolation and HUD event-path play-mode transitions. New localization rows are complete in all nine languages, with no duplicate keys.

Reverse track `e2`, seed 67890, two laps and five opponents: **6/6 finished** in 121.8 simulated seconds. Three AI recovery resets and one automatic reset across the field; no car remained stuck.

Prebuild solo gate: **49.3 seconds**, zero wall impacts, deaths or resets (limit 70 seconds). Final localization import: 217 keys × nine languages; directional triangle included in the static atlas.

WebGL build succeeded: `Builds/GameplayImprovements/CarsGame-web-0.2.0.zip` (about 15 MiB). Browser checks passed at 1280×720 and 812×375 with touch controls explicitly enabled. Inspected garage, live goals, combat source feedback and results. Real clicks selected speed/survival/firepower; speed remained selected after page reload. Both race flows reached results and balanced gameplay start/stop calls.

Final browser logs contain no C# exceptions, missing localization keys or missing glyphs. Remaining local warnings: unavailable host SDK (mock is intentional), favicon/resource 404s, and SwiftShader/URP fallback warnings. A physical phone and production host SDK were not tested.

Persistent evidence: `Builds/GameplayImprovements/qa/` (test XML, race JSON, browser logs, screenshots and size report). Earlier temporary baseline files were removed when the environment restarted; their measurements above were recorded before that restart.

The shared working tree is being edited by other sessions. The archived build is the tested snapshot; a concurrent mines-only targeting-range change in `AiGunner.cs` / `EventRules.cs` arrived afterward and is not included in that archive.
