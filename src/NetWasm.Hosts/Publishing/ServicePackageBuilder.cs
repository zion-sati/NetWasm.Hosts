// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;

namespace NetWasm.Hosts.Publishing;

/// <summary>Builds each requested host's publish.</summary>
public interface IServicePackageBuilder
{
    /// <summary>One package per requested host, in the order requested.</summary>
    ImmutableArray<HostPackage> Build(ServicePublishRequest request);
}

/// <summary>
/// Builder of the hosts' publishes. It checks the request and selects every host's writer before any work, reads the
/// transpiled component and collects the host modules once, then gives each host its own files beside copies of
/// the shared ones (gen/ and host/).
/// </summary>
public sealed class ServicePackageBuilder(
    IServiceHostWriterSelector writers,
    ITranspiledComponentReader components,
    IHostModuleCollector hostModules) : IServicePackageBuilder
{
    public ImmutableArray<HostPackage> Build(ServicePublishRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Hosts.IsEmpty)
        {
            throw new ServiceHostException("Set NetWasmServiceHosts to the hosts to publish for: cloudflare, deno or both (cloudflare;deno).");
        }
        var repeatedHost = request.Hosts.GroupBy(host => host, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (repeatedHost is not null)
        {
            throw new ServiceHostException($"NetWasmServiceHosts names '{repeatedHost.Key}' more than once.");
        }
        ValidateVariables(request.Variables);
        var selected = request.Hosts.Select(writers.Select).ToImmutableArray();
        var component = components.Read(request.ComponentDirectory, request.ComponentName);
        var modules = hostModules.Collect(ServiceLayout.HostEntryName, request.HostingDirectories);
        ImmutableArray<PackageFile> shared =
        [
            .. component.CoreModuleFiles.Prepend(component.JavaScriptFile).Select(file => new CopiedFile(
                ServiceLayout.ComponentDirectory + "/" + file, Path.Combine(request.ComponentDirectory, file))),
            .. modules.Select(module => new CopiedFile(ServiceLayout.HostDirectory + "/" + module.Name, module.SourcePath)),
        ];
        var input = new ServiceHostInput(component, request.AssemblyName, request.Variables, request.Settings);
        return [.. selected.Select(writer => new HostPackage(writer.Host, [.. writer.Write(input), .. shared]))];
    }

    // A WASI environment variable has a name without '='; a name declared twice would leave its value to chance.
    private static void ValidateVariables(ImmutableArray<EnvironmentVariable> variables)
    {
        foreach (var variable in variables)
        {
            if (variable.Name.Length == 0 || variable.Name.Contains('='))
            {
                throw new ServiceHostException($"NetWasmEnvironmentVariable '{variable.Name}' is not a variable name.");
            }
        }
        var repeated = variables.GroupBy(variable => variable.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (repeated is not null)
        {
            throw new ServiceHostException($"NetWasmEnvironmentVariable '{repeated.Key}' is declared more than once.");
        }
    }
}
