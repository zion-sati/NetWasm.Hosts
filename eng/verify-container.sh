#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# The container guide end to end: publishes the template's service in a pinned .NET SDK image with NetWasm's packages
# from NuGet.org, builds the guide's Dockerfile around it and requests the container, on the default port and on one
# set through PORT as Cloud Run does.
# usage: eng/verify-container.sh [NetWasm version, default global.json's]
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
here="$root/eng/verify-container"
for tool in docker python3 curl; do command -v "$tool" > /dev/null || { echo "missing $tool" >&2; exit 1; }; done
docker info > /dev/null 2>&1 || { echo "Docker is not running" >&2; exit 1; }
# Docker runs a cached image of another architecture under emulation unless the platform is named.
platform=$(docker version --format '{{.Server.Os}}/{{.Server.Arch}}')
netwasm=${1:-$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["msbuild-sdks"]["NetWasm.Sdk"])' "$root/global.json")}
sdk_image=mcr.microsoft.com/dotnet/sdk:10.0@sha256:bef136a371963aaa46ec97882845ef6f54dec15728bb4b128b1c2a400ffbc9b4

work="$root/artifacts/verify-container"
rm -rf "$work" && mkdir -p "$work"
name=netwasm-hosts-container-$$
image=netwasm-hosts-hello:$$
trap 'docker exec "$name" chmod -R a+rwX /work > /dev/null 2>&1 || true; docker rm -f "$name" "$name-default" "$name-port" > /dev/null 2>&1 || true; docker rmi -f "$image" > /dev/null 2>&1 || true' EXIT
docker run --platform "$platform" -d --name "$name" -v "$here:/check:ro" -v "$work:/work" "$sdk_image" sleep infinity > /dev/null
docker exec "$name" bash /check/publish.sh "$netwasm"
docker build --platform "$platform" -q -t "$image" -f "$here/Dockerfile" "$work/Hello" > /dev/null

expect() { # label, url, expected
  local body=""
  for _ in $(seq 1 60); do body=$(curl -fsS "$2" 2> /dev/null || true); [[ -n "$body" ]] && break; sleep 1; done
  if [[ "$body" == "$3" ]]; then echo "$1: $body"; else echo "$1 FAILED: got '$body'"; exit 1; fi
}
docker run --platform "$platform" -d --name "$name-default" -p 127.0.0.1:18080:8080 "$image" > /dev/null
expect "default port" http://127.0.0.1:18080/hello "Hello from C#: GET /hello (0 bytes)"
docker run --platform "$platform" -d --name "$name-port" -e PORT=9090 -p 127.0.0.1:19090:9090 "$image" > /dev/null
expect "PORT=9090" "http://127.0.0.1:19090/orders?id=4" "Hello from C#: GET /orders?id=4 (0 bytes)"
