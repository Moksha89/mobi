@echo off
:: Mobile Control Hub - Quick Start
:: Double-click this file to launch the web dashboard
:: Make sure you've run setup.bat first!

echo.
echo Starting Mobile Control Hub Web Dashboard...
echo.
echo The dashboard will be available at:
echo   http://localhost:5000
echo.
echo Press Ctrl+C to stop the server.
echo.

dotnet run --project "%~dp0src\MobileControlHub.WebApi" --configuration Release
