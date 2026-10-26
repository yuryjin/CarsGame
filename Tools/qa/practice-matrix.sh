#!/usr/bin/env bash
# Use PROJECT pointing to a frozen Assets/Packages/ProjectSettings snapshot.
set -uo pipefail
source "$(dirname "$0")/common.sh"
matrix_work="$WORK"
filter='RealDriftCompletesTheExerciseAndRetryRequiresANewDrift;AimExerciseRequiresARealProjectileHitAndNeverAwardsCampaignProgress;CeilingExerciseCanBeReachedByDrivingFromTheFloor;ShortcutLessonCompletesThroughARealBridgePassage'
mapfile -t bodies < <(python3 - "$PROJECT/Assets/CarsGame/Core/Economy/CarBodies.cs" <<'PY'
import os,re,sys
selected=os.environ.get('CG_PRACTICE_BODIES','').split()
if selected:
 print('\n'.join(selected))
else:
 for body in re.findall(r'new BodyDef\(("[^"]+"|DefaultId)',open(sys.argv[1]).read()):
  print('classic' if body=='DefaultId' else body.strip('"'))
PY
)
for body in "${bodies[@]}"; do
  started=$(date +%s)
  echo "START practice $body $(date -Is)"
  CG_WORK="$matrix_work/$body" CG_PRACTICE_BODY="$body" bash "$(dirname "$0")/play-tests.sh" "$filter" > "$matrix_work/$body.run.log" 2>&1
  status=$?
  printf '%s %s %s\n' "$body" "$status" "$(($(date +%s)-started))" >> "$matrix_work/status.txt"
  tail -n 10 "$matrix_work/$body.run.log"
  if [ "$status" -ne 0 ]; then
    echo 'A prepared driving route failed; inspect evidence before interpreting it as a handling defect.'
    exit "$status"
  fi
done
