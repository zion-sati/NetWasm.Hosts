#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# Runs in the Node.js container (see ../verify-service-hosts.sh): serves each published Worker with wrangler dev and
# requests it.
set -euo pipefail
export WRANGLER_SEND_METRICS=false CI=1
echo "wrangler $(wrangler --version | tail -1)"

start() { # label, wrangler dev arguments...
  local label=$1; shift
  (cd "/work/$label/cloudflare" && exec wrangler dev --ip 127.0.0.1 --port 8787 "$@") > "/tmp/$label.log" 2>&1 &
  server=$!
  for _ in $(seq 1 120); do
    curl -s -o /dev/null http://127.0.0.1:8787/ready 2>/dev/null && return 0
    sleep 1
  done
  echo "$label: wrangler dev did not start"; tail -30 "/tmp/$label.log"; exit 1
}

stop() { kill "$server" 2>/dev/null; wait "$server" 2>/dev/null || true; }

expect() { # label, expected, curl arguments...
  local label=$1 expected=$2; shift 2
  local actual
  actual=$(curl -sS "$@")
  if [[ "$actual" == "$expected" ]]; then echo "$label: $actual"
  else echo "$label FAILED: got '$actual', expected '$expected'"; tail -30 /tmp/*.log; exit 1; fi
}

start template
expect "GET" "Hello from C#: GET /hello?name=x (0 bytes)" "http://127.0.0.1:8787/hello?name=x"
expect "POST" "Hello from C#: POST /orders (12 bytes)" -X POST --data-binary "twelve bytes" http://127.0.0.1:8787/orders
expect "Host header" "Hello from C#: GET / (0 bytes)" -H "Host: other.example" http://127.0.0.1:8787/
stop

start httpclient
expect "HttpClient through the Worker's fetch" "Order 42 totals 19.95" http://127.0.0.1:8787/
stop

start environment --var MODE:worker
expect "declared variables, MODE set by the Worker" "declared worker unset" http://127.0.0.1:8787/
stop

start large
expect "1 MiB streamed" "1048576" -o /dev/null -w '%{size_download}' http://127.0.0.1:8787/
stop
