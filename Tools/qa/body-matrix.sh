#!/usr/bin/env bash
# Physical coverage of all bodies on the five track families, one isolated Unity copy.
# PROJECT must be a frozen source snapshot when work continues in the main checkout.
set -uo pipefail
source "$(dirname "$0")/common.sh"
mkdir -p "$WORK"
mapfile -t bodies < <(python3 - "$PROJECT/Assets/CarsGame/Core/Economy/CarBodies.cs" <<'PY'
import re,sys
s=open(sys.argv[1]).read()
for body in re.findall(r'new BodyDef\(("[^"]+"|DefaultId)',s):
 print('classic' if body=='DefaultId' else body.strip('"'))
PY
)
[ "${#bodies[@]}" -eq 20 ] || { echo 'Expected the 20 authored car bodies' >&2; exit 2; }
for event in ${CG_BODY_EVENTS:-e1 e16 e17 e19 e20}; do
  laps=3; [ "$event" != e1 ] || laps=1
  for body in "${bodies[@]}"; do
    tag="${event}_${body}"
    started=$(date +%s)
    echo "START $tag $(date -Is)"
    timeout -k 20 900 bash "$(dirname "$0")/race.sh" "$tag" "CG_EVENT=$event" "CG_BODY=$body" "CG_LAPS=$laps" CG_TIER=normal CG_AI=0 CG_SEED=12345 > "$WORK/$tag.run.log" 2>&1
    status=$?
    printf '%s %s %s\n' "$tag" "$status" "$(($(date +%s)-started))" >> "$WORK/status.txt"
    echo "END $tag status=$status $(date -Is)"
    tail -n 4 "$WORK/$tag.run.log"
    [ "$status" -eq 0 ] || { echo 'Runner failed; diagnose this case before continuing the matrix.' >&2; exit "$status"; }
  done
done
