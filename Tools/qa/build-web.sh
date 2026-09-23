#!/bin/bash
# Yandex WebGL build (zip + size report) from a project copy. Output: $WORK/build/{web,CarsGame-web-<ver>.zip}.
source "$(dirname "$0")/common.sh"; SIM=${SIM:-$WORK/sim3}; ensure_sim
rm -rf "$WORK/build"
"$UNITY" -batchmode -nographics -projectPath "$SIM" -executeMethod RacingGame.EditorTools.BuildCommands.BuildYandex -buildOutput "$WORK/build/web" -quit -logFile "$WORK/build.log" >/dev/null 2>&1
echo "build exit=$?"; grep -m3 "error CS\|Build Finished\|WebZip\]" "$WORK/build.log"; ls "$WORK/build"
