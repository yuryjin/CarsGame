# Garage showcase and test drive

The garage keeps a car showcase beside its scrolling catalog. On narrow layouts the showcase sits above the catalog. Arrow buttons browse all twenty bodies without buying or equipping them; the body icons and class tabs also select the preview. The vehicle rotates automatically, supports dragging with a mouse or touch, and has rotation buttons for keyboard/gamepad navigation.

The preview uses the existing body and primary-weapon builders, selected paint and livery. A disabled template produces a mesh-only display car, so previewing never registers a vehicle with the race or physics. One 768×512 render target and one camera exist while the garage is open and are released when it closes. The comparison shows the candidate's body ratings; the white marks indicate the equipped body. Purchased upgrades and the build preset are shared when trying either body on the track.

The test-drive button works for owned and locked bodies. It starts a one-lap practice session on the main circuit with a moving opponent that does not fire, using the player's purchased upgrades and weapons. The session returns to the garage after 45 seconds of race time, at the end of the session, or through its return button. Pause stops the clock; retry starts a fresh test drive of the same body.

Test drives do not consume rewarded upgrade trials, grant rewards, record race statistics/stars/bests/ghosts, unlock content, upload sessions, or invoke the post-race advertising checkpoint. The preceding event, difficulty and spawner body override are restored when leaving or disabling the flow. Test drives do not change the owned or equipped body.

No Inspector, scene, prefab or package setup is needed. New UI strings are included in all nine supported languages and imported into the existing localization tables.

## Validation

`GaragePlayTests` covers all bodies, visible roof-mounted weapons, rotation, lack of physics/race registration, resource cleanup, locked-body test drives, pause, retry, manual return, session completion, timeout, unchanged saves and preservation of an active rewarded trial.

Validation completed on the isolated project copy `/tmp/cars-qa/garage/sim`:

- Full EditMode suite: 345 passed, 0 failed.
- Final `GaragePlayTests`: 3 passed, 0 failed, including a completed test drive through `QaFinishNow` and preservation of an active rewarded trial.
- WebGL build succeeded. Archive: `Builds/Garage/CarsGame-web-0.2.0.zip` (18,906,386 bytes).
- Browser check ran through `webtest` on the NVIDIA RTX 4070 Ti via ANGLE/Vulkan. Real clicks opened the garage, inspected a locked body, started practice, returned, then completed another practice session. Coins, race count and prior event remained unchanged. Desktop 1280×720 and mobile-sized 812×375 screenshots were inspected. No JavaScript/C# exceptions, missing localization keys or missing glyphs were observed by the browser probe.

Artifacts: `Builds/Garage/qa/` contains test XML, Unity/browser screenshots, browser state snapshots, the browser probe and source checksums. Physical phone input and the production host SDK remain untested.
