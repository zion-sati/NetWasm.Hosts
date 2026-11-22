#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# The Google Cloud Run functions guide end to end: publishes its JsModule function in a pinned .NET SDK image with
# NetWasm's packages from NuGet.org, installs the app's locked npm packages and serves it with Google's Functions
# Framework on Node.js 24, as Cloud Run functions does, then requests it.
# usage: eng/verify-cloud-run-functions.sh [NetWasm version, default global.json's]
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
for tool in docker python3 curl; do command -v "$tool" > /dev/null || { echo "missing $tool" >&2; exit 1; }; done
docker info > /dev/null 2>&1 || { echo "Docker is not running" >&2; exit 1; }
# Docker runs a cached image of another architecture under emulation unless the platform is named.
platform=$(docker version --format '{{.Server.Os}}/{{.Server.Arch}}')
netwasm=${1:-$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["msbuild-sdks"]["NetWasm.Sdk"])' "$root/global.json")}

sdk_image=mcr.microsoft.com/dotnet/sdk:10.0@sha256:bef136a371963aaa46ec97882845ef6f54dec15728bb4b128b1c2a400ffbc9b4
node_image=node:24-bookworm-slim@sha256:dc26ae92c9fb15cf6b1d63e20f0944b2efe0f115b20e00be48fb23fceb061c54

work="$root/artifacts/verify-cloud-run-functions"
rm -rf "$work" && mkdir -p "$work"
name=netwasm-hosts-cloud-run-functions-$$
trap 'docker exec "$name" chmod -R a+rwX /work > /dev/null 2>&1 || true; docker rm -f "$name" "$name-function" > /dev/null 2>&1 || true' EXIT
docker run --platform "$platform" -d --name "$name" -v "$root/eng:/eng:ro" -v "$work:/work" "$sdk_image" sleep infinity > /dev/null
docker exec "$name" bash /eng/node-function/publish.sh "$netwasm" verify-cloud-run-functions
docker run --platform "$platform" --rm -v "$work/function:/function" -w /function "$node_image" \
  npm ci --omit=dev --no-audit --no-fund --loglevel=error

# Cloud Run sets PORT; the framework listens on it.
docker run --platform "$platform" -d --name "$name-function" -p 127.0.0.1:18082:9090 -e PORT=9090 \
  -v "$work/function:/function:ro" -w /function "$node_image" node_modules/.bin/functions-framework --target=hello > /dev/null
expect() { # label, path, expected, curl arguments...
  local label=$1 path=$2 expected=$3 body=""
  shift 3
  for _ in $(seq 1 60); do body=$(curl -fsS "$@" "http://127.0.0.1:18082$path" 2> /dev/null || true); [[ -n "$body" ]] && break; sleep 1; done
  if [[ "$body" == "$expected" ]]; then echo "$label: $body"; else
    echo "$label FAILED: got '$body'"; docker logs "$name-function" 2>&1 | tail -40; exit 1
  fi
}
expect "GET" /hello "Hello from C#: GET /hello (0 bytes)"
expect "POST" /orders "Hello from C#: POST /orders (12 bytes)" -H "content-type: text/plain" --data-binary "twelve bytes"
expect "PUT" /orders/7 "Hello from C#: PUT /orders/7 (12 bytes)" -X PUT -H "content-type: application/json" --data-binary '{"total":42}'
expect "binary body" /upload "Hello from C#: POST /upload (12 bytes)" -H "content-type: application/octet-stream" --data-binary "twelve bytes"
