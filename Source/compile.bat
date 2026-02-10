@echo off
REM ===============================================
REM  Windy - Compile Script (FINAL FIX)
REM  Put in: GameData\Windy\Source\compile.bat
REM  Outputs: GameData\Windy\Plugins\Windy.dll
REM ===============================================

REM === EDIT THESE PATHS IF NEEDED ===
set KSP_DIR=D:\SteamLibrary\steamapps\common\ModTestingKSP
set CSC_PATH="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

REM === NO NEED TO EDIT BELOW ===

set SRC_DIR=%~dp0
pushd "%SRC_DIR%\.."
set MOD_DIR=%CD%
set OUTPUT_DIR=%MOD_DIR%\Plugins

REM Prefer x64, fallback to 32-bit folder if needed
set REF_DIR=%KSP_DIR%\KSP_x64_Data\Managed
if not exist "%REF_DIR%" set REF_DIR=%KSP_DIR%\KSP_Data\Managed

if not exist "%REF_DIR%" (
  echo Could not find Managed folder.
  echo Tried:
  echo   %KSP_DIR%\KSP_x64_Data\Managed
  echo   %KSP_DIR%\KSP_Data\Managed
  pause
  goto :eof
)

if not exist "%REF_DIR%\Assembly-CSharp.dll" (
  echo ERROR: Missing %REF_DIR%\Assembly-CSharp.dll
  pause
  goto :eof
)

if not exist "%REF_DIR%\UnityEngine.dll" (
  echo ERROR: Missing %REF_DIR%\UnityEngine.dll
  pause
  goto :eof
)

REM Build reference list - START WITH THE ESSENTIALS
set REFS=/reference:"%REF_DIR%\Assembly-CSharp.dll"
set REFS=%REFS% /reference:"%REF_DIR%\UnityEngine.dll"

REM CRITICAL: CoreModule must come early (has MonoBehaviour, Vector3, Rect, etc.)
if exist "%REF_DIR%\UnityEngine.CoreModule.dll" (
  set REFS=%REFS% /reference:"%REF_DIR%\UnityEngine.CoreModule.dll"
) else (
  echo WARNING: UnityEngine.CoreModule.dll not found, some types may fail
)

REM Add all other UnityEngine modules
if exist "%REF_DIR%\UnityEngine.AudioModule.dll" set REFS=%REFS% /reference:"%REF_DIR%\UnityEngine.AudioModule.dll"
if exist "%REF_DIR%\UnityEngine.IMGUIModule.dll" set REFS=%REFS% /reference:"%REF_DIR%\UnityEngine.IMGUIModule.dll"
if exist "%REF_DIR%\UnityEngine.InputLegacyModule.dll" set REFS=%REFS% /reference:"%REF_DIR%\UnityEngine.InputLegacyModule.dll"
if exist "%REF_DIR%\UnityEngine.PhysicsModule.dll" set REFS=%REFS% /reference:"%REF_DIR%\UnityEngine.PhysicsModule.dll"
if exist "%REF_DIR%\UnityEngine.AnimationModule.dll" set REFS=%REFS% /reference:"%REF_DIR%\UnityEngine.AnimationModule.dll"
if exist "%REF_DIR%\UnityEngine.UI.dll" set REFS=%REFS% /reference:"%REF_DIR%\UnityEngine.UI.dll"
if exist "%REF_DIR%\UnityEngine.UIModule.dll" set REFS=%REFS% /reference:"%REF_DIR%\UnityEngine.UIModule.dll"
if exist "%REF_DIR%\UnityEngine.TextRenderingModule.dll" set REFS=%REFS% /reference:"%REF_DIR%\UnityEngine.TextRenderingModule.dll"

REM Optional KSP assemblies
if exist "%REF_DIR%\Assembly-CSharp-firstpass.dll" set REFS=%REFS% /reference:"%REF_DIR%\Assembly-CSharp-firstpass.dll"
if exist "%REF_DIR%\KSPUtil.dll" set REFS=%REFS% /reference:"%REF_DIR%\KSPUtil.dll"

echo.
echo ===============================================
echo Building Windy.dll
echo KSP Folder : %KSP_DIR%
echo Source Dir : %SRC_DIR%
echo Output Dir : %OUTPUT_DIR%
echo Managed Dir: %REF_DIR%
echo ===============================================
echo.

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

if exist "%OUTPUT_DIR%\Windy.dll" del "%OUTPUT_DIR%\Windy.dll"

pushd "%SRC_DIR%"

REM Make sure we actually have .cs files
set HAS_CS=
for %%G in (*.cs) do set HAS_CS=1
if "%HAS_CS%"=="" (
  echo ERROR: No .cs files found in:
  echo   %SRC_DIR%
  pause
  popd
  popd
  goto :eof
)

REM Compile everything in Source\
%CSC_PATH% /nologo /langversion:5 /target:library /out:"%OUTPUT_DIR%\Windy.dll" /debug- /optimize+ %REFS% *.cs

if errorlevel 1 (
  echo.
  echo *** Build FAILED ***
  echo Scroll up for the first error line.
) else (
  echo.
  echo *** Build SUCCEEDED ***
  echo Created:
  echo   %OUTPUT_DIR%\Windy.dll
)

echo.
pause
popd
popd