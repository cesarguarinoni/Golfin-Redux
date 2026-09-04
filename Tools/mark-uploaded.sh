#!/bin/bash
# mark-uploaded.sh — advance the App Store upload regression guard.
#
# WHAT IT DOES
#   Writes `git rev-list --count HEAD` into Docs/Versioning/last_uploaded_build.txt,
#   but ONLY when that number is strictly greater than what the file already holds.
#   BuildStampGenerator.OnPreprocessBuild refuses any store-bound build whose computed
#   build number is <= this value, so a stale-or-lower write would either be useless or
#   permanently wedge the build. Never regress; only ever move forward.
#
# WHY IT EXISTS
#   The guard used to be fed by a Unity menu item (GOLFIN/Build/Mark Current Commit As
#   Uploaded) that a human had to remember. Nobody ran it after the 2026-08-17 upload of
#   1.5.7 (2192), so the file sat at 0 and the guard was inert from the day it was written.
#   A safety net that depends on memory is not a safety net. This script is invoked
#   automatically from an Xcode Archive post-action injected by
#   Assets/Editor/iOSArchivePostAction.cs. The menu item stays as a manual escape hatch.
#
# KNOWN TRADE-OFF — ARCHIVE, NOT UPLOAD
#   The post-action fires on Product -> Archive, not on a successful App Store Connect
#   upload. Archiving and then discarding the archive still advances the guard. This is
#   deliberate: Xcode exposes no "upload succeeded" hook, and over-strict is the safe
#   direction here — the build number is git rev-list --count HEAD, so any further commit
#   clears the guard again, and Cesar will have committed before the next store build.
#   Do not try to detect real upload success.
#
# ALWAYS EXITS 0
#   A failing post-action is invisible in Xcode's UI — it would silently look like the
#   archive itself failed, or like nothing happened at all. Every failure path here logs
#   and returns success. Docs/Versioning/.mark-uploaded.log (gitignored) is the ONLY
#   diagnostic; read it when the guard did not move.
#
# DOES NOT COMMIT
#   The guard file is tracked, but this script never runs `git commit`. Cesar picks it up
#   with his next change. A build post-action that writes to the index is a surprise
#   nobody wants.
#
# ONE GUARD PER APP STORE RECORD (gps_standalone_shell §D8)
#   App Store Connect's "build number must be unique" rule is per APP, and this project now
#   ships to two records: the game (com.nextinnovation.golfingame) and the PLAYLIFE shell
#   (com.nextinnovation.golfingps, ASC app "GOLFIN GPS"). One shared guard file would make a
#   standalone upload at commit N refuse the GAME's upload at commit N — a collision that does
#   not exist at Apple. So the record is an argument, and each gets its own file:
#     game       -> Docs/Versioning/last_uploaded_build.txt          (unchanged; the default)
#     standalone -> Docs/Versioning/last_uploaded_build.golfingps.txt
#   Shipping BOTH variants of ONE commit is still sequential for the two GAME variants ("punch
#   it" and "punch it GPS" share a record) — the standalone is the one that is now independent.
#
# USAGE
#   Tools/mark-uploaded.sh [REPO_ROOT] [RECORD]
#   REPO_ROOT defaults to this script's own parent directory (i.e. the repo root, since
#   the script lives in Tools/). RECORD is `game` (default) or `standalone`.
#   Run it by hand from anywhere for debugging.

set -u

RECORD="${2:-game}"
case "$RECORD" in
  game)       GUARD_REL="Docs/Versioning/last_uploaded_build.txt" ;;
  standalone) GUARD_REL="Docs/Versioning/last_uploaded_build.golfingps.txt" ;;
  *)          echo "mark-uploaded.sh: unknown record '$RECORD' (expected: game, standalone)" >&2
              # Never fail a post-action; fall back to the game guard and say so in the log.
              GUARD_REL="Docs/Versioning/last_uploaded_build.txt" ;;
esac
LOG_REL="Docs/Versioning/.mark-uploaded.log"

# Absolute path first: Xcode post-action environments do not reliably inherit a useful
# PATH, so a bare `git` can simply not resolve. Fall back to PATH lookup for non-Mac use.
GIT="/usr/bin/git"
[ -x "$GIT" ] || GIT="$(command -v git 2>/dev/null || echo /usr/bin/git)"

SCRIPT_DIR="$(cd -- "$(dirname -- "$0")" && pwd -P)"
DEFAULT_REPO="$(dirname -- "$SCRIPT_DIR")"   # Tools/.. == repo root
REPO="${1:-$DEFAULT_REPO}"

# The caller passes a path derived from Xcode's $PROJECT_DIR. If that resolves to
# something that is not a git work tree, prefer the script's own location — the script
# physically lives in the repo, so it is the more trustworthy anchor.
if ! "$GIT" -C "$REPO" rev-parse --show-toplevel >/dev/null 2>&1; then
  REPO="$DEFAULT_REPO"
fi
REPO="$(cd -- "$REPO" 2>/dev/null && pwd -P || echo "$DEFAULT_REPO")"

GUARD="$REPO/$GUARD_REL"
LOG="$REPO/$LOG_REL"
STAMP="$(date '+%Y-%m-%d %H:%M:%S %z')"

mkdir -p "$(dirname -- "$LOG")" 2>/dev/null

log() {
  # old / new / wrote / sha — every run, success or failure.
  printf '%s\trecord=%s\told=%s\tnew=%s\twrote=%s\tsha=%s\t%s\n' \
    "$STAMP" "${RECORD:-game}" "${OLD:-?}" "${NEW:-?}" "$1" "${SHA:-?}" "$2" >>"$LOG" 2>/dev/null
}

is_number() { case "${1:-}" in ''|*[!0-9]*) return 1 ;; *) return 0 ;; esac; }

NEW="$("$GIT" -C "$REPO" rev-list --count HEAD 2>/dev/null | tr -d '[:space:]')"
SHA="$("$GIT" -C "$REPO" rev-parse --short=7 HEAD 2>/dev/null | tr -d '[:space:]')"

if ! is_number "$NEW"; then
  log no "git rev-list --count HEAD failed or returned non-numeric in $REPO"
  exit 0
fi

# Missing or non-numeric guard file counts as 0 — the guard is "not yet set", which is
# exactly the state this whole task exists to get out of.
OLD=0
if [ -f "$GUARD" ]; then
  RAW="$(tr -d '[:space:]' <"$GUARD" 2>/dev/null)"
  is_number "$RAW" && OLD="$RAW"
fi

if [ "$NEW" -le "$OLD" ]; then
  log no "no advance (new <= current)"
  exit 0
fi

mkdir -p "$(dirname -- "$GUARD")" 2>/dev/null
if printf '%s\n' "$NEW" >"$GUARD" 2>/dev/null; then
  log yes "advanced $GUARD_REL"
else
  log no "write failed: $GUARD"
fi

exit 0
