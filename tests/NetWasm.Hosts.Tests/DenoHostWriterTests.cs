// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;
using NetWasm.Hosts.Publishing;

namespace NetWasm.Hosts.Tests;

public sealed class DenoHostWriterTests
{
    [Fact]
    public void TheEntryCompilesTheCoreModulesAtStartupAndPassesOnlyTheDeclaredVariables()
    {
        var file = Assert.IsType<GeneratedFile>(Assert.Single(new DenoHostWriter().Write(new(
            new("Hello.js", ["Hello.conditional-suspending-0.wasm", "Hello.core.wasm"]),
            "/obj/component",
            "Hello",
            [new("GREETING", "declared")],
            ImmutableDictionary<string, string>.Empty))));

        Assert.Equal("main.mjs", file.RelativePath);
        Assert.Equal(
            """
            // Written by NetWasm.Hosts: Deno serving the Hello component.
            import { createFetchServiceHost } from "./host/fetch-service-host.mjs";
            import * as component from "./gen/Hello.js";

            const coreModules = [
              "Hello.conditional-suspending-0.wasm",
              "Hello.core.wasm",
            ];
            const modules = Object.fromEntries(await Promise.all(coreModules.map(async name =>
              [name, await WebAssembly.compile(await Deno.readFile(new URL("./gen/" + name, import.meta.url)))])));
            const environment = [
              {
                "name": "GREETING",
                "value": "declared"
              }
            ];
            const env = Object.fromEntries(environment.flatMap(({ name }) => {
              const value = Deno.env.get(name);
              return value === undefined ? [] : [[name, value]];
            }));
            const host = createFetchServiceHost({ component, environment, modules });
            // Deno keeps serving while an instance is released after its response.
            const context = Object.freeze({ waitUntil: promise => promise.catch(error => console.error(error)) });

            export default { fetch: request => host.fetch(request, env, context) };

            """.ReplaceLineEndings("\n"),
            file.Text);
    }

    [Fact]
    public void TheHostIsNamedDeno() => Assert.Equal("deno", new DenoHostWriter().Host);
}
