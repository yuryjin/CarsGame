#!/usr/bin/env bash
# The full PlayMode suite renders garage previews; run it with a real GPU.
# Optional filter: Tools/qa/play-tests.sh 'CameraFeedbackPlayTests;GaragePlayTests'
# CG_NOGRAPHICS=1 is only for filters that never create RenderTextures.
set -euo pipefail
source "$(dirname "$0")/common.sh"
ensure_sim
tag=${CG_TEST_TAG:-play}
[[ "$tag" =~ ^[a-zA-Z0-9_-]+$ ]] || { echo 'Invalid CG_TEST_TAG' >&2; exit 2; }
result="$WORK/$tag.xml"
log="$WORK/$tag.log"
rm -f "$result"
args=(-batchmode -projectPath "$SIM" -runTests -testPlatform PlayMode -testResults "$result" -logFile "$log")
if [ -n "${1:-}" ]; then args+=(-testFilter "$1"); fi
if [ "${CG_NOGRAPHICS:-0}" = 1 ]; then
  runner=("$UNITY" -nographics)
else
  runner=(xvfb-run -a -s '-screen 0 1280x720x24' "$UNITY" -force-vulkan)
fi
status=0
taskset -c "${CG_CPU_SET:-0-3}" timeout "${CG_TEST_TIMEOUT:-1800}" "${runner[@]}" "${args[@]}" > /dev/null 2>&1 || status=$?
sync_meta
echo "Unity exit=$status; log=$log; results=$result"
if [ -f "$result" ]; then
  python3 - "$result" <<'PY'
import sys
import xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
print('total/passed/failed', root.get('total'), root.get('passed'), root.get('failed'))
for case in root.iter('test-case'):
    if case.get('result') == 'Failed':
        print('FAIL', case.get('fullname'), '\n', case.findtext('failure/message', '')[:800])
if int(root.get('failed', '0')):
    sys.exit(1)
PY
else
  rg -m 8 'error CS|Exception|Licensing initialization failed|Vulkan.*failed' "$log" || true
  [ "$status" -ne 0 ] || status=1
fi
exit "$status"
