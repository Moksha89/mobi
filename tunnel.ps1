# Mobile Control Hub - Cloud Tunnel Script
# Creates an SSH reverse tunnel so the dashboard is accessible at:
#   http://69.197.142.77:5000
#
# This script:
#   1. Generates an SSH key pair (if not already done)
#   2. Copies the public key to the VPS (one-time, requires password)
#   3. Establishes a persistent SSH reverse tunnel with auto-reconnect
#
# Usage: powershell -ExecutionPolicy Bypass -File tunnel.ps1
#   or double-click tunnel.bat

param(
    [string]$VpsHost = "69.197.142.77",
    [string]$VpsUser = "administrator",
    [int]$RemotePort = 5000,
    [int]$LocalPort = 5000,
    [switch]$SetupKeysOnly
)

$ErrorActionPreference = "Stop"

$AppName = "Mobile Control Hub"
$SshKeyDir = Join-Path $env:USERPROFILE ".ssh"
$SshKeyPath = Join-Path $SshKeyDir "mch_tunnel_key"
$SshKeyPub = "$SshKeyPath.pub"

function Write-Step($msg) { Write-Host "`n>> $msg" -ForegroundColor Cyan }
function Write-Ok($msg) { Write-Host "   [OK] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "   [!] $msg" -ForegroundColor Yellow }
function Write-Err($msg) { Write-Host "   [X] $msg" -ForegroundColor Red }

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "   $AppName - Cloud Access Tunnel" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "   This makes your dashboard accessible from anywhere at:" -ForegroundColor White
Write-Host "   http://${VpsHost}:${RemotePort}" -ForegroundColor Green
Write-Host ""

# ============================================================
# Step 1: Generate SSH Key Pair (if needed)
# ============================================================
Write-Step "Step 1: Checking SSH key pair..."

if (-not (Test-Path $SshKeyDir)) {
    New-Item -ItemType Directory -Path $SshKeyDir -Force | Out-Null
}

if (Test-Path $SshKeyPath) {
    Write-Ok "SSH key already exists: $SshKeyPath"
} else {
    Write-Host "   Generating new SSH key pair..."
    try {
        ssh-keygen -t ed25519 -f $SshKeyPath -N '""' -C "mch-tunnel@$env:COMPUTERNAME" 2>$null
        if (Test-Path $SshKeyPath) {
            Write-Ok "SSH key pair generated"
        } else {
            # Fallback: try with empty passphrase differently
            & ssh-keygen -t ed25519 -f $SshKeyPath -N "" -C "mch-tunnel@$env:COMPUTERNAME"
            if (Test-Path $SshKeyPath) {
                Write-Ok "SSH key pair generated"
            } else {
                Write-Err "Failed to generate SSH key. Please run: ssh-keygen -t ed25519 -f $SshKeyPath"
                exit 1
            }
        }
    } catch {
        Write-Err "ssh-keygen failed: $_"
        Write-Host "   Make sure OpenSSH is installed (Windows 10/11 has it built-in)"
        exit 1
    }
}

# ============================================================
# Step 2: Copy Public Key to VPS (if needed)
# ============================================================
Write-Step "Step 2: Setting up key-based authentication to VPS..."

Write-Host "   Testing if key auth already works..."
$testResult = & ssh -o StrictHostKeyChecking=no -o BatchMode=yes -o ConnectTimeout=5 -i $SshKeyPath "${VpsUser}@${VpsHost}" "echo KEY_AUTH_OK" 2>$null
if ($testResult -eq "KEY_AUTH_OK") {
    Write-Ok "Key-based authentication already configured"
} else {
    Write-Host "   Key auth not set up yet. Copying public key to VPS..."
    Write-Host "   You will be asked for the VPS password (one-time only)." -ForegroundColor Yellow
    Write-Host ""

    $pubKey = Get-Content $SshKeyPub -Raw
    $pubKey = $pubKey.Trim()

    # Use ssh to append the public key to authorized_keys on the VPS
    Write-Host "   Connecting to ${VpsUser}@${VpsHost}..."
    $sshCmd = "mkdir -p ~/.ssh && chmod 700 ~/.ssh && echo '$pubKey' >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys && echo 'KEY_COPIED_OK'"
    
    try {
        $result = & ssh -o StrictHostKeyChecking=no "${VpsUser}@${VpsHost}" $sshCmd
        if ($result -match "KEY_COPIED_OK") {
            Write-Ok "Public key copied to VPS. Password login no longer needed for tunnel."
        } else {
            Write-Warn "Key copy may have failed. You may be prompted for password each time."
        }
    } catch {
        Write-Warn "Could not copy key automatically: $_"
        Write-Host "   You can manually copy your public key:" -ForegroundColor Yellow
        Write-Host "   type $SshKeyPub | ssh ${VpsUser}@${VpsHost} `"cat >> ~/.ssh/authorized_keys`"" -ForegroundColor Gray
    }
}

if ($SetupKeysOnly) {
    Write-Host ""
    Write-Ok "Key setup complete. Run tunnel.ps1 again (without -SetupKeysOnly) to start the tunnel."
    exit 0
}

# ============================================================
# Step 3: Start Persistent SSH Reverse Tunnel
# ============================================================
Write-Step "Step 3: Starting SSH reverse tunnel..."
Write-Host "   Local:  http://localhost:${LocalPort}" -ForegroundColor White
Write-Host "   Cloud:  http://${VpsHost}:${RemotePort}" -ForegroundColor Green
Write-Host ""
Write-Host "   The tunnel will auto-reconnect if disconnected." -ForegroundColor Gray
Write-Host "   Press Ctrl+C to stop." -ForegroundColor Gray
Write-Host ""

$retryDelay = 5
$maxRetryDelay = 60

while ($true) {
    Write-Host "   [$(Get-Date -Format 'HH:mm:ss')] Connecting tunnel..." -ForegroundColor Gray

    # SSH reverse tunnel: binds RemotePort on VPS to LocalPort on this PC
    $sshArgs = @(
        "-o", "StrictHostKeyChecking=no",
        "-o", "ServerAliveInterval=30",
        "-o", "ServerAliveCountMax=3",
        "-o", "ExitOnForwardFailure=yes",
        "-o", "BatchMode=yes",
        "-i", $SshKeyPath,
        "-N",
        "-R", "0.0.0.0:${RemotePort}:localhost:${LocalPort}",
        "${VpsUser}@${VpsHost}"
    )

    $process = Start-Process -FilePath "ssh" -ArgumentList $sshArgs -NoNewWindow -Wait -PassThru

    if ($process.ExitCode -eq 0) {
        Write-Host "   [$(Get-Date -Format 'HH:mm:ss')] Tunnel disconnected cleanly." -ForegroundColor Yellow
        $retryDelay = 5
    } else {
        Write-Warn "Tunnel exited with code $($process.ExitCode)"

        # If BatchMode fails (no key auth), fall back to interactive
        if ($process.ExitCode -eq 255) {
            Write-Host "   Key auth may not be working. Trying with password prompt..." -ForegroundColor Yellow
            $sshArgsFallback = @(
                "-o", "StrictHostKeyChecking=no",
                "-o", "ServerAliveInterval=30",
                "-o", "ServerAliveCountMax=3",
                "-o", "ExitOnForwardFailure=yes",
                "-N",
                "-R", "0.0.0.0:${RemotePort}:localhost:${LocalPort}",
                "${VpsUser}@${VpsHost}"
            )
            $process = Start-Process -FilePath "ssh" -ArgumentList $sshArgsFallback -NoNewWindow -Wait -PassThru
        }
    }

    Write-Host "   [$(Get-Date -Format 'HH:mm:ss')] Reconnecting in $retryDelay seconds..." -ForegroundColor Gray
    Start-Sleep -Seconds $retryDelay
    $retryDelay = [Math]::Min($retryDelay * 2, $maxRetryDelay)
}
