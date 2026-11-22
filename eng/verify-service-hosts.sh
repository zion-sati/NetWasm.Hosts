#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# NetWasm.Hosts end to end, in pinned containers: packs the package, publishes NetWasm's serverless template with it
# for Cloudflare and Deno (NetWasm's packages from NuGet.org) in the .NET SDK image, and serves each publish with
# wrangler dev in the Node.js image and with deno serve in the Deno image. All share the SDK container's network,
# with a small orders service on 127.0.0.1:9000.
# usage: eng/verify-service-hosts.sh [package version, default global.json's NetWasm version]
# NETWASM_HOSTS_PACKAGES names a directory of already built packages to verify instead of packing them here.
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
here="$root/eng/verify-service-hosts"
for tool in docker dotnet python3; do command -v "$tool" > /dev/null || { echo "missing $tool" >&2; exit 1; }; done
docker info > /dev/null 2>&1 || { echo "Docker is not running" >&2; exit 1; }
# Docker runs a cached image of another architecture under emulation unless the platform is named.
platform=$(docker version --format '{{.Server.Os}}/{{.Server.Arch}}')
netwasm=$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["msbuild-sdks"]["NetWasm.Sdk"])' "$root/global.json")
version=${1:-$netwasm}

wrangler=4.158.0
sdk_image=mcr.microsoft.com/dotnet/sdk:10.0@sha256:bef136a371963aaa46ec97882845ef6f54dec15728bb4b128b1c2a400ffbc9b4
node_image=node:22-bookworm@sha256:e13726ca604184d7a91fcacab1fc09eefd642dc28e79ac34e85540dd93b78cfb
deno_image=denoland/deno:2.9.8@sha256:a0d88a7fcbd44f062cba2748e19df67d30290ad12aa203c7c1e0b12462c483f0
python_image=python:3.13-alpine@sha256:9ef1a599f863231c3d91e941389bfdc4a2d6070557243d54df1a4e57015b960e

work="$root/artifacts/verify-service-hosts"
rm -rf "$work" && mkdir -p "$work"
if [[ -n "${NETWASM_HOSTS_PACKAGES:-}" ]]; then
  packages=$(cd "$NETWASM_HOSTS_PACKAGES" && pwd)
else
  packages="$root/artifacts/packages"
  rm -rf "$packages" && mkdir -p "$packages"
  dotnet pack "$root/src/NetWasm.Hosts/NetWasm.Hosts.csproj" -c Release -o "$packages" \
    -p:Version="$version" --nologo -v q
fi
[[ -f "$packages/NetWasm.Hosts.$version.nupkg" ]] || { echo "no NetWasm.Hosts.$version.nupkg in $packages" >&2; exit 1; }

name=netwasm-hosts-services-$$
# The containers write as root: hand the files back before removing the containers.
trap 'docker exec "$name" chmod -R a+rwX /work > /dev/null 2>&1 || true; docker rm -f "$name" "$name-node" "$name-deno" "$name-upstream" > /dev/null 2>&1 || true' EXIT
docker run --platform "$platform" -d --name "$name" -v "$here:/check:ro" -v "$packages:/packages:ro" -v "$work:/work" \
  "$sdk_image" sleep infinity > /dev/null
docker run --platform "$platform" -d --name "$name-node" --network "container:$name" -v "$here:/check:ro" -v "$work:/work" \
  "$node_image" sleep infinity > /dev/null
docker run --platform "$platform" -d --name "$name-deno" --network "container:$name" -v "$here:/check:ro" -v "$work:/work" \
  --entrypoint sleep "$deno_image" infinity > /dev/null
docker run --platform "$platform" -d --name "$name-upstream" --network "container:$name" -v "$here/upstream:/srv:ro" \
  "$python_image" python -m http.server 9000 --bind 127.0.0.1 --directory /srv > /dev/null

docker exec "$name" bash /check/publish.sh "$netwasm" "$version"
docker exec "$name-node" npm install -g --silent "wrangler@$wrangler" > /dev/null
docker exec "$name-node" bash /check/serve-cloudflare.sh
docker exec "$name-deno" bash /check/serve-deno.sh
