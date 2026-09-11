#!/usr/bin/env bash
#
# Tools/asc-build-state.sh — what App Store Connect says about one build of one app record.
#
#   Tools/asc-build-state.sh <bundle-id> <build-number>
#     → "1.0.0 (2873) state=VALID"   or   "pending"   (build not yet visible at Apple)
#
# The lane's own log only proves the UPLOAD; Apple then processes the binary for minutes and can
# still reject it (PROCESSING → VALID | INVALID). Docs/PUNCH_IT_ROUTINE.md step 4: "Confirm at
# Apple, not in fastlane's log" — this is that query, extracted from testflight-unattended.sh so
# every lane (game record com.nextinnovation.golfingame, shell record
# com.nextinnovation.golfingps) and every session asks the same way. Read-only.
#
# Needs fastlane's bundled Ruby + spaceship (Homebrew cellar) and the ASC API key from
# fastlane/.env (ASC_KEY_ID / ASC_ISSUER_ID / ASC_KEY_PATH). Exit 0 = query ran (state is in the
# output, including "pending"); exit 2 = usage; exit 3 = toolchain/credentials missing.
set -euo pipefail

BUNDLE="${1:-}"; BUILD="${2:-}"
[[ -n "$BUNDLE" && -n "$BUILD" ]] || { echo "usage: $0 <bundle-id> <build-number>" >&2; exit 2; }

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CELLAR="$(ls -d /opt/homebrew/Cellar/fastlane/*/libexec 2>/dev/null | tail -1)"
[[ -n "$CELLAR" ]] || { echo "ERROR: fastlane cellar not found under /opt/homebrew/Cellar/fastlane" >&2; exit 3; }
[[ -f "$REPO/fastlane/.env" ]] || { echo "ERROR: $REPO/fastlane/.env missing (ASC API key)" >&2; exit 3; }
set -a; . "$REPO/fastlane/.env"; set +a

GEM_HOME="$CELLAR" LC_ALL=en_US.UTF-8 /opt/homebrew/bin/ruby -e '
  require "spaceship"
  Spaceship::ConnectAPI.token = Spaceship::ConnectAPI::Token.create(
    key_id: ENV["ASC_KEY_ID"], issuer_id: ENV["ASC_ISSUER_ID"], filepath: ENV["ASC_KEY_PATH"])
  app = Spaceship::ConnectAPI::App.find(ARGV[0]) or abort("no App Store Connect app with bundle id #{ARGV[0]}")
  b = Spaceship::ConnectAPI.get_builds(filter: { app: app.id, version: ARGV[1] },
        includes: "preReleaseVersion", limit: 1).to_models.first
  puts b ? "#{b.pre_release_version&.version} (#{b.version}) state=#{b.processing_state}" : "pending"
' "$BUNDLE" "$BUILD" 2>/dev/null | tail -1
