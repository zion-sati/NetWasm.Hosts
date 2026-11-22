// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;
using System.Text;

namespace NetWasm.Hosts.Publishing;

/// <summary>
/// Strategy for Vercel Functions on the Node.js runtime: api/service.mjs compiles the core modules once per instance
/// and exports the fetch handler Vercel calls; vercel.json sends every path to it and ships the core modules with
/// it; package.json selects Node.js 24. The component's environment is the declared variables, each replaced by the
/// function's value of the same name when it has one.
/// </summary>
public sealed class VercelHostWriter : IServiceHostWriter
{
    public const string EntryFileName = "api/service.mjs";
    public const string ConfigFileName = "vercel.json";
    public const string PackageFileName = "package.json";

    public string Host => "vercel";

    public ImmutableArray<PackageFile> Write(ServiceHostInput input)
    {
        var component = input.Component;
        var entry = new StringBuilder()
            .Append("// Written by NetWasm.Hosts: a Vercel Function on Node.js serving the ")
            .Append(Path.GetFileNameWithoutExtension(component.JavaScriptFile)).Append(" component.\n")
            .Append("import { readFile } from \"node:fs/promises\";\n")
            .Append("import { createFetchServiceHost } from ").Append(JavaScriptText.Literal(ServiceLayout.HostEntryImportFrom(1))).Append(";\n")
            .Append("import * as component from ").Append(JavaScriptText.Literal(ServiceLayout.ComponentImport(component.JavaScriptFile, 1))).Append(";\n\n")
            .Append(JavaScriptText.CompiledCoreModules(component.CoreModuleFiles, "readFile", ServiceLayout.ComponentImport("", 1)))
            .Append(JavaScriptText.Environment(input.Variables, "process.env[name]"))
            .Append("const host = createFetchServiceHost({ component, environment, modules });\n")
            .Append("// The function keeps serving while an instance is released after its response.\n")
            .Append("const context = Object.freeze({ waitUntil: promise => promise.catch(error => console.error(error)) });\n\n")
            .Append("export default { fetch: request => host.fetch(request, env, context) };\n");
        return
        [
            new GeneratedFile(EntryFileName, entry.ToString()),
            new GeneratedFile(ConfigFileName, $$"""
                {
                  "rewrites": [{ "source": "/(.*)", "destination": "/api/service" }],
                  "functions": {
                    "{{EntryFileName}}": { "includeFiles": "{{ServiceLayout.ComponentDirectory}}/**" }
                  }
                }

                """.ReplaceLineEndings("\n")),
            new GeneratedFile(PackageFileName, """
                {
                  "private": true,
                  "engines": { "node": "24.x" }
                }

                """.ReplaceLineEndings("\n")),
        ];
    }
}
