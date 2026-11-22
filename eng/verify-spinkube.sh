#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# SpinKube end to end: publishes the template's service in a pinned .NET SDK image with NetWasm's packages from
# NuGet.org, then, from a pinned Docker CLI image on the host's Docker socket, creates a kind cluster on the Spin
# shim's node image, installs SpinKube as its quickstart does, and serves the service as a SpinApp from a local
# registry. Tool versions are in verify-spinkube/tools.env, each download checked against tools.sha256.
# usage: eng/verify-spinkube.sh [NetWasm version, default global.json's]
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
here="$root/eng/verify-spinkube"
for tool in docker python3; do command -v "$tool" > /dev/null || { echo "missing $tool" >&2; exit 1; }; done
docker info > /dev/null 2>&1 || { echo "Docker is not running" >&2; exit 1; }
# Docker runs a cached image of another architecture under emulation unless the platform is named.
platform=$(docker version --format '{{.Server.Os}}/{{.Server.Arch}}')
netwasm=${1:-$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["msbuild-sdks"]["NetWasm.Sdk"])' "$root/global.json")}

sdk_image=mcr.microsoft.com/dotnet/sdk:10.0@sha256:bef136a371963aaa46ec97882845ef6f54dec15728bb4b128b1c2a400ffbc9b4
tools_image=docker:27-cli@sha256:851f91d241214e7c6db86513b270d58776379aacc5eb9c4a87e5b47115e3065c
registry_image=registry:2@sha256:a3d8aaa63ed8681a604f1dea0aa03f100d5895b6a58ace528858a7b332415373
node_image=ghcr.io/spinframework/containerd-shim-spin/kind:v0.27.2@sha256:99a28b661bc94499f95616fd1d2c22ab5a403429eeabe8c25263e40c35da9a24

work="$root/artifacts/verify-spinkube"
rm -rf "$work" && mkdir -p "$work/app"
id=$$
name=netwasm-hosts-spinkube-$id cluster=nwh-$id registry=nwh-registry-$id network=nwh-kind-$id
cleanup() {
  docker exec "$name" chmod -R a+rwX /work > /dev/null 2>&1 || true
  docker rm -f "$cluster-control-plane" "$name" "$registry" > /dev/null 2>&1 || true
  docker network rm "$network" > /dev/null 2>&1 || true
}
trap cleanup EXIT

docker run --platform "$platform" -d --name "$name" -v "$here:/check:ro" -v "$work:/work" "$sdk_image" sleep infinity > /dev/null
docker exec "$name" bash -c "set -euo pipefail
  dotnet new install NetWasm.Templates@$netwasm > /dev/null
  cd /tmp && rm -rf Hello && dotnet new netwasm-serverless -n Hello > /dev/null && cd Hello
  dotnet publish -c Release -o publish 2>&1 | tail -1
  cp publish/Hello.wasm /check/spin.toml /work/app/"

docker network create "$network" > /dev/null
docker run --platform "$platform" -d --name "$registry" --network "$network" "$registry_image" > /dev/null
docker run --platform "$platform" --rm -v /var/run/docker.sock:/var/run/docker.sock -v "$here:/check:ro" -v "$work:/work" \
  -e KIND_EXPERIMENTAL_DOCKER_NETWORK="$network" -e NODE_IMAGE="$node_image" --network "$network" \
  "$tools_image" sh /check/inside.sh "$cluster" "$registry"
