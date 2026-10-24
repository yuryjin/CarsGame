# Contextual driving help — 2026-10-23

The previous goal turn was verified progress: bounded AI recovery, before/after controls, full e10/e11 browser races, a saved build and reload checks. This iteration continues campaign coverage and actual combat inputs.

## Observed problem

On the verified AiRecovery WebGL build, e12's pause help only described mine tactics, drifting and star goals. It omitted the mine key and basic driving controls. e11's special-event help omitted loadout switching. Full-arsenal mode also had no wheel/page explanation. Actual E, compact Q, and full-arsenal Q/Tab/0 inputs worked; the missing information was in the UI.

`/tmp/cars-combat-help/before-valid/` captures all four failing help assertions after the actual input checks passed. An earlier probe incorrectly assumed the Arsenal event automatically selected full-arsenal loadout: it only increases pickups. The corrected probe uses the existing garage toggle, in a separate browser context that never writes the campaign's saved storage file.

## Changes

The pause help always includes driving/braking/recovery controls. It adds event advice, avoids a duplicated drift lesson, describes the actual compact/full/touch weapon controls, and includes secondary controls only when equipped. Mines-only and hot-bomb events no longer lose basic driving help or advertise an unavailable primary. Charges explain pressing again to detonate. Existing gamepad mappings remain documented. The in-race help no longer reuses garage copy saying equipment is “below”.

Mines-only start hints identify the actual keyboard action or touch button. Twelve new entries cover all nine languages. The Chinese subset/atlas is rebuilt, and Russian/Ukrainian kill-goal phrasing now works for a target of one without a plural mismatch.

## Validation

- Baseline full browser campaign, driving-only autopilot: e12 second in 2:40.199, no deaths, 176 coins; e13 second in 1:49.696, no deaths, 176 coins. One reset in each; one AI reset in e12.
- Fresh-context reload: 3,126 coins / 19 races, loadout/upgrades retained, e14 unlocked. No captured exception/localization/glyph errors.
- Localization import: 428 keys × 9 languages. Static atlas: 690 characters, 2048×1024, no missing glyphs.
- Unity regressions: 369 EditMode tests and three PlayMode scenarios passed. The standard solo lap remains 49.2 s with no deaths/automatic recovery.
- Desktop browser probe passed actual E mine placement, Q compact swapping, Q/Tab/0 full-arsenal selection, and every previously missing help assertion.
- Real touch input placed a mine. Help at 640×360 passed readable-font, overflow scrolling by finger, fixed Back, preserved pause and reopen-at-top checks for every language. The selector has ten choices: platform default plus nine explicit locales; the first run covered default plus eight locales, and a separate Ukrainian run completed coverage. Captured exception/localization/glyph errors were empty.

Campaign evidence lives in `/tmp/cars-campaign-next/`; this is the preceding verified build, not a natural-race claim for the new UI build. Browser tests use webtest/NVIDIA Vulkan, close their browsers and keep audio on AgentSink. Unity work remains in an isolated copy with isolated preferences.


The campaign probe now supports explicit DNF recording and, only when opted in on localhost with `sdk=mock`, clicking the actual one-life Watch button once. Default probes still require a completed distance. Reload checks no longer infer a next-event unlock from a DNF placing. These additions prepare e14 coverage; they are not yet evidence that its continue flow has been exercised.


## Final build and continuation run

`Builds/DrivingHelp-2026-10-23/` is the verified archive; ZIP SHA256 `f6466a4f00704eff7cf3386399bcab0ddf61100ca0c28491f8f4028327155516`. The exact build-source manifest contains 1,354 files and C# matched the workspace.

On this new build, e14 finished first in 3:58.123 with one death and 240 coins. The actual continue offer appeared after the death; the probe clicked Watch once using the localhost mock SDK and the car resumed driving. No second continue was requested. This is evidence of successful one-time continuation, not a new test of rejecting a second offer (the existing OneLife PlayMode scenario covers that rule). Fresh-context reload confirmed 3,366 coins / 20 races, unchanged upgrades/loadout and e15 unlocked. The DNF-recording branch was not exercised because the car finished.

The full race used driving-only autopilot; separate desktop and mobile probes used real keyboard/touch input. All browsers ran through webtest on NVIDIA Vulkan, with audio on AgentSink. No live deployment or user-profile edit occurred.

## Next observed issue

The e14 continue screenshot shows a Russian kill feed naming “Вега” while the large death toast says “Вас разбил Vega”. `GameFlow.OnWreck` formats the raw participant displayName; other HUD surfaces use `HudScreen.GetLocalizedRacerName`. Investigate and correct this inconsistency in the next iteration, using the captured screenshot rather than inferring a new gameplay defect.
