#!/bin/bash
# One autopilot race through Tests/PlayMode/RaceHarness.cs (one 50 Hz tick per frame, faster than real time).
# Usage: Tools/qa/race.sh <tag> [CG_AI=0] [CG_LAPS=2] [CG_EVENT=e1] [CG_TIER=normal] [CG_SEED=12345] ["CG_PARAMS=field=value ..."]
# Parallel runs need separate copies: SIM=/tmp/cars-qa/sim2 Tools/qa/race.sh b ...
source "$(dirname "$0")/common.sh"; ensure_sim
tag=$1; shift
export CG_OUT="$WORK/race_$tag.json"; for kv in "$@"; do export "$kv"; done; rm -f "$CG_OUT"
"$UNITY" -batchmode -nographics -projectPath "$SIM" -runTests -testPlatform PlayMode -testFilter RaceHarness -testResults "$WORK/race_$tag.xml" -logFile "$WORK/race_$tag.log" >/dev/null 2>&1
echo "exit=$? tag=$tag"
[ -f "$CG_OUT" ] || { grep -m10 "error CS\|Exception" "$WORK/race_$tag.log"; exit 1; }
python3 - "$CG_OUT" <<'P'
import json, sys
r = json.load(open(sys.argv[1]))
print(f"evt={r['evt']} tier={r['tier']} params='{r['parameters']}' race={r['raceSeconds']:.1f}s shots={r['shots']} hits={r['hits']}")
for c in r['cars']:
    laps = ' '.join(f"{x:.1f}" for x in c['lapSeconds']); at = ' '.join(str(int(x)) for x in c['impactAt'][:12])
    print(f"  {c['name']:12}{'*' if c['player'] else ' '} laps[{laps}] off={c['offTrackSeconds']:.1f}s walls={c['wallImpacts']} D={c['deaths']} K={c['kills']} autoRes={c['autoResets']} maxLat={c['maxLateral']:.1f} at[{at}]")
P
