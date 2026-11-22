// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetWasm.Hosts.Publishing;

/// <summary>Text the entry writers put into JavaScript: string literals and the declared variables.</summary>
internal static class JavaScriptText
{
    public static string Literal(string text) => JsonSerializer.Serialize(text, HostsJsonContext.Default.String);

    /// <summary>
    /// `modules`: every core module read with the host's file reader from the component's directory, as a URL
    /// relative to the entry, and compiled once.
    /// </summary>
    public static string CompiledCoreModules(ImmutableArray<string> files, string reader, string directory)
    {
        var text = new System.Text.StringBuilder("const coreModules = [\n");
        foreach (var file in files)
        {
            text.Append("  ").Append(Literal(file)).Append(",\n");
        }
        return text.Append("];\n")
            .Append("const modules = Object.fromEntries(await Promise.all(coreModules.map(async name =>\n")
            .Append("  [name, await WebAssembly.compile(await ").Append(reader).Append("(new URL(")
            .Append(Literal(directory)).Append(" + name, import.meta.url)))])));\n")
            .ToString();
    }

    /// <summary>
    /// The declared variables as `environment`, and as `env` each one the host has a value for, read with the
    /// host's lookup of `name`; no other variable of the host reaches the component.
    /// </summary>
    public static string Environment(ImmutableArray<EnvironmentVariable> variables, string lookup) =>
        "const environment = " + Variables(variables, "") + ";\n"
        + "const env = Object.fromEntries(environment.flatMap(({ name }) => {\n"
        + "  const value = " + lookup + ";\n"
        + "  return value === undefined ? [] : [[name, value]];\n"
        + "}));\n";

    public static string Variables(ImmutableArray<EnvironmentVariable> variables, string indent) =>
        JsonSerializer.Serialize(variables.ToArray(), HostsJsonContext.Default.EnvironmentVariableArray)
            .ReplaceLineEndings("\n" + indent);
}

/// <summary>wrangler.json, as much of it as the publish writes.</summary>
internal sealed record WranglerConfigDocument(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("main")] string Main,
    [property: JsonPropertyName("compatibility_date")] string CompatibilityDate);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WranglerConfigDocument))]
[JsonSerializable(typeof(EnvironmentVariable[]))]
[JsonSerializable(typeof(string))]
internal sealed partial class HostsJsonContext : JsonSerializerContext;
