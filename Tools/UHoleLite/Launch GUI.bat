@echo off
echo Starting UHole Lite GUI...
cd /d "%~dp0"
start http://127.0.0.1:4174
node scripts/dev-server.mjs
pause
