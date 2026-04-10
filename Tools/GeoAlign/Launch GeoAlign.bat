@echo off
echo Starting GeoAlign...
cd /d "%~dp0"
if not exist node_modules (
    echo Installing dependencies...
    call npm install
)
start http://127.0.0.1:3200
node scripts/dev-server.mjs
pause
