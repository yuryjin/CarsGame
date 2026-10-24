# Racer name consistency — 2026-10-23

The previous iteration was verified progress: contextual driving help, actual keyboard/touch input, nine-language help and an e14 completion with the local mock continue offer. Its screenshot `DrivingHelp-2026-10-23/campaign/e14-continue.png` exposed a concrete inconsistency: the Russian feed named «Вега», but the death toast named «Vega».

## Reproduction and change

`RacerNamesPlayTests` reproduced the death toast, kill toast and bomb-owner mismatch through the actual gameplay notification path. It also found that literal names (`Guest 42`) and already translated mission-unit names (`Броневик`) became an ellipsis. The HUD assumed absent localization keys returned bracketed text, while `Loc.Get` actually returns `…` and logs a warning.

`Loc.TryGet` now supplies an optional lookup without a placeholder or warning. Required `Loc.Get` calls retain their existing fallback. The common racer-name formatter translates known names and preserves other names, including numbered suffixes. Death, kill and bomb notifications use that formatter. Names inserted into rich-text surfaces are escaped, including kill-feed cache entries.

The controlled before test failed on all six observed behaviors and recorded three missing-name warnings. The same test passed after the fix. Fatal damage and bomb transfer in this Unity regression are controlled stimuli, not claims of keyboard combat kills. No serialized definition, weapon balance or save schema changed.

## Campaign baseline

On the preceding DrivingHelp build, a full e15 driving-only autopilot race finished second in 5:00.930, with two deaths, four resets and 176 coins. Fresh-browser reload retained 3,542 coins / 21 races, upgrades/loadout and the e16 unlock. Exception, localization and glyph error captures were empty. This is the baseline profile for further runs, not evidence for the new executable.

## Validation status

The isolated regression passes. All 369 EditMode tests and six PlayMode scenarios passed, including natural bot bomb passing/completion, contact/explosion/respawn/retry, finish transitions and contextual controls. The physical build gate passed at 49.2 seconds with zero deaths or automatic recovery, and the WebGL build succeeded. The exact source manifest has 1,356 files; all 283 C# files match the workspace. The preceding-build browser probe reproduced «Бомба у Bolt 2» while the world label said «Болт 2»; its first attempt incorrectly selected the tutorial sentence and was corrected to select the timed carrier banner. The same ordinary bomb-play browser probe passes on the new build: «Бомба у Болт 2: 16 с», with no truncation or captured exception/localization/glyph errors. The full e16 run on this executable finished second in 3:22.025, with two deaths, five total resets (one AI recovery), and 176 coins. All three laps completed. Fresh-context reload retained 3,718 coins / 22 races, loadout/upgrades and e17 unlocked. No exception/localization/glyph errors were captured. This run uses driving-only autopilot, not manual combat, and is not a fixed-seed before/after handling comparison. All browser runs use webtest/NVIDIA Vulkan and AgentSink. Unity uses an isolated project and preference directory. No deployment or user-profile replacement is involved.


## Verified archive and next coverage

`Builds/RacerNames-2026-10-23/` contains the ZIP, source hashes, failing and passing Unity regression, broader test results, before/after browser evidence and campaign/profile checks. ZIP SHA256: `cae077e9857ff174b1cc8d929eb863db04832782e1dda38227ba9ef1d68279d5`. This iteration is verified progress; no active blocking condition exists.

Next useful play coverage is e17 on the preserved profile and the mission route. The close-pack bomb screenshots also show overlapping distant nameplates; that is a readability investigation candidate, not a proven combat or physics defect. Preserve the verified build while evaluating any further change.
