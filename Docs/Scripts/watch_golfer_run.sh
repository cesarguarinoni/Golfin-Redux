#!/usr/bin/env bash
# Watch a GolferTestVerification run in the Unity Editor.log and EXIT LOUDLY on every outcome —
# success, exception, silence, timeout — so nobody has to notice a dead run by eye
# (2026-09-15: a putt stance scan died on a NotSupportedException inside an event handler and the
# editor sat in play mode for 40 minutes while a success-only watcher waited).
#
# usage: watch_golfer_run.sh "<success regex>" [silence_seconds=150] [max_seconds=900] [log_line_offset]
#   exit 0 DONE      the success regex matched (line printed)
#   exit 2 FAILED    an exception or STALLED line appeared (line + last harness line printed)
#   exit 3 STALLED   no new harness line for silence_seconds (last harness line printed)
#   exit 4 TIMEOUT   max_seconds elapsed (last harness line printed)
# Only lines written after the script starts (or after log_line_offset) are considered.
pat="$1"; silence="${2:-150}"; max="${3:-900}"
L="$LOCALAPPDATA/Unity/Editor/Editor.log"
n0="${4:-$(wc -l < "$L")}"
start=$(date +%s); last=$start; lastline=""
while :; do
  new=$(tail -n +"$n0" "$L" | grep -a "^\[GolferVerify\]\|^[A-Za-z.]*Exception: \|STALLED")
  cur=$(printf '%s\n' "$new" | grep -a "^\[GolferVerify\]" | tail -1)
  if [ "$cur" != "$lastline" ]; then lastline="$cur"; last=$(date +%s); fi
  if [ -n "$pat" ] && printf '%s\n' "$new" | grep -aq "$pat"; then
    echo "DONE: $(printf '%s\n' "$new" | grep -a "$pat" | tail -1 | cut -c1-320)"; exit 0
  fi
  bad=$(printf '%s\n' "$new" | grep -a "^[A-Za-z.]*Exception: \|STALLED" | tail -1)
  if [ -n "$bad" ]; then echo "FAILED: ${bad:0:320}"; echo "last harness line: ${lastline:0:240}"; exit 2; fi
  now=$(date +%s)
  if [ $((now - last)) -ge "$silence" ]; then echo "STALLED: no harness line for ${silence}s; last: ${lastline:0:240}"; exit 3; fi
  if [ $((now - start)) -ge "$max" ]; then echo "TIMEOUT after ${max}s; last: ${lastline:0:240}"; exit 4; fi
  sleep 4
done
