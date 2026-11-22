// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

import { app } from "@azure/functions";
import { open } from "./Functions.node.mjs";

// One instance per worker: opening starts while the worker loads the app, and every request reuses it.
const opened = open();

// Every method and path; host.json drops the default "api" route prefix.
app.http("hello", {
  methods: ["GET", "POST", "PUT", "DELETE", "PATCH"],
  authLevel: "anonymous",
  route: "{*path}",
  handler: async request => {
    const service = await opened;
    return {
      headers: { "content-type": "text/plain; charset=utf-8" },
      body: await service.handle(request.method, new URL(request.url).pathname, await request.text()),
    };
  },
});
