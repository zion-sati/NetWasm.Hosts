// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NetWasm.Hosts.Publishing;

namespace NetWasm.Hosts;

/// <summary>
/// Facade for the publish step: reads MSBuild's inputs into a request, builds each requested host's files and
/// writes them. A publish input no host can be published from is reported as NWHOSTS010 with what to change.
/// </summary>
public sealed class PublishServiceHostsTask : Microsoft.Build.Utilities.Task
{
    private readonly IServicePackageBuilder _builder;
    private readonly IHostPackageWriter _writer;

    public PublishServiceHostsTask()
        : this(
            new ServicePackageBuilder(
                new ServiceHostWriterSelector([new CloudflareHostWriter(new WorkerNameResolver()), new DenoHostWriter()]),
                new TranspiledComponentReader(new DirectoryFileLister()),
                new HostModuleCollector(new TextFileReader())),
            new HostPackageWriter())
    {
    }

    internal PublishServiceHostsTask(IServicePackageBuilder builder, IHostPackageWriter writer)
    {
        _builder = builder;
        _writer = writer;
    }

    [Required] public string ComponentDirectory { get; set; } = string.Empty;
    [Required] public string ComponentName { get; set; } = string.Empty;
    [Required] public string[] HostingDirectories { get; set; } = [];
    [Required] public string OutputRoot { get; set; } = string.Empty;
    [Required] public string AssemblyName { get; set; } = string.Empty;
    public string[] Hosts { get; set; } = [];
    public ITaskItem[] EnvironmentVariables { get; set; } = [];
    public ITaskItem[] Settings { get; set; } = [];

    [Output] public ITaskItem[] WrittenFiles { get; private set; } = [];

    public override bool Execute()
    {
        try
        {
            var packages = _builder.Build(new(
                ComponentDirectory,
                ComponentName,
                [.. HostingDirectories],
                [.. Hosts.Select(host => host.Trim()).Where(host => host.Length != 0)],
                AssemblyName,
                [.. EnvironmentVariables.Select(item => new EnvironmentVariable(item.ItemSpec, item.GetMetadata("Value")))],
                Settings.ToImmutableDictionary(item => item.ItemSpec, item => item.GetMetadata("Value"), StringComparer.Ordinal)));
            WrittenFiles = [.. _writer.Write(OutputRoot, packages).Select(path => new TaskItem(path))];
            return true;
        }
        catch (ServiceHostException exception)
        {
            Log.LogError(null, "NWHOSTS010", null, null, 0, 0, 0, 0, exception.Message);
            return false;
        }
    }
}
