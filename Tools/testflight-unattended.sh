#!/usr/bin/env bash
#
# Tools/testflight-unattended.sh — `punch it` with nobody watching.
#
#   ./Tools/testflight-unattended.sh [--oneshot-label <launchd-label>]
#
# Tools/testflight.sh is the INTERACTIVE entry point: it assumes a human already cleared the
# two preconditions the lane refuses to proceed without — a clean tree and a closed Editor.
# A scheduled run has no human, so this wrapper clears them itself, under the two rules Cesar
# set on 2026-08-18 when he asked for a 23:33 build:
#
#   DIRTY TREE  -> commit everything and ship it. His explicit call: a build always happens,
#                  even if what is half-written at fire time goes out under a commit nobody
#                  reviewed. (The alternative — abort and upload nothing — was the other option
#                  offered, and rejected.)
#   UNITY OPEN  -> ask it to quit GRACEFULLY and wait. If Unity holds unsaved scene work it
#                  raises a save dialog instead of quitting, the lock never clears, and this
#                  script ABORTS. It never force-kills: losing scene work to get a tester build
#                  is a bad trade, and no build is the safe failure.
#
# Everything after that is Tools/testflight.sh unchanged, so there is exactly one definition of
# what a TestFlight build is. Output goes to Builds/testflight-unattended.log (gitignored) and a
# macOS notification, because the whole point is that the result is read hours later.
#
# EXIT CODES  0 uploaded · 4 Unity would not quit · other = whatever the lane returned
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO" || exit 1
mkdir -p "$REPO/Builds"
LOG="$REPO/Builds/testflight-unattended.log"
LABEL=""
[ "${1:-}" = "--oneshot-label" ] && LABEL="${2:-}"

say() { printf '%s  %s\n' "$(date '+%F %H:%M:%S')" "$*" >>"$LOG"; }
notify() {
  if [ -x /opt/homebrew/bin/terminal-notifier ]; then
    /opt/homebrew/bin/terminal-notifier -title "GOLFIN TestFlight" -message "$1" >/dev/null 2>&1
  else
    osascript -e "display notification \"$1\" with title \"GOLFIN TestFlight\"" >/dev/null 2>&1
  fi
}
cleanup_agent() {
  # One-shot: remove the launchd agent so this never fires a second time unasked.
  [ -n "$LABEL" ] || return 0
  launchctl bootout "gui/$(id -u)/$LABEL" >/dev/null 2>&1
  rm -f "$HOME/Library/LaunchAgents/$LABEL.plist"
  say "removed launchd agent $LABEL (one-shot)"
}
finish() { say "RESULT: $2 (exit $1)"; notify "$2"; cleanup_agent; exit "$1"; }

say "=========================================================="
say "scheduled TestFlight run starting (pid $$)"

# ── 1. Unity ───────────────────────────────────────────────────────────────────
if [ -f "$REPO/Temp/UnityLockfile" ]; then
  say "Unity holds the project lock — requesting a graceful quit"
  osascript -e 'tell application "Unity" to quit' >>"$LOG" 2>&1 &
  for _ in $(seq 1 30); do
    [ -f "$REPO/Temp/UnityLockfile" ] || break
    sleep 3
  done
  if [ -f "$REPO/Temp/UnityLockfile" ]; then
    finish 4 "ABORTED — Unity would not quit (unsaved work?). Nothing built, nothing uploaded."
  fi
  say "Unity quit; lock released"
else
  say "Unity not running"
fi

# ── 2. Tree ────────────────────────────────────────────────────────────────────
if [ -n "$(git status --porcelain --untracked-files=all)" ]; then
  N=$(git status --porcelain --untracked-files=all | wc -l | tr -d ' ')
  say "tree dirty ($N paths) — auto-committing per the scheduled-run rule"
  git add -A
  git commit -q -F - <<MSG
chore(build): auto-commit swept by the scheduled TestFlight run

$N path(s) were uncommitted when Tools/testflight-unattended.sh fired. The lane refuses a dirty
tree because the build number is \`git rev-list --count HEAD\` and would otherwise not describe
the binary, so the scheduled run commits rather than skipping the build — Cesar's standing
instruction for unattended runs (2026-08-18).

Nobody reviewed this content before it shipped. If it should not have gone to testers, the build
it produced is the one to pull.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
MSG
  say "committed $(git rev-parse --short HEAD)"
  # GIT_TERMINAL_PROMPT=0: a credential prompt in a launchd job has no terminal to answer it and
  # would hang the run forever. Fail fast and carry on — the build does not depend on the push.
  GIT_TERMINAL_PROMPT=0 GIT_ASKPASS=/usr/bin/true git push -q 2>>"$LOG" \
    && say "pushed" || say "push failed (non-fatal — build continues)"
else
  say "tree already clean"
fi

BUILD_NUMBER="$(git rev-list --count HEAD)"
say "building $(git rev-parse --short HEAD) → build number $BUILD_NUMBER"

# ── 3. The lane, unchanged ─────────────────────────────────────────────────────
"$REPO/Tools/testflight.sh" >>"$LOG" 2>&1
CODE=$?
[ $CODE -eq 0 ] || finish $CODE "FAILED at the lane (exit $CODE) — see Builds/testflight-unattended.log"
say "lane exit 0 — uploaded build $BUILD_NUMBER"

# ── 4. Confirm at Apple, not just in fastlane's log ────────────────────────────
CELLAR="$(ls -d /opt/homebrew/Cellar/fastlane/*/libexec 2>/dev/null | tail -1)"
if [ -n "$CELLAR" ]; then
  set -a; . "$REPO/fastlane/.env" 2>/dev/null; set +a
  for _ in $(seq 1 12); do
    OUT=$(GEM_HOME="$CELLAR" LC_ALL=en_US.UTF-8 /opt/homebrew/bin/ruby -e '
      require "spaceship"
      Spaceship::ConnectAPI.token = Spaceship::ConnectAPI::Token.create(
        key_id: ENV["ASC_KEY_ID"], issuer_id: ENV["ASC_ISSUER_ID"], filepath: ENV["ASC_KEY_PATH"])
      app = Spaceship::ConnectAPI::App.find("com.nextinnovation.golfingame")
      b = Spaceship::ConnectAPI.get_builds(filter: { app: app.id, version: ARGV[0] },
            includes: "preReleaseVersion", limit: 1).to_models.first
      puts b ? "#{b.pre_release_version&.version} (#{b.version}) state=#{b.processing_state}" : "pending"
    ' "$BUILD_NUMBER" 2>/dev/null | tail -1)
    say "App Store Connect: ${OUT:-query failed}"
    case "$OUT" in *"state=VALID"*) finish 0 "✅ $OUT — live on TestFlight";; esac
    sleep 45
  done
  finish 0 "Uploaded build $BUILD_NUMBER; not yet VALID at Apple when polling stopped"
fi
finish 0 "Uploaded build $BUILD_NUMBER (Apple-side check skipped — fastlane cellar not found)"
