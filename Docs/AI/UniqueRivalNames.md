# Distinct rival names — 2026-10-23

The preceding BombHud iteration was verified progress: its final native/browser checks, full e18 run and reload passed, and its executable/evidence were archived. A separate concrete issue remained: two rivals called Bolt in the same grid and standings.

## Evidence and correction

Difficulty_easy references the Rookie/Bolt profile at array positions 0 and 3. RaceSpawner previously appended a number based on complete traversals of the profile array, so both positions received Bolt and position 4 received Bolt 2. Repeated fallback aiNames were not numbered at all.

A new native regression failed before the change for the actual five-rival Easy grid, its retry, repeated fallback names and differently cased names that localize identically. It separately confirmed the intended driver profile sequence. The previous executable's mobile browser probe also failed with 28 repeated observations of duplicate visible names at 1280×720 and 640×360.

The spawner now checks names already assigned to rivals, ignoring case, and adds the first available numerical suffix on collision. It retains the configured base name and starts fresh with each field. It does not change profile ordering/frequency, driver behavior, paint, weapons, player name, rewards or save data. Existing localized-name rendering carries the suffix into nameplates, bomb-owner text, combat messages and results.

## Validation status

Before-fix native XML and browser evidence are retained in `/tmp/cars-unique-names/`. The final five PlayMode scenarios passed. The actual Easy grid is now Bolt, Vega, Rex, Bolt 2, Bolt 3 and retry produces the identical sequence. Repeated fallback/case variants remain distinct after localization. All 369 EditMode tests and the 49.2-second physical gate also passed. WebGL build, final browser checks and full race/reload are pending. Tests use isolated Unity project/preferences and the separate QA browser profile. Browsers run through webtest/NVIDIA Vulkan; audio remains on AgentSink.
