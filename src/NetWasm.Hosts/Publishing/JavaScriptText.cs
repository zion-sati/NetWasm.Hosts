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
