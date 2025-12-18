@echo off
setlocal enabledelayedexpansion

echo ====
echo RP-1 Full Thrust - Build Script
echo ====
echo.

set SEARCH_DIR=%cd%
echo Searching for KSP installation (looking for GameData)...
echo Starting from: %SEARCH_DIR%
echo KSP Path: %KSP_PATH%
echo Managed Assemblies Path: %MANAGED_PATH%
echo Source Path: %SRC_PATH%
echo Output Path: %OUTPUT_PATH%
echo.

:SEARCH_LOOP
if exist "%SEARCH_DIR%\GameData" (
    set KSP_PATH=%SEARCH_DIR%
    goto FOUND_KSP
)
for %%I in ("%SEARCH_DIR%\..") do set SEARCH_DIR=%%~fI
if "%SEARCH_DIR%"=="%SystemDrive%\" goto NOT_FOUND
goto SEARCH_LOOP

:NOT_FOUND
echo ERROR: Could not find KSP installation (no GameData folder found)
echo Run this script from somewhere inside your KSP folder tree, e.g. GameData\RP-1FullThrust\Source
pause
exit /b 1

:FOUND_KSP
echo Found KSP installation at: %KSP_PATH%
echo.

if not exist "%KSP_PATH%\KSP_x64_Data\Managed\Assembly-CSharp.dll" (
    echo ERROR: This doesn't look like a valid KSP install - missing Assembly-CSharp.dll
    pause
    exit /b 1
)

echo Searching for C# compiler: prefer KSP's Mono csc.exe...
set CSC_PATH=

set KSP_CSC="%KSP_PATH%\KSP_x64_Data\MonoBleedingEdge\lib\mono\4.5\csc.exe"
if exist %KSP_PATH%\KSP_x64_Data\MonoBleedingEdge\lib\mono\4.5\csc.exe (
    set CSC_PATH=%KSP_PATH%\KSP_x64_Data\MonoBleedingEdge\lib\mono\4.5\csc.exe
    echo Found KSP csc at: %CSC_PATH%
) else (
    echo KSP csc not found, falling back to .NET Framework locations...
    for %%V in (v4.8 v4.7.2 v4.0.30319) do (
        if exist "%SystemRoot%\Microsoft.NET\Framework64\%%V\csc.exe" (
            set CSC_PATH=%SystemRoot%\Microsoft.NET\Framework64\%%V\csc.exe
            goto FOUND_CSC
        )
        if exist "%SystemRoot%\Microsoft.NET\Framework\%%V\csc.exe" (
            set CSC_PATH=%SystemRoot%\Microsoft.NET\Framework\%%V\csc.exe
            goto FOUND_CSC
        )
    )
)

:FOUND_CSC
if "%CSC_PATH%"=="" (
    echo ERROR: csc.exe not found in KSP or .NET Framework.
    echo Please install .NET Framework developer pack or ensure KSP's MonoBleedingEdge exists.
    pause
    exit /b 1
)

echo Using C# compiler: "%CSC_PATH%"
echo.

set MANAGED_PATH=%KSP_PATH%\KSP_x64_Data\Managed
set SRC_PATH=%~dp0
set OUTPUT_PATH=%KSP_PATH%\GameData\RP-1FullThrust\Plugins
set GAMEDATA_MODDIR=%KSP_PATH%\GameData\RP-1FullThrust

echo Source folder: %SRC_PATH%
echo Output folder (DLL): %OUTPUT_PATH%
echo Mod GameData folder: %GAMEDATA_MODDIR%
echo Managed assemblies at: %MANAGED_PATH%
echo.

if not exist "%OUTPUT_PATH%" (
    echo Creating output folder at: %OUTPUT_PATH%
    mkdir "%OUTPUT_PATH%" 2>nul
)

echo Building RP-1 Full Thrust DLL...
echo.

"%CSC_PATH%" /target:library /out:"%OUTPUT_PATH%\RP1FullThrust.dll" "%SRC_PATH%RP1FullThrustLoadingImages.cs" "%SRC_PATH%Properties\AssemblyInfo.cs" ^
/reference:"%MANAGED_PATH%\Assembly-CSharp.dll" ^
/reference:"%MANAGED_PATH%\UnityEngine.CoreModule.dll" ^
/reference:"%MANAGED_PATH%\UnityEngine.dll" ^
/reference:"%MANAGED_PATH%\UnityEngine.UI.dll" ^
/reference:"%MANAGED_PATH%\UnityEngine.UIModule.dll" ^
/reference:"%MANAGED_PATH%\UnityEngine.TextRenderingModule.dll" ^
/reference:"%MANAGED_PATH%\UnityEngine.ImageConversionModule.dll" ^
/optimize+ /debug- /nologo

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED!
    echo Check the compiler output above for errors.
    pause
    exit /b 1
)

echo.
echo BUILD SUCCESSFUL!
echo DLL written to:
echo   %OUTPUT_PATH%\RP1FullThrust.dll
echo.

if not exist "%GAMEDATA_MODDIR%" mkdir "%GAMEDATA_MODDIR%" 2>nul
echo Install to GameData now? (Y/N)
set /p INSTALL_CHOICE=
if /I "%INSTALL_CHOICE%"=="Y" (
    if not exist "%GAMEDATA_MODDIR%\Plugins" mkdir "%GAMEDATA_MODDIR%\Plugins" 2>nul
    copy /Y "%OUTPUT_PATH%\RP1FullThrust.dll" "%GAMEDATA_MODDIR%\Plugins\" >nul
    if %ERRORLEVEL% EQU 0 (
        echo Installed RP1FullThrust.dll to %GAMEDATA_MODDIR%\Plugins\
    ) else (
        echo Failed to copy DLL - please copy manually from %OUTPUT_PATH%
    )
)

echo.
pause
endlocal