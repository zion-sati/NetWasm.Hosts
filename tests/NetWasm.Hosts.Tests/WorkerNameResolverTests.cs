// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using NetWasm.Hosts.Publishing;

namespace NetWasm.Hosts.Tests;

public sealed class WorkerNameResolverTests
{
    private readonly WorkerNameResolver _resolver = new();

    [Theory]
    [InlineData("orders")]
    [InlineData("a")]
    [InlineData("orders-2")]
    public void ARequestedNameThatIsAWorkerNameIsKept(string requested) =>
        Assert.Equal(requested, _resolver.Resolve(requested, "Ignored"));

    [Theory]
    [InlineData("Orders")]
    [InlineData("-orders")]
    [InlineData("orders-")]
    [InlineData("orders_api")]
    public void ARequestedNameThatIsNotAWorkerNameStopsThePublish(string requested)
    {
        var failure = Assert.Throws<ServiceHostException>(() => _resolver.Resolve(requested, "Hello"));
        Assert.Contains($"'{requested}' is not a Worker name", failure.Message);
    }

    [Fact]
    public void ARequestedNameLongerThan63CharactersStopsThePublish() =>
        Assert.Throws<ServiceHostException>(() => _resolver.Resolve(new string('a', 64), "Hello"));

    [Theory]
    [InlineData("Hello", "hello")]
    [InlineData("Contoso.Orders.Api", "contoso-orders-api")]
    [InlineData("__My  App!!", "my-app")]
    public void TheAssemblyNameBecomesTheWorkerName(string assemblyName, string expected) =>
        Assert.Equal(expected, _resolver.Resolve("", assemblyName));

    [Fact]
    public void ALongAssemblyNameIsCutTo63CharactersWithoutATrailingDash()
    {
        var name = _resolver.Resolve("", new string('a', 62) + ".bcd");
        Assert.Equal(new string('a', 62), name);
    }

    [Fact]
    public void AnAssemblyNameWithNoLetterOrDigitAsksForAName()
    {
        var failure = Assert.Throws<ServiceHostException>(() => _resolver.Resolve("", "___"));
        Assert.Contains("set NetWasmCloudflareWorkerName", failure.Message);
    }
}
