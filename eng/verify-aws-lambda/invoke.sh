#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# Runs in the Lambda container: invokes the function through the Lambda runtime interface emulator as a function
# URL would.
set -euo pipefail
url=http://127.0.0.1:8080/2015-03-31/functions/function/invocations
for _ in $(seq 1 60); do curl -s -o /dev/null "$url" -d '{}' && break; sleep 1; done
invoke() { # label, event, expected body as a JSON string
  local actual
  actual=$(curl -sS "$url" -d "$2" | node -e 'let s="";process.stdin.on("data",d=>s+=d).on("end",()=>{const r=JSON.parse(s);process.stdout.write(`${r.statusCode} ${JSON.stringify(r.body)}`)})' 2>&1 || true)
  if [[ "$actual" == "200 $3" ]]; then echo "$1: $actual"; else echo "$1 FAILED: got '$actual'"; exit 1; fi
}
invoke "GET" '{"rawPath":"/hello","requestContext":{"http":{"method":"GET"}}}' '"Hello from C#: GET /hello (0 bytes)\n"'
invoke "POST" '{"rawPath":"/orders","requestContext":{"http":{"method":"POST"}},"body":"twelve bytes","isBase64Encoded":false}' '"Hello from C#: POST /orders (12 bytes)\n"'
invoke "base64 body" '{"rawPath":"/orders","requestContext":{"http":{"method":"PUT"}},"body":"dHdlbHZlIGJ5dGVz","isBase64Encoded":true}' '"Hello from C#: PUT /orders (12 bytes)\n"'
