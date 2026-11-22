#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# Runs in the Node.js 24 container (see ../verify-service-hosts.sh): serves each Vercel publish by calling its fetch
# export as Vercel's Node.js runtime does (vercel-runtime.mjs) and requests it. The environment holds a variable no
# variant declares (UNDECLARED), which must not reach the component.
set -euo pipefail
echo "node $(node --version)"

start() { # label, environment assignments...
  local label=$1; shift
  (exec env UNDECLARED=leak "$@" node /check/vercel-runtime.mjs "/work/$label/vercel" 3000) > "/tmp/$label.log" 2>&1 &
  server=$!
  for _ in $(seq 1 120); do
    [[ "$(node /check/request.mjs http://127.0.0.1:3000/ready GET "" status 2> /dev/null || true)" == 200 ]] && return 0
    sleep 1
  done
  echo "$label: the function did not start"; tail -30 "/tmp/$label.log"; exit 1
}

stop() { kill "$server" 2> /dev/null; wait "$server" 2> /dev/null || true; }

expect() { # label, expected, request arguments...
  local label=$1 expected=$2; shift 2
  local actual
  actual=$(node /check/request.mjs "$@")
  if [[ "$actual" == "$expected" ]]; then echo "$label: $actual"
  else echo "$label FAILED: got '$actual', expected '$expected'"; tail -30 /tmp/*.log; exit 1; fi
}

start template
expect "GET" "Hello from C#: GET /hello?name=x (0 bytes)" "http://127.0.0.1:3000/hello?name=x"
expect "POST" "Hello from C#: POST /orders (12 bytes)" http://127.0.0.1:3000/orders POST "twelve bytes"
stop

start httpclient
expect "HttpClient through Node.js's fetch" "Order 42 totals 19.95" http://127.0.0.1:3000/
stop

start environment MODE=vercel
expect "declared variables, MODE set by the function, UNDECLARED kept out" "declared vercel unset" http://127.0.0.1:3000/
stop

start large
expect "1 MiB streamed" "1048576" http://127.0.0.1:3000/ GET "" size
stop
