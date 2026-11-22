// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

// node vercel-runtime.mjs <the publish's vercel directory> <port>: serves every request through the function's fetch
// export, as Vercel's Node.js runtime calls it, with a standard Request and the response streamed back. It stands in
// for Vercel, which needs an account even for vercel dev.
import { createServer } from "node:http";
import { Readable } from "node:stream";
import { pathToFileURL } from "node:url";
import { join } from "node:path";

const [directory, port] = process.argv.slice(2);
const { default: handler } = await import(pathToFileURL(join(directory, "api", "service.mjs")).href);

createServer(async (incoming, outgoing) => {
  try {
    const hasBody = incoming.method !== "GET" && incoming.method !== "HEAD";
    const request = new Request(new URL(incoming.url, `http://${incoming.headers.host}`), {
      method: incoming.method,
      headers: Object.entries(incoming.headers).flatMap(([name, value]) =>
        Array.isArray(value) ? value.map(item => [name, item]) : [[name, value]]),
      body: hasBody ? Readable.toWeb(incoming) : undefined,
      duplex: hasBody ? "half" : undefined,
    });
    const response = await handler.fetch(request);
    outgoing.writeHead(response.status, [...response.headers].flat());
    if (response.body) {
      for await (const chunk of response.body) outgoing.write(chunk);
    }
    outgoing.end();
  } catch (error) {
    console.error(error);
    outgoing.writeHead(500).end(String(error));
  }
}).listen(Number(port), "127.0.0.1");
