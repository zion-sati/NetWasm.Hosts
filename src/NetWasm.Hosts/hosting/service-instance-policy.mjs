// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

const handlerExport = "wasi:http/handler@0.3.2";
const optionKeys = ["createExecution", "instantiate"];

// A new instance of the service component for every request: it handles that one request and
// is released once its response has been sent. Reusing an instance across requests waits on
// evidence that concurrent handle calls are safe and that reuse pays.
export function createPerRequestInstances(options) {
  assertExactDataObject(options, optionKeys, "service instance policy options");
  if (typeof options.createExecution !== "function" || typeof options.instantiate !== "function") {
    throw new TypeError("service instance policy actions are required");
  }
  const { createExecution, instantiate } = options;
  return Object.freeze({
    // The handler of an instance for one request, and the release of everything it holds.
    async acquire(environment) {
      const execution = await createExecution(environment);
      try {
        const root = await instantiate(execution.imports);
        const handler = root?.[handlerExport];
        if (typeof handler?.handle !== "function") {
          throw new TypeError("the service component exports no wasi:http handler");
        }
        return Object.freeze({ handle: request => handler.handle(request), release: execution.release });
      } catch (error) {
        await execution.release();
        throw error;
      }
    },
  });
}

function assertExactDataObject(value, keys, label) {
  if (value === null || typeof value !== "object" || Array.isArray(value)
      || Object.getOwnPropertySymbols(value).length !== 0) {
    throw new TypeError(`${label} is invalid`);
  }
  const prototype = Object.getPrototypeOf(value);
  const descriptors = Object.getOwnPropertyDescriptors(value);
  const actualKeys = Object.keys(descriptors).sort();
  if (prototype !== null && prototype !== Object.prototype
      || actualKeys.length !== keys.length
      || actualKeys.some((key, index) => key !== keys[index])
      || Object.values(descriptors).some(descriptor => !descriptor.enumerable || !("value" in descriptor))) {
    throw new TypeError(`${label} shape is invalid`);
  }
}
