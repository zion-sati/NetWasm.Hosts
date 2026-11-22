// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

import { open } from "./Functions.node.mjs";

// One instance per execution environment: opened while Lambda initializes the function, reused by every request.
const app = await open();

// A function URL or API Gateway HTTP API request (payload format 2.0).
export const handler = async event => {
  const body = event.body === undefined ? ""
    : event.isBase64Encoded ? Buffer.from(event.body, "base64").toString("utf8")
    : event.body;
  return {
    statusCode: 200,
    headers: { "content-type": "text/plain; charset=utf-8" },
    body: await app.handle(event.requestContext.http.method, event.rawPath, body),
  };
};
