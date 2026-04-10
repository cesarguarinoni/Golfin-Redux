@echo off
echo Exporting  UHole Lite GUI...
cd /d "%~dp0"
node scripts/generate-terrain.mjs lomond-country-club 1
node scripts/export-hole.mjs lomond-country-club 1
pause
