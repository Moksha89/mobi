# Testing Mobile Control Hub Web Dashboard

## Overview
The web dashboard consists of:
- **Backend**: ASP.NET Core 8 Web API (`src/MobileControlHub.WebApi/`)
- **Frontend**: React 18 + TypeScript + Vite (`src/MobileControlHub.WebApi/ClientApp/`)

## Frontend Testing (React)

### Setup
```bash
cd src/MobileControlHub.WebApi/ClientApp
npm install
npm run dev    # Starts Vite dev server on http://localhost:5173
```

### What to verify
- All 6 pages render without React crashes: Dashboard (/), Devices (/devices), Sessions (/sessions), VPS (/vps), Logs (/logs), Settings (/settings)
- Sidebar navigation highlights the active page
- Dark theme (Catppuccin-inspired) renders correctly
- Interactive elements work: search inputs, dropdowns, checkboxes, buttons
- Browser console shows NO React errors (API 500s are expected without backend)

### Key pages and interactive elements
- **Dashboard**: 6 stat cards, Service Health table, Recent Logs, Refresh button
- **Devices**: Search filter input, Rescan button, device cards with action buttons (scrcpy, reconnect, screenshot, reboot, copy)
- **Sessions**: Refresh button, Stop All button, session table with stop/restart actions
- **VPS / Remote**: VPS config form (host, ports), Save/Test buttons, RustDesk setup instructions. Typing VPS IP auto-updates relay/ID server placeholders.
- **Logs**: Level dropdown (Debug/Info/Warning/Error/Critical), category input, device serial input, entries limit, Apply/Clear buttons
- **Settings**: Tool path inputs (adb, scrcpy, RustDesk), monitoring checkboxes, scrcpy default args, Save Settings button

### Build verification
```bash
cd src/MobileControlHub.WebApi/ClientApp
npm run build   # TypeScript + Vite build, outputs to ../wwwroot/
```

## Backend Testing (ASP.NET Core)

Requires Windows with .NET 8 SDK. Cannot be tested on Linux without dotnet installed.

```bash
# On Windows:
dotnet build src/MobileControlHub.WebApi/MobileControlHub.WebApi.csproj
dotnet run --project src/MobileControlHub.WebApi
# Then open http://localhost:5000
```

### API endpoints to verify
- GET /api/status
- GET /api/devices
- GET /api/sessions
- GET /api/logs/recent
- GET /api/settings
- GET /api/settings/vps

## Known Limitations
- Backend (ASP.NET Core) requires Windows + .NET 8 SDK to build and run
- Without backend running, frontend shows "Internal Server Error" banners (expected)
- Full end-to-end testing (device discovery, scrcpy sessions) requires Android phones connected via USB
- The Vite dev server proxies /api and /hubs requests to http://localhost:5000

## Devin Secrets Needed
- VPS_HOST: VPS IP address for RustDesk server connectivity testing
- VPS_USERNAME: SSH username for VPS access
- VPS_PASSWORD: SSH password for VPS access
