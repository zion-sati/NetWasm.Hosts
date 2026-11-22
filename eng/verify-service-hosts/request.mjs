// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

// deno run --allow-net request.mjs <url> [method] [body] [size|status], or node request.mjs with the same arguments:
// prints the response's body, its size in bytes, or its status.
const args = globalThis.Deno ? Deno.args : process.argv.slice(2);
const [url, method = "GET", body, print] = args;
const response = await fetch(url, { method, body: body || undefined });
const bytes = new Uint8Array(await response.arrayBuffer());
const text = { size: String(bytes.length), status: String(response.status) }[print];
const output = text === undefined ? bytes : new TextEncoder().encode(text);
if (globalThis.Deno) await Deno.stdout.write(output);
else process.stdout.write(output);
