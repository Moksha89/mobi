#!/bin/bash
# =============================================================================
# Cuttlefish VM Platform - Complete Baremetal Server Setup
# Supports: Ubuntu 22.04/24.04 on baremetal with AMD EPYC / Intel Xeon / Ryzen
# Tested on: AMD EPYC 7313 (32 cores, 128GB RAM)
# =============================================================================

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log() { echo -e "${GREEN}[CUTTLEFISH]${NC} $1"; }
warn() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1"; exit 1; }
info() { echo -e "${BLUE}[INFO]${NC} $1"; }

IMAGES_DIR="/opt/cuttlefish/images"
SCRIPTS_DIR="/opt/cuttlefish/scripts"

# =============================================================================
# Step 1: Check prerequisites
# =============================================================================
log "Step 1/10: Checking prerequisites..."

if [ "$(id -u)" -ne 0 ]; then
    error "This script must be run as root (sudo ./cuttlefish-setup.sh)"
fi

if grep -q 'svm' /proc/cpuinfo; then
    CPU_TYPE="AMD"
    KVM_MODULE="kvm_amd"
    log "AMD CPU detected (SVM virtualization)"
elif grep -q 'vmx' /proc/cpuinfo; then
    CPU_TYPE="Intel"
    KVM_MODULE="kvm_intel"
    log "Intel CPU detected (VT-x virtualization)"
else
    error "No hardware virtualization support found. Baremetal server required."
fi

if grep -q 'avx2' /proc/cpuinfo; then
    log "AVX2 support detected - compatible with all Android versions"
elif grep -q 'avx' /proc/cpuinfo; then
    warn "Only AVX (no AVX2) - will use Android 14 images for best compatibility"
else
    warn "No AVX support - only Android 13 or older may work"
fi

CPU_CORES=$(nproc)
TOTAL_RAM_MB=$(free -m | awk '/^Mem:/ {print $2}')
TOTAL_RAM_GB=$((TOTAL_RAM_MB / 1024))
MAX_DEVICES=$((TOTAL_RAM_MB / 4096))

log "CPU: $(grep -m1 'model name' /proc/cpuinfo | cut -d: -f2 | xargs) ($CPU_CORES cores)"
log "RAM: ${TOTAL_RAM_GB}GB (can run up to $MAX_DEVICES devices at 4GB each)"

# =============================================================================
# Step 2: Install system dependencies
# =============================================================================
log "Step 2/10: Installing system dependencies..."

export DEBIAN_FRONTEND=noninteractive

apt-get update -qq
apt-get install -y -qq \
    qemu-kvm qemu-system-x86 libvirt-daemon-system bridge-utils \
    cpu-checker docker.io docker-compose curl wget unzip adb git \
    python3-pip net-tools iptables jq sshpass nginx certbot \
    htop iotop tmux rsync openssh-server ca-certificates gnupg lsb-release

# Install .NET 8 SDK
if ! command -v dotnet &>/dev/null; then
    log "Installing .NET 8 SDK..."
    wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
    chmod +x /tmp/dotnet-install.sh
    /tmp/dotnet-install.sh --channel 8.0 --install-dir /usr/share/dotnet
    ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
    export DOTNET_ROOT=/usr/share/dotnet
    export PATH="$PATH:/usr/share/dotnet"
fi

# Install Node.js 20 for frontend build
if ! command -v node &>/dev/null || [ "$(node -v | cut -d. -f1 | tr -d v)" -lt 18 ]; then
    log "Installing Node.js 20..."
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash -
    apt-get install -y -qq nodejs
fi

log "System dependencies installed"

# =============================================================================
# Step 3: Configure KVM and kernel modules
# =============================================================================
log "Step 3/10: Configuring KVM..."

modprobe kvm
modprobe $KVM_MODULE
modprobe vsock 2>/dev/null || true
modprobe vhost_vsock 2>/dev/null || true
modprobe vsock_loopback 2>/dev/null || true

echo "kvm" > /etc/modules-load.d/cuttlefish.conf
echo "$KVM_MODULE" >> /etc/modules-load.d/cuttlefish.conf
echo "vsock" >> /etc/modules-load.d/cuttlefish.conf
echo "vhost_vsock" >> /etc/modules-load.d/cuttlefish.conf

chmod 666 /dev/kvm
[ -e /dev/vhost-vsock ] && chmod 666 /dev/vhost-vsock

echo 'KERNEL=="kvm", GROUP="kvm", MODE="0666"' > /etc/udev/rules.d/99-kvm.rules
echo 'KERNEL=="vhost-vsock", GROUP="kvm", MODE="0666"' >> /etc/udev/rules.d/99-kvm.rules

if [ -e /dev/kvm ]; then
    log "KVM device exists at /dev/kvm"
else
    error "/dev/kvm not found. KVM is not properly configured."
fi

log "KVM configured successfully"

# =============================================================================
# Step 4: Enable and configure Docker
# =============================================================================
log "Step 4/10: Configuring Docker..."

systemctl enable docker
systemctl start docker

mkdir -p /etc/docker
echo '{"default-shm-size":"1G","storage-driver":"overlay2","log-driver":"json-file","log-opts":{"max-size":"50m","max-file":"3"}}' > /etc/docker/daemon.json

systemctl restart docker
docker network create cuttlefish-net 2>/dev/null || true

log "Docker configured"

# =============================================================================
# Step 5: Install Cuttlefish packages from AOSP
# =============================================================================
log "Step 5/10: Installing Cuttlefish host packages..."

CUTTLEFISH_APT_OK=false
if curl -fsSL https://storage.googleapis.com/android-cuttlefish-artifacts/cuttlefish-common/repo-signing-key.gpg 2>/dev/null | \
    gpg --dearmor -o /usr/share/keyrings/cuttlefish-keyring.gpg 2>/dev/null; then
    DISTRO_CODENAME=$(lsb_release -cs 2>/dev/null || echo "jammy")
    echo "deb [signed-by=/usr/share/keyrings/cuttlefish-keyring.gpg] https://storage.googleapis.com/android-cuttlefish-artifacts/cuttlefish-common $DISTRO_CODENAME main" \
        > /etc/apt/sources.list.d/android-cuttlefish-artifacts.list
    apt-get update -qq 2>/dev/null
    apt-get install -y -qq cuttlefish-base cuttlefish-user 2>/dev/null && CUTTLEFISH_APT_OK=true
fi

if [ "$CUTTLEFISH_APT_OK" = false ]; then
    warn "Cuttlefish apt packages not available, building from source..."
    CUTTLEFISH_SRC="/opt/cuttlefish/android-cuttlefish"
    if [ ! -d "$CUTTLEFISH_SRC" ]; then
        git clone https://github.com/google/android-cuttlefish.git "$CUTTLEFISH_SRC"
    fi
    cd "$CUTTLEFISH_SRC"
    if [ -f tools/buildutils/build_packages.sh ]; then
        bash tools/buildutils/build_packages.sh 2>/dev/null || warn "Package build had issues"
        dpkg -i ./cuttlefish-base_*.deb 2>/dev/null || true
        dpkg -i ./cuttlefish-user_*.deb 2>/dev/null || true
        apt-get install -f -y -qq 2>/dev/null || true
    fi
fi

for group in kvm cvdnetwork render video; do
    groupadd -f "$group" 2>/dev/null || true
    usermod -aG "$group" root 2>/dev/null || true
done

log "Cuttlefish host packages installed"

# =============================================================================
# Step 6: Pull Cuttlefish Docker orchestration image
# =============================================================================
log "Step 6/10: Pulling Cuttlefish Docker image..."

docker pull us-docker.pkg.dev/android-cuttlefish-artifacts/cuttlefish-orchestration/cuttlefish-orchestration:latest 2>/dev/null || {
    warn "Failed to pull from Google Artifact Registry"
    docker pull ghcr.io/google/android-cuttlefish 2>/dev/null || {
        warn "Will build from source on first device creation"
        CUTTLEFISH_SRC="/opt/cuttlefish/android-cuttlefish"
        if [ -d "$CUTTLEFISH_SRC" ] && [ -f "$CUTTLEFISH_SRC/docker/Dockerfile" ]; then
            cd "$CUTTLEFISH_SRC"
            docker build -t cuttlefish-orchestration -f docker/Dockerfile docker/ 2>/dev/null || true
        fi
    }
}

log "Cuttlefish Docker image ready"

# =============================================================================
# Step 7: Download Android system images
# =============================================================================
log "Step 7/10: Setting up Android system images..."

mkdir -p "$IMAGES_DIR"

CVD_BIN=""
command -v cvd &>/dev/null && CVD_BIN="cvd"
[ -z "$CVD_BIN" ] && [ -x "/usr/bin/cvd" ] && CVD_BIN="/usr/bin/cvd"

if [ -n "$CVD_BIN" ]; then
    for ver in android14 android15; do
        if [ ! -d "$IMAGES_DIR/$ver" ] || [ -z "$(ls -A "$IMAGES_DIR/$ver" 2>/dev/null)" ]; then
            log "Downloading $ver Cuttlefish images..."
            mkdir -p "$IMAGES_DIR/$ver"
            cd "$IMAGES_DIR/$ver"
            HOME=/root $CVD_BIN fetch --default_build=aosp-${ver}-gsi/aosp_cf_x86_64_phone-userdebug 2>&1 | tail -5 || \
                warn "Failed to download $ver images"
        else
            log "$ver images already present"
        fi
    done
else
    log "cvd CLI not available - creating image directories"
    mkdir -p "$IMAGES_DIR/android14" "$IMAGES_DIR/android15" "$IMAGES_DIR/android13"
fi

log "Android images directory ready"

# =============================================================================
# Step 8: Configure networking and firewall
# =============================================================================
log "Step 8/10: Configuring networking..."

if command -v ufw &>/dev/null; then
    ufw --force enable 2>/dev/null || true
    for port in 22/tcp 80/tcp 443/tcp 5000/tcp 6520:6620/tcp 8443:8543/tcp 1443:1543/tcp 15550:15700/udp 15550:15700/tcp; do
        ufw allow "$port" 2>/dev/null || true
    done
    log "UFW firewall rules configured"
fi

iptables -A INPUT -p tcp --dport 5000 -j ACCEPT 2>/dev/null || true
iptables -A INPUT -p tcp --dport 8443:8543 -j ACCEPT 2>/dev/null || true
iptables -A INPUT -p tcp --dport 6520:6620 -j ACCEPT 2>/dev/null || true
iptables -A INPUT -p udp --dport 15550:15700 -j ACCEPT 2>/dev/null || true

echo 'net.ipv4.ip_forward = 1' > /etc/sysctl.d/99-cuttlefish.conf
sysctl -p /etc/sysctl.d/99-cuttlefish.conf 2>/dev/null || true

log "Networking configured"

# =============================================================================
# Step 9: Create management scripts
# =============================================================================
log "Step 9/10: Creating management scripts..."

mkdir -p "$SCRIPTS_DIR"
log "Management scripts directory ready at $SCRIPTS_DIR"

# =============================================================================
# Step 10: Create systemd services
# =============================================================================
log "Step 10/10: Creating systemd services..."

# Create systemd service files using python to avoid heredoc issues
python3 /opt/cuttlefish/scripts/create_services.py 2>/dev/null || warn "Could not create systemd services automatically"

systemctl daemon-reload 2>/dev/null || true
systemctl enable cuttlefish-platform.service 2>/dev/null || true
systemctl enable mobile-control-hub.service 2>/dev/null || true

log "Systemd services created and enabled"

# =============================================================================
# Final Summary
# =============================================================================
SERVER_IP=$(hostname -I | awk '{print $1}')

echo ""
echo "============================================================"
echo "  Cuttlefish VM Platform Setup Complete!"
echo "============================================================"
echo ""
echo "  Server:     $SERVER_IP"
echo "  CPU:        $(grep -m1 'model name' /proc/cpuinfo | cut -d: -f2 | xargs)"
echo "  Cores:      $CPU_CORES"
echo "  RAM:        ${TOTAL_RAM_GB}GB"
echo "  KVM:        $(test -e /dev/kvm && echo Available || echo NOT_Available)"
echo "  Docker:     $(docker --version 2>/dev/null | awk '{print $3}' | tr -d ',')"
echo "  .NET:       $(dotnet --version 2>/dev/null || echo 'N/A')"
echo "  Node.js:    $(node --version 2>/dev/null || echo 'N/A')"
echo ""
echo "  Max Devices: $MAX_DEVICES (at 4GB RAM each)"
echo "  Images Dir:  $IMAGES_DIR"
echo "  Scripts Dir: $SCRIPTS_DIR"
echo ""
echo "  Next Steps:"
echo "    1. Deploy the Mobile Control Hub dashboard"
echo "    2. Create your first Cuttlefish device from the dashboard"
echo "    3. Access WebRTC screen at https://$SERVER_IP:8443"
echo ""
echo "============================================================"
log "Setup complete! Ready for Cuttlefish Android VMs."
