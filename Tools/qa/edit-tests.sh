#!/bin/bash
# All EditMode tests in batch Unity on a project copy. Usage: Tools/qa/edit-tests.sh
source "$(dirname "$0")/common.sh"; ensure_sim
"$UNITY" -batchmode -nographics -projectPath "$SIM" -runTests -testPlatform EditMode -testResults "$WORK/edit.xml" -logFile "$WORK/edit.log" >/dev/null 2>&1
echo "exit=$?"; sync_meta
python3 - "$WORK/edit.xml" <<'P'
import re, sys
s = open(sys.argv[1]).read()
m = re.search(r'<test-run[^>]*total="(\d+)" passed="(\d+)" failed="(\d+)"', s)
print('total/passed/failed', m.groups() if m else None)
for t in re.finditer(r'<test-case [^>]*fullname="([^"]+)"[^>]*result="Failed".*?<message><!\[CDATA\[(.*?)\]\]>', s, re.S): print('FAIL', t.group(1), '::', t.group(2)[:300])
P
grep -m5 "error CS" "$WORK/edit.log"
