// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

import assert from "node:assert/strict";
import test from "node:test";

import { createPerRequestInstances } from "./service-instance-policy.mjs";

const handlerExport = "wasi:http/handler@0.3.2";

function policy(root, events = []) {
  return createPerRequestInstances(Object.freeze({
    createExecution(environment) {
      events.push(["execution", environment]);
      return Object.freeze({ imports: Object.freeze({ marker: true }), release: async () => { events.push(["release"]); } });
    },
    async instantiate(imports) {
      events.push(["instantiate", imports.marker]);
      if (root instanceof Error) throw root;
      return root;
    },
  }));
}

test("the policy requires exactly its two actions", () => {
  for (const value of [null, [], {}, { createExecution() {} }, Object.create({ createExecution() {}, instantiate() {} }),
    { createExecution() {}, instantiate() {}, extra: 1 }, { [Symbol("x")]: 1 }]) {
    assert.throws(() => createPerRequestInstances(value), /service instance policy options/);
  }
  assert.throws(() => createPerRequestInstances({ createExecution: 1, instantiate() {} }), /actions are required/);
  assert.throws(() => createPerRequestInstances({ createExecution() {}, instantiate: 1 }), /actions are required/);
});

test("every request gets a new instance whose handler it calls and then releases", async () => {
  const events = [];
  const handled = [];
  const root = { [handlerExport]: { handle(request) { handled.push([this === root[handlerExport], request]); return "response"; } } };
  const instances = policy(root, events);
  const first = await instances.acquire("env");
  assert.equal(await first.handle("request"), "response");
  assert.deepEqual(handled, [[true, "request"]]);
  await first.release();
  await instances.acquire("env");
  assert.deepEqual(events, [
    ["execution", "env"], ["instantiate", true], ["release"], ["execution", "env"], ["instantiate", true]]);
});

test("a component without a wasi:http handler is released and refused", async () => {
  for (const root of [undefined, {}, { [handlerExport]: {} }]) {
    const events = [];
    await assert.rejects(policy(root, events).acquire("env"), /exports no wasi:http handler/);
    assert.deepEqual(events.at(-1), ["release"]);
  }
});

test("an instantiation that fails releases what the execution holds", async () => {
  const events = [];
  await assert.rejects(policy(new Error("trap"), events).acquire("env"), /trap/);
  assert.deepEqual(events.at(-1), ["release"]);
});
