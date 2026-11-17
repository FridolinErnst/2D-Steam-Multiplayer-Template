#!/usr/bin/env bash
# One-shot check against the open Unity Editor: recompile, report compile errors and console errors,
# and optionally run Edit Mode tests matching a filter.
#
#   Tools/verify.sh                      # compile + console
#   Tools/verify.sh HoldPathTests        # ... + run tests whose name matches the filter
#   Tools/verify.sh MyFixture playmode   # mode: editor (default) | playmode (one filter per run)
#   TEST_TIMEOUT=120 Tools/verify.sh X   # test run limit in seconds (default 300)
#
# Exit code: 0 ok, 1 compile/console errors or failed tests, 2 Editor not reachable,
# 3 Editor busy (in Play Mode, compiling, running tests, or not answering): nothing was done, try later.
set -uo pipefail
cd "$(dirname "$0")/.."

FILTER="${1:-}"
MODE="${2:-editor}"
TIMEOUT=300
TEST_TIMEOUT="${TEST_TIMEOUT:-300}"

busy() { echo "Unity Editor busy: $1. Nothing was done; try again later." >&2; exit 3; }

# Another session may be using the same Editor. Check before touching it, and fail fast instead of waiting.
editor=$(unity command editor status --timeout 15 2>&1)
if [[ $? -ne 0 ]]; then
  if echo "$editor" | grep -q "did not complete within"; then busy "not answering within 15 s"; fi
  echo "Unity Editor not reachable. Open the project in Unity (or run: unity status --until-ready)." >&2
  exit 2
fi
echo "$editor" | grep -q "playMode: stopped" || busy "in Play Mode (recompiling now would break the session)"
echo "$editor" | grep -q "compiling: true" && busy "already compiling"
unity command test status --timeout 15 2>/dev/null | grep -q "status: no_tests" || busy "a test run is in progress"

unity command editor set_auto_tick --enabled true >/dev/null 2>&1
unity command script recompile >/dev/null 2>&1

# The domain reload drops the connection for a moment; keep polling.
start=$(date +%s)
while :; do
  status=$(unity command script recompile_status 2>/dev/null || true)
  echo "$status" | grep -qE "status: (completed|up_to_date)|failed: true" && break
  if (( $(date +%s) - start > TIMEOUT )); then echo "Recompile did not finish in ${TIMEOUT}s." >&2; exit 1; fi
  sleep 2
done

if echo "$status" | grep -q "failed: true"; then
  echo "COMPILE FAILED"
  echo "$status" | sed -n '/errors:/,$p'
  exit 1
fi
echo "compile: ok"

errors=$(unity command console status 2>/dev/null | grep -oE '"consoleErrors":[0-9]+' | cut -d: -f2)
if [[ "${errors:-0}" != "0" ]]; then
  echo "console: ${errors} error(s)"
  unity command console get --level error 2>/dev/null | head -60
  exit 1
fi
echo "console: 0 errors"

if [[ -n "$FILTER" ]]; then
  echo "tests ($MODE): $FILTER"
  unity command test run --mode "$MODE" --filter "$FILTER" --timeout "$TEST_TIMEOUT" 2>&1 | tail -40
  exit "${PIPESTATUS[0]}"
fi
