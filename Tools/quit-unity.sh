#!/usr/bin/env bash
#
# Tools/quit-unity.sh — ask THE EDITOR PROCESS (by pid) to quit gracefully, and wait for it to exit.
#
# WHY NOT `osascript -e 'tell application "Unity" to quit'`
#   The Editor spawns asset-import workers from the SAME binary (`Unity -adb2 -batchMode …`), and
#   every one of them registers with LaunchServices under the Editor's bundle id
#   (com.unity3d.UnityEditor5.x). AppleScript resolves `application "Unity"` by bundle id, so with
#   a worker alive the quit Apple Event goes to WHICHEVER of them the system picks. Observed
#   2026-09-11: the event hit import worker 4 — it quit, the Editor logged "Unexpected transport
#   error from import worker 4 (possible crash)", the worker's shutdown removed Temp/UnityLockfile,
#   and the Editor kept running with no lock. Every "graceful quit" that then "had to be
#   force-quit" was this: the quit never reached the Editor, and the lockfile check said it had.
#
# WHAT THIS DOES INSTEAD
#   1. Finds the Editor pid: the `Unity -projectPath <repo>` process that is NOT a batchmode worker.
#   2. Sends the graceful quit to THAT pid — NSRunningApplication.terminate() via JXA, the same
#      Apple Event the Dock's Quit sends. Unity runs its normal quit path: wantsToQuit handlers,
#      the Save-Scene prompt if anything is dirty, layout save, lock release.
#   3. Waits for the PID to exit (not for the lockfile — see above). A dirty scene leaves Unity
#      alive behind a Save dialog, which is the correct outcome for an unattended caller: refuse.
#
# USAGE
#   Tools/quit-unity.sh [REPO_ROOT] [TIMEOUT_SECONDS]      (defaults: repo root, 120)
#
# EXIT CODES
#   0  the Editor is not running, or quit and exited within the timeout
#   4  the Editor is still running after the timeout (unsaved work behind a dialog, or a hang)
#
# The caller decides whether it is SAFE to quit (read scene dirty state over MCP first —
# Docs/PUNCH_IT_ROUTINE.md "Never force-quit blind"); this script only makes the quit reach the
# right process and reports honestly whether it happened.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="${1:-$(cd "$SCRIPT_DIR/.." && pwd)}"
TIMEOUT="${2:-120}"

editor_pid() {
  # Workers carry -adb2/-batchMode; the Editor carries -projectPath <this repo> and neither.
  pgrep -f "Unity.app/Contents/MacOS/Unity .*-projectPath ${PROJECT}" 2>/dev/null \
    | while read -r pid; do
        cmd="$(ps -o command= -p "$pid" 2>/dev/null || true)"
        case "$cmd" in *-batchMode*|*-batchmode*|*-adb2*) ;; *) echo "$pid"; return 0 ;; esac
      done
}

PID="$(editor_pid || true)"
if [[ -z "$PID" ]]; then
  echo "[quit-unity] no Unity Editor process on $PROJECT"
  exit 0
fi

WORKERS="$(pgrep -f "Unity.app/Contents/MacOS/Unity -adb2" 2>/dev/null | tr '\n' ' ' || true)"
echo "[quit-unity] Editor pid $PID (import workers: ${WORKERS:-none}) — sending graceful quit to pid $PID"

# NSRunningApplication.terminate: the graceful quit event, addressed by pid. No GUI scripting,
# no Accessibility grant, and it cannot land on a worker. JXA exposes a zero-argument ObjC
# method as a property — `app.terminate` (no parentheses) INVOKES it and yields the BOOL.
SENT="$(osascript -l JavaScript -e "
  ObjC.import('AppKit');
  var app = \$.NSRunningApplication.runningApplicationWithProcessIdentifier($PID);
  if (app.isNil()) throw new Error('no running application with pid $PID');
  app.terminate;
")"
if [[ "$SENT" != "true" ]]; then
  echo "ERROR: the quit event was not accepted by pid $PID (terminate returned '$SENT')." >&2
  exit 4
fi

for ((i = 0; i < TIMEOUT; i += 2)); do
  if ! kill -0 "$PID" 2>/dev/null; then
    echo "[quit-unity] Editor pid $PID exited after ~${i}s"
    if [[ -f "$PROJECT/Temp/UnityLockfile" ]]; then
      echo "[quit-unity] WARNING: Temp/UnityLockfile still present after exit — stale lock, remove it before a batchmode build" >&2
    fi
    exit 0
  fi
  sleep 2
done

echo "ERROR: Unity Editor pid $PID is still running ${TIMEOUT}s after the quit request." >&2
echo "       Either a Save-Scene dialog is up (unsaved work — look at the Editor) or the quit hung." >&2
echo "       NOT force-killing: that is Cesar's call (Docs/PUNCH_IT_ROUTINE.md)." >&2
exit 4
