#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# Runs in the Node.js 24 container with netlify-cli (see ../verify-service-hosts.sh): serves each Netlify publish with
# netlify dev --offline, which bundles and runs the edge function in its local Deno edge runtime with no Netlify
# account, and requests it. The environment holds a variable no variant declares (UNDECLARED), which must not reach
# the component.
set -euo pipefail
echo "netlify-cli $(netlify --version | head -1)"

start() { # label, environment assignments...
  local label=$1; shift
  # Its own process group, so stopping it also stops the Deno edge runtime it starts.
  (cd "/work/$label/netlify" && exec setsid env UNDECLARED=leak "$@" netlify dev --offline --no-open --port 8888 \
    --framework '#static') > "/tmp/$label.log" 2>&1 &
  server=$!
  for _ in $(seq 1 180); do
    [[ "$(node /check/request.mjs http://127.0.0.1:8888/ready GET "" status 2> /dev/null || true)" == 200 ]] && return 0
    kill -0 "$server" 2> /dev/null || break
    sleep 1
  done
  echo "$label: netlify dev did not serve the edge function"; tail -40 "/tmp/$label.log"; exit 1
}

stop() {
  kill -TERM -- "-$server" 2> /dev/null || true
  wait "$server" 2> /dev/null || true
  for _ in $(seq 1 30); do node /check/request.mjs http://127.0.0.1:8888/ > /dev/null 2>&1 || return 0; sleep 1; done
  echo "netlify dev did not stop"; exit 1
}

expect() { # label, expected, request arguments...
  local label=$1 expected=$2; shift 2
  local actual
  actual=$(node /check/request.mjs "$@")
  if [[ "$actual" == "$expected" ]]; then echo "$label: $actual"
  else echo "$label FAILED: got '$actual', expected '$expected'"; tail -40 /tmp/*.log; exit 1; fi
}

start template
expect "GET" "Hello from C#: GET /hello?name=x (0 bytes)" "http://127.0.0.1:8888/hello?name=x"
expect "POST" "Hello from C#: POST /orders (12 bytes)" http://127.0.0.1:8888/orders POST "twelve bytes"
stop

start httpclient
expect "HttpClient through the edge function's fetch" "Order 42 totals 19.95" http://127.0.0.1:8888/
stop

start environment MODE=netlify
expect "declared variables, MODE set by the site, UNDECLARED kept out" "declared netlify unset" http://127.0.0.1:8888/
stop

start large
expect "1 MiB streamed" "1048576" http://127.0.0.1:8888/ GET "" size
stop
