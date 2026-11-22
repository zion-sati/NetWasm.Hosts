// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

import assert from "node:assert/strict";
import test from "node:test";

import { createOutgoingResponseStreamer } from "./outgoing-response-streamer.mjs";
import { createWasiHttpFetch } from "./wasi-http-fetch.mjs";

const encoder = new TextEncoder();
const decoder = new TextDecoder();
const types = createWasiHttpFetch(Object.freeze({ fetch: async () => new Response("") }))["wasi:http/types@0.3.2"];
const streamer = createOutgoingResponseStreamer(types);
const ok = { tag: "ok", val: undefined };

// A response the guest built with the wasi:http types, its body the chunks it writes.
function guestResponse(status, headers, contents) {
  const fields = types.Fields.fromList(headers.map(([name, value]) => [name, typeof value === "string" ? encoder.encode(value) : value]));
  const [response] = types.Response.new(fields.val, contents, Promise.resolve(ok));
  assert.deepEqual(response.setStatusCode(status), ok);
  return response;
}

// A response whose body the test controls, through types that hand it over as is.
function controlledResponse(status, body, trailers = Promise.resolve(ok)) {
  const consumed = [];
  const fake = Object.freeze({
    Response: Object.freeze({
      consumeBody(response, res) {
        consumed.push(res);
        return [body, trailers];
      },
    }),
  });
  const response = { getStatusCode: () => status, getHeaders: () => ({ copyAll: () => [] }) };
  return { streamed: createOutgoingResponseStreamer(fake).stream(response), consumed };
}

function iterable(iterator) {
  return { [Symbol.asyncIterator]: () => iterator };
}

test("the streamer requires the wasi:http types", () => {
  for (const value of [null, {}, { Response: {} }]) {
    assert.throws(() => createOutgoingResponseStreamer(value), /requires the wasi:http types/);
  }
});

test("a guest's response reaches the client with its status, headers and body", async () => {
  const response = guestResponse(201, [["content-type", "text/plain"], ["x-name", [0x63, 0x61, 0x66, 0xe9]]],
    { async *[Symbol.asyncIterator]() { yield encoder.encode("hello, "); yield encoder.encode("world"); } });
  const { response: sent, done } = streamer.stream(response);
  assert.equal(sent.status, 201);
  assert.equal(sent.headers.get("content-type"), "text/plain");
  assert.equal(sent.headers.get("x-name"), "café");
  assert.equal(await sent.text(), "hello, world");
  assert.deepEqual(await done, ok);
});

test("the response starts before the guest has written all of its body", async () => {
  let finish;
  const rest = new Promise(resolve => { finish = resolve; });
  const response = guestResponse(200, [],
    { async *[Symbol.asyncIterator]() { yield encoder.encode("first"); await rest; yield encoder.encode("last"); } });
  const { response: sent, done } = streamer.stream(response);
  const reader = sent.body.getReader();
  assert.equal(decoder.decode((await reader.read()).value), "first");
  finish();
  assert.equal(decoder.decode((await reader.read()).value), "last");
  assert.equal((await reader.read()).done, true);
  assert.deepEqual(await done, ok);
});

test("a guest response without a body is an empty one", async () => {
  const { response: sent, done } = streamer.stream(guestResponse(200, [], undefined));
  assert.equal(await sent.text(), "");
  assert.deepEqual(await done, ok);
});

test("a status that carries no body sends none and ends the guest's body", async () => {
  let returned = false;
  const { streamed, consumed } = controlledResponse(204, iterable({
    next: () => assert.fail("a 204 body is not read"),
    return: async () => { returned = true; return { done: true }; },
  }));
  assert.equal(streamed.response.status, 204);
  assert.equal(streamed.response.body, null);
  assert.deepEqual(await streamed.done, ok);
  assert.equal(consumed[0], streamed.done);
  assert.equal(returned, true);
});

test("a body iterator that cannot be ended early is left alone", async () => {
  const { streamed } = controlledResponse(304, iterable({ next: () => assert.fail("a 304 body is not read") }));
  assert.equal(streamed.response.body, null);
  assert.deepEqual(await streamed.done, ok);
});

test("trailers a Fetch response cannot carry are dropped, even when they fail", async () => {
  const { streamed } = controlledResponse(200, iterable({ next: async () => ({ done: true }) }),
    Promise.reject(new Error("trailers failed")));
  assert.equal(await streamed.response.text(), "");
  assert.deepEqual(await streamed.done, ok);
});

test("a body that fails errors the client's read", async () => {
  const { streamed } = controlledResponse(200, iterable({ next: async () => { throw new Error("guest trapped"); } }));
  await assert.rejects(streamed.response.text(), /guest trapped/);
  assert.deepEqual(await streamed.done,
    { tag: "err", val: { tag: "internal-error", val: "the response body failed" } });
});

test("a client that cancels ends the guest's body", async () => {
  let returned = false;
  const { streamed } = controlledResponse(200, iterable({
    next: async () => ({ done: false, value: encoder.encode("x") }),
    return: async () => { returned = true; return { done: true }; },
  }));
  await streamed.response.body.cancel();
  assert.deepEqual(await streamed.done,
    { tag: "err", val: { tag: "internal-error", val: "the client cancelled the response body" } });
  assert.equal(returned, true);
});

test("a client may cancel a body whose iterator cannot be ended early", async () => {
  const { streamed } = controlledResponse(200, iterable({ next: async () => ({ done: false, value: encoder.encode("x") }) }));
  await streamed.response.body.cancel();
  assert.equal((await streamed.done).tag, "err");
});
