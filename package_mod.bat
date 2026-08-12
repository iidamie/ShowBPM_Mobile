@echo off
setlocal enabledelayedexpansion

set "MOD_ID=ShowBPM"
set "ROOT=%~dp0"
set "ROOT=%ROOT:~0,-1%"

set /p VERSION=<"%ROOT%\VERSION.txt"
if "%VERSION%"=="" (
    echo VERSION.txt is empty.
    exit /b 1
)

set "DLL=%~1"
if not "%DLL%"=="" (
    if not exist "%DLL%" (
        echo DLL not found: %DLL%
        exit /b 1
    )
) else (
    if exist "%ROOT%\MobilePlugin\bin\Release\net10.0\%MOD_ID%.dll" set "DLL=%ROOT%\MobilePlugin\bin\Release\net10.0\%MOD_ID%.dll"
    if "!DLL!"=="" if exist "%ROOT%\%MOD_ID%.dll" set "DLL=%ROOT%\%MOD_ID%.dll"
)

if "%DLL%"=="" (
    echo %MOD_ID%.dll was not found.
    echo Build MobilePlugin first, or pass the DLL path:
    echo package_mod.bat path\to\%MOD_ID%.dll
    exit /b 1
)

set "TMP=%ROOT%\tmp_package"
set "MOD_DIR=%TMP%\%MOD_ID%"
set "ZIP=%ROOT%\%MOD_ID%-%VERSION%.zip"

if exist "%TMP%" rmdir /s /q "%TMP%"
if exist "%ZIP%" del /q "%ZIP%"
mkdir "%MOD_DIR%"

copy /y "%DLL%" "%MOD_DIR%\%MOD_ID%.dll" >nul

cd /d "%TMP%"
tar -a -c -f "%ZIP%" "%MOD_ID%"
set "TAR_EXIT=%ERRORLEVEL%"
cd /d "%ROOT%"

if not "%TAR_EXIT%"=="0" (
    echo Failed to create zip.
    exit /b %TAR_EXIT%
)

rmdir /s /q "%TMP%"
echo Created: %ZIP%
pause
