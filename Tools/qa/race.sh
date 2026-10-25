#!/bin/bash
# One autopilot race through Tests/PlayMode/RaceHarness.cs (one 50 Hz tick per frame, faster than real time).
# Usage: Tools/qa/race.sh <tag> [CG_AI=0] [CG_LAPS=2] [CG_EVENT=e1] [CG_TIER=normal] [CG_SEED=12345] ["CG_PARAMS=field=value ..."] [CG_MAX_LAP_SECONDS=70]
# Parallel runs need separate copies: SIM=/tmp/cars-qa/sim2 Tools/qa/race.sh b ...
source "$(dirname "$0")/common.sh"; ensure_sim
tag=$1; shift
export CG_OUT="$WORK/race_$tag.json"; for kv in "$@"; do export "$kv"; done; rm -f "$CG_OUT"
"$UNITY" -batchmode -nographics -projectPath "$SIM" -runTests -testPlatform PlayMode -testFilter RaceHarness -testResults "$WORK/race_$tag.xml" -logFile "$WORK/race_$tag.log" >/dev/null 2>&1
unity_exit=$?; echo "exit=$unity_exit tag=$tag"
[ "$unity_exit" -eq 0 ] || { grep -m10 "error CS\|Exception" "$WORK/race_$tag.log" || true; exit "$unity_exit"; }
[ -f "$CG_OUT" ] || { grep -m10 "error CS\|Exception" "$WORK/race_$tag.log"; exit 1; }
python3 - "$CG_OUT" <<'P'
import json, os, sys
r = json.load(open(sys.argv[1]))
print(f"evt={r['evt']} tier={r['tier']} params='{r['parameters']}' race={r['raceSeconds']:.1f}s shots={r['shots']} hits={r['hits']}")
for c in r['cars']:
    laps = ' '.join(f"{x:.1f}" for x in c['lapSeconds']); at = ' '.join(str(int(x)) for x in c['impactAt'][:12])
    print(f"  {c['name']:12}{'*' if c['player'] else ' '} laps[{laps}] off={c['offTrackSeconds']:.1f}s walls={c['wallImpacts']} D={c['deaths']} K={c['kills']} autoRes={c['autoResets']} totalRes={c.get('resets', 'unmeasured')} maxLat={c['maxLateral']:.1f} at[{at}]")
if 'CG_MAX_LAP_SECONDS' in os.environ:
    limit = float(os.environ['CG_MAX_LAP_SECONDS'])
    players = [c for c in r['cars'] if c['player']]
    if len(r['cars']) != 1 or len(players) != 1 or not players[0]['finished'] or len(players[0]['lapSeconds']) != 1 or players[0]['lapSeconds'][0] > limit or players[0]['deaths'] != 0:
        sys.exit(f"race gate failed: expected one finished solo lap <= {limit:g}s and 0 deaths")
    print(f"race gate passed: solo lap <= {limit:g}s and 0 deaths")
P
