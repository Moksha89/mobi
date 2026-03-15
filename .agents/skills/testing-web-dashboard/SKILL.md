# Testing Mobile Control Hub Web Dashboard

## Overview
The web dashboard consists of:
- **Backend**: ASP.NET Core 8 Web API (`src/MobileControlHub.WebApi/`)
- **Frontend**: React 18 + TypeScript + Vite (`src/MobileControlHub.WebApi/ClientApp/` or `/home/ubuntu/mch-web/`)

## Full-Stack Testing (Recommended)

### 1. Install .NET 8 SDK (if not present)
```bash
wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 8.0
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools
```

### 2. Build the WebApi project
```bash
dotnet build src/MobileControlHub.WebApi/MobileControlHub.WebApi.csproj
```

**Known issue**: `ProcessRunner` is a static class and must NOT be registered in DI via `AddSingleton<ProcessRunner>()`. If you see CS0718 build error, remove that line from Program.cs.

### 3. Run the backend
```bash
export DOTNET_ROOT=$HOME/.dotnet && export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools
dotnet run --project src/MobileControlHub.WebApi/MobileControlHub.WebApi.csproj
# Runs on http://0.0.0.0:5000
```

### 4. Run the frontend dev server
```bash
cd src/MobileControlHub.WebApi/ClientApp  # or /home/ubuntu/mch-web
npm install
npm run dev    # Runs on http://localhost:5173 with proxy to :5000
```

### 5. Test all 6 pages
Navigate to each page via sidebar and verify:
- **Dashboard** (/): 6 stat cards, Service Health table, Recent Logs with real data. Monitor should show "Running".
- **Devices** (/devices): Search input, Rescan button, "0 device(s) detected" empty state
- **Sessions** (/sessions): Refresh button, "0 active scrcpy session(s)" empty state
- **VPS / Remote** (/vps): Form with default ports (21117, 21116, 21118), Save/Test buttons
- **Logs** (/logs): Filter bar, real log entries loaded from SQLite (count > 0)
- **Settings** (/settings): Form fields populated with saved values from SQLite

### What to look for
- No "Internal Server Error" orange/red banners on any page (this means API calls are failing)
- Browser console should be clean (no React errors; network errors for SignalR websocket are acceptable)
- Dark Catppuccin theme renders correctly
- Interactive elements work: inputs, dropdowns, checkboxes, buttons

## Frontend-Only Testing
If you can't run the backend (no .NET SDK), you can still test the React frontend:
```bash
npm run dev
```
- All pages will show "Internal Server Error" banners (expected without backend)
- You can still verify layout, navigation, dark theme, and interactive elements
- Browser console will show 500 errors from failed API calls (expected)

## Build Verification
```bash
# Backend
dotnet build src/MobileControlHub.WebApi/MobileControlHub.WebApi.csproj

# Frontend (TypeScript + Vite)
cd src/MobileControlHub.WebApi/ClientApp
npm run build   # Outputs to ../wwwroot/
```

## API Endpoints to Verify
```bash
curl -s http://localhost:5000/api/status
curl -s http://localhost:5000/api/devices
curl -s http://localhost:5000/api/sessions
curl -s http://localhost:5000/api/logs/recent
curl -s http://localhost:5000/api/settings
curl -s http://localhost:5000/api/settings/vps
```
All should return valid JSON (not HTML error pages).

## Known Limitations
- On Linux: ADB/scrcpy errors in logs are expected (no `tools\adb.exe` on Linux)
- Full device testing requires Windows PC with Android phones connected via USB
- WPF UI project (`MobileControlHub.UI`) requires Windows SDK to build
- SignalR WebSocket connection may fail in dev mode (non-critical)

## Devin Secrets Needed
- VPS_HOST: VPS IP address for RustDesk server connectivity testing
- VPS_USERNAME: SSH username for VPS access
- VPS_PASSWORD: SSH password for VPS access
