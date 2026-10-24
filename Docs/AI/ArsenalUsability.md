# Arsenal usability — 2026-10-23

Second improvement pass following `PlayerExperience.md`.

## Implemented

- The arsenal has five filters: all, ranged, close combat, control, and traps. Categories derive from the actual attack profile; a status projectile is control, while a status mine remains a trap. Looking and filtering never buys equipment.
- Cards distinguish spread, volleys, rapid fire, heavy shots, homing, ricochets and pulling attacks. They show base damage per projectile and pellet count, shot interval, magazine/reload or heat-only operation, direct-attack range or trap radius, and status duration where applicable. These are base values, not a promise of damage after upgrades, distance, shields or missed pellets.
- The selected loadout slot is shown above the list. Back returns to the garage. Existing machine-gun ownership and compact/full arsenal rules still apply.
- The HUD keeps the active weapon's name visible above ammo/reload/heat. Heat-only weapon information is refreshed on subsequent races and language changes; secondary/status caches reset when binding a new car.
- The result screen gives the cup reward a separate, taller multiline area.
- Rejected race starts validate readiness and unlocking before changing the active event, difficulty or test-drive snapshot. A locked-event request cannot silently replace the current race's rules or consume the garage return state.
- Seventeen localization entries were added in all nine existing languages. No new packages, scenes, prefab wiring or save migration.

## Validation

- EditMode: **357/357 passed**, including twelve new weapon-guide cases.
- PlayMode: **4/4 passed**: all-body garage previews, save-safe test-drive exit/finish and timeout, plus the expanded player-experience scenario. The latter covers filters, the back route, weapon naming, repeated heat-only loadouts, normal-race rejection of a locked event and preservation of the test-drive return state after a rejected event.
- Localization: **416 keys × 9 languages**, 674 characters in the generated fallback atlas, zero missing glyphs.
- Physical race gate: one solo e1 lap in 48.4 seconds, zero deaths (limit: 70 seconds).
- WebGL build succeeded: 18.3 MB uncompressed.
- Browser: **passed**, twelve recorded states/screenshots at 1280×720 and 812×375 on NVIDIA RTX 4070 Ti (ANGLE/Vulkan) through `webtest`. Actual clicks/keys validate filters, loadout persistence after reload, compact/full mode, Q/1/2 switching, permanent weapon naming, locked-event rejection, goals, results, mode-specific touch buttons and pause help. Zero captured JavaScript/C# exceptions, missing keys or missing glyphs.
- Inspected arsenal/results/garage/race/help screenshots. Cup rewards fit their enlarged area; mobile text remains too small in several existing surfaces, recorded for the next iteration.
- Build, size report and Unity results: `Builds/ArsenalUsability-2026-10-23/`.

Gameplay balance is unchanged by this pass. Tests use an isolated project copy; graphics run through Vulkan on the RTX 4070 Ti, with audio kept on `AgentSink`. Browser tests use `webtest` and isolated storage.

## Next observed issue

At 812×375, the existing 1920×1080 canvas reference scales some body copy below a comfortable reading size. The next iteration should improve small-screen menus/help and validate actual text bounds and touch targets, rather than only shrinking a desktop layout. The current twelve screenshots in `Builds/ArsenalUsability-2026-10-23/browser/` provide the baseline. Physical phone handling and the production platform SDK have not been tested. Keyboard Escape from the arsenal also still returns to the main menu instead of following its Back button; align that route in the next iteration.
