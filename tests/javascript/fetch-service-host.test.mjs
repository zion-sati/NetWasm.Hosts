// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

import assert from "node:assert/strict";
import test from "node:test";

import { createFetchServiceHost } from "./fetch-service-host.mjs";

const encoder = new TextEncoder();
const decoder = new TextDecoder();
const ok = { tag: "ok", val: undefined };
const core = new WebAssembly.Module(Uint8Array.of(0x00, 0x61, 0x73, 0x6d, 0x01, 0x00, 0x00, 0x00));
const modules = Object.freeze({ "service.core.wasm": core });

function guestStream(chunks) {
  return { async *[Symbol.asyncIterator]() { for (const chunk of chunks) yield encoder.encode(chunk); } };
}

// A transpiled component as jco instantiates it: core modules through getCoreModule, imports by
// unversioned interface name, the handler export by versioned name.
function component(handle, seen = {}) {
  return Object.freeze({
    async instantiate(getCoreModule, imports) {
      seen.core = getCoreModule("service.core.wasm");
      seen.imports = Object.keys(imports);
      return { "wasi:http/handler@0.3.2": { handle: request => handle(request, imports) } };
    },
  });
}

function worker(handle, { environment = [], seen } = {}) {
  return createFetchServiceHost(Object.freeze({ component: component(handle, seen), environment, modules }));
}

function context() {
  const pending = [];
  return { pending, ctx: { waitUntil: promise => pending.push(promise) } };
}

// A service that echoes the request and environment, and relays an upstream call.
async function echo(request, imports) {
  const { Fields, Request, Response } = imports["wasi:http/types"];
  const [chunks] = Request.consumeBody(request, Promise.resolve(ok));
  let body = "";
  for await (const chunk of chunks) body += decoder.decode(chunk);
  await imports["wasi:cli/stdout"].writeViaStream(guestStream(["handled ", request.getPathWithQuery()]));
  await imports["wasi:cli/stderr"].writeViaStream(guestStream(["no errors"]));

  const [upstream] = Request.new(new Fields(), undefined, Promise.resolve(ok), undefined);
  upstream.setScheme({ tag: "HTTPS" });
  upstream.setAuthority("upstream.test");
  upstream.setPathWithQuery("/orders");
  const relayed = await imports["wasi:http/client"].send(upstream);
  const [upstreamBody] = Response.consumeBody(relayed.val, Promise.resolve(ok));
  let upstreamText = "";
  for await (const chunk of upstreamBody) upstreamText += decoder.decode(chunk);

  const environment = imports["wasi:cli/environment"].getEnvironment().map(([name, value]) => `${name}=${value}`);
  const headers = Fields.fromList([["x-method", encoder.encode(request.getMethod().tag)]]);
  const [response] = Response.new(headers.val,
    guestStream([body, "|", upstreamText, "|", environment.join(",")]), Promise.resolve(ok));
  response.setStatusCode(202);
  return response;
}

test("the host requires its component, core modules and declared environment", () => {
  const valid = { component: component(echo), environment: [], modules };
  for (const options of [null, {}, { ...valid, extra: 1 }]) {
    assert.throws(() => createFetchServiceHost(options), /fetch service host options/);
  }
  for (const value of [null, {}, { instantiate: 1 }]) {
    assert.throws(() => createFetchServiceHost({ ...valid, component: value }), /transpiled service component/);
  }
  for (const value of [null, { "a.wasm": new Uint8Array(8) }]) {
    assert.throws(() => createFetchServiceHost({ ...valid, modules: value }), /must be compiled modules/);
  }
  for (const value of [null, [null], [{ name: "A" }], [{ name: 1, value: "x" }]]) {
    assert.throws(() => createFetchServiceHost({ ...valid, environment: value }), /declared environment/);
  }
});

test("a request reaches the component's handler and its response streams back", async t => {
  const logged = [];
  t.mock.method(console, "log", text => logged.push(["log", text]));
  t.mock.method(console, "error", text => logged.push(["error", text]));
  t.mock.method(globalThis, "fetch", async input => new Response(`upstream ${new URL(input).pathname}`));
  const seen = {};
  const { pending, ctx } = context();
  const response = await worker(echo, {
    environment: [{ name: "A", value: "1" }, { name: "B", value: "2" }],
    seen,
  }).fetch(new Request("https://svc.test/items?id=4", { method: "POST", body: "payload" }),
    { B: "3", C: "4", ORDERS: { binding: true } }, ctx);

  assert.equal(response.status, 202);
  assert.equal(response.headers.get("x-method"), "post");
  assert.equal(await response.text(), "payload|upstream /orders|A=1,B=3,C=4");
  await Promise.all(pending);
  assert.equal(pending.length, 1);
  assert.equal(seen.core, core);
  assert.ok(seen.imports.includes("wasi:http/types") && seen.imports.includes("wasi:cli/environment"));
  assert.ok(seen.imports.every(name => !name.includes("@")));
  assert.deepEqual(logged, [["log", "handled "], ["log", "/items?id=4"], ["error", "no errors"]]);
});

test("a Worker without variables gives the component only the declared ones", async () => {
  const { ctx } = context();
  const environment = async (request, imports) => {
    const { Fields, Response } = imports["wasi:http/types"];
    const pairs = imports["wasi:cli/environment"].getEnvironment().map(pair => pair.join("="));
    return Response.new(new Fields(), guestStream([pairs.join(",")]), Promise.resolve(ok))[0];
  };
  const response = await worker(environment, { environment: [{ name: "MODE", value: "service" }] })
    .fetch(new Request("http://svc.test/"), undefined, ctx);
  assert.equal(await response.text(), "MODE=service");
});

test("a component that fails answers 500 and is released", async t => {
  const errors = [];
  t.mock.method(console, "error", (...values) => errors.push(values));
  const response = await worker(async () => { throw new Error("guest trapped"); })
    .fetch(new Request("http://svc.test/"), {}, context().ctx);
  assert.equal(response.status, 500);
  assert.equal(await response.text(), "Internal Server Error\n");
  assert.match(errors[0][0], /failed to handle the request/);
  assert.match(errors[0][1].message, /guest trapped/);
});

test("a component whose core module is missing answers 500", async t => {
  const errors = [];
  t.mock.method(console, "error", (...values) => errors.push(values));
  const missing = Object.freeze({
    async instantiate(getCoreModule) { getCoreModule("other.core.wasm"); },
  });
  const response = await createFetchServiceHost(Object.freeze({ component: missing, environment: [], modules }))
    .fetch(new Request("http://svc.test/"), {}, context().ctx);
  assert.equal(response.status, 500);
  assert.match(errors[0][1].message, /no core module 'other.core.wasm'/);
});
