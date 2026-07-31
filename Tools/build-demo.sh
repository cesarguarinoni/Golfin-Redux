#!/usr/bin/env bash
#
# Tools/build-demo.sh — batchmode GOLFIN demo build (demo_build_slice §3.5).
#
#   ./Tools/build-demo.sh [--platform ios|android]     (default: ios)
#
# What it does:
#   1. Refuses to run while the interactive Unity Editor has the project open
#      (two Unity instances on one project corrupt the Library / AssetDatabase).
#   2. Excludes the MCP tooling from the release payload — the reason the §2.1
#      ProjectSettings define-strip does NOT work (the MCP package's RecompileGate
#      force-re-adds UNITY_MCP_READY on every load, and the SignalR/AspNetCore DLLs
#      live in Assets/Plugins/NuGet as managed plugins that ship by import settings,
#      not asmdef references). So we physically remove both for the build:
#        - strip com.ivanmurzak.unity.mcp* from Packages/manifest.json
#        - move Assets/Plugins/NuGet (SignalR/AspNetCore/McpPlugin/Roslyn) aside
#      Both are ALWAYS restored on exit (trap), even if the build fails or is killed.
#   3. Launches Unity batchmode -executeMethod DemoBuild.Build{IOS,Android}Demo,
#      which activates the demo build profile and dumps Builds/build-report-*.txt
#      (BuildReport summary + top-50 packed assets + an MCP/SignalR leak tripwire).
#
# iOS batchmode emits an Xcode PROJECT (not an .ipa); the .ipa comes from an Xcode
# archive afterwards (testflight_distribution / Order 424). Android emits an APK directly.
#
# Override the Editor path with:  UNITY_PATH=/path/to/Unity ./Tools/build-demo.sh
#
set -euo pipefail

PLATFORM="ios"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --platform) PLATFORM="${2:-}"; shift 2 ;;
    -h|--help)  echo "usage: build-demo.sh [--platform ios|android]"; exit 0 ;;
    *) echo "unknown arg: $1" >&2; exit 2 ;;
  esac
done

case "$PLATFORM" in
  ios)     METHOD="GolfinRedux.DemoEditor.DemoBuild.BuildIOSDemo";     UTARGET="iOS" ;;
  android) METHOD="GolfinRedux.DemoEditor.DemoBuild.BuildAndroidDemo"; UTARGET="Android" ;;
  *) echo "invalid --platform '$PLATFORM' (expected: ios | android)" >&2; exit 2 ;;
esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(cd "$SCRIPT_DIR/.." && pwd)"
UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity}"

[[ -x "$UNITY" ]] || { echo "ERROR: Unity not found/executable at: $UNITY (override with UNITY_PATH=...)" >&2; exit 3; }

if [[ -f "$PROJECT/Temp/UnityLockfile" ]]; then
  echo "ERROR: $PROJECT/Temp/UnityLockfile exists — close the Unity Editor before building (two instances corrupt the project)." >&2
  exit 4
fi

MANIFEST="$PROJECT/Packages/manifest.json"
LOCK="$PROJECT/Packages/packages-lock.json"
NUGET="$PROJECT/Assets/Plugins/NuGet"
BK="$PROJECT/Builds/.mcp-exclude-backup"
LOG="$PROJECT/Builds/build-demo-$PLATFORM.log"
mkdir -p "$PROJECT/Builds"

restore() {
  echo "[build-demo] restoring MCP package + NuGet DLLs + hole data…"
  [[ -f "$BK/manifest.json" ]]      && mv -f "$BK/manifest.json"      "$MANIFEST"
  [[ -f "$BK/packages-lock.json" ]] && mv -f "$BK/packages-lock.json" "$LOCK"
  [[ -d "$BK/NuGet" ]]              && mv -f "$BK/NuGet"              "$NUGET"
  [[ -f "$BK/NuGet.meta" ]]         && mv -f "$BK/NuGet.meta"         "$NUGET.meta"
  if [[ -f "$BK/holedata/.moved" ]]; then
    while IFS= read -r rel; do
      [[ -z "$rel" ]] && continue
      [[ -e "$BK/holedata/$rel" ]]      && { mkdir -p "$(dirname "$PROJECT/$rel")"; mv -f "$BK/holedata/$rel" "$PROJECT/$rel"; }
      [[ -e "$BK/holedata/$rel.meta" ]] && mv -f "$BK/holedata/$rel.meta" "$PROJECT/$rel.meta"
    done < "$BK/holedata/.moved"
  fi
  rm -rf "$BK" 2>/dev/null || true
}
trap restore EXIT

echo "[build-demo] excluding MCP package + SignalR/NuGet DLLs for the release build…"
mkdir -p "$BK"
cp "$MANIFEST" "$BK/manifest.json"
[[ -f "$LOCK" ]] && cp "$LOCK" "$BK/packages-lock.json"

python3 - "$MANIFEST" <<'PY'
import json, sys
path = sys.argv[1]
data = json.load(open(path))
deps = data.get("dependencies", {})
removed = [k for k in list(deps) if k.startswith("com.ivanmurzak.unity.mcp")]
for k in removed:
    del deps[k]
json.dump(data, open(path, "w"), indent=2)
open(path, "a").write("\n")
print(f"[build-demo] removed from manifest: {removed}")
PY

if [[ -d "$NUGET" ]]; then
  mv "$NUGET" "$BK/NuGet"
  [[ -f "$NUGET.meta" ]] && mv "$NUGET.meta" "$BK/NuGet.meta"
  echo "[build-demo] moved Assets/Plugins/NuGet aside for the build."
fi

# Strip non-demo hole data from Resources. Assets/Resources ALWAYS ships (it ignores the
# scene-list override), so all 18 holes' heightmaps + zones.json (~370 MB) otherwise bloat
# the one-hole demo. Keep only the holes listed in demo_config.csv (playable_holes); the
# rest — plus the _test data — are moved aside for the build and restored on exit.
HOLEDATA_ROOT="$PROJECT/Assets/Resources/HoleData"
if [[ -d "$HOLEDATA_ROOT" ]]; then
  KEEP_HOLES="$(grep '^playable_holes' "$PROJECT/Assets/Resources/Data/demo_config.csv" | cut -d, -f2 | tr ';' '\n' | sed 's/[[:space:]]//g; s/^hole_/Hole_/')"
  mkdir -p "$BK/holedata"
  : > "$BK/holedata/.moved"
  while IFS= read -r hd; do
    base="$(basename "$hd")"
    if ! grep -qxF "$base" <<< "$KEEP_HOLES"; then
      rel="${hd#$PROJECT/}"
      mkdir -p "$(dirname "$BK/holedata/$rel")"
      mv "$hd" "$BK/holedata/$rel"
      [[ -f "$hd.meta" ]] && mv "$hd.meta" "$BK/holedata/$rel.meta"
      echo "$rel" >> "$BK/holedata/.moved"
    fi
  done < <(find "$HOLEDATA_ROOT" -mindepth 2 -maxdepth 2 -type d -name 'Hole_*' | sort)
  if [[ -d "$HOLEDATA_ROOT/_test" ]]; then
    rel="Assets/Resources/HoleData/_test"
    mkdir -p "$(dirname "$BK/holedata/$rel")"
    mv "$PROJECT/$rel" "$BK/holedata/$rel"
    [[ -f "$PROJECT/$rel.meta" ]] && mv "$PROJECT/$rel.meta" "$BK/holedata/$rel.meta"
    echo "$rel" >> "$BK/holedata/.moved"
  fi
  echo "[build-demo] moved $(grep -c . "$BK/holedata/.moved") non-demo hole-data folder(s) aside (keeping: $(echo $KEEP_HOLES))."
fi

echo "[build-demo] launching Unity batchmode ($PLATFORM → $UTARGET)…  log: $LOG"
set +e
"$UNITY" -batchmode -quit -nographics \
  -projectPath "$PROJECT" \
  -buildTarget "$UTARGET" \
  -executeMethod "$METHOD" \
  -logFile "$LOG"
CODE=$?
set -e

echo "[build-demo] Unity exited: $CODE"
echo "[build-demo] build log:    $LOG"
echo "[build-demo] build report: $PROJECT/Builds/build-report-*.txt   (check the 'MCP/SignalR exclusion' line)"
exit $CODE
