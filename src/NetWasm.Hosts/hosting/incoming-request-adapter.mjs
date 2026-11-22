// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

import { fieldValueBytes } from "./field-value-text.mjs";
import { withoutForbiddenFields } from "./forbidden-header-filter.mjs";

// The methods wasi:http names. Fetch upper-cases the standard ones it receives; any other
// spelling, "patch" among them, is a method of its own.
const methods = new Map([
  ["GET", "get"], ["HEAD", "head"], ["POST", "post"], ["PUT", "put"], ["DELETE", "delete"],
  ["CONNECT", "connect"], ["OPTIONS", "options"], ["TRACE", "trace"], ["PATCH", "patch"],
]);

// A Fetch API request as the wasi:http request a service component handles: its method,
// scheme, authority, path and query, its headers but the forbidden ones, and its body streamed
// as the guest reads it. A Fetch request has no trailers.
export function createIncomingRequestAdapter(types) {
  if (types === null || typeof types !== "object"
      || typeof types.Fields?.fromList !== "function" || typeof types.Request?.new !== "function") {
    throw new TypeError("incoming request adapter requires the wasi:http types");
  }
  const { Fields, Request } = types;
  return Object.freeze({
    adapt(incoming) {
      const url = new URL(incoming.url);
      const headers = Fields.fromList(withoutForbiddenFields([...incoming.headers])
        .map(([name, value]) => [name, fieldValueBytes(value)]));
      expectOk(headers, "headers");
      const [request] = Request.new(
        headers.val,
        incoming.body === null ? undefined : requestBody(incoming.body),
        Promise.resolve(Object.freeze({ tag: "ok", val: undefined })),
        undefined);
      expectOk(request.setMethod(method(incoming.method)), "method");
      expectOk(request.setScheme(scheme(url.protocol)), "scheme");
      expectOk(request.setAuthority(url.host), "authority");
      expectOk(request.setPathWithQuery(url.pathname + url.search), "path");
      return request;
    },
  });
}

function method(name) {
  const known = methods.get(name);
  return Object.freeze(known === undefined ? { tag: "other", val: name } : { tag: known });
}

function scheme(protocol) {
  if (protocol === "http:") return Object.freeze({ tag: "HTTP" });
  if (protocol === "https:") return Object.freeze({ tag: "HTTPS" });
  return Object.freeze({ tag: "other", val: protocol.slice(0, -1) });
}

function expectOk(result, part) {
  if (result?.tag !== "ok") throw new TypeError(`incoming request ${part} are not valid wasi:http values`);
}

// The body's chunks as the guest reads them. A body the guest leaves unread costs nothing: the
// platform discards it with the request.
async function* requestBody(stream) {
  const reader = stream.getReader();
  for (;;) {
    const { done, value } = await reader.read();
    if (done) return;
    yield value;
  }
}
