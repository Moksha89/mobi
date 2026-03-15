@echo off
:: Mobile Control Hub - Quick Start
:: Double-click this file to launch the web dashboard + cloud tunnel
:: Make sure you've run setup.bat or setup-all.bat first!

cd /d "%~dp0"

echo.
echo Starting Mobile Control Hub Web Dashboard...
echo.
echo The dashboard will be available at:
echo   Local:  http://localhost:5000
echo   Cloud:  http://69.197.142.77:5000 (if tunnel is running)
echo.

:: Start the cloud tunnel in a separate window
echo Starting cloud access tunnel...
start "MCH Cloud Tunnel" cmd /c "cd /d "%~dp0" && powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tunnel.ps1""

echo Starting web dashboard (press Ctrl+C to stop)...
echo.

dotnet run --project "%~dp0src\MobileControlHub.WebApi" --configuration Release

echo.
echo    Dashboard stopped. Press any key to close...
pause >nul
