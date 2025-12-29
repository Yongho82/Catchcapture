@echo off
setlocal

echo ==========================================
echo   CatchCapture Installer Build Script
echo ==========================================

:: Set paths
set PROJECT_DIR=%~dp0
set ISS_FILE=%PROJECT_DIR%installer\CatchCapture.iss
set OUTPUT_DIR=%PROJECT_DIR%publish_folder

:: 1. Clean and Build
echo [1/3] Building project (Release)...
dotnet publish "%PROJECT_DIR%CatchCapture.csproj" -c Release -r win-x64 --self-contained false --output "%OUTPUT_DIR%"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Build failed.
    pause
    exit /b %ERRORLEVEL%
)

:: 2. Check for Inno Setup
echo [2/3] Checking for Inno Setup...
set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
    echo ERROR: Inno Setup (ISCC.exe) not found at %ISCC%
    echo Please install Inno Setup 6 or update the path in this script.
    pause
    exit /b 1
)

:: 3. Run Inno Setup
echo [3/3] Packaging with Inno Setup...
"%ISCC%" "%ISS_FILE%"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Packaging failed.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ==========================================
echo   DONE! Installer is in installer\dist\
echo ==========================================
pause
