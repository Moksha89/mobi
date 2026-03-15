@echo off
:: ============================================================
:: Mobile Control Hub - ONE-CLICK SETUP & LAUNCH
:: ============================================================
:: This is the ONLY file you need to run.
:: It handles: setup, build, launch dashboard, cloud tunnel, RustDesk
::
:: Right-click > "Run as administrator" (recommended)
:: ============================================================

:: Change to the directory where this bat file lives
:: (When run as administrator, Windows defaults to C:\Windows\System32)
cd /d "%~dp0"

echo.
echo ============================================================
echo    Mobile Control Hub - One-Click Setup ^& Launch
echo ============================================================
echo.
echo    Working directory: %cd%
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup-all.ps1"

echo.
if errorlevel 1 (
    echo    [ERROR] Setup encountered an error. See above for details.
) else (
    echo    Setup completed.
)
echo.
echo    Press any key to close this window...
pause >nul
