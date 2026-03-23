#!/bin/bash
# =============================================================================
# Cuttlefish Deploy Script - Deploy Mobile Control Hub + Cuttlefish Platform
# Usage: ./cuttlefish-deploy.sh [SERVER_IP] [ROOT_PASSWORD]
# Or set env vars: CUTTLEFISH_SERVER_IP and CUTTLEFISH_SERVER_PASS
# =============================================================================

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log() { echo -e "${GREEN}[DEPLOY]${NC} $1"; }
warn() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1"; exit 1; }

SERVER_IP="${1:-$CUTTLEFISH_SERVER_IP}"
SERVER_PASS="${2:-$CUTTLEFISH_SERVER_PASS}"
SERVER_USER="${CUTTLEFISH_SERVER_USER:-root}"

if [ -z "$SERVER_IP" ]; then
    echo "Usage: $0 <server_ip> [root_password]"
    echo ""
    echo "Or set environment variables:"
    echo "  export CUTTLEFISH_SERVER_IP=x.x.x.x"
    echo "  export CUTTLEFISH_SERVER_PASS=your_password"
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_DIR="$(dirname "$SCRIPT_DIR")"
SSH_OPTS="-o StrictHostKeyChecking=no -o ConnectTimeout=15 -o ServerAliveInterval=30"

# SSH helper function
run_remote() {
    if [ -n "$SERVER_PASS" ]; then
        sshpass -p "$SERVER_PASS" ssh $SSH_OPTS ${SERVER_USER}@${SERVER_IP} "$1"
    else
        ssh $SSH_OPTS ${SERVER_USER}@${SERVER_IP} "$1"
    fi
}

# SCP helper function
copy_to_remote() {
    if [ -n "$SERVER_PASS" ]; then
        sshpass -p "$SERVER_PASS" scp $SSH_OPTS -r "$1" ${SERVER_USER}@${SERVER_IP}:"$2"
    else
        scp $SSH_OPTS -r "$1" ${SERVER_USER}@${SERVER_IP}:"$2"
    fi
}

# =============================================================================
# Step 1: Test SSH connection
# =============================================================================
log "Step 1/6: Testing SSH connection to $SERVER_IP..."

if ! run_remote "echo connected" &>/dev/null; then
    error "Cannot connect to $SERVER_IP via SSH. Check IP and credentials."
fi

log "SSH connection successful"

# =============================================================================
# Step 2: Copy and run setup script
# =============================================================================
log "Step 2/6: Running Cuttlefish setup on server..."

copy_to_remote "$SCRIPT_DIR/cuttlefish-setup.sh" "/tmp/cuttlefish-setup.sh"
run_remote "chmod +x /tmp/cuttlefish-setup.sh && bash /tmp/cuttlefish-setup.sh"

log "Server setup complete"

# =============================================================================
# Step 3: Build and deploy the dashboard
# =============================================================================
log "Step 3/6: Building Mobile Control Hub dashboard..."

# Build frontend
cd "$REPO_DIR/src/MobileControlHub.WebApi/ClientApp"
if [ -f package.json ]; then
    npm install --silent 2>/dev/null
    npm run build --silent 2>/dev/null
    log "Frontend built"
fi

# Build backend
cd "$REPO_DIR"
dotnet publish src/MobileControlHub.WebApi/MobileControlHub.WebApi.csproj \
    -c Release -o /tmp/mch-publish --no-restore 2>/dev/null || \
dotnet publish src/MobileControlHub.WebApi/MobileControlHub.WebApi.csproj \
    -c Release -o /tmp/mch-publish 2>/dev/null

log "Backend built"

# =============================================================================
# Step 4: Deploy to server
# =============================================================================
log "Step 4/6: Deploying to server..."

run_remote "mkdir -p /opt/mobile-control-hub"
copy_to_remote "/tmp/mch-publish/" "/opt/mobile-control-hub/"

# Copy frontend build if it exists
WWWROOT="$REPO_DIR/src/MobileControlHub.WebApi/ClientApp/dist"
[ ! -d "$WWWROOT" ] && WWWROOT="$REPO_DIR/src/MobileControlHub.WebApi/ClientApp/build"
if [ -d "$WWWROOT" ]; then
    run_remote "mkdir -p /opt/mobile-control-hub/wwwroot"
    copy_to_remote "$WWWROOT/" "/opt/mobile-control-hub/wwwroot/"
fi

log "Dashboard deployed"

# =============================================================================
# Step 5: Configure and start services
# =============================================================================
log "Step 5/6: Starting services..."

# Set the cuttlefish host to localhost since dashboard runs on the same server
run_remote "export MCH_CUTTLEFISH_HOST=localhost && systemctl restart mobile-control-hub.service 2>/dev/null || true"

# Start the dashboard directly if systemd fails
run_remote "systemctl is-active mobile-control-hub.service &>/dev/null || (cd /opt/mobile-control-hub && MCH_CUTTLEFISH_HOST=localhost ASPNETCORE_URLS=http://0.0.0.0:5000 nohup dotnet MobileControlHub.WebApi.dll > /var/log/mch.log 2>&1 &)"

log "Services started"

# =============================================================================
# Step 6: Verify deployment
# =============================================================================
log "Step 6/6: Verifying deployment..."

sleep 5

# Check if dashboard is responding
if run_remote "curl -sf http://localhost:5000/api/cuttlefish/status" &>/dev/null; then
    log "Dashboard API is responding"
else
    warn "Dashboard API not responding yet (may still be starting)"
fi

# Check Docker
run_remote "docker ps --format \"{{.Names}} - {{.Status}}\" 2>/dev/null"

# Check KVM
run_remote "test -e /dev/kvm && echo KVM: Available || echo KVM: NOT Available"

echo ""
echo "============================================================"
echo "  Deployment Complete!"
echo "============================================================"
echo ""
echo "  Dashboard:  http://$SERVER_IP:5000"
echo "  WebRTC:     https://$SERVER_IP:8443 (after creating a device)"
echo ""
echo "  Create your first Cuttlefish device:"
echo "    Go to http://$SERVER_IP:5000/devices"
echo "    Click + New Device > Cuttlefish tab"
echo "    Select profile and Android version"
echo "    Click Create"
echo ""
echo "============================================================"
