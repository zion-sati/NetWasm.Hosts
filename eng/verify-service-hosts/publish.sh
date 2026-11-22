#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# Runs in the .NET SDK container (see ../verify-service-hosts.sh): creates the template's service with NetWasm's packages
# from NuGet.org and NetWasm.Hosts from /packages, and publishes four variants for Cloudflare into /work.
set -euo pipefail
netwasm=$1 version=$2
dotnet new install "NetWasm.Templates@$netwasm" > /dev/null
cd /tmp && rm -rf Hello && dotnet new netwasm-serverless -n Hello > /dev/null && cd Hello
dotnet new nugetconfig > /dev/null
dotnet nuget add source /packages -n local > /dev/null
dotnet add package NetWasm.Hosts --version "$version" > /dev/null
sed -i 's|</PropertyGroup>|  <NetWasmServiceHosts>cloudflare</NetWasmServiceHosts>\n  </PropertyGroup>|' Hello.csproj

publish() { # label
  rm -rf "/tmp/publish-$1"
  dotnet publish -c Release -o "/tmp/publish-$1" 2>&1 | tail -2
  rm -rf "/work/$1" && mkdir -p "/work/$1" && cp -r "/tmp/publish-$1/cloudflare" "/work/$1/"
}

echo "== the template's service"
publish template
find /work/template -type f | sort | sed 's|^/work/template/|  |'
cat /work/template/cloudflare/wrangler.json
echo "== the HttpClient example"
cp /check/glue/HttpClient.cs ServiceExports.cs
publish httpclient
echo "== declared variables"
cp /check/glue/Environment.cs ServiceExports.cs
sed -i 's|</Project>|  <ItemGroup>\n    <NetWasmEnvironmentVariable Include="GREETING" Value="declared" />\n    <NetWasmEnvironmentVariable Include="MODE" Value="declared" />\n  </ItemGroup>\n</Project>|' Hello.csproj
publish environment
echo "== a 1 MiB response"
cp /check/glue/Large.cs ServiceExports.cs
publish large
