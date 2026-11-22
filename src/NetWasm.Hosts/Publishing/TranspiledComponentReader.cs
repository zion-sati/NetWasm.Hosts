// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;

namespace NetWasm.Hosts.Publishing;

/// <summary>Reads what jco wrote for a component.</summary>
public interface ITranspiledComponentReader
{
    /// <summary>The component's JavaScript module and core modules in the directory.</summary>
    TranspiledComponent Read(string directory, string componentName);
}

/// <summary>
/// jco writes the component's module (&lt;name&gt;.js) and the core modules it loads: the component's own
/// (&lt;name&gt;.core*.wasm) and the trampolines for suspending calls (&lt;name&gt;.conditional-suspending-*.wasm).
/// Anything else in the directory means the transpile was not the one this host expects.
/// </summary>
public sealed class TranspiledComponentReader(IDirectoryFileLister lister) : ITranspiledComponentReader
{
    public TranspiledComponent Read(string directory, string componentName)
    {
        var files = lister.List(directory);
        var javaScript = componentName + ".js";
        var cores = files.Where(file => file.EndsWith(".wasm", StringComparison.Ordinal)
                && (file.StartsWith(componentName + ".core", StringComparison.Ordinal)
                    || file.StartsWith(componentName + ".conditional-suspending-", StringComparison.Ordinal))
                && !file.Contains('/'))
            .ToImmutableArray();
        if (!files.Contains(javaScript))
        {
            throw new ServiceHostException($"jco wrote no {javaScript} in {directory}.");
        }
        if (cores.IsEmpty)
        {
            throw new ServiceHostException($"jco wrote no core module for {componentName} in {directory}.");
        }
        var unexpected = files.Except(cores.Add(javaScript), StringComparer.Ordinal).ToImmutableArray();
        return unexpected.IsEmpty
            ? new(javaScript, cores)
            : throw new ServiceHostException(
                $"jco wrote files the Worker does not expect in {directory}: {string.Join(", ", unexpected)}.");
    }
}
