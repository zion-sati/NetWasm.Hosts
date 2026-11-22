// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;
using NetWasm.Hosts.Publishing;

namespace NetWasm.Hosts.Tests;

public sealed class VercelHostWriterTests
{
    private static ImmutableArray<PackageFile> Write() => new VercelHostWriter().Write(new(
        new("Hello.js", ["Hello.conditional-suspending-0.wasm", "Hello.core.wasm"]),
        "/obj/component",
        "Hello",
        [new("GREETING", "declared")],
        ImmutableDictionary<string, string>.Empty));

    [Fact]
    public void TheFunctionCompilesTheCoreModulesOnceAndExportsTheFetchHandler()
    {
        var files = Write();

        Assert.Equal(["api/service.mjs", "vercel.json", "package.json"], files.Select(file => file.RelativePath));
        Assert.Equal(
            """
            // Written by NetWasm.Hosts: a Vercel Function on Node.js serving the Hello component.
            import { readFile } from "node:fs/promises";
            import { createFetchServiceHost } from "../host/fetch-service-host.mjs";
            import * as component from "../gen/Hello.js";

            const coreModules = [
              "Hello.conditional-suspending-0.wasm",
              "Hello.core.wasm",
            ];
            const modules = Object.fromEntries(await Promise.all(coreModules.map(async name =>
              [name, await WebAssembly.compile(await readFile(new URL("../gen/" + name, import.meta.url)))])));
            const environment = [
              {
                "name": "GREETING",
                "value": "declared"
              }
            ];
            const env = Object.fromEntries(environment.flatMap(({ name }) => {
              const value = process.env[name];
              return value === undefined ? [] : [[name, value]];
            }));
            const host = createFetchServiceHost({ component, environment, modules });
            // The function keeps serving while an instance is released after its response.
            const context = Object.freeze({ waitUntil: promise => promise.catch(error => console.error(error)) });

            export default { fetch: request => host.fetch(request, env, context) };

            """.ReplaceLineEndings("\n"),
            Assert.IsType<GeneratedFile>(files[0]).Text);
    }

    [Fact]
    public void EveryPathGoesToTheFunctionWhichShipsWithTheCoreModulesOnNodeJs24()
    {
        var files = Write();

        Assert.Equal(
            """
            {
              "rewrites": [{ "source": "/(.*)", "destination": "/api/service" }],
              "functions": {
                "api/service.mjs": { "includeFiles": "gen/**" }
              }
            }

            """.ReplaceLineEndings("\n"),
            Assert.IsType<GeneratedFile>(files[1]).Text);
        Assert.Equal(
            """
            {
              "private": true,
              "engines": { "node": "24.x" }
            }

            """.ReplaceLineEndings("\n"),
            Assert.IsType<GeneratedFile>(files[2]).Text);
    }

    [Fact]
    public void TheHostIsNamedVercel() => Assert.Equal("vercel", new VercelHostWriter().Host);
}
