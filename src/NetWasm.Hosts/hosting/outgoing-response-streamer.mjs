// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

import { fieldValueText } from "./field-value-text.mjs";

// The statuses a Fetch response carries no body with.
const nullBodyStatuses = new Set([101, 103, 204, 205, 304]);
const ok = Object.freeze({ tag: "ok", val: undefined });
// A settled future nobody else reads: its failure has nowhere to go.
const ignore = () => {};
const internalError = message => Object.freeze({
  tag: "err",
  val: Object.freeze({ tag: "internal-error", val: message }),
});

// A service component's wasi:http response as a Fetch API response. The body is a stream that
// pulls from the component's body as the client reads it, so the response starts before the
// component has written all of it. Fetch responses carry no trailers. `done` settles once the
// body has ended, failed or been cancelled: the component's instance is needed until then.
export function createOutgoingResponseStreamer(types) {
  if (types === null || typeof types !== "object" || typeof types.Response?.consumeBody !== "function") {
    throw new TypeError("outgoing response streamer requires the wasi:http types");
  }
  return Object.freeze({
    stream(response) {
      const status = response.getStatusCode();
      const headers = new Headers();
      for (const [name, value] of response.getHeaders().copyAll()) headers.append(name, fieldValueText(value));

      let settle;
      const done = new Promise(resolve => { settle = resolve; });
      const [body, trailers] = types.Response.consumeBody(response, done);
      Promise.resolve(trailers).catch(ignore);
      const iterator = body[Symbol.asyncIterator]();
      if (nullBodyStatuses.has(status)) {
        settle(ok);
        Promise.resolve(iterator.return?.()).catch(ignore);
        return Object.freeze({ response: new Response(null, { status, headers }), done });
      }

      const readable = new ReadableStream({
        async pull(controller) {
          let next;
          try {
            next = await iterator.next();
          } catch (error) {
            settle(internalError("the response body failed"));
            controller.error(error);
            return;
          }
          if (next.done) {
            settle(ok);
            controller.close();
          } else {
            controller.enqueue(next.value);
          }
        },
        async cancel() {
          settle(internalError("the client cancelled the response body"));
          await iterator.return?.();
        },
      }, { highWaterMark: 0 });
      return Object.freeze({ response: new Response(readable, { status, headers }), done });
    },
  });
}
