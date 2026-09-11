#!/usr/bin/env bash
#
# Tools/assert-unity-closed.sh — refuse to continue while the Unity Editor holds the project.
#
# WHY
#   A batchmode build cannot take the project lock. If the interactive Editor is open, Unity
#   either refuses to start or (worse) two instances share one Library/AssetDatabase and
#   corrupt it. The error Unity itself prints in that situation is cryptic and buried a few
#   thousand lines into the batchmode log, so the fastlane lane calls this first and fails
#   in one readable line instead.
#
# USAGE
#   Tools/assert-unity-closed.sh [REPO_ROOT]
#   REPO_ROOT defaults to this script's own parent directory (the repo root — the script
#   lives in Tools/).
#
# EXIT CODES
#   0  no lock and no Editor process: safe to run a batchmode build
#   4  Temp/UnityLockfile present: the Editor is open (or crashed and left a stale lock)
#   4  an interactive Editor process has this project open even though the lock is GONE — an
#      AppleScript `quit` addressed by app name can land on an asset-import WORKER (same binary,
#      same bundle id), whose shutdown removes the lockfile while the Editor keeps running
#      (2026-09-11). The lock alone therefore proves nothing; the process list is checked too.
#      Quit it with Tools/quit-unity.sh, which addresses the Editor by pid.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="${1:-$(cd "$SCRIPT_DIR/.." && pwd)}"
LOCK="$PROJECT/Temp/UnityLockfile"

# The interactive Editor: `Unity -projectPath <this repo>` with no batchmode flag. Import workers
# (`-adb2 -batchMode`) die with the Editor and are not a lock holder in their own right.
editor_pids() {
  pgrep -f "Unity.app/Contents/MacOS/Unity .*-projectPath ${PROJECT}" 2>/dev/null \
    | while read -r pid; do
        cmd="$(ps -o command= -p "$pid" 2>/dev/null || true)"
        case "$cmd" in *-batchMode*|*-batchmode*|*-adb2*) ;; *) echo "$pid" ;; esac
      done
}

if [[ ! -f "$LOCK" ]]; then
  LIVE="$(editor_pids || true)"
  if [[ -n "$LIVE" ]]; then
    echo "ERROR: no lock file, but a Unity Editor process still has this project open (pid ${LIVE//$'\n'/ })." >&2
    echo "       The lock was released without the Editor exiting — a quit addressed to the app NAME" >&2
    echo "       hit an import worker instead of the Editor. A batchmode build now would share" >&2
    echo "       Library/ with the open Editor. Quit it by pid:  Tools/quit-unity.sh" >&2
    exit 4
  fi
  echo "[assert-unity-closed] OK — no Unity lock at $LOCK and no Editor process on $PROJECT"
  exit 0
fi

echo "ERROR: the Unity Editor has this project open." >&2
echo "       lock file: $LOCK" >&2

# A crash leaves the lock behind with no Editor running. Distinguish the two cases, because
# the fix is completely different and "close Unity" is maddening advice when Unity is closed.
if pgrep -f "Unity.app/Contents/MacOS/Unity .*-projectpath $PROJECT" >/dev/null 2>&1 ||
   pgrep -f "Unity.app/Contents/MacOS/Unity .*-projectPath $PROJECT" >/dev/null 2>&1; then
  echo "       A Unity Editor process is running on this project. Quit it (Cmd-Q) and re-run." >&2
else
  echo "       No Unity process is running on this project, so the lock looks STALE (a crash" >&2
  echo "       leaves it behind). If you are sure no Editor is open, remove it:" >&2
  echo "         rm '$LOCK'" >&2
fi
exit 4
