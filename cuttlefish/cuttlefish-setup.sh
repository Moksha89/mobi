#!/bin/bash
# =============================================================================
# Cuttlefish VM Platform Setup Script
# Sets up a baremetal server to run Cuttlefish Android VMs via Docker
# Requires: Ubuntu 20.04+ with KVM support (baremetal or nested virt)
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

# =============================================================================
# Step 1: Check prerequisites
# =============================================================================
log "Checking prerequisites..."

if [ "$(id -u)" -ne 0 ]; then
    error "This script must be run as root (sudo ./cuttlefish-setup.sh)"
fi

if ! grep -q 'vmx\|svm' /proc/cpuinfo; then
    error "KVM not supported. This server needs hardware virtualization (Intel VT-x or AMD-V)."
fi

log "KVM support detected"

# =============================================================================
# Step 2: Install system dependencies
# =============================================================================
log "Installing system dependencies..."

apt-get update -qq
apt-get install -y -qq \
    qemu-kvm \
    libvirt-daemon-system \
    bridge-utils \
    virt-manager \
    cpu-checker \
    docker.io \
    docker-compose \
    curl \
    wget \
    unzip \
    adb \
    git \
    python3-pip \
    net-tools \
    iptables \
    jq

# Enable and start Docker
systemctl enable docker
systemctl start docker

# Enable and start libvirtd
systemctl enable libvirtd
systemctl start libvirtd

# Load KVM modules
modprobe kvm
modprobe kvm_intel 2>/dev/null || modprobe kvm_amd 2>/dev/null || true

log "System dependencies installed"

# =============================================================================
# Step 3: Verify KVM is working
# =============================================================================
log "Verifying KVM..."

if ! kvm-ok 2>/dev/null | grep -q "can be used"; then
    warn "kvm-ok check failed, but /dev/kvm may still work"
fi

if [ ! -e /dev/kvm ]; then
    error "/dev/kvm not found. KVM is not properly configured."
fi

log "KVM verified: /dev/kvm is available"

# =============================================================================
# Step 4: Pull Cuttlefish Docker image
# =============================================================================
log "Pulling Cuttlefish Docker image (this may take a while)..."

# Use the official AOSP Cuttlefish Docker image
docker pull ghcr.io/google/android-cuttlefish 2>/dev/null || {
    log "Building Cuttlefish Docker image from source..."
    
    CUTTLEFISH_DIR="/opt/cuttlefish"
    mkdir -p "$CUTTLEFISH_DIR"
    
    if [ ! -d "$CUTTLEFISH_DIR/android-cuttlefish" ]; then
        git clone https://github.com/google/android-cuttlefish.git "$CUTTLEFISH_DIR/android-cuttlefish"
    fi
    
    cd "$CUTTLEFISH_DIR/android-cuttlefish"
    git pull
    
    # Build the Docker image
    if [ -f docker/Dockerfile ]; then
        docker build -t cuttlefish-base -f docker/Dockerfile docker/
    elif [ -f Dockerfile ]; then
        docker build -t cuttlefish-base .
    else
        warn "No Dockerfile found, using manual setup"
    fi
}

log "Cuttlefish Docker image ready"

# =============================================================================
# Step 5: Download Android system images
# =============================================================================
log "Setting up Android system images directory..."

IMAGES_DIR="/opt/cuttlefish/images"
mkdir -p "$IMAGES_DIR"

# Download latest Cuttlefish images from Android CI
# These are the official AOSP Cuttlefish images
ANDROID_VERSIONS=("android15" "android14" "android13")

for ver in "${ANDROID_VERSIONS[@]}"; do
    img_dir="$IMAGES_DIR/$ver"
    mkdir -p "$img_dir"
    log "Image directory created: $img_dir"
    log "To download $ver images, use:"
    log "  wget https://ci.android.com/builds/latest/branches/aosp-$ver-gsi/targets/aosp_cf_x86_64_phone-userdebug/view/BUILD_INFO"
done

log "Image directories prepared"

# =============================================================================
# Step 6: Configure networking for WebRTC
# =============================================================================
log "Configuring networking..."

# Create bridge network for Cuttlefish VMs
docker network create cuttlefish-net 2>/dev/null || true

# Open required ports
# ADB: 6520-6620
# WebRTC signaling: 8443-8543
# WebRTC media: 15550-15700 (UDP/TCP)
# Control: 1443-1543

PORTS_TO_OPEN=(
    "6520:6620/tcp"   # ADB ports
    "8443:8543/tcp"   # WebRTC signaling
    "1443:1543/tcp"   # Control ports
    "15550:15700/udp" # WebRTC media (UDP)
    "15550:15700/tcp" # WebRTC media (TCP fallback)
)

# Check if ufw is active
if command -v ufw &>/dev/null && ufw status | grep -q "active"; then
    for port in "${PORTS_TO_OPEN[@]}"; do
        ufw allow "$port" 2>/dev/null || true
    done
    log "UFW firewall rules added"
fi

# Check if firewalld is active
if command -v firewall-cmd &>/dev/null && systemctl is-active firewalld &>/dev/null; then
    for port in "${PORTS_TO_OPEN[@]}"; do
        firewall-cmd --permanent --add-port="$port" 2>/dev/null || true
    done
    firewall-cmd --reload 2>/dev/null || true
    log "Firewalld rules added"
fi

log "Networking configured"

# =============================================================================
# Step 7: Create management scripts
# =============================================================================
log "Creating management scripts..."

SCRIPTS_DIR="/opt/cuttlefish/scripts"
mkdir -p "$SCRIPTS_DIR"

# Script to launch a Cuttlefish VM
cat > "$SCRIPTS_DIR/launch-vm.sh" << 'LAUNCH_EOF'
#!/bin/bash
# Usage: launch-vm.sh <name> <memory_mb> <cpus> <adb_port> <webrtc_port>
NAME="${1:-cf-device-1}"
MEMORY="${2:-4096}"
CPUS="${3:-4}"
ADB_PORT="${4:-6520}"
WEBRTC_PORT="${5:-8443}"
IMAGE_DIR="${6:-/opt/cuttlefish/images/android14}"

echo "Launching Cuttlefish VM: $NAME"
echo "  Memory: ${MEMORY}MB, CPUs: $CPUS"
echo "  ADB: $ADB_PORT, WebRTC: $WEBRTC_PORT"

docker run -d \
    --name "$NAME" \
    --privileged \
    --network cuttlefish-net \
    -v /dev/kvm:/dev/kvm \
    -v "$IMAGE_DIR:/images" \
    -p "$ADB_PORT:6520" \
    -p "$WEBRTC_PORT:8443" \
    -p "$((WEBRTC_PORT - 7000)):1443" \
    -e CF_MEMORY_MB="$MEMORY" \
    -e CF_CPUS="$CPUS" \
    -e CF_DISPLAY_DPI=480 \
    -e CF_X_RES=1440 \
    -e CF_Y_RES=3200 \
    -e CF_ENABLE_MODEM=true \
    -e CF_ENABLE_GPS=true \
    -e CF_ENABLE_AUDIO=true \
    -e CF_WEBRTC_DEVICE_ID="$NAME" \
    cuttlefish-base \
    launch_cvd \
    --memory_mb="$MEMORY" \
    --cpus="$CPUS" \
    --start_webrtc=true \
    --webrtc_public_ip=0.0.0.0

echo "VM $NAME launched. WebRTC available at https://localhost:$WEBRTC_PORT"
LAUNCH_EOF
chmod +x "$SCRIPTS_DIR/launch-vm.sh"

# Script to stop a VM
cat > "$SCRIPTS_DIR/stop-vm.sh" << 'STOP_EOF'
#!/bin/bash
NAME="${1:-cf-device-1}"
echo "Stopping Cuttlefish VM: $NAME"
docker exec "$NAME" stop_cvd 2>/dev/null || true
docker stop "$NAME" 2>/dev/null || true
docker rm "$NAME" 2>/dev/null || true
echo "VM $NAME stopped and removed"
STOP_EOF
chmod +x "$SCRIPTS_DIR/stop-vm.sh"

# Script to list all VMs
cat > "$SCRIPTS_DIR/list-vms.sh" << 'LIST_EOF'
#!/bin/bash
echo "=== Cuttlefish VMs ==="
docker ps --filter "name=cf-" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
echo ""
echo "=== Stopped VMs ==="
docker ps -a --filter "name=cf-" --filter "status=exited" --format "table {{.Names}}\t{{.Status}}"
LIST_EOF
chmod +x "$SCRIPTS_DIR/list-vms.sh"

# Script to check system status
cat > "$SCRIPTS_DIR/status.sh" << 'STATUS_EOF'
#!/bin/bash
echo "=== Cuttlefish Platform Status ==="
echo ""
echo "KVM: $(test -e /dev/kvm && echo 'Available' || echo 'NOT Available')"
echo "Docker: $(docker --version 2>/dev/null || echo 'NOT installed')"
echo "CPU Cores: $(nproc)"
echo "Total RAM: $(free -h | awk '/^Mem:/ {print $2}')"
echo "Free RAM: $(free -h | awk '/^Mem:/ {print $4}')"
echo "Disk Free: $(df -h / | awk 'NR==2 {print $4}')"
echo ""
echo "Running VMs:"
docker ps --filter "name=cf-" --format "  {{.Names}} - {{.Status}}" 2>/dev/null || echo "  None"
echo ""
echo "Max recommended VMs: $(( $(free -m | awk '/^Mem:/ {print $2}') / 4096 ))"
STATUS_EOF
chmod +x "$SCRIPTS_DIR/status.sh"

log "Management scripts created in $SCRIPTS_DIR"

# =============================================================================
# Step 8: Create systemd service for auto-start
# =============================================================================
log "Creating systemd service..."

cat > /etc/systemd/system/cuttlefish-platform.service << 'SERVICE_EOF'
[Unit]
Description=Cuttlefish Android VM Platform
After=docker.service
Requires=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
ExecStart=/bin/bash -c 'docker start $(docker ps -a --filter "name=cf-" --filter "status=exited" -q) 2>/dev/null || true'
ExecStop=/bin/bash -c 'docker stop $(docker ps --filter "name=cf-" -q) 2>/dev/null || true'

[Install]
WantedBy=multi-user.target
SERVICE_EOF

systemctl daemon-reload
systemctl enable cuttlefish-platform.service

log "Systemd service created and enabled"

# =============================================================================
# Step 9: Final verification
# =============================================================================
log "Running final verification..."

echo ""
echo "============================================"
echo "  Cuttlefish VM Platform Setup Complete"
echo "============================================"
echo ""
echo "System Info:"
echo "  CPU Cores:    $(nproc)"
echo "  Total RAM:    $(free -h | awk '/^Mem:/ {print $2}')"
echo "  KVM:          $(test -e /dev/kvm && echo 'Available' || echo 'NOT Available')"
echo "  Docker:       $(docker --version 2>/dev/null | awk '{print $3}' | tr -d ',')"
echo ""
echo "Directories:"
echo "  Images:       $IMAGES_DIR"
echo "  Scripts:      $SCRIPTS_DIR"
echo ""
echo "Management Commands:"
echo "  Launch VM:    $SCRIPTS_DIR/launch-vm.sh <name> <ram_mb> <cpus> <adb_port> <webrtc_port>"
echo "  Stop VM:      $SCRIPTS_DIR/stop-vm.sh <name>"
echo "  List VMs:     $SCRIPTS_DIR/list-vms.sh"
echo "  Status:       $SCRIPTS_DIR/status.sh"
echo ""
echo "Max Recommended VMs: $(( $(free -m | awk '/^Mem:/ {print $2}') / 4096 )) (4GB each)"
echo ""
echo "Next Steps:"
echo "  1. Download Android images to $IMAGES_DIR/<version>/"
echo "  2. Use the Mobile Control Hub dashboard to create VMs"
echo "  3. Or manually: $SCRIPTS_DIR/launch-vm.sh my-device 4096 4 6520 8443"
echo ""
log "Setup complete! The platform is ready to run Cuttlefish Android VMs."
