#!/bin/bash
set -eu
source "$(dirname "$0")/common.sh"
ensure_sim
python3 "$PROJECT/Tools/qa/freeze-source.py" "$SIM" "$WORK/inputs"
exec "$UNITY" -batchmode -nographics -job-worker-count 2 -projectPath "$SIM" -executeMethod RacingGame.Net.RelayServer.RunFromCommandLine \
  -netLocal -netPort "${CG_ROOM_PORT:-7777}" -netPath /net -netSeconds "${CG_ROOM_SECONDS:-1800}" -quit -logFile "$WORK/relay.log"
