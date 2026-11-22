// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections;
using System.Collections.Immutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NetWasm.Hosts.Publishing;

namespace NetWasm.Hosts.Tests;

public sealed class PublishServiceHostsTaskTests
{
    private sealed class Engine : IBuildEngine
    {
        public List<BuildErrorEventArgs> Errors { get; } = [];
        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 0;
        public int ColumnNumberOfTaskNode => 0;
        public string ProjectFileOfTaskNode => "Hello.csproj";
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => false;
        public void LogCustomEvent(CustomBuildEventArgs e) { }
        public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e);
        public void LogMessageEvent(BuildMessageEventArgs e) { }
        public void LogWarningEvent(BuildWarningEventArgs e) { }
    }

    private sealed class Builder(Func<ServicePublishRequest, ImmutableArray<HostPackage>> build) : IServicePackageBuilder
    {
        public ServicePublishRequest? Request { get; private set; }

        public ImmutableArray<HostPackage> Build(ServicePublishRequest request)
        {
            Request = request;
            return build(request);
        }
    }

    private sealed class Writer : IHostPackageWriter
    {
        public (string Root, ImmutableArray<HostPackage> Packages)? Written { get; private set; }

        public ImmutableArray<string> Write(string root, ImmutableArray<HostPackage> packages)
        {
            Written = (root, packages);
            return ["/publish/deno/main.mjs"];
        }
    }

    private static PublishServiceHostsTask Task(IServicePackageBuilder builder, IHostPackageWriter writer, Engine engine) =>
        new(builder, writer)
        {
            BuildEngine = engine,
            ComponentDirectory = "/obj/component",
            ComponentName = "Hello",
            HostingDirectories = ["/netwasm", "/hosts"],
            Hosts = [" deno ", "", "cloudflare"],
            OutputRoot = "/publish",
            AssemblyName = "Hello",
            EnvironmentVariables = [new TaskItem("MODE", new Dictionary<string, string> { ["Value"] = "service" })],
            Settings = [new TaskItem("NetWasmCloudflareWorkerName", new Dictionary<string, string> { ["Value"] = "orders" })],
        };

    [Fact]
    public void ThePublishInputsBecomeTheRequestAndTheHostsAreWritten()
    {
        ImmutableArray<HostPackage> packages = [new("deno", [new GeneratedFile("main.mjs", "main")])];
        var builder = new Builder(_ => packages);
        var writer = new Writer();
        var engine = new Engine();
        var task = Task(builder, writer, engine);

        Assert.True(task.Execute());

        var request = builder.Request!;
        Assert.Equal("/obj/component", request.ComponentDirectory);
        Assert.Equal("Hello", request.ComponentName);
        Assert.Equal(["/netwasm", "/hosts"], request.HostingDirectories);
        Assert.Equal(["deno", "cloudflare"], request.Hosts);
        Assert.Equal("Hello", request.AssemblyName);
        Assert.Equal([new EnvironmentVariable("MODE", "service")], request.Variables);
        Assert.Equal("orders", Assert.Single(request.Settings, setting => setting.Key == "NetWasmCloudflareWorkerName").Value);
        Assert.Equal("/publish", writer.Written!.Value.Root);
        Assert.Equal(packages, writer.Written.Value.Packages);
        Assert.Equal(["/publish/deno/main.mjs"], task.WrittenFiles.Select(item => item.ItemSpec));
        Assert.Empty(engine.Errors);
    }

    [Fact]
    public void AnInputNoHostCanBePublishedFromIsReportedAndNothingIsWritten()
    {
        var writer = new Writer();
        var engine = new Engine();
        var task = Task(new Builder(_ => throw new ServiceHostException("no hosts")), writer, engine);

        Assert.False(task.Execute());

        var error = Assert.Single(engine.Errors);
        Assert.Equal("NWHOSTS010", error.Code);
        Assert.Equal("no hosts", error.Message);
        Assert.Null(writer.Written);
        Assert.Empty(task.WrittenFiles);
    }

    [Fact]
    public void MSBuildCreatesTheTaskWithItsCollaborators() =>
        Assert.Empty(new PublishServiceHostsTask().WrittenFiles);
}
