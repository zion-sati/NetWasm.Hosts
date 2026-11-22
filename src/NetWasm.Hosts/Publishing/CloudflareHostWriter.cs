// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NetWasm.Hosts.Publishing;

/// <summary>
/// Strategy for Cloudflare Workers: worker.mjs imports each core module as a compiled module (wrangler uploads a
/// .wasm import precompiled, as workerd compiles no Wasm at run time) and exports the Worker; wrangler.json names
/// the Worker, its entry and its runtime date.
/// </summary>
public sealed class CloudflareHostWriter(IWorkerNameResolver names) : IServiceHostWriter
{
    public const string NameSetting = "NetWasmCloudflareWorkerName";
    public const string CompatibilityDateSetting = "NetWasmCloudflareCompatibilityDate";
    public const string EntryFileName = "worker.mjs";
    public const string ConfigFileName = "wrangler.json";

    public string Host => "cloudflare";

    public ImmutableArray<PackageFile> Write(ServiceHostInput input)
    {
        var date = input.Settings.GetValueOrDefault(CompatibilityDateSetting, "");
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            throw new ServiceHostException($"{CompatibilityDateSetting} '{date}' is not a date (yyyy-MM-dd).");
        }
        var name = names.Resolve(input.Settings.GetValueOrDefault(NameSetting, ""), input.AssemblyName);
        return
        [
            new GeneratedFile(EntryFileName, Entry(input)),
            new GeneratedFile(ConfigFileName, JsonSerializer.Serialize(
                new WranglerConfigDocument(name, EntryFileName, date),
                HostsJsonContext.Default.WranglerConfigDocument) + "\n"),
        ];
    }

    private static string Entry(ServiceHostInput input)
    {
        var component = input.Component;
        var entry = new StringBuilder()
            .Append("// Written by NetWasm.Hosts: the Worker serving the ")
            .Append(Path.GetFileNameWithoutExtension(component.JavaScriptFile)).Append(" component.\n")
            .Append("import { createFetchServiceHost } from ").Append(JavaScriptText.Literal(ServiceLayout.HostEntryImport)).Append(";\n")
            .Append("import * as component from ").Append(JavaScriptText.Literal(ServiceLayout.ComponentImport(component.JavaScriptFile))).Append(";\n");
        for (var index = 0; index < component.CoreModuleFiles.Length; index++)
        {
            entry.Append("import module").Append(index).Append(" from ")
                .Append(JavaScriptText.Literal(ServiceLayout.ComponentImport(component.CoreModuleFiles[index]))).Append(";\n");
        }
        entry.Append("\nexport default createFetchServiceHost({\n")
            .Append("  component,\n")
            .Append("  environment: ").Append(JavaScriptText.Variables(input.Variables, "  ")).Append(",\n")
            .Append("  modules: {\n");
        for (var index = 0; index < component.CoreModuleFiles.Length; index++)
        {
            entry.Append("    ").Append(JavaScriptText.Literal(component.CoreModuleFiles[index])).Append(": module")
                .Append(index).Append(",\n");
        }
        return entry.Append("  },\n});\n").ToString();
    }
}
