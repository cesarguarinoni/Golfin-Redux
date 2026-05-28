#!/usr/bin/env bash
# GreenSlope Authoring Tool — macOS launcher
# Double-click in Finder or run from Terminal.
set -e

cd "$(dirname "$0")"

# Install dependencies if not present
if [ ! -d node_modules ]; then
  echo "[GreenSlope] Installing dependencies…"
  npm install
fi

# Extract PDF panels if not yet done (rendering needs PyMuPDF — bootstrap it)
if [ ! -f data/panels.json ]; then
  PY="$(command -v python3 || command -v python || true)"
  if [ -z "$PY" ]; then
    echo "[GreenSlope] ERROR: Python 3 not found. Install from https://www.python.org/downloads/ and retry." >&2
    exit 1
  fi
  if ! "$PY" -c "import fitz" >/dev/null 2>&1; then
    echo "[GreenSlope] Installing PyMuPDF…"
    "$PY" -m pip install --user PyMuPDF || "$PY" -m pip install PyMuPDF
  fi
  echo "[GreenSlope] Extracting green panels from PDF…"
  npm run extract
fi

echo "[GreenSlope] Starting server at http://127.0.0.1:4178"
# Open browser (non-blocking; give server 1s to start)
(sleep 1 && open "http://127.0.0.1:4178") &

npm run gui
