@echo off
:: ============================================================
:: Mobile Control Hub - ONE-CLICK SETUP & LAUNCH
:: ============================================================
:: This is the ONLY file you need to run.
:: It handles: setup, build, launch dashboard, cloud tunnel, RustDesk
::
:: Right-click > "Run as administrator" (recommended)
:: ============================================================

echo.
echo ============================================================
echo    Mobile Control Hub - One-Click Setup ^& Launch
echo ============================================================
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0setup-all.ps1"
