// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;

namespace NetWasm.Hosts.Publishing;

/// <summary>Writes the files one host needs around the shared component and host modules: its entry and config.</summary>
public interface IServiceHostWriter
{
    /// <summary>The host's name, as NetWasmServiceHosts lists it.</summary>
    string Host { get; }

    /// <summary>The host's own files.</summary>
    ImmutableArray<PackageFile> Write(ServiceHostInput input);
}

/// <summary>Selects a host's writer by name.</summary>
public interface IServiceHostWriterSelector
{
    /// <summary>The writer of the named host.</summary>
    IServiceHostWriter Select(string host);
}

/// <summary>Writers keyed by host name, composed once; a duplicate name is refused there, an unknown one here.</summary>
public sealed class ServiceHostWriterSelector : IServiceHostWriterSelector
{
    private readonly ImmutableDictionary<string, IServiceHostWriter> _writers;

    public ServiceHostWriterSelector(IEnumerable<IServiceHostWriter> writers)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, IServiceHostWriter>(StringComparer.Ordinal);
        foreach (var writer in writers)
        {
            if (!builder.TryAdd(writer.Host, writer))
            {
                throw new ArgumentException($"Two service host writers are named '{writer.Host}'.", nameof(writers));
            }
        }
        _writers = builder.ToImmutable();
    }

    public IServiceHostWriter Select(string host) =>
        _writers.TryGetValue(host, out var writer)
            ? writer
            : throw new ServiceHostException(
                $"NetWasmServiceHosts names '{host}', which is not a host NetWasm.Hosts publishes for: {string.Join(", ", _writers.Keys.Order(StringComparer.Ordinal))}.");
}
