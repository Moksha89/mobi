@echo off
:: Mobile Control Hub - Cloud Access Tunnel
:: Makes the dashboard accessible from anywhere at:
::   http://69.197.142.77:5000
::
:: First time: You'll be asked for the VPS password (one-time only)
:: After that: Tunnel connects automatically using SSH keys

echo.
echo Starting Cloud Access Tunnel...
echo Your dashboard will be accessible at: http://69.197.142.77:5000
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0tunnel.ps1"
