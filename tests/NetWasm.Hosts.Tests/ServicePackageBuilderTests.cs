// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;
using NetWasm.Hosts.Publishing;

namespace NetWasm.Hosts.Tests;

public sealed class ServicePackageBuilderTests
{
    private sealed class Calls
    {
        public List<string> Made { get; } = [];
    }

    private sealed class Writer(string host, Calls calls) : IServiceHostWriter
    {
        public string Host => host;

        public ImmutableArray<GeneratedFile> Write(ServiceHostInput input)
        {
            calls.Made.Add($"write {host} {input.Component.JavaScriptFile}|{input.AssemblyName}|{input.Variables.Length}|{input.Settings["S"]}");
            return [new(host + ".mjs", host)];
        }
    }

    private sealed class Selector(Calls calls) : IServiceHostWriterSelector
    {
        public IServiceHostWriter Select(string host)
        {
            calls.Made.Add($"select {host}");
            return host == "unknown" ? throw new ServiceHostException("unknown host") : new Writer(host, calls);
        }
    }

    private sealed class Components(Calls calls) : ITranspiledComponentReader
    {
        public TranspiledComponent Read(string directory, string componentName)
        {
            calls.Made.Add($"component {directory}|{componentName}");
            return new("Hello.js", ["Hello.core.wasm"]);
        }
    }

    private sealed class HostModules(Calls calls) : IHostModuleCollector
    {
        public ImmutableArray<HostModule> Collect(string entryName, ImmutableArray<string> directories)
        {
            calls.Made.Add($"modules {entryName}|{string.Join(",", directories)}");
            return [new("fetch-service-host.mjs", "/hosts/fetch-service-host.mjs")];
        }
    }

    private static ServicePackageBuilder Builder(Calls calls) => new(new Selector(calls), new Components(calls), new HostModules(calls));

    private static ServicePublishRequest Request(ImmutableArray<string> hosts, params EnvironmentVariable[] variables) =>
        new("/obj/component", "Hello", ["/netwasm", "/hosts"], hosts, "Hello", [.. variables],
            ImmutableDictionary<string, string>.Empty.Add("S", "setting"));

    [Fact]
    public void EachHostGetsItsOwnFilesBesideTheSharedComponentAndHostModules()
    {
        var calls = new Calls();

        var packages = Builder(calls).Build(Request(["deno", "cloudflare"], new EnvironmentVariable("A", "1")));

        ImmutableArray<PackageFile> shared =
        [
            new CopiedFile("gen/Hello.js", Path.Combine("/obj/component", "Hello.js")),
            new CopiedFile("gen/Hello.core.wasm", Path.Combine("/obj/component", "Hello.core.wasm")),
            new CopiedFile("host/fetch-service-host.mjs", "/hosts/fetch-service-host.mjs"),
        ];
        Assert.Equal(["deno", "cloudflare"], packages.Select(package => package.Host));
        Assert.Equal([new GeneratedFile("deno.mjs", "deno"), .. shared], packages[0].Files);
        Assert.Equal([new GeneratedFile("cloudflare.mjs", "cloudflare"), .. shared], packages[1].Files);
        Assert.Equal(
            [
                "select deno",
                "select cloudflare",
                "component /obj/component|Hello",
                "modules fetch-service-host.mjs|/netwasm,/hosts",
                "write deno Hello.js|Hello|1|setting",
                "write cloudflare Hello.js|Hello|1|setting",
            ],
            calls.Made);
    }

    [Fact]
    public void NoHostAsksForTheSetting()
    {
        var calls = new Calls();
        var failure = Assert.Throws<ServiceHostException>(() => Builder(calls).Build(Request([])));
        Assert.Equal("Set NetWasmServiceHosts to the hosts to publish for: cloudflare.", failure.Message);
        Assert.Empty(calls.Made);
    }

    [Fact]
    public void AHostNamedTwiceStopsThePublishFirst()
    {
        var calls = new Calls();
        var failure = Assert.Throws<ServiceHostException>(() => Builder(calls).Build(Request(["deno", "cloudflare", "deno"])));
        Assert.Equal("NetWasmServiceHosts names 'deno' more than once.", failure.Message);
        Assert.Empty(calls.Made);
    }

    [Fact]
    public void AnUnknownHostStopsThePublishBeforeTheComponentIsRead()
    {
        var calls = new Calls();
        Assert.Throws<ServiceHostException>(() => Builder(calls).Build(Request(["deno", "unknown"])));
        Assert.Equal(["select deno", "select unknown"], calls.Made);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A=B")]
    public void AVariableWithoutAValidNameStopsThePublishFirst(string name)
    {
        var calls = new Calls();
        var failure = Assert.Throws<ServiceHostException>(() =>
            Builder(calls).Build(Request(["deno"], new EnvironmentVariable(name, "x"))));
        Assert.Equal($"NetWasmEnvironmentVariable '{name}' is not a variable name.", failure.Message);
        Assert.Empty(calls.Made);
    }

    [Fact]
    public void AVariableDeclaredTwiceStopsThePublishFirst()
    {
        var calls = new Calls();
        var failure = Assert.Throws<ServiceHostException>(() => Builder(calls).Build(
            Request(["deno"], new("A", "1"), new("B", "2"), new("A", "3"))));
        Assert.Equal("NetWasmEnvironmentVariable 'A' is declared more than once.", failure.Message);
        Assert.Empty(calls.Made);
    }

    [Fact]
    public void ARequestIsRequired() =>
        Assert.Throws<ArgumentNullException>(() => Builder(new()).Build(null!));
}
