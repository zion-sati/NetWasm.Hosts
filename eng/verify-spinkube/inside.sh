#!/bin/sh
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# Runs in the tools container (see ../verify-spinkube.sh), on the cluster's network and with the Docker socket: installs
# the pinned tools, creates a kind cluster on the Spin shim's node image, installs SpinKube as its quickstart does,
# pushes the published service to the local registry and serves it as a SpinApp.
set -eu
cluster=$1 registry=$2
. /check/tools.env
case "$(uname -m)" in aarch64) arch=arm64 spin_arch=aarch64 ;; x86_64) arch=amd64 spin_arch=amd64 ;; *) exit 1 ;; esac
mkdir -p /tools && cd /tools
fetch() { # url, file[, name in tools.sha256, default the URL's file name]: the download must match its checksum
  wget -q -O "$2" "$1"
  key=${3:-$(basename "$1")}
  expected=$(awk -v name="$key" '$2 == name { print $1 }' /check/tools.sha256)
  [ -n "$expected" ] || { echo "no pinned checksum for $key"; exit 1; }
  echo "$expected  $2" | sha256sum -c -s - || { echo "checksum mismatch for $1"; exit 1; }
}
fetch "https://github.com/kubernetes-sigs/kind/releases/download/v$KIND/kind-linux-$arch" kind
fetch "https://dl.k8s.io/release/v$KUBECTL/bin/linux/$arch/kubectl" kubectl "kubectl-linux-$arch"
fetch "https://get.helm.sh/helm-v$HELM-linux-$arch.tar.gz" helm.tar.gz
fetch "https://github.com/spinframework/spin/releases/download/v$SPIN/spin-v$SPIN-static-linux-$spin_arch.tar.gz" spin.tar.gz
chmod +x kind kubectl && tar -xzf helm.tar.gz --strip-components=1 "linux-$arch/helm" && tar -xzf spin.tar.gz spin
export PATH=/tools:$PATH

# The spin runtime, as SpinKube's quickstart registers it, and registry hosts read from /etc/containerd/certs.d, so the
# node pulls from the registry container by its name on the cluster's network.
cat > /tmp/kind.yaml <<YAML
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
containerdConfigPatches:
- |-
  [plugins."io.containerd.cri.v1.runtime".containerd.runtimes.spin]
    runtime_type = "io.containerd.spin.v2"
  [plugins."io.containerd.cri.v1.runtime".containerd.runtimes.spin.options]
    SystemdCgroup = true
  [plugins."io.containerd.cri.v1.images".registry]
    config_path = "/etc/containerd/certs.d"
YAML
kind create cluster --name "$cluster" --image "$NODE_IMAGE" --config /tmp/kind.yaml --wait 300s
node="$cluster-control-plane"
docker exec "$node" mkdir -p "/etc/containerd/certs.d/$registry:5000"
printf '[host."http://%s:5000"]\n' "$registry" | docker exec -i "$node" sh -c "cat > /etc/containerd/certs.d/$registry:5000/hosts.toml"
kind get kubeconfig --internal --name "$cluster" > /tmp/kubeconfig
export KUBECONFIG=/tmp/kubeconfig

kubectl apply -f "https://github.com/cert-manager/cert-manager/releases/download/v$CERT_MANAGER/cert-manager.yaml" > /dev/null
kubectl wait --for=condition=available --timeout=300s deployment/cert-manager-webhook -n cert-manager
base="https://github.com/spinframework/spin-operator/releases/download/v$SPIN_OPERATOR"
kubectl apply -f "$base/spin-operator.runtime-class.yaml" -f "$base/spin-operator.crds.yaml" > /dev/null
helm upgrade --install spin-operator --namespace spin-operator --create-namespace --version "$SPIN_OPERATOR" --wait \
  oci://ghcr.io/spinframework/charts/spin-operator > /dev/null
# The operator's admission webhook answers a little after the chart reports the operator ready.
for attempt in $(seq 1 24); do
  kubectl apply -f "$base/spin-operator.shim-executor.yaml" > /tmp/executor.log 2>&1 && break
  [ "$attempt" -lt 24 ] || { cat /tmp/executor.log; exit 1; }
  sleep 5
done

cd /work/app
spin registry push --insecure "$registry:5000/hello:v1"
sed "s|__IMAGE__|$registry:5000/hello:v1|" /check/spinapp.yaml | kubectl apply -f -
for _ in $(seq 1 60); do kubectl get deployment hello > /dev/null 2>&1 && break; sleep 2; done
diagnose() {
  kubectl get spinapp,deployments,pods -o wide
  kubectl get events --sort-by=.lastTimestamp | tail -25
  kubectl describe pods -l core.spinkube.dev/app-name=hello | tail -40
  kubectl logs -l core.spinkube.dev/app-name=hello --tail=40 || true
}
kubectl rollout status deployment/hello --timeout=300s || { diagnose; exit 1; }
kubectl port-forward svc/hello 8083:80 > /tmp/forward.log 2>&1 &
for _ in $(seq 1 30); do body=$(wget -q -O - http://127.0.0.1:8083/hello 2> /dev/null || true); [ -n "$body" ] && break; sleep 1; done
expected="Hello from C#: GET /hello (0 bytes)"
if [ "$body" = "$expected" ]; then echo "SpinKube: $body"
else echo "SpinKube FAILED: got '$body'"; diagnose; exit 1; fi
