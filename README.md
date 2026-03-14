# Mobile Control Hub

A production-style Windows desktop application for remote device management, QA/testing, and administration of Android phones connected by USB to a Windows PC.

Built with **C# / WPF / .NET 8** using clean MVVM architecture.

---

## Features

### Dashboard
- System health overview: ADB, scrcpy, RustDesk status
- Connected device count and active session count
- Background monitor start/stop controls
- Real-time alerts and recent activity log

### Device Manager
- Auto-discover connected Android devices via ADB
- Display: serial, model, manufacturer, Android version, battery, screen state, connection type
- Per-device actions: scrcpy, reconnect, screenshot, reboot, copy info
- Bulk select and "Open All Selected" for multi-device scrcpy
- Search/filter devices by name, serial, model

### scrcpy Session Management
- Launch scrcpy per device with configurable arguments
- Track running processes and session state
- Auto-restart sessions (configurable)
- Stop individual or all sessions
- Custom scrcpy parameters per session or global defaults

### VPS / Remote Access Settings
- Configure VPS host/IP and SSH port
- Configure RustDesk relay server, ID/rendezvous server, and ports
- "Test Connection" button with status indicators (reachable/unreachable)
- Launch RustDesk client from the app

### Background Monitoring
- Automatic device connect/disconnect detection
- Periodic device state refresh with configurable interval
- ADB server failure detection and auto-recovery
- scrcpy session health monitoring
- Structured logging of all events

### Logging
- Structured logs stored in SQLite
- Log viewer with level filters (Info, Warning, Error)
- Filter by device serial
- Real-time log updates in UI
- Clear logs functionality

### Settings
- Configure paths to adb.exe, scrcpy.exe, RustDesk
- Monitoring intervals and retry settings
- Auto-reconnect and auto-restart toggles
- Windows startup registration
- Default scrcpy arguments
- Export/import configuration as JSON
- Browse buttons for executable paths

### Windows Startup
- Optional start on Windows boot (via Registry)
- Auto-start monitoring on launch
- Restore previous sessions on launch

---

## Architecture

```
MobileControlHub/
├── MobileControlHub.sln                    # Solution file
├── src/
│   ├── MobileControlHub.Domain/            # Domain layer
│   │   ├── Models/                         # AndroidDevice, ScrcpySession, etc.
│   │   ├── Interfaces/                     # IAdbService, IScrcpyService, etc.
│   │   └── Enums/                          # DeviceConnectionState, SessionState, etc.
│   │
│   ├── MobileControlHub.Infrastructure/    # Infrastructure layer
│   │   ├── Data/                           # DatabaseManager (SQLite)
│   │   ├── Services/                       # Service implementations
│   │   └── Helpers/                        # ProcessRunner, AdbOutputParser
│   │
│   └── MobileControlHub.UI/               # Presentation layer (WPF)
│       ├── Views/                          # XAML UserControls
│       ├── ViewModels/                     # MVVM ViewModels
│       ├── Converters/                     # Value converters
│       ├── Themes/                         # Dark theme resources
│       └── Assets/                         # Icons, images
│
└── tools/                                  # External binaries (adb, scrcpy)
```

### Layer Responsibilities

| Layer | Responsibility |
|-------|---------------|
| **Domain** | Models, interfaces, enums. Zero dependencies. |
| **Infrastructure** | Service implementations, SQLite database, process wrappers. Depends on Domain. |
| **UI** | WPF views, MVVM ViewModels, theme, converters. Depends on Domain + Infrastructure. |

### Key Interfaces

| Interface | Purpose |
|-----------|---------|
| `IAdbService` | Wraps adb.exe commands for device discovery and actions |
| `IScrcpyService` | Manages scrcpy session lifecycle |
| `IRustDeskService` | Wraps RustDesk client launch and connectivity checks |
| `IDeviceMonitorService` | Background polling and device state tracking |
| `IConfigurationService` | SQLite-backed config persistence with JSON export/import |
| `ILogService` | Structured logging to SQLite with real-time UI events |

### Design Patterns
- **MVVM** with data binding and commands
- **Dependency Injection** via `Microsoft.Extensions.DependencyInjection`
- **Repository/Service pattern** for data and process access
- **Observer pattern** via C# events for real-time UI updates
- **Process wrapper pattern** for external tool integration

---

## Prerequisites

- **Windows 10/11**
- **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Visual Studio 2022** (17.8+) with ".NET desktop development" workload, or VS Code with C# extension

### External Tools (place in `tools/` folder or configure paths in Settings)

- **ADB (Android Debug Bridge)** - [Platform Tools](https://developer.android.com/tools/releases/platform-tools)
- **scrcpy** - [GitHub Releases](https://github.com/Genymobile/scrcpy/releases)
- **RustDesk** (optional) - [GitHub Releases](https://github.com/rustdesk/rustdesk/releases)

---

## Setup & Build Instructions

### 1. Clone the Repository

```bash
git clone https://github.com/Moksha89/mobile-control-hub.git
cd mobile-control-hub
```

### 2. Place External Tools

Download and extract into the `tools/` directory:
```
tools/
├── adb.exe
├── AdbWinApi.dll
├── AdbWinUsbApi.dll
├── scrcpy.exe
├── scrcpy-server
└── (other scrcpy dependencies)
```

Or configure custom paths in Settings after first launch.

### 3. Build

**Via command line:**
```bash
dotnet restore
dotnet build --configuration Release
```

**Via Visual Studio:**
1. Open `MobileControlHub.sln`
2. Set `MobileControlHub.UI` as startup project
3. Build Solution (Ctrl+Shift+B)

### 4. Run

```bash
dotnet run --project src/MobileControlHub.UI
```

Or press F5 in Visual Studio.

### 5. Publish (Self-Contained)

```bash
dotnet publish src/MobileControlHub.UI -c Release -r win-x64 --self-contained true -o publish/
```

Copy the `tools/` folder into the `publish/` directory.

---

## Configuration

Settings are stored in SQLite at:
```
%LOCALAPPDATA%\MobileControlHub\mch.db
```

### Default Configuration

| Setting | Default Value |
|---------|--------------|
| ADB Path | `tools\adb.exe` |
| scrcpy Path | `tools\scrcpy.exe` |
| RustDesk Path | `C:\Program Files\RustDesk\rustdesk.exe` |
| Poll Interval | 5 seconds |
| ADB Timeout | 10 seconds |
| Max Retries | 3 |
| Default scrcpy Args | `--max-size=1024 --max-fps=30` |

### Export/Import

Use the Settings page to export configuration to JSON or import from a JSON file on your Desktop.

---

## RustDesk Self-Hosted Server Setup

This app does **not** fork or rewrite RustDesk. It provides helpers to:
1. Configure your self-hosted server endpoints
2. Test connectivity to relay and ID servers
3. Launch the installed RustDesk client

### Pointing RustDesk to Your Self-Hosted Server

1. Install RustDesk client on the Windows machine
2. Open RustDesk > Settings > Network
3. Set **ID Server** to your VPS IP (e.g., `your-vps-ip`)
4. Set **Relay Server** to your VPS IP (same unless split)
5. Set **API Server** to `http://your-vps-ip:21118` (if using hbbs API)
6. Save and restart RustDesk

Alternatively, configure these in the Mobile Control Hub VPS / Remote page and the app will display connectivity status.

### Default RustDesk Ports

| Service | Port |
|---------|------|
| hbbs (ID/Rendezvous) | 21116 (TCP+UDP) |
| hbbr (Relay) | 21117 (TCP) |
| API | 21118 (TCP) |

Ensure these ports are open on your VPS firewall.

---

## Test Plan

### Scenario: No Devices Connected
- Launch app
- Dashboard should show 0 devices
- Device Manager shows empty grid
- No errors in logs

### Scenario: Unauthorized Device
- Connect a device that hasn't authorized USB debugging
- Device should appear with "Unauthorized" state (amber indicator)
- Actions requiring shell access should show appropriate errors
- scrcpy launch should fail gracefully with error message

### Scenario: Multiple Devices
- Connect 2+ devices via USB hub
- All devices should appear in Device Manager
- Each device should show correct serial, model, battery
- "Open All Selected" should launch scrcpy for each selected device
- Sessions page should show all active sessions

### Scenario: ADB Missing
- Set ADB path to non-existent file
- Dashboard should show ADB as "Not Found" (red indicator)
- Device scan should log an error
- Alert should appear on Dashboard

### Scenario: scrcpy Missing
- Set scrcpy path to non-existent file
- Dashboard should show scrcpy as "Not Found" (red indicator)
- Attempting to open scrcpy session should show error
- Other device actions (screenshot, reboot) should still work

### Scenario: RustDesk Not Configured
- Leave RustDesk path empty or pointing to missing exe
- Dashboard shows RustDesk as "Not Running"
- VPS page connectivity tests should fail gracefully
- App continues to function for all non-RustDesk features

### Scenario: Device Disconnect During Active Session
- Start scrcpy session for a device
- Physically disconnect the USB cable
- Monitor should detect disconnection within poll interval
- scrcpy session should terminate
- If auto-restart is enabled, it should attempt restart (which will fail)
- Device should show as "Disconnected" in Device Manager
- Reconnecting USB should re-discover the device

### Scenario: ADB Server Crash
- Kill ADB server externally (`adb kill-server`)
- Monitor should detect failure after 3 consecutive errors
- Auto-recovery should restart ADB server
- Devices should re-appear after recovery

---

## Future Enhancements

1. **Web Dashboard / REST API** - Expose device data via ASP.NET Core API for remote monitoring
2. **Device Groups** - Organize devices into groups for batch operations
3. **Screen Recording** - Record device screen via ADB and save locally
4. **APK Manager** - Batch install/uninstall APKs across devices
5. **Device Health History** - Track battery, temperature, uptime over time with charts
6. **Notification System** - Toast notifications for device events (connect/disconnect/low battery)
7. **Remote Shell** - Embedded ADB shell terminal in the UI
8. **TCP/IP Wireless ADB** - Connect devices over WiFi without USB
9. **Multi-User Support** - Role-based access for team environments
10. **CI/CD Integration** - Trigger test runs on connected devices from CI pipelines
11. **Device Farm Mode** - Reserve/release devices for testing sessions
12. **Log Export** - Export logs to CSV/JSON for external analysis
13. **Plugin System** - Extensible architecture for custom device actions
14. **Dark/Light Theme Toggle** - User-selectable theme preference
15. **System Tray Mode** - Minimize to system tray with quick-access menu

---

## Assumptions & Limitations

- **Windows only** - WPF is a Windows-specific framework. The Domain and Infrastructure layers are cross-platform compatible for future porting.
- **USB connection required** - Primary mode is USB-connected devices. TCP/IP ADB support is listed as a future enhancement.
- **External tools not bundled** - ADB, scrcpy, and RustDesk must be downloaded separately due to licensing.
- **Single machine** - Designed for a single Windows host managing physically connected devices.
- **SQLite for local storage** - Not designed for concurrent multi-process access. The app uses WAL mode for best single-app performance.
- **RustDesk integration is passive** - The app helps configure and launch RustDesk but does not control it programmatically beyond basic process detection.
- **No elevated privileges required** - App runs as a standard user. Windows startup uses per-user Registry key (HKCU).

---

## License

This project is for legitimate remote device management, QA testing, and administration purposes only.
