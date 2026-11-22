#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# Runs in the .NET SDK container (see ../verify-container.sh): the template's service, published into /work/Hello.
set -euo pipefail
netwasm=$1
dotnet new install "NetWasm.Templates@$netwasm" > /dev/null
rm -rf /tmp/Hello && cd /tmp && dotnet new netwasm-serverless -n Hello > /dev/null && cd Hello
dotnet publish -c Release -o publish 2>&1 | tail -1
rm -rf /work/Hello && mkdir -p /work/Hello && cp -r publish /work/Hello/
