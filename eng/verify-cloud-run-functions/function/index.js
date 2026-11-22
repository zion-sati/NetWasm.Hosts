// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

import * as functions from "@google-cloud/functions-framework";
import { open } from "./Functions.node.mjs";

// One instance per container: opening starts while the framework loads the function, and every request reuses it.
const opened = open();

// The framework has read the body by the time the function runs; rawBody holds its bytes when there is one.
functions.http("hello", async (request, response) => {
  const service = await opened;
  const body = request.rawBody?.toString("utf8") ?? "";
  response.type("text/plain").send(await service.handle(request.method, request.path, body));
});
