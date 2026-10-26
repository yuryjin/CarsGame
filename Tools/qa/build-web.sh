#!/bin/bash
set -e
# Yandex WebGL build (zip + size report) from a project copy. Output: $WORK/build/{web,CarsGame-web-<ver>.zip}.
SIM=${SIM:-${CG_WORK:-/tmp/cars-qa}/sim3}
source "$(dirname "$0")/common.sh"
if [ -e "$WORK/build" ]; then
  echo "Build output already exists: $WORK/build. Choose a new CG_WORK directory to preserve previous artifacts." >&2
  exit 2
fi
SIM="$SIM" "$(dirname "$0")/race.sh" prebuild CG_AI=0 CG_LAPS=1 CG_EVENT=e1 CG_TIER=normal CG_SEED=12345 CG_PARAMS= CG_MAX_LAP_SECONDS=70
"$UNITY" -batchmode -nographics -projectPath "$SIM" -executeMethod RacingGame.EditorTools.BuildCommands.BuildYandex -buildOutput "$WORK/build/web" -quit -logFile "$WORK/build.log" >/dev/null 2>&1
echo "build exit=$?"; grep -m3 "error CS\|Build Finished\|WebZip\]" "$WORK/build.log" || true; ls "$WORK/build"
