#!/usr/bin/env bash
#
# Docs/Scripts/measure_ios_data.sh — build_size_diet's one measurement instrument.
#
#   ./Docs/Scripts/measure_ios_data.sh <label> [dataDir]
#
# Writes  Docs/Specs/Active/build_size_diet/reference/data_<label>.txt  containing, for every
# file in the built Data folder: raw bytes (= the INSTALL contribution) and deflate-compressed
# bytes (= what a tester downloads), plus the two gate numbers.
#
# WHY DEFLATE AND NOT `du`
#   The two gates in SPEC §Goal are different measures of the same folder — "install" is the
#   uncompressed Payload, "Payload-compressed" is the sum of the .ipa's per-entry compressed
#   sizes. `zip -X -q -9` per file reproduces the second one: measured against the shipped
#   Builds/ipa/Golfin.ipa this lands within ~1% per file, and the non-Data remainder
#   (UnityFramework + plists + icons = 106.6 MiB raw / 33.9 MiB compressed) is constant and
#   added back here so both lines are directly comparable to the .ipa.
set -euo pipefail
LABEL="${1:?usage: measure_ios_data.sh <label> [dataDir]}"
DATA="${2:-Builds/iOS-Full/Data}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/.."
cd "$ROOT"
OUT="Docs/Specs/Active/build_size_diet/reference/data_${LABEL}.txt"
[ -d "$DATA" ] || { echo "no such Data dir: $DATA" >&2; exit 2; }
python3 Docs/Scripts/measure_ios_data.py "$LABEL" "$DATA" > "$OUT"
head -14 "$OUT"
echo "→ $OUT"
