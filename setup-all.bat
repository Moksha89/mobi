@echo off
:: Mobile Control Hub - Complete One-Click Setup
:: RIGHT-CLICK this file > "Run as administrator" for best results
:: This sets up EVERYTHING: .NET, ADB, scrcpy, RustDesk, builds the app, and launches it

echo.
echo ============================================================
echo   Mobile Control Hub - Complete One-Click Setup
echo ============================================================
echo.
echo   This will install and configure everything automatically.
echo   For best results, run as Administrator.
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0setup-all.ps1"

if errorlevel 1 (
    echo.
    echo Setup encountered errors. Please check the output above.
    echo.
    pause
)
