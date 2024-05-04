@echo off
SET command=%1

if "%command%"=="" (
    echo Please provide a command: start, stop, or status.
    goto end
)

PowerShell -NoProfile -ExecutionPolicy Bypass -File "Neo4j.ps1" -Command "%command%"

:end
