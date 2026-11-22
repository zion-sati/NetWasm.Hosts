#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# Runs the host modules' tests at 100% line, branch and function coverage. A Worker's host modules import
# NetWasm.Hosting's (wasi-platform.mjs and the rest) from the same directory, as the published Worker does, so the
# tests run in a directory that holds both: NetWasm.Hosting's at the version global.json pins, then these.
# usage: eng/test-javascript.sh
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
for tool in node python3 curl unzip; do command -v "$tool" > /dev/null || { echo "missing $tool" >&2; exit 1; }; done
version=$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["msbuild-sdks"]["NetWasm.Sdk"])' "$root/global.json")

work="$root/artifacts/javascript-tests"
package="$work/netwasm.hosting.$version.nupkg"
mkdir -p "$work"
if [[ ! -f "$package" ]]; then
  curl -fsSL -o "$package.partial" "https://api.nuget.org/v3-flatcontainer/netwasm.hosting/$version/netwasm.hosting.$version.nupkg"
  mv "$package.partial" "$package"
fi
stage="$work/stage"
rm -rf "$stage" && mkdir -p "$stage"
unzip -q -j "$package" 'tools/netwasm/hosting/*' -d "$stage"
hosting="$root/src/NetWasm.Hosts/hosting"
coverage=()
for module in "$hosting"/*.mjs; do
  name=$(basename "$module")
  [[ ! -e "$stage/$name" ]] || { echo "$name is also a NetWasm.Hosting module" >&2; exit 1; }
  cp "$module" "$stage/$name"
  coverage+=("--test-coverage-include=$stage/$name")
done
cp "$root"/tests/javascript/*.test.mjs "$stage/"
cd "$stage"
node --test --experimental-test-coverage "${coverage[@]}" \
  --test-coverage-lines=100 --test-coverage-branches=100 --test-coverage-functions=100 ./*.test.mjs
