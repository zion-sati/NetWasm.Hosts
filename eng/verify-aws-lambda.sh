#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# The AWS Lambda guide end to end: publishes its JsModule function in a pinned .NET SDK image with NetWasm's packages
# from NuGet.org, runs it in AWS's Node.js 24 Lambda image (its runtime interface emulator) and invokes it.
# usage: eng/verify-aws-lambda.sh [NetWasm version, default global.json's]
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
here="$root/eng/verify-aws-lambda"
for tool in docker python3; do command -v "$tool" > /dev/null || { echo "missing $tool" >&2; exit 1; }; done
docker info > /dev/null 2>&1 || { echo "Docker is not running" >&2; exit 1; }
# Docker runs a cached image of another architecture under emulation unless the platform is named.
platform=$(docker version --format '{{.Server.Os}}/{{.Server.Arch}}')
netwasm=${1:-$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["msbuild-sdks"]["NetWasm.Sdk"])' "$root/global.json")}

sdk_image=mcr.microsoft.com/dotnet/sdk:10.0@sha256:bef136a371963aaa46ec97882845ef6f54dec15728bb4b128b1c2a400ffbc9b4
lambda_image=public.ecr.aws/lambda/nodejs:24@sha256:659129486b70ceab4e72dc7bddb49c84a6c4960725f6a14be7a5c402e83d3759

work="$root/artifacts/verify-aws-lambda"
rm -rf "$work" && mkdir -p "$work"
name=netwasm-hosts-lambda-$$
trap 'docker exec "$name" chmod -R a+rwX /work > /dev/null 2>&1 || true; docker rm -f "$name" "$name-lambda" > /dev/null 2>&1 || true' EXIT
docker run --platform "$platform" -d --name "$name" -v "$here:/check:ro" -v "$work:/work" "$sdk_image" sleep infinity > /dev/null
docker exec "$name" bash /check/publish.sh "$netwasm"
docker run --platform "$platform" -d --name "$name-lambda" --network "container:$name" -v "$work/function:/var/task:ro" -v "$here:/check:ro" \
  "$lambda_image" index.handler > /dev/null
docker exec "$name-lambda" bash /check/invoke.sh
