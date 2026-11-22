// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;
using NetWasm.Hosts.Publishing;

namespace NetWasm.Hosts.Tests;

public sealed class CloudflareHostWriterTests
{
    private sealed class Names : IWorkerNameResolver
    {
        public List<(string Requested, string AssemblyName)> Calls { get; } = [];

        public string Resolve(string requested, string assemblyName)
        {
            Calls.Add((requested, assemblyName));
            return "hello";
        }
    }

    private static ServiceHostInput Input(ImmutableDictionary<string, string> settings, params EnvironmentVariable[] variables) =>
        new(new("Hello.js", ["Hello.conditional-suspending-0.wasm", "Hello.core.wasm"]), "/obj/component", "Hello", [.. variables], settings);

    private static readonly ImmutableDictionary<string, string> Dated =
        ImmutableDictionary<string, string>.Empty.Add(CloudflareHostWriter.CompatibilityDateSetting, "2026-11-01");

    [Fact]
    public void TheWorkerImportsTheCompiledCoreModulesAndHandsThemToTheHost()
    {
        var files = new CloudflareHostWriter(new Names()).Write(
            Input(Dated, new("GREETING", "Hello \"world\""), new("MODE", "service")));

        Assert.Equal(["worker.mjs", "wrangler.json"], files.Select(file => file.RelativePath));
        Assert.Equal(
            """
            // Written by NetWasm.Hosts: the Worker serving the Hello component.
            import { createFetchServiceHost } from "./host/fetch-service-host.mjs";
            import * as component from "./gen/Hello.js";
            import module0 from "./gen/Hello.conditional-suspending-0.wasm";
            import module1 from "./gen/Hello.core.wasm";

            export default createFetchServiceHost({
              component,
              environment: [
                {
                  "name": "GREETING",
                  "value": "Hello \u0022world\u0022"
                },
                {
                  "name": "MODE",
                  "value": "service"
                }
              ],
              modules: {
                "Hello.conditional-suspending-0.wasm": module0,
                "Hello.core.wasm": module1,
              },
            });

            """.ReplaceLineEndings("\n"),
            Assert.IsType<GeneratedFile>(files[0]).Text);
    }

    [Fact]
    public void TheConfigurationNamesTheWorkerItsEntryAndItsRuntimeDate()
    {
        var names = new Names();
        var files = new CloudflareHostWriter(names).Write(
            Input(Dated.Add(CloudflareHostWriter.NameSetting, "orders")));

        Assert.Equal(
            """
            {
              "name": "hello",
              "main": "worker.mjs",
              "compatibility_date": "2026-11-01"
            }

            """.ReplaceLineEndings("\n"),
            Assert.IsType<GeneratedFile>(files[1]).Text.ReplaceLineEndings("\n"));
        Assert.Equal([("orders", "Hello")], names.Calls);
    }

    [Fact]
    public void WithoutANameSettingTheResolverMakesOneFromTheAssemblyName()
    {
        var names = new Names();
        new CloudflareHostWriter(names).Write(Input(Dated));
        Assert.Equal([("", "Hello")], names.Calls);
    }

    [Theory]
    [InlineData("2026-13-01")]
    [InlineData("1 November 2026")]
    [InlineData("")]
    public void ACompatibilityDateThatIsNotADateStopsThePublishFirst(string date)
    {
        var names = new Names();
        var failure = Assert.Throws<ServiceHostException>(() => new CloudflareHostWriter(names).Write(
            Input(ImmutableDictionary<string, string>.Empty.Add(CloudflareHostWriter.CompatibilityDateSetting, date))));
        Assert.Equal($"NetWasmCloudflareCompatibilityDate '{date}' is not a date (yyyy-MM-dd).", failure.Message);
        Assert.Empty(names.Calls);
    }

    [Fact]
    public void AMissingCompatibilityDateStopsThePublish() =>
        Assert.Throws<ServiceHostException>(() =>
            new CloudflareHostWriter(new Names()).Write(Input(ImmutableDictionary<string, string>.Empty)));

    [Fact]
    public void TheHostIsNamedCloudflare() => Assert.Equal("cloudflare", new CloudflareHostWriter(new Names()).Host);
}
