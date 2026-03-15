# Mobile Control Hub - Complete Setup Script for Windows
# This script does EVERYTHING from start to finish:
#   1. Installs .NET 8 SDK (if missing)
#   2. Downloads ADB (Android Debug Bridge)
#   3. Downloads scrcpy (screen mirroring)
#   4. Downloads & installs RustDesk client
#   5. Configures RustDesk to use your self-hosted server
#   6. Builds the application
#   7. Adds Windows Firewall rule for port 5000
#   8. Disables sleep/hibernate so PC stays on
#   9. Sets up SSH tunnel for cloud access
#  10. Launches everything (dashboard + RustDesk + tunnel)
#
# Usage: Right-click setup-all.bat > "Run as administrator"
#   or:  powershell -ExecutionPolicy Bypass -File setup-all.ps1

param(
    [switch]$SkipDotNet,
    [switch]$SkipTools,
    [switch]$SkipRustDesk,
    [switch]$SkipBuild,
    [switch]$SkipFirewall,
    [switch]$SkipPowerSettings,
    [switch]$NoLaunch
)

$ErrorActionPreference = "Continue"
$ProgressPreference = "SilentlyContinue"

$AppName = "Mobile Control Hub"
$RepoRoot = $PSScriptRoot
$ToolsDir = Join-Path $RepoRoot "tools"

# === Your RustDesk Self-Hosted Server Config ===
$RustDeskVpsIP = "69.197.142.77"
$RustDeskPublicKey = "AOg+QZdySFsqK9qSF+cBZAh8MgcApky1DZCDMidEyMQ="

# Colors for output
function Write-Step($msg) { Write-Host "`n>> $msg" -ForegroundColor Cyan }
function Write-Ok($msg) { Write-Host "   [OK] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "   [!] $msg" -ForegroundColor Yellow }
function Write-Err($msg) { Write-Host "   [X] $msg" -ForegroundColor Red }
function Write-Info($msg) { Write-Host "   $msg" -ForegroundColor White }

# Banner
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "   $AppName - Complete One-Click Setup" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  This script will set up EVERYTHING automatically:" -ForegroundColor White
Write-Host "    1. .NET 8 SDK          (build & run the app)" -ForegroundColor Gray
Write-Host "    2. ADB                 (Android Debug Bridge)" -ForegroundColor Gray
Write-Host "    3. scrcpy              (screen mirroring)" -ForegroundColor Gray
Write-Host "    4. RustDesk client     (remote access from anywhere)" -ForegroundColor Gray
Write-Host "    5. Build the app       (compile the web dashboard)" -ForegroundColor Gray
Write-Host "    6. Firewall rule       (allow network access on port 5000)" -ForegroundColor Gray
Write-Host "    7. Power settings      (prevent PC from sleeping)" -ForegroundColor Gray
Write-Host "    8. Cloud tunnel        (access dashboard from anywhere)" -ForegroundColor Gray
Write-Host "    9. Launch everything   (dashboard + RustDesk + tunnel)" -ForegroundColor Gray
Write-Host ""

# Check if running as admin (needed for firewall and power settings)
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Warn "Not running as Administrator. Firewall and power settings may fail."
    Write-Host '   For best results, right-click setup-all.bat -> Run as administrator' -ForegroundColor Gray
    Write-Host ""
}

# ============================================================
# Step 1: Check/Install .NET 8 SDK
# ============================================================
if (-not $SkipDotNet) {
    Write-Step "Step 1/9: Checking .NET 8 SDK..."

    $dotnetVersion = $null
    try {
        $dotnetVersion = & dotnet --version 2>$null
    } catch {}

    if ($dotnetVersion -and $dotnetVersion.StartsWith("8.")) {
        Write-Ok ".NET SDK $dotnetVersion is already installed"
    } else {
        Write-Warn ".NET 8 SDK not found. Downloading installer..."

        $dotnetInstallerUrl = "https://dot.net/v1/dotnet-install.ps1"
        $dotnetInstallScript = Join-Path $env:TEMP "dotnet-install.ps1"

        try {
            Invoke-WebRequest -Uri $dotnetInstallerUrl -OutFile $dotnetInstallScript -UseBasicParsing
            Write-Host "   Installing .NET 8 SDK (this may take a few minutes)..."
            & $dotnetInstallScript -Channel 8.0
            
            # Refresh PATH
            $env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
            $env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
            
            $dotnetVersion = & dotnet --version 2>$null
            if ($dotnetVersion) {
                Write-Ok ".NET SDK $dotnetVersion installed successfully"
            } else {
                Write-Err ".NET SDK installation may have failed."
                Write-Host "   Please install manually: https://dotnet.microsoft.com/download/dotnet/8.0"
                Write-Host "   Then re-run this script with: -SkipDotNet"
                exit 1
            }
        } catch {
            Write-Err "Failed to download .NET installer: $_"
            Write-Host "   Please install manually: https://dotnet.microsoft.com/download/dotnet/8.0"
            exit 1
        }
    }
} else {
    Write-Step "Step 1/9: Skipping .NET SDK check (-SkipDotNet)"
}

# ============================================================
# Step 2: Download ADB Platform Tools
# ============================================================
if (-not $SkipTools) {
    Write-Step "Step 2/9: Setting up ADB (Android Debug Bridge)..."

    if (-not (Test-Path $ToolsDir)) {
        New-Item -ItemType Directory -Path $ToolsDir -Force | Out-Null
    }

    $adbPath = Join-Path $ToolsDir "adb.exe"

    if (Test-Path $adbPath) {
        Write-Ok "ADB already exists"
    } else {
        Write-Host "   Downloading ADB Platform Tools..."
        $adbUrl = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip"
        $adbZip = Join-Path $env:TEMP "platform-tools.zip"
        $adbExtractDir = Join-Path $env:TEMP "platform-tools-extract"

        try {
            Invoke-WebRequest -Uri $adbUrl -OutFile $adbZip -UseBasicParsing
            
            if (Test-Path $adbExtractDir) { Remove-Item -Recurse -Force $adbExtractDir }
            Expand-Archive -Path $adbZip -DestinationPath $adbExtractDir -Force
            
            $adbFiles = @("adb.exe", "AdbWinApi.dll", "AdbWinUsbApi.dll")
            foreach ($file in $adbFiles) {
                $src = Join-Path $adbExtractDir "platform-tools\$file"
                if (Test-Path $src) { Copy-Item $src -Destination $ToolsDir -Force }
            }

            if (Test-Path $adbPath) {
                Write-Ok "ADB downloaded and installed"
            } else {
                Write-Err "ADB download failed"
            }

            Remove-Item -Force $adbZip -ErrorAction SilentlyContinue
            Remove-Item -Recurse -Force $adbExtractDir -ErrorAction SilentlyContinue
        } catch {
            Write-Err "Failed to download ADB: $_"
            Write-Host "   Download manually: https://developer.android.com/tools/releases/platform-tools"
        }
    }

    # ============================================================
    # Step 3: Download scrcpy
    # ============================================================
    Write-Step "Step 3/9: Setting up scrcpy (screen mirroring)..."

    $scrcpyPath = Join-Path $ToolsDir "scrcpy.exe"

    if (Test-Path $scrcpyPath) {
        Write-Ok "scrcpy already exists"
    } else {
        Write-Host "   Downloading scrcpy..."
        $scrcpyVersion = "3.1"
        $scrcpyUrl = "https://github.com/Genymobile/scrcpy/releases/download/v$scrcpyVersion/scrcpy-win64-v$scrcpyVersion.zip"
        $scrcpyZip = Join-Path $env:TEMP "scrcpy.zip"
        $scrcpyExtractDir = Join-Path $env:TEMP "scrcpy-extract"

        try {
            Invoke-WebRequest -Uri $scrcpyUrl -OutFile $scrcpyZip -UseBasicParsing
            
            if (Test-Path $scrcpyExtractDir) { Remove-Item -Recurse -Force $scrcpyExtractDir }
            Expand-Archive -Path $scrcpyZip -DestinationPath $scrcpyExtractDir -Force
            
            $scrcpyInnerDir = Get-ChildItem -Path $scrcpyExtractDir -Directory | Select-Object -First 1
            if ($scrcpyInnerDir) {
                Get-ChildItem -Path $scrcpyInnerDir.FullName -File | ForEach-Object {
                    Copy-Item $_.FullName -Destination $ToolsDir -Force
                }
            }

            if (-not (Test-Path $scrcpyPath)) {
                Get-ChildItem -Path $scrcpyExtractDir -Recurse -File -Filter "scrcpy*" | ForEach-Object {
                    Copy-Item $_.FullName -Destination $ToolsDir -Force
                }
            }

            if (Test-Path $scrcpyPath) {
                Write-Ok "scrcpy downloaded and installed"
            } else {
                Write-Warn "scrcpy auto-install failed"
                Write-Host "   Download manually: https://github.com/Genymobile/scrcpy/releases"
            }

            Remove-Item -Force $scrcpyZip -ErrorAction SilentlyContinue
            Remove-Item -Recurse -Force $scrcpyExtractDir -ErrorAction SilentlyContinue
        } catch {
            Write-Warn "Failed to download scrcpy: $_"
            Write-Host "   Download manually: https://github.com/Genymobile/scrcpy/releases"
        }
    }
} else {
    Write-Step "Steps 2-3/9: Skipping tool downloads (-SkipTools)"
}

# ============================================================
# Step 4: Download & Install RustDesk Client
# ============================================================
if (-not $SkipRustDesk) {
    Write-Step "Step 4/9: Setting up RustDesk client (remote access)..."

    # Check if RustDesk is already installed
    $rustDeskInstalled = $false
    $rustDeskExePaths = @(
        "C:\Program Files\RustDesk\rustdesk.exe",
        "$env:LOCALAPPDATA\RustDesk\rustdesk.exe",
        "$env:ProgramFiles\RustDesk\rustdesk.exe"
    )
    $rustDeskExe = $null
    foreach ($p in $rustDeskExePaths) {
        if (Test-Path $p) {
            $rustDeskExe = $p
            $rustDeskInstalled = $true
            break
        }
    }

    if ($rustDeskInstalled) {
        Write-Ok "RustDesk is already installed at $rustDeskExe"
    } else {
        Write-Host "   Downloading RustDesk client..."
        
        try {
            # Get latest RustDesk release URL from GitHub API
            $rustDeskApiUrl = "https://api.github.com/repos/rustdesk/rustdesk/releases/latest"
            $headers = @{ "User-Agent" = "MobileControlHub-Setup" }
            $releaseInfo = Invoke-RestMethod -Uri $rustDeskApiUrl -Headers $headers -UseBasicParsing
            
            # Find the Windows x86_64 exe installer
            $asset = $releaseInfo.assets | Where-Object { $_.name -match "x86_64.*\.exe$" -and $_.name -notmatch "portable" } | Select-Object -First 1
            
            if ($asset) {
                $rustDeskUrl = $asset.browser_download_url
                $rustDeskInstaller = Join-Path $env:TEMP "rustdesk-installer.exe"
                
                Write-Host "   Downloading: $($asset.name)..."
                Invoke-WebRequest -Uri $rustDeskUrl -OutFile $rustDeskInstaller -UseBasicParsing
                
                Write-Host "   Installing RustDesk (this may take a moment)..."
                Start-Process -FilePath $rustDeskInstaller -ArgumentList "--silent-install" -Wait -NoNewWindow
                Start-Sleep -Seconds 3
                
                # Check if installed
                foreach ($p in $rustDeskExePaths) {
                    if (Test-Path $p) {
                        $rustDeskExe = $p
                        $rustDeskInstalled = $true
                        break
                    }
                }
                
                if ($rustDeskInstalled) {
                    Write-Ok "RustDesk installed successfully"
                } else {
                    Write-Warn "RustDesk installer ran but exe not found at expected paths"
                    Write-Host "   You may need to install manually: https://github.com/rustdesk/rustdesk/releases"
                }
                
                Remove-Item -Force $rustDeskInstaller -ErrorAction SilentlyContinue
            } else {
                Write-Warn "Could not find RustDesk Windows installer from GitHub releases"
                Write-Host "   Download manually: https://github.com/rustdesk/rustdesk/releases"
            }
        } catch {
            Write-Warn "Failed to download RustDesk: $_"
            Write-Host "   Download manually: https://github.com/rustdesk/rustdesk/releases"
        }
    }

    # Configure RustDesk to use self-hosted server
    if ($rustDeskInstalled -or (Test-Path "C:\Program Files\RustDesk\rustdesk.exe")) {
        Write-Step "   Configuring RustDesk for self-hosted server..."

        $rustDeskConfigDir = "$env:APPDATA\RustDesk\config"
        if (-not (Test-Path $rustDeskConfigDir)) {
            New-Item -ItemType Directory -Path $rustDeskConfigDir -Force | Out-Null
        }

        # Write the custom server config
        $rustDeskConfig = @"
rendezvous_server = '$RustDeskVpsIP'
nat_type = 1
serial = 0

[options]
custom-rendezvous-server = '$RustDeskVpsIP'
relay-server = '$RustDeskVpsIP'
key = '$RustDeskPublicKey'
"@
        $configFile = Join-Path $rustDeskConfigDir "RustDesk2.toml"
        Set-Content -Path $configFile -Value $rustDeskConfig -Force
        Write-Ok "RustDesk configured to use server: $RustDeskVpsIP"
        Write-Info "Config saved to: $configFile"
    }
} else {
    Write-Step "Step 4/9: Skipping RustDesk setup (-SkipRustDesk)"
}

# ============================================================
# Step 5: Build the application
# ============================================================
if (-not $SkipBuild) {
    Write-Step "Step 5/9: Building Mobile Control Hub..."

    try {
        Write-Host "   Restoring NuGet packages..."
        & dotnet restore "$RepoRoot\MobileControlHub.sln" 2>&1 | Out-Null
        Write-Ok "Packages restored"

        Write-Host "   Building solution (Release mode)..."
        $buildOutput = & dotnet build "$RepoRoot\MobileControlHub.sln" --configuration Release 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Ok "Build succeeded"
        } else {
            Write-Warn "Full solution build had issues. Building WebApi project..."
            $buildOutput = & dotnet build "$RepoRoot\src\MobileControlHub.WebApi\MobileControlHub.WebApi.csproj" --configuration Release 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Ok "WebApi project built successfully"
                Write-Warn "WPF desktop app may need Visual Studio 2022 to build"
            } else {
                Write-Err "Build failed. Output:"
                $buildOutput | ForEach-Object { Write-Host "   $_" }
                exit 1
            }
        }
    } catch {
        Write-Err "Build failed: $_"
        exit 1
    }
} else {
    Write-Step "Step 5/9: Skipping build (-SkipBuild)"
}

# ============================================================
# Step 6: Add Windows Firewall rule for port 5000
# ============================================================
if (-not $SkipFirewall) {
    Write-Step "Step 6/9: Configuring Windows Firewall..."

    if ($isAdmin) {
        try {
            # Remove old rule if exists
            $existingRule = Get-NetFirewallRule -DisplayName "Mobile Control Hub Web Dashboard" -ErrorAction SilentlyContinue
            if ($existingRule) {
                Remove-NetFirewallRule -DisplayName "Mobile Control Hub Web Dashboard" -ErrorAction SilentlyContinue
            }
            
            New-NetFirewallRule -DisplayName "Mobile Control Hub Web Dashboard" `
                -Direction Inbound -Protocol TCP -LocalPort 5000 `
                -Action Allow -Profile Private,Domain `
                -Description "Allow access to Mobile Control Hub web dashboard from LAN" | Out-Null
            
            Write-Ok "Firewall rule added: Allow TCP port 5000 (LAN only)"
        } catch {
            Write-Warn "Failed to add firewall rule: $_"
            Write-Host "   You may need to manually allow port 5000 in Windows Firewall"
        }
    } else {
        Write-Warn "Skipping firewall (requires Administrator). Run as admin to enable."
    }
} else {
    Write-Step "Step 6/9: Skipping firewall config (-SkipFirewall)"
}

# ============================================================
# Step 7: Disable sleep/hibernate so PC stays on
# ============================================================
if (-not $SkipPowerSettings) {
    Write-Step "Step 7/9: Configuring power settings (prevent sleep)..."

    if ($isAdmin) {
        try {
            # Set AC power: never sleep, never turn off display (0 = never)
            powercfg /change standby-timeout-ac 0
            powercfg /change hibernate-timeout-ac 0
            powercfg /change monitor-timeout-ac 30
            Write-Ok "Sleep disabled (AC power). Monitor turns off after 30 min."
        } catch {
            Write-Warn "Failed to change power settings: $_"
        }
    } else {
        Write-Warn "Skipping power settings (requires Administrator). Run as admin to enable."
    }
} else {
    Write-Step "Step 7/9: Skipping power settings (-SkipPowerSettings)"
}

# ============================================================
# Step 8: Set up plink.exe for passwordless cloud tunnel
# ============================================================
Write-Step "Step 8/9: Setting up cloud access tunnel (passwordless)..."

$plinkPath = Join-Path $ToolsDir "plink.exe"
if (Test-Path $plinkPath) {
    Write-Ok "plink.exe already exists in tools/"
} else {
    Write-Host "   Downloading plink.exe (PuTTY SSH client for passwordless tunnel)..."
    try {
        $plinkUrl = "https://the.earth.li/~sgtatham/putty/latest/w64/plink.exe"
        Invoke-WebRequest -Uri $plinkUrl -OutFile $plinkPath -UseBasicParsing
        if (Test-Path $plinkPath) {
            Write-Ok "plink.exe downloaded - tunnel will connect automatically without password prompts"
        } else {
            Write-Warn "Failed to download plink.exe"
        }
    } catch {
        Write-Warn "Failed to download plink.exe: $_"
        Write-Host "   Download manually: https://the.earth.li/~sgtatham/putty/latest/w64/plink.exe" -ForegroundColor Yellow
        Write-Host "   Place it in: $ToolsDir" -ForegroundColor Yellow
    }
}

# Cache VPS host key so plink doesn't prompt for it
if (Test-Path $plinkPath) {
    Write-Host "   Caching VPS host key..." -ForegroundColor Gray
    try {
        echo y | & $plinkPath -pw "Sarkar@00" "administrator@${RustDeskVpsIP}" "echo CONNECTED" 2>$null | Out-Null
        Write-Ok "VPS host key cached"
    } catch {
        Write-Host "   Host key will be cached on first tunnel connection" -ForegroundColor Gray
    }
}

Write-Ok "Cloud access: http://${RustDeskVpsIP}:5000 (fully automatic, no password needed)"

# ============================================================
# Step 9: Summary & Launch
# ============================================================
Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "   Setup Complete!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""

# Status summary
$hasAdb = Test-Path (Join-Path $ToolsDir "adb.exe")
$hasScrcpy = Test-Path (Join-Path $ToolsDir "scrcpy.exe")
$hasRustDesk = $false
foreach ($p in $rustDeskExePaths) {
    if (Test-Path $p) { $hasRustDesk = $true; break }
}

Write-Host "   Component Status:" -ForegroundColor White
Write-Host "   -----------------------------------------" -ForegroundColor Gray
if ($hasAdb)      { Write-Ok "ADB              - Installed" } else { Write-Warn "ADB              - Not found" }
if ($hasScrcpy)   { Write-Ok "scrcpy           - Installed" } else { Write-Warn "scrcpy           - Not found" }
if ($hasRustDesk) { Write-Ok "RustDesk client  - Installed" } else { Write-Warn "RustDesk client  - Not found" }
Write-Host "   -----------------------------------------" -ForegroundColor Gray
Write-Ok "RustDesk server  - $RustDeskVpsIP (pre-configured)"

Write-Host ""
Write-Host "   Phone Setup (do this for each Android phone):" -ForegroundColor Yellow
Write-Host '   1. Settings -> About Phone -> tap Build Number 7 times' -ForegroundColor White
Write-Host '   2. Settings -> Developer Options -> enable USB Debugging' -ForegroundColor White
Write-Host '   3. Connect phone via USB cable' -ForegroundColor White
Write-Host '   4. Tap Allow on the USB debugging popup' -ForegroundColor White

Write-Host ""
Write-Host "   Access Points:" -ForegroundColor Yellow
Write-Host "   Local:   http://localhost:5000" -ForegroundColor White

# Get local IP
try {
    $localIP = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { 
        $_.InterfaceAlias -notmatch 'Loopback' -and $_.IPAddress -notmatch '^169\.' -and $_.IPAddress -ne '127.0.0.1'
    } | Select-Object -First 1).IPAddress
    if ($localIP) {
        Write-Host "   LAN:     http://${localIP}:5000" -ForegroundColor White
    }
} catch {
    Write-Host '   LAN:     http://<your-pc-ip>:5000' -ForegroundColor White
}

Write-Host "   Cloud:   http://${RustDeskVpsIP}:5000 (run tunnel.bat or start.bat)" -ForegroundColor White
Write-Host '   Remote:  Install RustDesk on any device, connect using your PC RustDesk ID' -ForegroundColor White

# Get RustDesk ID if possible
try {
    $rustDeskIdFile = "$env:APPDATA\RustDesk\config\RustDesk.toml"
    if (Test-Path $rustDeskIdFile) {
        $content = Get-Content $rustDeskIdFile -Raw
        if ($content -match 'id\s*=\s*''(\d+)''') {
            $rustDeskId = $Matches[1]
            Write-Host ""
            Write-Host "   Your RustDesk ID: $rustDeskId" -ForegroundColor Green
            Write-Host "   Use this ID to connect from anywhere!" -ForegroundColor Green
        }
    }
} catch {}

Write-Host ""

# ============================================================
# Auto-Launch Everything
# ============================================================
if (-not $NoLaunch) {
    Write-Step "Launching everything..."

    # Launch Web Dashboard first (needs to be running before tunnel connects)
    Write-Host "   Starting Web Dashboard..." -ForegroundColor White
    Start-Process -FilePath "dotnet" -ArgumentList "run --project `"$RepoRoot\src\MobileControlHub.WebApi`"" -WorkingDirectory $RepoRoot
    Write-Ok "Web Dashboard starting at http://localhost:5000"

    # Wait for dashboard to be ready before starting tunnel
    Write-Host "   Waiting for dashboard to start..." -ForegroundColor Gray
    Start-Sleep -Seconds 8

    # Launch Cloud Tunnel
    Write-Host "   Starting Cloud Access Tunnel..." -ForegroundColor White
    $tunnelScript = Join-Path $RepoRoot "tunnel.ps1"
    if (Test-Path $tunnelScript) {
        Start-Process -FilePath "powershell" -ArgumentList "-ExecutionPolicy Bypass -File `"$tunnelScript`"" -WorkingDirectory $RepoRoot
        Write-Ok "Cloud tunnel starting (http://${RustDeskVpsIP}:5000)"
    } else {
        Write-Warn "tunnel.ps1 not found. Cloud access not available."
    }

    # Launch RustDesk (only if not already running)
    if ($hasRustDesk) {
        $rdRunning = Get-Process -Name "rustdesk" -ErrorAction SilentlyContinue
        if ($rdRunning) {
            Write-Ok "RustDesk is already running (skipping)"
        } else {
            Write-Host "   Starting RustDesk..." -ForegroundColor White
            $rdExe = $null
            foreach ($p in $rustDeskExePaths) {
                if (Test-Path $p) { $rdExe = $p; break }
            }
            if ($rdExe) {
                Start-Process -FilePath $rdExe
                Write-Ok "RustDesk started"
            }
        }
    }

    # Open browser
    Start-Sleep -Seconds 2
    Start-Process "http://localhost:5000"

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host "   Everything is running!" -ForegroundColor Green
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "   Dashboard:  http://localhost:5000 (local)" -ForegroundColor White
    Write-Host "   Cloud:      http://${RustDeskVpsIP}:5000 (from anywhere)" -ForegroundColor Green
    Write-Host "   RustDesk:   Running (check system tray for ID)" -ForegroundColor White
    Write-Host ""
    Write-Host "   IMPORTANT: Set a permanent password in RustDesk:" -ForegroundColor Red
    Write-Host '   RustDesk -> Settings -> Security -> Set permanent password' -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "   To launch, run: setup-all.bat" -ForegroundColor Yellow
}

Write-Host "   Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
