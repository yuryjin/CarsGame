# Ghost replay — 2026-10-25

Implemented and verified in native Unity and a WebGL browser through the local mock SDK. Real platform sessions are not verified.

The old recorder discarded pitch/roll and treated fixed-tick samples as evenly spaced race time. The regression reproduced a 155.279° pose error on an inverted car. It also replayed every body as the stock sports car. Version 2 retains the full body pose, race-clock time, recovery cuts and selected body ID.

## Format

All numbers are little-endian. The raw payload is gzip-compressed, then base64-encoded for storage/session transport.

| Section | Layout |
| --- | --- |
| Header | version byte; track hash int32; tick rate int16; sample step byte; UTF-8 name byte length + up to255bytes; body ID byte length + up to32ASCIIbytes; sample count int32; duration milliseconds int32 |
| Each sample | milliseconds int32; x/y/z int32 centimetres; normalized quaternion x/y/z/w int16 components divided by32767; steer int8 divided by127; flags byte |
| Flags | bit0 boost; bit1 recovery/teleport cut |
| Trailer | FNV-1a uint32 of the entire preceding raw header and samples |

A sample occupies26bytes. The decoder bounds compressed data to96KiB and raw expansion to20,000samples plus bounded metadata, rejects invalid headers/poses/timelines/checksums, and returns null for unsupported/corrupt data. Body IDs are canonicalised against the car catalogue before constructing visuals; unknown IDs use Classic.

Version1 remains readable, including its original count-based duration and checksum rules. It has yaw-only poses and no body ID, so its appearance falls back to Classic. A fixed golden v1 fixture is included in EditMode tests.

## Runtime behaviour

- Capture GO and the final partial sampling interval. Timestamps follow RaceClock, so dropped fixed steps and pauses do not silently change replay duration.
- Car.Teleported captures a cut. Playback holds the preceding pose until the cut time, then snaps to the recovered position.
- Interpolate full rotations with quaternions. Late-loaded opponents align to the current race elapsed time.
- A sample-limit overrun discards the incomplete recording. An invalid/oversized payload cannot replace a valid best or be submitted as a complete run.
- Encode once on finish. Compare bests and send session metadata with integer milliseconds.
- Build each selected body's visuals on an inactive template. Ghosts contain only a transform, renderer, mesh filter and playback component; no physics or race participant.
- Cache one merged visual mesh per prefab and catalogue body. This preserves every visible triangle while replacing9–36same-material submeshes with one. Unknown remote IDs reuse the Classic cache entry.

This is a visual trajectory replay; it does not reproduce combat, animate individual wheel/turret motion, or change authoritative race physics/results.

## Evidence so far

Artifacts are under `/tmp/cars-improve-20261025` with source manifests:

- `ghost-pose-before`: inverted-pose regression failed by155.279°.
- `ghost-pose-after3`:4/4, including pause/clock/cuts, recording limit and camera recovery.
- `ghost-body-before`: tank-silhouette regression failed; `ghost-body-after`:8/8 with garage/camera coverage.
- `ghost-edit4`:390/390, including v1 compatibility, Unicode boundaries, irregular times, corrupt data, bounded expansion and oversized encoding.
- `full-play10`:85/85 before the merged-mesh optimisation. Natural finish verifies saved replay duration against the actual finish clock.
- `ghost-geometry-before`:all20bodies match the real car geometry; measured9–36submeshes per ghost.
- `ghost-geometry-after2`:5/5. All20bodies have one submesh and preserve all triangle vertices; maximum error below0.0001m. Native before/after contact sheets differ in only9of3,686,400channel values.
- `ghost-lifetime12`:9/9. Includes100additional cached spawns and5unknown-ID fallbacks without mesh-count growth, plus finish/playback/camera integration.
- `edit12`:390/390. WebGL candidates12–14 built successfully.
- `ghost-browser11`: passed v1/v2 loading, selected body/full rotation, pause, late loading, recovery cut, natural-finish recording/metadata and rejection of a stale response after leaving. No captured browser/game exceptions. Candidate11 predates mesh merging; merged geometry is verified by the native comparisons above.

The temporary `GetInstanceID` compilation error in the first merge attempt was fixed by using the prefab reference in the cache key. Those failed logs are retained. No publication or real SDK session write was performed.
