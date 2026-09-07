#!/usr/bin/env bash
#
# Tools/unity-build-ios.sh — headless iOS player build (fastlane_testflight_pipeline §4).
#
#   ./Tools/unity-build-ios.sh
#
# Runs Unity in batchmode against Golfin.EditorTools.CIBuild.BuildIOS, which activates the
# iOS-Full build profile via the BuildProfile API and writes Builds/iOS-Full/Unity-iPhone.xcodeproj.
# fastlane's `ios testflight_build` lane calls this, then hands that project to build_app.
#
# EXIT CODE IS LOAD-BEARING
#   Unity's exit code is propagated verbatim. Anything non-zero must stop the lane — otherwise
#   fastlane archives whatever Xcode project is already sitting in Builds/iOS-Full, which is
#   the previous build, and uploads a stale binary under a fresh build number. On failure the
#   tail of the batchmode log is echoed to stdout so the reason is in the fastlane output.
#
# UNITY VERSION
#   Derived from ProjectSettings/ProjectVersion.txt, never hardcoded — the next Editor upgrade
#   must fail loudly with "not installed", not silently build with the old Editor.
#   Override with: UNITY_PATH=/path/to/Unity ./Tools/unity-build-ios.sh
#
# EXIT CODES
#   0        build succeeded, Xcode project written
#   3        Unity Editor binary for this project's version not found
#   4        Unity Editor has the project open (from assert-unity-closed.sh)
#   5        Unity exited 0 but the Xcode project is missing (belt-and-braces)
#   other    Unity's own exit code
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(cd "$SCRIPT_DIR/.." && pwd)"

# punch_it_gps_variants — `./Tools/unity-build-ios.sh gps` builds the GPS variant: the
# iOS-Full-GPS profile, which carries the GOLFIN_GPS define GpsGate reads. Same OUT path, so
# everything downstream (build_app, the archive, the upload) is unchanged; the only difference
# is which profile Unity activated. No argument = the ordinary "punch it" build, unchanged.
#
# gps_standalone_shell — `./Tools/unity-build-ios.sh standalone` builds the PLAYLIFE thin shell:
# the iOS-Standalone profile (GOLFIN_GPS;GOLFIN_STANDALONE, ShellScene-only scene list). Same OUT
# path again, and a different bundle id / product name / icon applied at build time by
# StandaloneBuildPreprocessor and restored after — so downstream is unchanged here too, but the
# archive uploads to a DIFFERENT App Store record ("GOLFIN GPS", Apple ID 6737145432).
#
# golfer_3d_test — `./Tools/unity-build-ios.sh golfer` builds the GPS game plus the stand-in 3D
# golfer: the iOS-Full-Golfer profile (GOLFIN_GPS;GOLFIN_GOLFER_TEST). Same OUT path, same bundle
# id and same App Store record as `gps`, so downstream is unchanged; what differs is that
# GolferTestBuildGate lets Assets/Art/3D/Characters/_Test into this build and stashes it out of
# every other one. On device: a bare figure standing beside the ball, swinging on each shot.
VARIANT="${1:-}"
case "$VARIANT" in
  gps)        METHOD="Golfin.EditorTools.CIBuild.BuildIOSGps" ;;
  standalone) METHOD="Golfin.EditorTools.CIBuild.BuildIOSStandalone" ;;
  golfer)     METHOD="Golfin.EditorTools.CIBuild.BuildIOSGolferTest" ;;
  "")         METHOD="Golfin.EditorTools.CIBuild.BuildIOS" ;;
  *)          echo "ERROR: unknown variant '$VARIANT' (expected: gps, standalone, golfer, or no argument)" >&2; exit 2 ;;
esac

OUT="$PROJECT/Builds/iOS-Full"
LOG="$PROJECT/Builds/unity-build-ios.log"

# The lane calls this too; doing it here as well keeps a direct invocation just as safe.
"$SCRIPT_DIR/assert-unity-closed.sh" "$PROJECT" || exit $?

VERSION_FILE="$PROJECT/ProjectSettings/ProjectVersion.txt"
if [[ ! -f "$VERSION_FILE" ]]; then
  echo "ERROR: $VERSION_FILE not found — cannot determine the Unity version." >&2
  exit 3
fi
UNITY_VERSION="$(awk -F': *' '/^m_EditorVersion:/ {print $2; exit}' "$VERSION_FILE" | tr -d '[:space:]')"
if [[ -z "$UNITY_VERSION" ]]; then
  echo "ERROR: no m_EditorVersion line in $VERSION_FILE." >&2
  exit 3
fi

UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity}"
if [[ ! -x "$UNITY" ]]; then
  echo "ERROR: Unity $UNITY_VERSION not found/executable at: $UNITY" >&2
  echo "       ProjectVersion.txt asks for $UNITY_VERSION. Install it via Unity Hub, or override:" >&2
  echo "         UNITY_PATH=/path/to/Unity ./Tools/unity-build-ios.sh" >&2
  exit 3
fi

mkdir -p "$PROJECT/Builds"
echo "[unity-build-ios] Unity   : $UNITY  ($UNITY_VERSION, from ProjectVersion.txt)"
echo "[unity-build-ios] project : $PROJECT"
echo "[unity-build-ios] method  : $METHOD"
echo "[unity-build-ios] log     : $LOG"
echo "[unity-build-ios] building… (IL2CPP; expect 20-45 min)"

"$UNITY" -batchmode -quit -nographics \
  -projectPath "$PROJECT" \
  -buildTarget iOS \
  -executeMethod "$METHOD" \
  -logFile "$LOG"
CODE=$?

if [[ $CODE -ne 0 ]]; then
  echo "[unity-build-ios] Unity exited $CODE — last 120 lines of $LOG:" >&2
  tail -n 120 "$LOG" >&2 2>/dev/null || echo "  (no log at $LOG)" >&2
  exit $CODE
fi

# Unity exiting 0 is necessary but not sufficient: assert the artifact this pipeline hands
# to xcodebuild actually exists, so a no-op run can never be mistaken for a fresh build.
if [[ ! -d "$OUT/Unity-iPhone.xcodeproj" ]]; then
  echo "[unity-build-ios] Unity exited 0 but $OUT/Unity-iPhone.xcodeproj is missing." >&2
  tail -n 120 "$LOG" >&2 2>/dev/null || true
  exit 5
fi

echo "[unity-build-ios] OK → $OUT/Unity-iPhone.xcodeproj"
grep -E '^\[CIBuild\] (Info\.plist|SUCCEEDED|active build profile)' "$LOG" 2>/dev/null || true
exit 0
