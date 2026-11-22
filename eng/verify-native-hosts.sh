#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# The hosts that run NetWasm's wasi:http service component natively, in a pinned .NET SDK container: Wasmtime, Spin
# and wasmCloud at the versions in verify-native-hosts/tools.env, each download checked against tools.sha256. The
# service comes from NetWasm's serverless template with NetWasm's packages from NuGet.org; an orders service on
# 127.0.0.1:9000 answers the HttpClient example.
# usage: eng/verify-native-hosts.sh [NetWasm version, default global.json's]
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
here="$root/eng/verify-native-hosts"
for tool in docker python3; do command -v "$tool" > /dev/null || { echo "missing $tool" >&2; exit 1; }; done
docker info > /dev/null 2>&1 || { echo "Docker is not running" >&2; exit 1; }
# Docker runs a cached image of another architecture under emulation unless the platform is named.
platform=$(docker version --format '{{.Server.Os}}/{{.Server.Arch}}')
netwasm=${1:-$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["msbuild-sdks"]["NetWasm.Sdk"])' "$root/global.json")}

sdk_image=mcr.microsoft.com/dotnet/sdk:10.0@sha256:bef136a371963aaa46ec97882845ef6f54dec15728bb4b128b1c2a400ffbc9b4
python_image=python:3.13-alpine@sha256:9ef1a599f863231c3d91e941389bfdc4a2d6070557243d54df1a4e57015b960e

name=netwasm-hosts-native-$$
trap 'docker rm -f "$name" "$name-upstream" > /dev/null 2>&1 || true' EXIT
docker run --platform "$platform" -d --name "$name" -v "$here:/check:ro" "$sdk_image" sleep infinity > /dev/null
docker run --platform "$platform" -d --name "$name-upstream" --network "container:$name" -v "$root/eng/verify-service-hosts/upstream:/srv:ro" \
  "$python_image" python -m http.server 9000 --bind 127.0.0.1 --directory /srv > /dev/null
docker exec "$name" bash /check/inside.sh "$netwasm"
