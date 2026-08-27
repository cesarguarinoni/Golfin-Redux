#!/usr/bin/env bash
#
# Tools/perfbot-cooled.sh — run one PerfBaselineBot job to the COOLED protocol.
#
#   ./Tools/perfbot-cooled.sh <jobIdx> <tier> [runs] [cooldownSeconds]
#   ./Tools/perfbot-cooled.sh 19 high 3 300
#
# WHY THIS EXISTS. The protocol says "cooled to Nominal, 3 runs, median". A fixed sleep cannot
# deliver that: how long an iPhone needs is not knowable in advance and depends on what else the
# phone has been doing. So this does not assume — it LAUNCHES, reads the bot's own
# thermalAtBoot= line, and only counts the run if it booted Nominal. A warm run is discarded and
# retried after a longer wait, with the cooldown backing off each time.
#
# The other half of why: tier jobs are always run ascending (Low, Mid, High), so High — the tier
# that heats the device most — systematically starts from the worst conditions and is the one
# tier a warm triage cannot measure at all. Run High FIRST, cooled, or its number is fiction.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
JOB="${1:-}"; TIER="${2:-}"; WANT="${3:-3}"; COOL="${4:-300}"
[[ -z "$JOB" || -z "$TIER" ]] && { echo "usage: $0 <jobIdx> <tier> [runs] [cooldownSeconds]" >&2; exit 2; }

OUT="$ROOT/Docs/Diagnostics/perfbot_cooled"; mkdir -p "$OUT"
STAMP="$(date +%Y-%m-%d_%H-%M-%S)"
SUMMARY="$OUT/job${JOB}_${TIER}_${STAMP}.txt"

good=0; attempt=0; wait_s=0
: > "$SUMMARY"

while (( good < WANT && attempt < WANT + 4 )); do
  attempt=$(( attempt + 1 ))

  if (( wait_s > 0 )); then
    echo "[cooled] cooling ${wait_s}s before attempt ${attempt}…" | tee -a "$SUMMARY"
    sleep "$wait_s"
  fi

  log="/tmp/cooled_${JOB}_${attempt}.out"
  FORCE=1 TIMEOUT=260 "$ROOT/Tools/perfbot-runjob.sh" "$JOB" 0 "$TIER" > "$log" 2>&1

  boot=$(grep -aoE "thermalAtBoot=[A-Za-z]+" "$log" | head -1 | cut -d= -f2)
  stats=$(grep -aE "STATS .*label=[A-Za-z0-9_]+ " "$log" | head -1 | sed 's/.*\[PerfBot\] //')

  if [[ "$boot" == "Nominal" ]]; then
    good=$(( good + 1 ))
    echo "[cooled] attempt ${attempt}: boot=Nominal  COUNTED (${good}/${WANT})" | tee -a "$SUMMARY"
    echo "    $stats" | tee -a "$SUMMARY"
    grep -aE "STATS .*_late " "$log" | head -1 | sed 's/.*\[PerfBot\] /    /' | tee -a "$SUMMARY"
    wait_s="$COOL"
  else
    echo "[cooled] attempt ${attempt}: boot=${boot:-unknown}  DISCARDED (not cooled)" | tee -a "$SUMMARY"
    wait_s=$(( COOL + attempt * 120 ))    # back off — the device clearly needs longer
  fi
done

echo | tee -a "$SUMMARY"
echo "[cooled] job=$JOB tier=$TIER  counted ${good}/${WANT} cooled runs in ${attempt} attempts" | tee -a "$SUMMARY"
echo "[cooled] summary: $SUMMARY"
