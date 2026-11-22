// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;
using NetWasm.Hosts.Publishing;

namespace NetWasm.Hosts.Tests;

public sealed class NetlifyHostWriterTests
{
    private static ImmutableArray<PackageFile> Write() => new NetlifyHostWriter().Write(new(
        new("Hello.js", ["Hello.conditional-suspending-0.wasm", "Hello.core.wasm"]),
        "/obj/component",
        "Hello",
        [new("GREETING", "declared")],
        ImmutableDictionary<string, string>.Empty));

    [Fact]
    public void TheEdgeFunctionCompilesTheEmbeddedCoreModulesAndServesEveryPath()
    {
        var files = Write();

        Assert.Equal(
            ["netlify/edge-functions/service.js", "gen/Hello.conditional-suspending-0.wasm.mjs", "gen/Hello.core.wasm.mjs", "netlify.toml", "public/.keep"],
            files.Select(file => file.RelativePath));
        Assert.Equal(
            """
            // Written by NetWasm.Hosts: a Netlify edge function serving the Hello component.
            import { createFetchServiceHost } from "../../host/fetch-service-host.mjs";
            import * as component from "../../gen/Hello.js";
            import module0 from "../../gen/Hello.conditional-suspending-0.wasm.mjs";
            import module1 from "../../gen/Hello.core.wasm.mjs";

            // Netlify's bundler carries modules but no .wasm files, so each core module comes embedded in base64.
            const compile = base64 => WebAssembly.compile(Uint8Array.from(atob(base64), character => character.charCodeAt(0)));
            const modules = {
              "Hello.conditional-suspending-0.wasm": await compile(module0),
              "Hello.core.wasm": await compile(module1),
            };
            const environment = [
              {
                "name": "GREETING",
                "value": "declared"
              }
            ];
            const env = Object.fromEntries(environment.flatMap(({ name }) => {
              const value = Netlify.env.get(name);
              return value === undefined ? [] : [[name, value]];
            }));
            const host = createFetchServiceHost({ component, environment, modules });
            // The isolate keeps running while an instance is released after its response.
            const context = Object.freeze({ waitUntil: promise => promise.catch(error => console.error(error)) });

            export default request => host.fetch(request, env, context);
            export const config = { path: "/*" };

            """.ReplaceLineEndings("\n"),
            Assert.IsType<GeneratedFile>(files[0]).Text);
    }

    [Fact]
    public void EachCoreModuleIsEmbeddedFromTheComponentsDirectory()
    {
        var files = Write();

        Assert.Equal(new EmbeddedModuleFile("gen/Hello.conditional-suspending-0.wasm.mjs", Path.Combine("/obj/component", "Hello.conditional-suspending-0.wasm")), files[1]);
        Assert.Equal(new EmbeddedModuleFile("gen/Hello.core.wasm.mjs", Path.Combine("/obj/component", "Hello.core.wasm")), files[2]);
    }

    [Fact]
    public void TheSiteConfigNamesAnEmptyPublishDirectory()
    {
        var files = Write();

        Assert.Equal(
            """
            # Written by NetWasm.Hosts. The edge function in netlify/edge-functions serves every path; the publish
            # directory holds no files of its own.
            [build]
              publish = "public"

            """.ReplaceLineEndings("\n"),
            Assert.IsType<GeneratedFile>(files[3]).Text);
        Assert.Equal(new GeneratedFile("public/.keep", ""), files[4]);
    }

    [Fact]
    public void TheHostIsNamedNetlify() => Assert.Equal("netlify", new NetlifyHostWriter().Host);
}
