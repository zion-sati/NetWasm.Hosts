// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

import assert from "node:assert/strict";
import test from "node:test";

import { createIncomingRequestAdapter } from "./incoming-request-adapter.mjs";
import { createWasiHttpFetch } from "./wasi-http-fetch.mjs";

const encoder = new TextEncoder();
const decoder = new TextDecoder();
const types = createWasiHttpFetch(Object.freeze({ fetch: async () => new Response("") }))["wasi:http/types@0.3.2"];
const adapter = createIncomingRequestAdapter(types);

function incoming(url, { method = "GET", headers = [], body = null } = {}) {
  return { url, method, headers: new Headers(headers), body };
}

function source(chunks) {
  let index = 0;
  return new ReadableStream({
    pull(controller) {
      if (index === chunks.length) controller.close();
      else controller.enqueue(encoder.encode(chunks[index++]));
    },
  }, { highWaterMark: 0 });
}

function body(request) {
  return types.Request.consumeBody(request, Promise.resolve({ tag: "ok", val: undefined }));
}

test("the adapter requires the wasi:http types", () => {
  for (const value of [null, {}, { Fields: types.Fields }, { Request: types.Request }]) {
    assert.throws(() => createIncomingRequestAdapter(value), /requires the wasi:http types/);
  }
});

test("a request's method, scheme, authority, path and query reach the guest", () => {
  const request = adapter.adapt(incoming("https://api.example.test:8443/orders/7?expand=items#top"));
  assert.deepEqual(request.getMethod(), { tag: "get" });
  assert.deepEqual(request.getScheme(), { tag: "HTTPS" });
  assert.equal(request.getAuthority(), "api.example.test:8443");
  assert.equal(request.getPathWithQuery(), "/orders/7?expand=items");
});

test("headers reach the guest as Latin-1 bytes, without the forbidden ones", () => {
  const request = adapter.adapt(incoming("http://a.test/", {
    headers: [["host", "a.test"], ["connection", "keep-alive"], ["x-name", "café"], ["accept", "text/plain"]],
  }));
  assert.deepEqual(request.getScheme(), { tag: "HTTP" });
  assert.deepEqual(
    request.getHeaders().copyAll().map(([name, value]) => [name, [...value]]),
    [["accept", [...encoder.encode("text/plain")]], ["x-name", [0x63, 0x61, 0x66, 0xe9]]]);
});

test("standard methods map to wasi:http's, any other spelling is its own method", () => {
  const methodOf = method => adapter.adapt(incoming("http://a.test/", { method })).getMethod();
  assert.deepEqual(methodOf("PATCH"), { tag: "patch" });
  assert.deepEqual(methodOf("DELETE"), { tag: "delete" });
  assert.deepEqual(methodOf("patch"), { tag: "other", val: "patch" });
  assert.deepEqual(methodOf("PURGE"), { tag: "other", val: "PURGE" });
});

test("a scheme other than HTTP or HTTPS reaches the guest by name", () => {
  assert.deepEqual(adapter.adapt(incoming("ftp://files.test/a")).getScheme(), { tag: "other", val: "ftp" });
});

test("a request without a body has an empty one and no trailers", async () => {
  const [chunks, trailers] = body(adapter.adapt(incoming("http://a.test/")));
  const read = [];
  for await (const chunk of chunks) read.push(chunk);
  assert.deepEqual(read, []);
  assert.deepEqual(await trailers, { tag: "ok", val: undefined });
});

test("a body streams to the guest as it reads it", async () => {
  const request = adapter.adapt(incoming("http://a.test/", { method: "POST", body: source(["pay", "load"]) }));
  const [chunks] = body(request);
  let text = "";
  for await (const chunk of chunks) text += decoder.decode(chunk);
  assert.equal(text, "payload");
});

test("a body that fails fails the guest's read", async () => {
  const failing = new ReadableStream({ pull(controller) { controller.error(new Error("connection reset")); } });
  const [chunks] = body(adapter.adapt(incoming("http://a.test/", { method: "PUT", body: failing })));
  await assert.rejects(async () => { for await (const _ of chunks); }, /connection reset/);
});

test("headers wasi:http refuses fail the adaptation", () => {
  const request = { url: "http://a.test/", method: "GET", headers: [["bad name", "x"]], body: null };
  assert.throws(() => adapter.adapt(request), /incoming request headers are not valid wasi:http values/);
});
