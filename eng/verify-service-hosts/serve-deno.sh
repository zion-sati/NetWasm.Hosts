#!/usr/bin/env bash
# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# Runs in the Deno container (see ../verify-service-hosts.sh): serves each Deno publish with deno serve and requests it.
# The environment holds a variable no variant declares (UNDECLARED), which must not reach the component.
set -euo pipefail
echo "$(deno --version | head -1)"

start() { # label, environment assignments...
  local label=$1; shift
  (cd "/work/$label/deno" && exec env UNDECLARED=leak "$@" deno serve --allow-read --allow-env --allow-net \
    --host 127.0.0.1 --port 8000 main.mjs) > "/tmp/$label.log" 2>&1 &
  server=$!
  for _ in $(seq 1 120); do
    deno run --allow-net /check/request.mjs http://127.0.0.1:8000/ready > /dev/null 2>&1 && return 0
    sleep 1
  done
  echo "$label: deno serve did not start"; tail -30 "/tmp/$label.log"; exit 1
}

# deno serve stops on SIGTERM once no request has left work behind: a handler still suspended after its response
# would keep it running.
stop() {
  kill -TERM "$server" 2> /dev/null
  for _ in $(seq 1 10); do
    kill -0 "$server" 2> /dev/null || { wait "$server" 2> /dev/null || true; return 0; }
    sleep 1
  done
  echo "deno serve did not stop on SIGTERM: a request left work behind"; kill -KILL "$server"; exit 1
}

expect() { # label, expected, request arguments...
  local label=$1 expected=$2; shift 2
  local actual
  actual=$(deno run --allow-net /check/request.mjs "$@")
  if [[ "$actual" == "$expected" ]]; then echo "$label: $actual"
  else echo "$label FAILED: got '$actual', expected '$expected'"; tail -30 /tmp/*.log; exit 1; fi
}

start template
expect "GET" "Hello from C#: GET /hello?name=x (0 bytes)" "http://127.0.0.1:8000/hello?name=x"
expect "POST" "Hello from C#: POST /orders (12 bytes)" http://127.0.0.1:8000/orders POST "twelve bytes"
stop

start httpclient
expect "HttpClient through Deno's fetch" "Order 42 totals 19.95" http://127.0.0.1:8000/
stop

start environment MODE=deno
expect "declared variables, MODE set by the process, UNDECLARED kept out" "declared deno unset" http://127.0.0.1:8000/
stop

start large
expect "1 MiB streamed" "1048576" http://127.0.0.1:8000/ GET "" size
stop
