// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;
using NetWasm.Hosts.Publishing;

namespace NetWasm.Hosts.Tests;

public sealed class ServiceHostWriterSelectorTests
{
    private sealed class Writer(string host) : IServiceHostWriter
    {
        public string Host => host;

        public ImmutableArray<PackageFile> Write(ServiceHostInput input) => [];
    }

    [Fact]
    public void AHostIsSelectedByItsName()
    {
        var deno = new Writer("deno");
        var selector = new ServiceHostWriterSelector([new Writer("cloudflare"), deno]);
        Assert.Same(deno, selector.Select("deno"));
    }

    [Fact]
    public void AnUnknownHostIsRefusedWithTheHostsThereAre()
    {
        var failure = Assert.Throws<ServiceHostException>(() =>
            new ServiceHostWriterSelector([new Writer("deno"), new Writer("cloudflare")]).Select("Deno"));
        Assert.Equal("NetWasmServiceHosts names 'Deno', which is not a host NetWasm.Hosts publishes for: cloudflare, deno.", failure.Message);
    }

    [Fact]
    public void TwoWritersOfOneHostAreRefusedWhenComposed()
    {
        var failure = Assert.Throws<ArgumentException>(() => new ServiceHostWriterSelector([new Writer("deno"), new Writer("deno")]));
        Assert.StartsWith("Two service host writers are named 'deno'.", failure.Message);
    }
}
