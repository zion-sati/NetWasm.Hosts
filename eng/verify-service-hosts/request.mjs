// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

// deno run --allow-net request.mjs <url> [method] [body] [size]: prints the response's body, or its size in bytes.
const [url, method = "GET", body, size] = Deno.args;
const response = await fetch(url, { method, body: body || undefined });
const bytes = new Uint8Array(await response.arrayBuffer());
await Deno.stdout.write(size === "size" ? new TextEncoder().encode(String(bytes.length)) : bytes);
