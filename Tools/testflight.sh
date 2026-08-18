#!/usr/bin/env bash
#
# Tools/testflight.sh — `fastlane ios testflight_build` with the environment it needs.
#
#   ./Tools/testflight.sh
#
# WHY THIS EXISTS — the locale is not optional
#   fastlane's gym pipes every line of xcodebuild's output through error-matching regexes.
#   xcodebuild prints '➜' (U+279C) in its dependency graph, on line ~18 of any archive. If Ruby
#   started with Encoding.default_external = US-ASCII — which is what happens in ANY shell
#   without LANG/LC_ALL set, including cron, CI, and a non-interactive `bash -c` — matching that
#   line raises `ArgumentError: invalid byte sequence in US-ASCII`, fastlane dies, and it takes
#   xcodebuild with it about three seconds into the archive. The failure looks like a build
#   failure and is not one. Observed for real 2026-08-18.
#
#   Setting LC_ALL in fastlane/.env does NOT fix it. Ruby fixes its external encoding at process
#   start; dotenv loads .env after that, so .env only silences fastlane's cosmetic
#   "requires your locale to be set to UTF-8" warning while leaving the encoding US-ASCII.
#   The export has to happen BEFORE the fastlane process starts — i.e. here.
#
#   Homebrew's bin is prepended for the same class of reason: `brew shellenv` is not in
#   ~/.zprofile on this machine, so `fastlane` is not on PATH in a fresh shell.
#
# Everything else about the run is the Fastfile's business — see fastlane/Fastfile and
# Docs/TESTFLIGHT_RUNBOOK.md § "One command".
set -euo pipefail

export LC_ALL="${LC_ALL:-en_US.UTF-8}"
export LANG="${LANG:-en_US.UTF-8}"
export PATH="/opt/homebrew/bin:$PATH"

command -v fastlane >/dev/null 2>&1 || {
  echo "ERROR: fastlane not found on PATH (looked in /opt/homebrew/bin)." >&2
  echo "       Install it with:  brew install fastlane" >&2
  exit 3
}

cd "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exec fastlane ios "${1:-testflight_build}"
