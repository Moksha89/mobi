#!/bin/bash
# Mobile Control Hub - K8s Cloud Android Platform Setup Script
# Run this on the K8s master node to prepare the cluster for Android devices.
#
# Usage: bash k8s-setup.sh [KUBECONFIG_PATH]
#
# Prerequisites:
#   - Kubernetes cluster running (K3s or standard K8s)
#   - kubectl configured and working
#   - Sufficient resources (recommended: 16+ cores, 64+ GB RAM)

set -e

echo "============================================================"
echo "  Mobile Control Hub - K8s Cloud Android Setup"
echo "============================================================"
echo ""

# Check kubectl
if ! command -v kubectl &> /dev/null; then
    echo "[ERROR] kubectl not found. Install it first:"
    echo "  curl -LO https://dl.k8s.io/release/stable.txt"
    echo "  curl -LO https://dl.k8s.io/release/\$(cat stable.txt)/bin/linux/amd64/kubectl"
    echo "  chmod +x kubectl && sudo mv kubectl /usr/local/bin/"
    exit 1
fi

# Optional: set KUBECONFIG
if [ -n "$1" ]; then
    export KUBECONFIG="$1"
    echo "[INFO] Using KUBECONFIG: $KUBECONFIG"
fi

# Verify cluster connection
echo ""
echo "[Step 1/7] Verifying cluster connection..."
if ! kubectl cluster-info &> /dev/null; then
    echo "[ERROR] Cannot connect to Kubernetes cluster."
    echo "  Make sure kubectl is configured. Try: kubectl cluster-info"
    exit 1
fi
echo "[OK] Cluster is reachable"
kubectl get nodes -o wide
echo ""

# Check node resources
echo "[Step 2/7] Checking node resources..."
kubectl top nodes 2>/dev/null || echo "[WARN] Metrics server not available. Install it for resource monitoring."
echo ""

# Create namespace
echo "[Step 3/7] Creating android-cloud namespace..."
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
kubectl apply -f "$SCRIPT_DIR/namespace.yaml"
echo "[OK] Namespace created"
echo ""

# Apply RBAC
echo "[Step 4/7] Applying RBAC (ServiceAccount, ClusterRole, ClusterRoleBinding)..."
kubectl apply -f "$SCRIPT_DIR/rbac.yaml"
echo "[OK] RBAC configured"
echo ""

# Check for GPU support
echo "[Step 5/7] Checking GPU support..."
GPU_NODES=$(kubectl get nodes -o jsonpath='{range .items[*]}{.metadata.name}{"\t"}{.status.allocatable.nvidia\.com/gpu}{"\n"}{end}' 2>/dev/null | grep -v "^$" || true)
if [ -n "$GPU_NODES" ]; then
    echo "[OK] GPU nodes detected:"
    echo "$GPU_NODES"
else
    echo "[INFO] No GPU nodes detected. Devices will use SwiftShader (software rendering)."
    echo "  For GPU acceleration, install NVIDIA device plugin:"
    echo "  kubectl create -f https://raw.githubusercontent.com/NVIDIA/k8s-device-plugin/v0.14.1/nvidia-device-plugin.yml"
fi
echo ""

# Pre-pull Redroid images
echo "[Step 6/7] Pre-pulling Android container images (this may take a few minutes)..."
cat <<EOF | kubectl apply -f -
apiVersion: apps/v1
kind: DaemonSet
metadata:
  name: image-prepull
  namespace: android-cloud
  labels:
    app: image-prepull
spec:
  selector:
    matchLabels:
      app: image-prepull
  template:
    metadata:
      labels:
        app: image-prepull
    spec:
      initContainers:
        - name: pull-android15
          image: redroid/redroid:15.0.0_64only-latest
          command: ["echo", "Image pulled"]
          resources:
            limits:
              cpu: "100m"
              memory: "64Mi"
        - name: pull-android14
          image: redroid/redroid:14.0.0_64only-latest
          command: ["echo", "Image pulled"]
          resources:
            limits:
              cpu: "100m"
              memory: "64Mi"
        - name: pull-android13
          image: redroid/redroid:13.0.0_64only-latest
          command: ["echo", "Image pulled"]
          resources:
            limits:
              cpu: "100m"
              memory: "64Mi"
        - name: pull-scrcpy
          image: emptysuns/scrcpy-web:v0.1
          command: ["echo", "Image pulled"]
          resources:
            limits:
              cpu: "100m"
              memory: "64Mi"
      containers:
        - name: pause
          image: registry.k8s.io/pause:3.9
          resources:
            limits:
              cpu: "10m"
              memory: "16Mi"
      terminationGracePeriodSeconds: 0
EOF
echo "[OK] Image pre-pull DaemonSet created. Images will be cached on all nodes."
echo ""

# Enable kernel modules for Redroid
echo "[Step 7/7] Checking kernel modules for Android containers..."
MODULES_NEEDED=("binder_linux" "ashmem_linux")
MODULES_MISSING=()
for mod in "${MODULES_NEEDED[@]}"; do
    if ! lsmod 2>/dev/null | grep -q "$mod"; then
        MODULES_MISSING+=("$mod")
    fi
done

if [ ${#MODULES_MISSING[@]} -gt 0 ]; then
    echo "[WARN] Missing kernel modules: ${MODULES_MISSING[*]}"
    echo "  Run on each node:"
    echo "    sudo modprobe binder_linux devices=binder,hwbinder,vndbinder"
    echo "    sudo modprobe ashmem_linux"
    echo "  To make permanent, add to /etc/modules-load.d/android.conf:"
    echo "    echo -e 'binder_linux\nashmem_linux' | sudo tee /etc/modules-load.d/android.conf"
    echo "    echo 'options binder_linux devices=binder,hwbinder,vndbinder' | sudo tee /etc/modprobe.d/android.conf"
else
    echo "[OK] All required kernel modules loaded"
fi

echo ""
echo "============================================================"
echo "  Setup Complete!"
echo "============================================================"
echo ""
echo "Next steps:"
echo "  1. Configure MCH dashboard with this cluster's kubeconfig"
echo "  2. Set environment variable: K8S_KUBECONFIG_PATH=/path/to/kubeconfig"
echo "  3. Restart the MCH dashboard service"
echo "  4. Create devices from the dashboard: Devices -> K8s Cloud Platform -> New Device"
echo ""
echo "Cluster info:"
kubectl get nodes -o wide
echo ""
echo "Namespace status:"
kubectl get all -n android-cloud
echo ""
echo "To monitor devices: kubectl get pods -n android-cloud -w"
echo "To view logs: kubectl logs -n android-cloud <pod-name> -c redroid"
echo ""
