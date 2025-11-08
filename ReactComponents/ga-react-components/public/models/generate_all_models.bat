@echo off
REM Batch script to generate all Blender models
REM Run this script to create all Egyptian-themed 3D models for BSP DOOM Explorer

echo Generating All Blender Models...
echo.

set BLENDER="C:\Program Files\Blender Foundation\Blender 4.5\blender.exe"

REM Check if Blender exists
if not exist %BLENDER% (
    echo [ERROR] Blender not found at %BLENDER%
    echo Please update the BLENDER variable in this script
    pause
    exit /b 1
)

echo [OK] Found Blender 4.5
echo.

REM Generate Ankh
echo Generating Ankh...
%BLENDER% --background --python create_ankh.py
if exist ankh.glb (
    echo [OK] Created: ankh.glb
) else (
    echo [FAIL] Failed to create ankh.glb
)
echo.

REM Generate Stele
echo Generating Stele...
%BLENDER% --background --python create_stele.py
if exist stele.glb (
    echo [OK] Created: stele.glb
) else (
    echo [FAIL] Failed to create stele.glb
)
echo.

REM Generate Scarab
echo Generating Scarab...
%BLENDER% --background --python create_scarab.py
if exist scarab.glb (
    echo [OK] Created: scarab.glb
) else (
    echo [FAIL] Failed to create scarab.glb
)
echo.

REM Generate Pyramid
echo Generating Pyramid...
%BLENDER% --background --python create_pyramid.py
if exist pyramid.glb (
    echo [OK] Created: pyramid.glb
) else (
    echo [FAIL] Failed to create pyramid.glb
)
echo.

REM Generate Lotus
echo Generating Lotus...
%BLENDER% --background --python create_lotus.py
if exist lotus.glb (
    echo [OK] Created: lotus.glb
) else (
    echo [FAIL] Failed to create lotus.glb
)
echo.

echo ================================================================
echo Generation Complete!
echo ================================================================
echo.
echo Models are ready to use in Three.js!
echo Location: %CD%
echo.
echo Next steps:
echo 1. Update BSPDoomExplorer.tsx to use the new models
echo 2. Update Models3DTest.tsx to display the new models
echo 3. Test in the React app: npm run dev
echo.

pause

