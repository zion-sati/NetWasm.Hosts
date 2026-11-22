// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Text.RegularExpressions;

namespace NetWasm.Hosts.Publishing;

/// <summary>Resolves the Worker's name.</summary>
public interface IWorkerNameResolver
{
    /// <summary>The requested name when there is one, else one made from the assembly name.</summary>
    string Resolve(string requested, string assemblyName);
}

/// <summary>
/// Cloudflare takes a Worker name of lower-case letters, digits and dashes, at most 63 characters, that starts and
/// ends with a letter or digit. A requested name must already be one; an assembly name becomes one.
/// </summary>
public sealed partial class WorkerNameResolver : IWorkerNameResolver
{
    private const int MaximumLength = 63;

    public string Resolve(string requested, string assemblyName)
    {
        if (!string.IsNullOrEmpty(requested))
        {
            return ValidName().IsMatch(requested)
                ? requested
                : throw new ServiceHostException(
                    $"NetWasmCloudflareWorkerName '{requested}' is not a Worker name: lower-case letters, digits and dashes, at most {MaximumLength} characters, starting and ending with a letter or digit.");
        }
        var name = Separators().Replace(assemblyName.ToLowerInvariant(), "-").Trim('-');
        if (name.Length > MaximumLength)
        {
            name = name[..MaximumLength].TrimEnd('-');
        }
        return name.Length != 0
            ? name
            : throw new ServiceHostException(
                $"The assembly name '{assemblyName}' makes no Worker name; set NetWasmCloudflareWorkerName.");
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex ValidName();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex Separators();
}
