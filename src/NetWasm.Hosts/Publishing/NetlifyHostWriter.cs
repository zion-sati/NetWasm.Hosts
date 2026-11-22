// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;
using System.Text;

namespace NetWasm.Hosts.Publishing;

/// <summary>
/// Strategy for Netlify Edge Functions: netlify/edge-functions/service.js serves every path. Netlify's bundler
/// carries modules but no .wasm files, and an edge function has no file system, so each core module comes as a
/// module embedding it in base64, compiled once per isolate. netlify.toml names an empty publish directory. The
/// component's environment is the declared variables, each replaced by the site's value of the same name when it
/// has one.
/// </summary>
public sealed class NetlifyHostWriter : IServiceHostWriter
{
    public const string EntryFileName = "netlify/edge-functions/service.js";
    public const string ConfigFileName = "netlify.toml";
    public const string PublishDirectoryFileName = "public/.keep";

    public string Host => "netlify";

    public ImmutableArray<PackageFile> Write(ServiceHostInput input)
    {
        var component = input.Component;
        var entry = new StringBuilder()
            .Append("// Written by NetWasm.Hosts: a Netlify edge function serving the ")
            .Append(Path.GetFileNameWithoutExtension(component.JavaScriptFile)).Append(" component.\n")
            .Append("import { createFetchServiceHost } from ").Append(JavaScriptText.Literal(ServiceLayout.HostEntryImportFrom(2))).Append(";\n")
            .Append("import * as component from ").Append(JavaScriptText.Literal(ServiceLayout.ComponentImport(component.JavaScriptFile, 2))).Append(";\n");
        for (var index = 0; index < component.CoreModuleFiles.Length; index++)
        {
            entry.Append("import module").Append(index).Append(" from ")
                .Append(JavaScriptText.Literal(ServiceLayout.ComponentImport(EmbeddedName(component.CoreModuleFiles[index]), 2))).Append(";\n");
        }
        entry.Append("\n// Netlify's bundler carries modules but no .wasm files, so each core module comes embedded in base64.\n")
            .Append("const compile = base64 => WebAssembly.compile(Uint8Array.from(atob(base64), character => character.charCodeAt(0)));\n")
            .Append("const modules = {\n");
        for (var index = 0; index < component.CoreModuleFiles.Length; index++)
        {
            entry.Append("  ").Append(JavaScriptText.Literal(component.CoreModuleFiles[index]))
                .Append(": await compile(module").Append(index).Append("),\n");
        }
        entry.Append("};\n")
            .Append(JavaScriptText.Environment(input.Variables, "Netlify.env.get(name)"))
            .Append("const host = createFetchServiceHost({ component, environment, modules });\n")
            .Append("// The isolate keeps running while an instance is released after its response.\n")
            .Append("const context = Object.freeze({ waitUntil: promise => promise.catch(error => console.error(error)) });\n\n")
            .Append("export default request => host.fetch(request, env, context);\n")
            .Append("export const config = { path: \"/*\" };\n");
        return
        [
            new GeneratedFile(EntryFileName, entry.ToString()),
            .. component.CoreModuleFiles.Select(file => new EmbeddedModuleFile(
                ServiceLayout.ComponentDirectory + "/" + EmbeddedName(file), Path.Combine(input.ComponentDirectory, file))),
            new GeneratedFile(ConfigFileName, """
                # Written by NetWasm.Hosts. The edge function in netlify/edge-functions serves every path; the publish
                # directory holds no files of its own.
                [build]
                  publish = "public"

                """.ReplaceLineEndings("\n")),
            new GeneratedFile(PublishDirectoryFileName, ""),
        ];
    }

    private static string EmbeddedName(string coreModuleFile) => coreModuleFile + ".mjs";
}
