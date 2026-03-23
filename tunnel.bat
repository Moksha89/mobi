@echo off
:: Mobile Control Hub - Cloud Access Tunnel
:: Makes the dashboard accessible from anywhere at:
::   http://69.197.142.77:5000
::
:: Fully automatic - no password prompts

cd /d "%~dp0"

echo.
echo Starting Cloud Access Tunnel...
echo Your dashboard will be accessible at: http://69.197.142.77:5000
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tunnel.ps1"

echo.
echo    Tunnel stopped. Press any key to close...
pause >nul
