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
#   0  no lock: safe to run a batchmode build
#   4  Temp/UnityLockfile present: the Editor is open (or crashed and left a stale lock)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="${1:-$(cd "$SCRIPT_DIR/.." && pwd)}"
LOCK="$PROJECT/Temp/UnityLockfile"

if [[ ! -f "$LOCK" ]]; then
  echo "[assert-unity-closed] OK — no Unity lock at $LOCK"
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
