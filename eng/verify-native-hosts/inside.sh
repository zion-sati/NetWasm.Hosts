#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# Runs in the .NET SDK container (see ../verify-native-hosts.sh): installs the pinned hosts, creates the template's
# service with NetWasm's packages from NuGet.org, publishes it and serves it with Wasmtime, Spin and wasmCloud; then
# the same for the HttpClient example against the orders service on 127.0.0.1:9000.
set -euo pipefail
netwasm=$1
case "$(uname -m)" in
  aarch64) arch=aarch64 spin_arch=aarch64 ;;
  x86_64) arch=x86_64 spin_arch=amd64 ;;
  *) echo "unsupported architecture $(uname -m)"; exit 1 ;;
esac
apt-get update -qq > /dev/null && apt-get install -y -qq xz-utils > /dev/null
mkdir -p /tools && cd /tools

fetch() { # url, output: the download must match its checksum in tools.sha256
  curl -fsSL -o "$2" "$1"
  local expected
  expected=$(awk -v name="$(basename "$1")" '$2 == name { print $1 }' /check/tools.sha256)
  [[ -n "$expected" ]] || { echo "no pinned checksum for $(basename "$1")"; exit 1; }
  echo "$expected  $2" | sha256sum -c --quiet - || { echo "checksum mismatch for $1"; exit 1; }
}
source /check/tools.env
fetch "https://github.com/bytecodealliance/wasmtime/releases/download/v$WASMTIME/wasmtime-v$WASMTIME-$arch-linux.tar.xz" wasmtime.tar.xz
tar -xJf wasmtime.tar.xz --strip-components=1
fetch "https://github.com/spinframework/spin/releases/download/v$SPIN/spin-v$SPIN-linux-$spin_arch.tar.gz" spin.tar.gz
tar -xzf spin.tar.gz spin
fetch "https://github.com/wasmCloud/wasmCloud/releases/download/v$WASH/wash-$arch-unknown-linux-gnu" wash
chmod +x wash
export PATH=/tools:$PATH
echo "wasmtime $(wasmtime --version | cut -d' ' -f2), $(spin --version | cut -d' ' -f1-2), $(wash --version)"

dotnet new install "NetWasm.Templates@$netwasm" > /dev/null
cd /tmp && rm -rf Hello && dotnet new netwasm-serverless -n Hello > /dev/null && cd Hello

serve() { # label, url, expected, command...
  local label=$1 url=$2 expected=$3; shift 3
  "$@" > "/tmp/$label.log" 2>&1 &
  local pid=$! body=""
  for _ in $(seq 1 240); do body=$(curl -fsS "$url" 2> /dev/null || true); [[ -n "$body" ]] && break; sleep 1; done
  kill "$pid" 2> /dev/null; wait "$pid" 2> /dev/null || true
  if [[ "$body" == "$expected" ]]; then echo "$label: $body"; else echo "$label FAILED: got '$body'"; tail -20 "/tmp/$label.log"; exit 1; fi
}

write_spin() { # outbound line
  cat > spin.toml <<TOML
spin_manifest_version = 2

[application]
name = "hello"
version = "0.1.0"

[[trigger.http]]
route = "/..."
component = "service"

[component.service]
source = "publish/Hello.wasm"
$1
TOML
}

# Spin refuses an outbound request the manifest does not allow, so Handle throws and the host answers 500.
spin_refuses() {
  write_spin ""
  spin up --listen 127.0.0.1:3000 > /tmp/spin-refused.log 2>&1 &
  local pid=$! status=""
  for _ in $(seq 1 240); do status=$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:3000/hello 2> /dev/null || true); [[ "$status" != "000" && -n "$status" ]] && break; sleep 1; done
  kill "$pid" 2> /dev/null; wait "$pid" 2> /dev/null || true
  if [[ "$status" == "500" ]]; then echo "spin without allowed_outbound_hosts: 500"; else echo "spin without allowed_outbound_hosts FAILED: status '$status'"; tail -20 /tmp/spin-refused.log; exit 1; fi
}

run_hosts() { # expected body, spin outbound line
  local expected=$1 outbound=$2
  dotnet publish -c Release -o publish 2>&1 | tail -1
  echo "publish/Hello.wasm: $(stat -c %s publish/Hello.wasm) bytes"
  serve wasmtime http://127.0.0.1:8080/hello "$expected" wasmtime serve -Sp3 -Scli --addr 127.0.0.1:8080 publish/Hello.wasm
  write_spin "$outbound"
  serve spin http://127.0.0.1:3000/hello "$expected" spin up --listen 127.0.0.1:3000
  mkdir -p .wash && cat > .wash/config.yaml <<YAML
build:
  command: dotnet publish -c Release -o publish
  component_path: publish/Hello.wasm
dev:
  address: 127.0.0.1:8080
YAML
  serve wasmcloud http://127.0.0.1:8080/hello "$expected" wash dev --non-interactive
}

echo "== the template's service"
run_hosts "Hello from C#: GET /hello (0 bytes)" ""
echo "== the HttpClient example"
cp /check/HttpClient.cs ServiceExports.cs
run_hosts "Order 42 totals 19.95" 'allowed_outbound_hosts = ["http://127.0.0.1:9000"]'
spin_refuses
