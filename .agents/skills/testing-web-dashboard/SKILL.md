# Testing Mobile Control Hub Web Dashboard

## Overview
The Mobile Control Hub has a web dashboard (ASP.NET Core 8 backend + React/Vite frontend) that can be tested locally on Linux without a real Android device. UI controls render regardless of device connectivity.

## Prerequisites

### .NET 8 SDK
- Location: `/home/ubuntu/.dotnet` (may already be installed)
- Add to PATH: `export PATH="/home/ubuntu/.dotnet:$PATH"`
- Verify: `dotnet --version` should show 8.x
- If missing, install via: `curl -sSL https://dot.net/v1/dotnet-install.sh | bash`

### System.Drawing.Common
- The JPEG compression feature uses `System.Drawing.Common` which is Windows-only
- The NuGet package must be referenced in the .csproj file
- On Linux, the `System.Drawing` calls will throw `PlatformNotSupportedException` at runtime, but the catch block falls back to raw PNG — this is expected behavior
- Build warnings (CA1416) about platform compatibility are expected and harmless

### Frontend Dependencies
- Location: `src/MobileControlHub.WebApi/ClientApp/`
- Install: `npm install` (if node_modules missing)
- Vite dev server proxies `/api` requests to the backend on port 5000

## Running the Full Stack

### Step 1: Start Backend
```bash
export PATH="/home/ubuntu/.dotnet:$PATH"
dotnet run --project src/MobileControlHub.WebApi/MobileControlHub.WebApi.csproj --urls "http://0.0.0.0:5000"
```
- Runs on port 5000
- SQLite DB auto-created on first run
- API endpoints: `/api/status`, `/api/devices`, `/api/sessions`, `/api/logs/recent`, `/api/settings`

### Step 2: Start Frontend Dev Server
```bash
cd src/MobileControlHub.WebApi/ClientApp
npm run dev
```
- Runs on port 5173
- Hot reload enabled
- Proxies API calls to localhost:5000

### Step 3: Access in Browser
- Frontend: `http://localhost:5173`
- 7 pages: Dashboard, Devices, Sessions, VPS/Remote, Logs, Settings, Device Screen

## Testing the Screen Viewer

### Navigation
- Direct URL: `http://localhost:5173/devices/{any-serial}/screen`
- Or: Devices page → "View Screen" button on a device card
- Without a real device, use any string as the serial (e.g., `test-device`)

### Expected Behavior Without a Device
- "Screenshot failed (503)" banner appears — this is expected on Linux without ADB
- All UI controls still render and are interactive
- FPS shows 0, KB/frame shows "—"

### UI Sections to Verify (top to bottom in right panel)
1. **Stream Performance**: Quality slider, Resolution slider, Fast/Medium/HD presets
   - Fast: Quality 30%, Resolution 30%
   - Medium: Quality 50%, Resolution 50%
   - HD: Quality 85%, Resolution 100%
2. **Navigation**: Back, Home, Recent buttons
3. **Power & Screen**: Wake, Power, Lock, Mute buttons
4. **Volume**: Vol +, Vol - buttons
5. **Scroll / Swipe**: Up, Down, Left, Right buttons
6. **More Controls**: Bright -, Bright +, Screenshot, Search, Notifs, Quick Set, App Switch, Rotate
7. **Text Input**: Type Text button
8. **Quick Keys**: Enter, Del, Tab, Space, Esc, Menu, FwdDel, MvHome, MvEnd
9. **PIN / Password Unlock**: 0-9 keypad, Del (red), Clr, Enter/Unlock (green)
   - Clicking digits shows masked asterisks in display
   - Clr resets to placeholder text

## Known Issues / Gotchas
- `System.Drawing.Common` must be added as a NuGet package — without it, the build fails with CS1069 errors about Bitmap, ImageCodecInfo, etc.
- On Linux, JPEG compression falls back to raw PNG at runtime (PlatformNotSupportedException) — the catch block handles this gracefully
- The control panel is scrollable — scroll down to see More Controls, Quick Keys, and PIN pad sections
- The screen viewer page title bar shows device resolution as "1080x1920" (hardcoded default when device info unavailable)

## Devin Secrets Needed
- `VPS_HOST`: VPS IP address for cloud tunnel testing (currently 69.197.142.77)
- `VPS_PASSWORD`: VPS administrator password for SSH tunnel
