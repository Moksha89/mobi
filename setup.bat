@echo off
:: Mobile Control Hub - Quick Setup
:: Double-click this file to run the setup script
:: Or right-click > "Run as administrator" for best results

echo.
echo ============================================
echo   Mobile Control Hub - Setup
echo ============================================
echo.
echo This will install .NET 8 SDK, download ADB and scrcpy,
echo build the app, and optionally launch the web dashboard.
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0setup.ps1"

if errorlevel 1 (
    echo.
    echo Setup encountered errors. Please check the output above.
    echo.
)

pause
