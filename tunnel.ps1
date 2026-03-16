# Mobile Control Hub - Cloud Tunnel Script
# Creates an SSH reverse tunnel so the dashboard is accessible at:
#   http://69.197.142.77:5000
#
# This script uses plink.exe (PuTTY) for passwordless automated SSH tunneling.
# The VPS password is embedded so no manual entry is ever needed.

param(
    [string]$VpsHost = "69.197.142.77",
    [string]$VpsUser = "administrator",
    [string]$VpsPass = "Sarkar@00",
    [int]$RemotePort = 5000,
    [int]$LocalPort = 5000
)

$ErrorActionPreference = "Continue"

$AppName = "Mobile Control Hub"
$RepoRoot = $PSScriptRoot
$ToolsDir = Join-Path $RepoRoot "tools"

function Write-Step($msg) { Write-Host "`n>> $msg" -ForegroundColor Cyan }
function Write-Ok($msg) { Write-Host "   [OK] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "   [!] $msg" -ForegroundColor Yellow }
function Write-Err($msg) { Write-Host "   [X] $msg" -ForegroundColor Red }

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "   $AppName - Cloud Access Tunnel" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "   Dashboard will be accessible from anywhere at:" -ForegroundColor White
Write-Host "   http://${VpsHost}:${RemotePort}" -ForegroundColor Green
Write-Host "   Physical devices will be bridged via ADB (port 15037)" -ForegroundColor White
Write-Host ""

# ============================================================
# Step 1: Ensure plink.exe is available
# ============================================================
Write-Step "Step 1: Checking SSH tools..."

$plinkPath = $null

# Check tools dir first
$plinkInTools = Join-Path $ToolsDir "plink.exe"
if (Test-Path $plinkInTools) {
    $plinkPath = $plinkInTools
}

# Check PATH
if (-not $plinkPath) {
    $plinkInPath = Get-Command plink.exe -ErrorAction SilentlyContinue
    if ($plinkInPath) { $plinkPath = $plinkInPath.Source }
}

# Download plink if not found
if (-not $plinkPath) {
    Write-Host "   Downloading plink.exe (PuTTY SSH client)..."
    if (-not (Test-Path $ToolsDir)) {
        New-Item -ItemType Directory -Path $ToolsDir -Force | Out-Null
    }
    try {
        $plinkUrl = "https://the.earth.li/~sgtatham/putty/latest/w64/plink.exe"
        Invoke-WebRequest -Uri $plinkUrl -OutFile $plinkInTools -UseBasicParsing
        if (Test-Path $plinkInTools) {
            $plinkPath = $plinkInTools
            Write-Ok "plink.exe downloaded to tools/"
        }
    } catch {
        Write-Warn "Failed to download plink.exe: $_"
    }
}

if ($plinkPath) {
    Write-Ok "Using plink.exe for automated tunnel (no password prompts)"
} else {
    Write-Err "Could not find or download plink.exe"
    Write-Host "   Download manually from: https://the.earth.li/~sgtatham/putty/latest/w64/plink.exe" -ForegroundColor Yellow
    Write-Host "   Place it in: $ToolsDir" -ForegroundColor Yellow
    exit 1
}

# ============================================================
# Step 2: Cache host key (accept it automatically)
# ============================================================
Write-Step "Step 2: Caching VPS host key..."

# Use echo y to auto-accept the host key on first connection
try {
    $testArgs = @("-batch", "-pw", $VpsPass, "${VpsUser}@${VpsHost}", "echo CONNECTED")
    echo y | & $plinkPath $testArgs 2>$null | Out-Null
    # Run again in batch mode to verify
    $testProc = Start-Process -FilePath $plinkPath -ArgumentList $testArgs -NoNewWindow -Wait -PassThru -RedirectStandardOutput "$env:TEMP\mch_test.txt" -RedirectStandardError "$env:TEMP\mch_test_err.txt"
    $testOut = Get-Content "$env:TEMP\mch_test.txt" -ErrorAction SilentlyContinue
    Remove-Item "$env:TEMP\mch_test.txt", "$env:TEMP\mch_test_err.txt" -ErrorAction SilentlyContinue
    if ($testOut -match "CONNECTED") {
        Write-Ok "VPS connection verified"
    } else {
        # Host key might not be cached yet, try with auto-accept
        Write-Host "   Accepting VPS host key..." -ForegroundColor Gray
        echo y | & $plinkPath -pw $VpsPass "${VpsUser}@${VpsHost}" "echo CONNECTED" 2>$null
        Write-Ok "Host key accepted"
    }
} catch {
    Write-Warn "Could not verify VPS connection: $_"
    Write-Host "   Will try to connect anyway..." -ForegroundColor Gray
}

# ============================================================
# Step 3: Pre-flight check
# ============================================================
Write-Step "Step 3: Pre-flight checks..."

Write-Host "   Checking if dashboard is running on localhost:${LocalPort}..." -ForegroundColor Gray
try {
    $webReq = [System.Net.WebRequest]::Create("http://127.0.0.1:${LocalPort}/")
    $webReq.Timeout = 3000
    $resp = $webReq.GetResponse()
    $resp.Close()
    Write-Ok "Dashboard is running on port ${LocalPort}"
} catch {
    Write-Warn "Dashboard may not be running on port ${LocalPort} yet."
    Write-Host "   Continuing anyway (tunnel will work once dashboard starts)..." -ForegroundColor Gray
}

# ============================================================
# Step 4: Clear stale tunnel and connect with auto-reconnect
# ============================================================
Write-Step "Step 4: Starting SSH reverse tunnel..."
Write-Host "   Local:  http://localhost:${LocalPort}" -ForegroundColor White
Write-Host "   Cloud:  http://${VpsHost}:${RemotePort}" -ForegroundColor Green
Write-Host ""
Write-Host "   The tunnel will auto-reconnect if disconnected." -ForegroundColor Gray
Write-Host "   Press Ctrl+C to stop." -ForegroundColor Gray
Write-Host ""

function Clear-StaleTunnel {
    try {
        $clearArgs = @("-batch", "-pw", $VpsPass, "${VpsUser}@${VpsHost}", "fuser -k ${RemotePort}/tcp 2>/dev/null; echo CLEARED")
        $proc = Start-Process -FilePath $plinkPath -ArgumentList $clearArgs -NoNewWindow -Wait -PassThru -RedirectStandardOutput "$env:TEMP\mch_clear.txt" -RedirectStandardError "$env:TEMP\mch_clear_err.txt"
        $out = Get-Content "$env:TEMP\mch_clear.txt" -ErrorAction SilentlyContinue
        Remove-Item "$env:TEMP\mch_clear.txt", "$env:TEMP\mch_clear_err.txt" -ErrorAction SilentlyContinue
        if ($out -match "CLEARED") {
            Write-Host "   Cleared stale connection on VPS port ${RemotePort}" -ForegroundColor Gray
        }
    } catch {}
}

$retryDelay = 5
$maxRetryDelay = 60

while ($true) {
    $timestamp = Get-Date -Format 'HH:mm:ss'
    Write-Host "   [$timestamp] Connecting tunnel..." -ForegroundColor Gray

    # Clear any stale tunnel before attempting to connect
    Clear-StaleTunnel

    # Establish the tunnel using plink with embedded password
    # Forward dashboard (5000) AND ADB server (5037 -> 15037) for physical device bridging
    $tunnelArgs = @(
        "-batch",
        "-pw", $VpsPass,
        "-N",
        "-R", "0.0.0.0:${RemotePort}:127.0.0.1:${LocalPort}",
        "-R", "127.0.0.1:15037:127.0.0.1:5037",
        "${VpsUser}@${VpsHost}"
    )

    $timestamp = Get-Date -Format 'HH:mm:ss'
    Write-Host "   [$timestamp] Tunnel ACTIVE! http://${VpsHost}:${RemotePort}" -ForegroundColor Green
    $process = Start-Process -FilePath $plinkPath -ArgumentList $tunnelArgs -NoNewWindow -Wait -PassThru

    $timestamp = Get-Date -Format 'HH:mm:ss'
    if ($process.ExitCode -eq 0) {
        Write-Host "   [$timestamp] Tunnel disconnected cleanly." -ForegroundColor Yellow
        $retryDelay = 5
    } else {
        Write-Warn "[$timestamp] Tunnel exited with code $($process.ExitCode). Will retry..."
    }

    Write-Host "   [$timestamp] Reconnecting in $retryDelay seconds..." -ForegroundColor Gray
    Start-Sleep -Seconds $retryDelay
    $retryDelay = [Math]::Min($retryDelay * 2, $maxRetryDelay)
}
