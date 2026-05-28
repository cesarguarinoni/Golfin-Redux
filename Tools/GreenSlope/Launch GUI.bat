@echo off
cd /d "%~dp0"

REM Install dependencies if not present
if not exist node_modules (
  echo [GreenSlope] Installing dependencies...
  call npm install
)

REM Extract PDF panels if not yet done (rendering needs PyMuPDF - bootstrap it)
if not exist data\panels.json (
  python -c "import fitz" >NUL 2>&1
  if errorlevel 1 (
    echo [GreenSlope] Installing PyMuPDF...
    python -m pip install PyMuPDF
  )
  echo [GreenSlope] Extracting green panels from PDF...
  call npm run extract
)

echo [GreenSlope] Starting server at http://127.0.0.1:4178
start "" "http://127.0.0.1:4178"
node scripts/dev-server.mjs
pause
