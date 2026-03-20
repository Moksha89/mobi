# Testing Mobile Control Hub

## Local Development Setup

### Backend (ASP.NET Core 8)
```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet run --project src/MobileControlHub.WebApi/MobileControlHub.WebApi.csproj --urls "http://0.0.0.0:5000"
```
Verify: `curl -s http://localhost:5000/api/cuttlefish/status` should return JSON with `isConfigured: false`

### Frontend (Vite + React)
```bash
cd src/MobileControlHub.WebApi/ClientApp
npm run dev -- --host 0.0.0.0 --port 5173
```
Verify: `curl -s http://localhost:5173` should return 200

## Key UI Testing Paths

### Devices Page (`/devices`)
- Contains 4 device panels: Cloud Devices (Redroid), K8s Cloud Platform, Cuttlefish VM Platform, Physical Devices
- Each panel has expand/collapse, refresh, and create buttons
- Cuttlefish panel shows "Not configured" empty state when no host is set up

### Cuttlefish Create VM Modal
- Click "New VM" button in Cuttlefish panel header
- Modal loads 12 hardware profiles (Samsung, Pixel, OnePlus, Xiaomi, etc.) and 5 Android images
- Profile search filters by name, brand, model, category
- Selecting a profile highlights it with purple border + checkmark
- "Create VM" button stays disabled until a profile is selected

### CuttlefishScreen (`/cuttlefish/:deviceId/screen`)
- WebRTC viewer with 8 Genymotion-like sidebar control panels
- Sidebar panels: GPS, Battery, Network, Rotate, Navigation, Power, Camera, Biometrics
- Each panel expands when clicked, showing relevant controls
- Shows error state gracefully when device ID doesn't exist
- Top bar has Back, Hide controls, Fullscreen, Reconnect buttons

## VPS Deployment Verification
- Dashboard URL: `http://69.197.142.77:5000`
- API endpoints to verify:
  - `GET /api/cuttlefish/status` - Returns Cuttlefish host status
  - `GET /api/cuttlefish/profiles` - Returns 12 hardware profiles
  - `GET /api/cuttlefish/images` - Returns 5 Android images
  - `GET /api/devices` - Returns device list
- Deployed as self-contained .NET 8 app at `/opt/mch` via systemd service `mch-dashboard`
- Restart: `sudo systemctl restart mch-dashboard`

## Important Notes
- Cuttlefish VMs require a baremetal server with KVM support — the VPS only serves the dashboard
- Without a Cuttlefish host, the panel shows "Not configured" which is expected
- The `cuttlefish-setup.sh` script provisions the baremetal server (not the VPS)
- Frontend uses Vite proxy to forward `/api` requests to the backend on port 5000
- TypeScript compilation: `npx tsc --noEmit` from the ClientApp directory
- Self-contained publish: `dotnet publish -c Release -r linux-x64 --self-contained true`

## Devin Secrets Needed
- `VPS_PASSWORD` - Password for VPS at 69.197.142.77 (user: administrator)
- `TWILIO_ACCOUNT_SID` - Twilio account SID for phone number provisioning
- `TWILIO_AUTH_TOKEN` - Twilio auth token
- `GENYMOTION_API_TOKEN` - Genymotion Cloud API token
