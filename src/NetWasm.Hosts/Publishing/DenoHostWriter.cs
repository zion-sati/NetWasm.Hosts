// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;
using System.Text;

namespace NetWasm.Hosts.Publishing;

/// <summary>
/// Strategy for Deno (deno serve): main.mjs compiles the core modules once at startup and exports the
/// fetch handler. The component's environment is the declared variables, each replaced by the process's value of
/// the same name when it has one; no other variable of the process reaches the component.
/// </summary>
public sealed class DenoHostWriter : IServiceHostWriter
{
    public const string EntryFileName = "main.mjs";

    public string Host => "deno";

    public ImmutableArray<PackageFile> Write(ServiceHostInput input)
    {
        var component = input.Component;
        var entry = new StringBuilder()
            .Append("// Written by NetWasm.Hosts: Deno serving the ")
            .Append(Path.GetFileNameWithoutExtension(component.JavaScriptFile)).Append(" component.\n")
            .Append("import { createFetchServiceHost } from ").Append(JavaScriptText.Literal(ServiceLayout.HostEntryImport)).Append(";\n")
            .Append("import * as component from ").Append(JavaScriptText.Literal(ServiceLayout.ComponentImport(component.JavaScriptFile))).Append(";\n\n")
            .Append(JavaScriptText.CompiledCoreModules(component.CoreModuleFiles, "Deno.readFile", ServiceLayout.ComponentImport("")))
            .Append(JavaScriptText.Environment(input.Variables, "Deno.env.get(name)"))
            .Append("const host = createFetchServiceHost({ component, environment, modules });\n")
            .Append("// Deno keeps serving while an instance is released after its response.\n")
            .Append("const context = Object.freeze({ waitUntil: promise => promise.catch(error => console.error(error)) });\n\n")
            .Append("export default { fetch: request => host.fetch(request, env, context) };\n");
        return [new GeneratedFile(EntryFileName, entry.ToString())];
    }
}
