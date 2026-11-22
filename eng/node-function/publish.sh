#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# Runs in the .NET SDK container of a Node.js function check (eng/verify-aws-lambda.sh and the others), with eng/ at
# /eng: publishes the shared JsModule project with NetWasm's packages from NuGet.org into /work/function, beside the
# provider's own files from /eng/<check>/function.
# usage: publish.sh <NetWasm version> <check directory under eng/>
set -euo pipefail
netwasm=$1
check=$2
[[ -d "/eng/$check/function" ]] || { echo "no /eng/$check/function" >&2; exit 1; }
rm -rf /tmp/Functions /tmp/function && cp -r /eng/node-function/Functions /tmp/Functions && cd /tmp/Functions
printf '{ "msbuild-sdks": { "NetWasm.Sdk": "%s" } }\n' "$netwasm" > global.json
dotnet publish -c Release -o /tmp/function 2>&1 | tail -2
cp -r "/eng/$check/function/." /tmp/function/
rm -rf /work/function && cp -r /tmp/function /work/function
find /work/function -type f | sort | sed 's|^/work/function/|  |'
