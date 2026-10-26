#!/usr/bin/env bash
# Usage: Tools/qa/tests.sh [edit|play|all] [Unity test filter]
set -euo pipefail
qa_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
mode=${1:-edit}
case "$mode" in
  edit|play|all) ;;
  *) echo 'Usage: Tools/qa/tests.sh [edit|play|all] [Unity test filter]' >&2; exit 2 ;;
esac
if [ "$#" -gt 2 ]; then echo 'Too many arguments' >&2; exit 2; fi
if [ -n "${CG_TEST_TAG:-}" ] && [[ ! "$CG_TEST_TAG" =~ ^[a-zA-Z0-9_-]+$ ]]; then
  echo 'Invalid CG_TEST_TAG' >&2; exit 2
fi
source "$qa_dir/common.sh"
# Serialize access before copying/importing: Unity cannot share a project between runs.
mkdir -p "$SIM"
exec 9>"$SIM/.qa-tests.lock"
flock 9
ensure_sim

run_suite() {
  local suite=$1 platform=$2 tag=$1 status=0 report_status=0
  if [ -n "${CG_TEST_TAG:-}" ]; then
    tag=$CG_TEST_TAG
    if [ "$mode" = all ]; then tag="$tag-$suite"; fi
  fi
  local result="$WORK/$tag.xml" log="$WORK/$tag.log" runner_log="$WORK/$tag.runner.log"
  local -a runner args
  rm -f "$result"
  args=(-batchmode -projectPath "$SIM" -runTests -testPlatform "$platform" -testResults "$result" -logFile "$log")
  if [ -n "$test_filter" ]; then args+=(-testFilter "$test_filter"); fi
  if [ "$suite" = edit ] || [ "${CG_NOGRAPHICS:-0}" = 1 ]; then
    runner=("$UNITY" -nographics)
  else
    # Garage previews require RenderTextures, so the full PlayMode suite needs a GPU.
    : > "$WORK/$tag.xvfb.log"
    runner=(xvfb-run -a -e "$WORK/$tag.xvfb.log" -s '-screen 0 1280x720x24' "$UNITY" -force-vulkan)
  fi
  echo "Running $platform${test_filter:+ ($test_filter)}; log=$log"
  # Keep the lock in this shell only; Unity's background services can outlive the Editor.
  taskset -c "${CG_CPU_SET:-0-3}" timeout -k 20 "${CG_TEST_TIMEOUT:-1800}" "${runner[@]}" "${args[@]}" < /dev/null > "$runner_log" 2>&1 9>&- || status=$?
  sync_meta
  python3 "$qa_dir/report-unity-tests.py" "$result" || report_status=$?
  echo "Unity exit=$status; results=$result; launcher=$runner_log"
  if [ "$status" -ne 0 ] || [ "$report_status" -ne 0 ]; then
    if [ -f "$log" ]; then
      rg -m 8 'error CS|Exception|Licensing initialization failed|Vulkan.*failed' "$log" || true
    fi
    tail -n 10 "$runner_log"
    if [ "$status" -ne 0 ]; then return "$status"; fi
    return "$report_status"
  fi
}

test_filter=${2:-}
if [ "$mode" = edit ] || [ "$mode" = all ]; then run_suite edit EditMode; fi
if [ "$mode" = play ] || [ "$mode" = all ]; then run_suite play PlayMode; fi
