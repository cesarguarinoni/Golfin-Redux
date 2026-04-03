@echo off
cd /d "%~dp0"
start "Funity Server" cmd /k node .\src\server.mjs
timeout /t 2 /nobreak >nul
start "" http://127.0.0.1:4173
