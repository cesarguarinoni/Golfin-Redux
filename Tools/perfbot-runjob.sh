#!/usr/bin/env bash
#
# Tools/perfbot-runjob.sh — arm and run ONE PerfBaselineBot job on the device.
#
#   ./Tools/perfbot-runjob.sh <jobIdx> [runIdx] [tier]
#   ./Tools/perfbot-runjob.sh 14 0 low
#   ./Tools/perfbot-runjob.sh 20 0            # tier comes from the Schedule entry
#
# PerfBaselineBot's own header documents this script ("runjob.sh writes job.txt every time")
# but it was never in the repo. This is it.
#
# WHAT IT DOES
#   1. Refuses to run unless the device reports thermal Nominal on the PREVIOUS run's log —
#      the measurement protocol is "cooled to Nominal" and a run started warm is not a data
#      point, it is a data point's evil twin. Override with FORCE=1 when you are deliberately
#      measuring a warm device.
#   2. Writes Documents/perfbot/job.txt into the app's data container. Start() consumes and
#      DELETES it, so exactly one launch is automated and the next launch belongs to whoever is
#      holding the phone.
#   3. Launches the app with the console attached and tees everything to a log file.
#   4. Greps out the [PerfBot] STATS / TEE / ENDURANCE / JOB_DONE lines at the end.
#
# JOB INDICES (Assets/Scripts/Dev/PerfBaselineBot.cs — 0-13 are FROZEN)
#   14-16  T_h08_tee_{low,mid,high}          17-19  T_h06_tee_{low,mid,high}
#   20-22  T_h06_endurance_{high,mid,low}    23-25  T_h01_tee_{low,mid,high}
#
# The `tier` argument is optional and independent of the schedule entry: it appends
# "tier=<x>" to job.txt, which re-tiers the run without needing a matching Schedule row.
# Use "auto" to explicitly CLEAR a pinned override so a Low run cannot leak into the next launch.
set -uo pipefail

DEVICE="${DEVICE:-C3F920D6-6B96-577C-B6B6-D83789823DB6}"   # "The Dark Urge" iPhone 15 Pro Max
BUNDLE="${BUNDLE:-com.nextinnovation.golfingame}"
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTDIR="$PROJECT/Docs/Diagnostics/perfbot"

JOB="${1:-}"
RUN="${2:-0}"
TIER="${3:-}"

if [[ -z "$JOB" ]]; then
  echo "usage: $0 <jobIdx> [runIdx] [tier: low|mid|high|auto]" >&2
  exit 2
fi

mkdir -p "$OUTDIR"
STAMP="$(date +%Y-%m-%d_%H-%M-%S)"
LABEL="job${JOB}_run${RUN}${TIER:+_$TIER}"
LOG="$OUTDIR/${LABEL}_${STAMP}.log"

# ── 1. Thermal gate ─────────────────────────────────────────────────────────────
# The bot logs thermalAtBoot= on every launch. The most recent log we collected is the best
# evidence we have of how warm the phone is right now; if the LAST run ended anywhere above
# Nominal, the device has not cooled and this run would be measuring the throttle, not the tier.
PREV="$(ls -t "$OUTDIR"/*.log 2>/dev/null | head -1)"
if [[ -n "$PREV" && "${FORCE:-0}" != "1" ]]; then
  LASTTHERM="$(grep -o 'thermal=[A-Za-z-]*' "$PREV" | tail -1 | cut -d= -f2)"
  if [[ -n "$LASTTHERM" && "$LASTTHERM" != "Nominal" ]]; then
    echo "REFUSING: the previous run ($(basename "$PREV")) ended at thermal=$LASTTHERM." >&2
    echo "          Let the phone cool to Nominal, or re-run with FORCE=1 to measure warm." >&2
    exit 4
  fi
fi

# ── 2. Arm ──────────────────────────────────────────────────────────────────────
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
mkdir -p "$TMP/perfbot"
printf '%s %s%s\n' "$JOB" "$RUN" "${TIER:+ tier=$TIER}" > "$TMP/perfbot/job.txt"
echo "[runjob] job.txt = $(cat "$TMP/perfbot/job.txt")"

xcrun devicectl device copy to \
  --device "$DEVICE" \
  --domain-type appDataContainer \
  --domain-identifier "$BUNDLE" \
  --source "$TMP/perfbot" \
  --destination "Documents/" || { echo "[runjob] copy-to FAILED" >&2; exit 5; }

# ── 3. Launch with the console attached ─────────────────────────────────────────
#
# The bot SOAKS FOREVER after JOB_DONE (it keeps logging thermal every 30 s so an operator can
# read the device's state whenever they pick the phone up). So --console never returns on its
# own: stream it in the background, wait for the JOB_DONE line, then terminate. TIMEOUT is the
# backstop for a run that wedges — a tee job reaches JOB_DONE in ~90 s, an endurance job in
# ~400 s, so the default covers a tee job and endurance jobs pass TIMEOUT=600.
TIMEOUT="${TIMEOUT:-240}"
echo "[runjob] launching $BUNDLE (timeout ${TIMEOUT}s) — log: $LOG"

xcrun devicectl device process launch \
  --device "$DEVICE" \
  --console \
  --terminate-existing \
  "$BUNDLE" > "$LOG" 2>&1 &
LAUNCH_PID=$!

DONE=0
for ((t=0; t<TIMEOUT; t++)); do
  if grep -aq "\[PerfBot\] JOB_DONE" "$LOG" 2>/dev/null; then DONE=1; echo "[runjob] JOB_DONE after ${t}s"; break; fi
  if grep -aq "\[PerfBot\] ABORT" "$LOG" 2>/dev/null; then echo "[runjob] ABORT seen after ${t}s"; break; fi
  kill -0 "$LAUNCH_PID" 2>/dev/null || { echo "[runjob] console stream ended after ${t}s"; break; }
  sleep 1
done
[[ $DONE -eq 0 ]] && echo "[runjob] WARN no JOB_DONE within ${TIMEOUT}s — see the log"

kill "$LAUNCH_PID" 2>/dev/null
xcrun devicectl device process terminate --device "$DEVICE" --bundle-identifier "$BUNDLE" >/dev/null 2>&1

# ── 4. Report ───────────────────────────────────────────────────────────────────
echo
echo "===== [PerfBot] lines from $LABEL ====="
grep -aE '\[PerfBot\] (JOB|TIER|TEE|STATS|ENDURANCE|JOB_DONE|ABORT|WARN)' "$LOG" || \
  echo "(none — did the app launch? is this a GOLFIN_TESTBUILD build?)"
echo
echo "log: $LOG"
