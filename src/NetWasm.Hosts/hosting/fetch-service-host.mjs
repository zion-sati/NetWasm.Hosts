// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

import { createIncomingRequestAdapter } from "./incoming-request-adapter.mjs";
import { createOutgoingResponseStreamer } from "./outgoing-response-streamer.mjs";
import { projectProviderModuleIdentity } from "./provider-module-identity.mjs";
import { createPerRequestInstances } from "./service-instance-policy.mjs";
import { createWasiFilesystem } from "./wasi-filesystem.mjs";
import { createWasiHttpFetch } from "./wasi-http-fetch.mjs";
import { createWasiPlatform } from "./wasi-platform.mjs";

const optionKeys = ["component", "environment", "modules"];

// A NetWasm wasi:http service component behind a Fetch API handler, the interface of Cloudflare
// Workers and other JavaScript isolates: each request reaches the component's handler as a wasi:http request and its
// response streams back; the component's own requests go out through the host's fetch.
// - component: the transpiled component's module (jco, async instantiation).
// - modules: its core modules by file name, compiled (workerd compiles no Wasm at run time, so a
//   Worker imports them precompiled).
// - environment: the variables the project declares, as { name, value }. The string values of the
//   env each request brings (a Worker's variables and secrets) join them, and
//   override a declared variable of the same name.
// fetch(request, env, ctx) answers a request; ctx.waitUntil keeps the instance's release running
// after the response is returned. The component sees no preopened directories.
export function createFetchServiceHost(options) {
  assertExactDataObject(options, optionKeys, "fetch service host options");
  const { component, modules } = options;
  if (component === null || typeof component !== "object" || typeof component.instantiate !== "function") {
    throw new TypeError("the transpiled service component is required");
  }
  if (modules === null || typeof modules !== "object"
      || Object.values(modules).some(module => !(module instanceof WebAssembly.Module))) {
    throw new TypeError("the service component's core modules must be compiled modules");
  }
  const declared = readEnvironment(options.environment);

  const http = createWasiHttpFetch(Object.freeze({ fetch: (input, init) => fetch(input, init) }));
  const types = http["wasi:http/types@0.3.2"];
  const platform = createWasiPlatform(Object.freeze({
    initialDirectory: null,
    network: Object.freeze({ modules: http }),
  }));
  const requests = createIncomingRequestAdapter(types);
  const responses = createOutgoingResponseStreamer(types);
  const instances = createPerRequestInstances(Object.freeze({
    createExecution(env) {
      const filesystem = createWasiFilesystem(Object.freeze({ hostFiles: null, preopens: Object.freeze([]) }));
      const wasi = platform(Object.freeze({
        arguments: Object.freeze([]),
        environment: environmentOf(declared, env),
        filesystem,
        grants: Object.freeze({ network: "allowAll" }),
        signal: null,
        stderr: consoleSink(text => console.error(text)),
        stdin: null,
        stdout: consoleSink(text => console.log(text)),
      }));
      return Object.freeze({
        imports: componentImports(wasi.imports),
        async release() {
          await wasi.release();
          await filesystem.dispose();
        },
      });
    },
    instantiate: imports => component.instantiate(name => coreModule(modules, name), imports),
  }));

  return Object.freeze({
    async fetch(incoming, env, ctx) {
      let instance;
      try {
        instance = await instances.acquire(env);
        const streamed = responses.stream(await instance.handle(requests.adapt(incoming)));
        ctx.waitUntil(streamed.done.then(() => instance.release()));
        return streamed.response;
      } catch (error) {
        if (instance !== undefined) await instance.release();
        console.error("The NetWasm service component failed to handle the request:", error);
        return new Response("Internal Server Error\n", { status: 500 });
      }
    },
  });
}

function readEnvironment(value) {
  if (!Array.isArray(value) || value.some(variable => variable === null || typeof variable !== "object"
      || typeof variable.name !== "string" || typeof variable.value !== "string")) {
    throw new TypeError("the declared environment must be a list of { name, value } variables");
  }
  return value.map(({ name, value: text }) => [name, text]);
}

function environmentOf(declared, env) {
  const variables = new Map(declared);
  for (const [name, value] of Object.entries(env ?? {})) {
    if (typeof value === "string") variables.set(name, value);
  }
  return Object.freeze(Array.from(variables, ([name, value]) => Object.freeze({ name, value })));
}

// jco names an import by its interface without the version.
function componentImports(imports) {
  const projected = Object.create(null);
  for (const [module, value] of Object.entries(imports)) {
    projected[projectProviderModuleIdentity(Object.freeze({ module })).componentModule] = value;
  }
  return Object.freeze(projected);
}

function coreModule(modules, name) {
  if (!Object.hasOwn(modules, name)) throw new TypeError(`the service component has no core module '${name}'`);
  return modules[name];
}

// Standard output and error reach the host's log as the guest writes them.
function consoleSink(log) {
  const decoder = new TextDecoder();
  return Object.freeze({ write: bytes => log(decoder.decode(bytes, { stream: true })) });
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
