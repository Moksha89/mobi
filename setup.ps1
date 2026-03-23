# Mobile Control Hub - Automated Setup Script for Windows
# Run this script in PowerShell as Administrator
# Usage: Right-click setup.ps1 > "Run with PowerShell"
#   or:  powershell -ExecutionPolicy Bypass -File setup.ps1

param(
    [switch]$SkipDotNet,
    [switch]$SkipTools,
    [switch]$SkipBuild,
    [switch]$LaunchAfterSetup
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$AppName = "Mobile Control Hub"
$RepoRoot = $PSScriptRoot
$ToolsDir = Join-Path $RepoRoot "tools"

# Colors for output
function Write-Step($msg) { Write-Host "`n>> $msg" -ForegroundColor Cyan }
function Write-Ok($msg) { Write-Host "   [OK] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "   [!] $msg" -ForegroundColor Yellow }
function Write-Err($msg) { Write-Host "   [X] $msg" -ForegroundColor Red }

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "   $AppName - Setup Script" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "This script will:"
Write-Host "  1. Check/install .NET 8 SDK"
Write-Host "  2. Download ADB (Android Debug Bridge)"
Write-Host "  3. Download scrcpy (screen mirroring)"
Write-Host "  4. Build the application"
Write-Host "  5. Optionally launch the web dashboard"
Write-Host ""

# ============================================================
# Step 1: Check .NET 8 SDK
# ============================================================
if (-not $SkipDotNet) {
    Write-Step "Checking .NET 8 SDK..."

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
                Write-Err ".NET SDK installation may have failed. Please install manually from:"
                Write-Host "         https://dotnet.microsoft.com/download/dotnet/8.0"
                Write-Host ""
                Write-Host "   After installing, re-run this script with: -SkipDotNet"
                exit 1
            }
        } catch {
            Write-Err "Failed to download .NET installer: $_"
            Write-Host "   Please install .NET 8 SDK manually from:"
            Write-Host "   https://dotnet.microsoft.com/download/dotnet/8.0"
            exit 1
        }
    }
} else {
    Write-Step "Skipping .NET SDK check (-SkipDotNet)"
}

# ============================================================
# Step 2: Download ADB Platform Tools
# ============================================================
if (-not $SkipTools) {
    Write-Step "Setting up ADB (Android Debug Bridge)..."

    if (-not (Test-Path $ToolsDir)) {
        New-Item -ItemType Directory -Path $ToolsDir -Force | Out-Null
    }

    $adbPath = Join-Path $ToolsDir "adb.exe"

    if (Test-Path $adbPath) {
        Write-Ok "ADB already exists at $adbPath"
    } else {
        Write-Host "   Downloading ADB Platform Tools..."
        $adbUrl = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip"
        $adbZip = Join-Path $env:TEMP "platform-tools.zip"
        $adbExtractDir = Join-Path $env:TEMP "platform-tools-extract"

        try {
            Invoke-WebRequest -Uri $adbUrl -OutFile $adbZip -UseBasicParsing
            
            if (Test-Path $adbExtractDir) {
                Remove-Item -Recurse -Force $adbExtractDir
            }
            
            Expand-Archive -Path $adbZip -DestinationPath $adbExtractDir -Force
            
            # Copy ADB files to tools directory
            $adbFiles = @("adb.exe", "AdbWinApi.dll", "AdbWinUsbApi.dll")
            foreach ($file in $adbFiles) {
                $src = Join-Path $adbExtractDir "platform-tools\$file"
                if (Test-Path $src) {
                    Copy-Item $src -Destination $ToolsDir -Force
                }
            }

            if (Test-Path $adbPath) {
                Write-Ok "ADB downloaded and installed to $ToolsDir"
            } else {
                Write-Err "ADB download succeeded but files not found"
            }

            # Cleanup
            Remove-Item -Force $adbZip -ErrorAction SilentlyContinue
            Remove-Item -Recurse -Force $adbExtractDir -ErrorAction SilentlyContinue
        } catch {
            Write-Err "Failed to download ADB: $_"
            Write-Host "   Please download manually from:"
            Write-Host "   https://developer.android.com/tools/releases/platform-tools"
            Write-Host "   Extract adb.exe to: $ToolsDir"
        }
    }

    # ============================================================
    # Step 3: Download scrcpy
    # ============================================================
    Write-Step "Setting up scrcpy (screen mirroring)..."

    $scrcpyPath = Join-Path $ToolsDir "scrcpy.exe"

    if (Test-Path $scrcpyPath) {
        Write-Ok "scrcpy already exists at $scrcpyPath"
    } else {
        Write-Host "   Downloading scrcpy..."
        
        # Get latest scrcpy release from GitHub
        $scrcpyVersion = "3.1"
        $scrcpyUrl = "https://github.com/Genymobile/scrcpy/releases/download/v$scrcpyVersion/scrcpy-win64-v$scrcpyVersion.zip"
        $scrcpyZip = Join-Path $env:TEMP "scrcpy.zip"
        $scrcpyExtractDir = Join-Path $env:TEMP "scrcpy-extract"

        try {
            Invoke-WebRequest -Uri $scrcpyUrl -OutFile $scrcpyZip -UseBasicParsing
            
            if (Test-Path $scrcpyExtractDir) {
                Remove-Item -Recurse -Force $scrcpyExtractDir
            }
            
            Expand-Archive -Path $scrcpyZip -DestinationPath $scrcpyExtractDir -Force
            
            # Find the scrcpy directory (it's usually nested)
            $scrcpyInnerDir = Get-ChildItem -Path $scrcpyExtractDir -Directory | Select-Object -First 1
            if ($scrcpyInnerDir) {
                # Copy all scrcpy files to tools directory
                Get-ChildItem -Path $scrcpyInnerDir.FullName -File | ForEach-Object {
                    Copy-Item $_.FullName -Destination $ToolsDir -Force
                }
            }

            if (Test-Path $scrcpyPath) {
                Write-Ok "scrcpy downloaded and installed to $ToolsDir"
            } else {
                Write-Err "scrcpy download may have failed. Trying alternative approach..."
                # Try copying from root of extract
                Get-ChildItem -Path $scrcpyExtractDir -Recurse -File -Filter "scrcpy*" | ForEach-Object {
                    Copy-Item $_.FullName -Destination $ToolsDir -Force
                }
                if (Test-Path $scrcpyPath) {
                    Write-Ok "scrcpy installed to $ToolsDir"
                } else {
                    Write-Warn "Could not auto-install scrcpy."
                    Write-Host "   Please download manually from:"
                    Write-Host "   https://github.com/Genymobile/scrcpy/releases"
                    Write-Host "   Extract all files to: $ToolsDir"
                }
            }

            # Cleanup
            Remove-Item -Force $scrcpyZip -ErrorAction SilentlyContinue
            Remove-Item -Recurse -Force $scrcpyExtractDir -ErrorAction SilentlyContinue
        } catch {
            Write-Warn "Failed to download scrcpy: $_"
            Write-Host "   Please download manually from:"
            Write-Host "   https://github.com/Genymobile/scrcpy/releases"
            Write-Host "   Extract all files to: $ToolsDir"
        }
    }
} else {
    Write-Step "Skipping tool downloads (-SkipTools)"
}

# ============================================================
# Step 4: Build the application
# ============================================================
if (-not $SkipBuild) {
    Write-Step "Building Mobile Control Hub..."

    try {
        Write-Host "   Restoring NuGet packages..."
        & dotnet restore "$RepoRoot\MobileControlHub.sln" 2>&1 | Out-Null
        Write-Ok "Packages restored"

        Write-Host "   Building solution (Release mode)..."
        $buildOutput = & dotnet build "$RepoRoot\MobileControlHub.sln" --configuration Release 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Ok "Build succeeded"
        } else {
            # Try building just the WebApi project (WPF may fail without Windows Desktop SDK)
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
    Write-Step "Skipping build (-SkipBuild)"
}

# ============================================================
# Step 5: Summary & Launch
# ============================================================
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "   Setup Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""

# Check what's available
$hasAdb = Test-Path (Join-Path $ToolsDir "adb.exe")
$hasScrcpy = Test-Path (Join-Path $ToolsDir "scrcpy.exe")

Write-Host "   Tools Status:" -ForegroundColor White
if ($hasAdb) { Write-Ok "ADB installed" } else { Write-Warn "ADB not found - download from https://developer.android.com/tools/releases/platform-tools" }
if ($hasScrcpy) { Write-Ok "scrcpy installed" } else { Write-Warn "scrcpy not found - download from https://github.com/Genymobile/scrcpy/releases" }

Write-Host ""
Write-Host "   Next Steps:" -ForegroundColor White
Write-Host "   1. Connect Android phone(s) via USB" -ForegroundColor White
Write-Host "   2. Enable USB Debugging on each phone:" -ForegroundColor White
Write-Host "      Settings > About Phone > tap Build Number 7 times" -ForegroundColor Gray
Write-Host "      Settings > Developer Options > enable USB Debugging" -ForegroundColor Gray
Write-Host "   3. Tap 'Allow' on the USB debugging prompt on each phone" -ForegroundColor White
Write-Host ""
Write-Host "   To start the Web Dashboard:" -ForegroundColor Yellow
Write-Host "   cd $RepoRoot" -ForegroundColor White
Write-Host "   dotnet run --project src\MobileControlHub.WebApi" -ForegroundColor White
Write-Host "   Then open: http://localhost:5000" -ForegroundColor White
Write-Host ""
Write-Host "   To start the Desktop App (requires Visual Studio 2022):" -ForegroundColor Yellow
Write-Host "   Open MobileControlHub.sln in Visual Studio" -ForegroundColor White
Write-Host "   Set MobileControlHub.UI as startup project > F5" -ForegroundColor White
Write-Host ""
Write-Host "   Access from other devices on your network:" -ForegroundColor Yellow

# Try to get local IP
try {
    $localIP = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.InterfaceAlias -notmatch 'Loopback' -and $_.IPAddress -notmatch '^169\.' } | Select-Object -First 1).IPAddress
    if ($localIP) {
        Write-Host "   http://${localIP}:5000" -ForegroundColor White
    }
} catch {
    Write-Host "   http://<your-pc-ip>:5000" -ForegroundColor White
}

Write-Host ""

# Optionally launch
if ($LaunchAfterSetup) {
    Write-Step "Launching Web Dashboard..."
    Start-Process -FilePath "dotnet" -ArgumentList "run --project `"$RepoRoot\src\MobileControlHub.WebApi`"" -WorkingDirectory $RepoRoot
    Start-Sleep -Seconds 3
    Start-Process "http://localhost:5000"
    Write-Ok "Web Dashboard launched at http://localhost:5000"
} else {
    $response = Read-Host "   Launch the Web Dashboard now? (y/n)"
    if ($response -eq 'y' -or $response -eq 'Y') {
        Write-Step "Launching Web Dashboard..."
        Start-Process -FilePath "dotnet" -ArgumentList "run --project `"$RepoRoot\src\MobileControlHub.WebApi`"" -WorkingDirectory $RepoRoot
        Start-Sleep -Seconds 5
        Start-Process "http://localhost:5000"
        Write-Ok "Web Dashboard launched at http://localhost:5000"
    }
}

Write-Host ""
