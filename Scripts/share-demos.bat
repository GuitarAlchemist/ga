@echo off
REM share-demos.bat — Start ga-client dev server and expose via Cloudflare Tunnel
REM Usage: scripts\share-demos.bat

setlocal

set SCRIPT_DIR=%~dp0
set CLIENT_DIR=%SCRIPT_DIR%..\Apps\ga-client
set PORT=5173
set DEV_URL=http://localhost:%PORT%

echo Starting ga-client dev server on port %PORT%...
cd /d "%CLIENT_DIR%"
start /b cmd /c "npm run dev -- --port %PORT%"

echo Waiting for dev server...
set READY=0
for /L %%i in (1,1,30) do (
    if %READY%==0 (
        curl -s %DEV_URL% >nul 2>&1
        if not errorlevel 1 (
            set READY=1
            echo Dev server ready.
        ) else (
            timeout /t 1 /nobreak >nul
        )
    )
)

if %READY%==0 (
    echo ERROR: Dev server did not start within 30 seconds.
    exit /b 1
)

echo.
echo Starting Cloudflare Tunnel...
echo Share the URL below with anyone to access the demo.
echo Press Ctrl+C to stop.
echo.
cloudflared tunnel --url %DEV_URL%
