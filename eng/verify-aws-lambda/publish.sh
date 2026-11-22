#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# Runs in the .NET SDK container (see ../verify-aws-lambda.sh): publishes the function with NetWasm's packages from
# NuGet.org into /work/function, beside its handler.
set -euo pipefail
netwasm=$1
rm -rf /tmp/Functions && cp -r /check/Functions /tmp/Functions && cd /tmp/Functions
printf '{ "msbuild-sdks": { "NetWasm.Sdk": "%s" } }\n' "$netwasm" > global.json
dotnet publish -c Release -o /tmp/function 2>&1 | tail -2
cp index.mjs /tmp/function/
rm -rf /work/function && cp -r /tmp/function /work/function
find /work/function -type f | sort | sed 's|^/work/function/|  |'
