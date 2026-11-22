// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;
using NetWasm.Hosts.Publishing;

namespace NetWasm.Hosts.Tests;

public sealed class TranspiledComponentReaderTests
{
    private sealed class Lister(params string[] files) : IDirectoryFileLister
    {
        public ImmutableArray<string> List(string directory) => [.. files];
    }

    [Fact]
    public void TheComponentModuleAndEveryCoreModuleItLoadsAreRead()
    {
        var component = new TranspiledComponentReader(new Lister(
                "Hello.conditional-suspending-0.wasm", "Hello.core.wasm", "Hello.core2.wasm", "Hello.js"))
            .Read("/gen", "Hello");

        Assert.Equal("Hello.js", component.JavaScriptFile);
        Assert.Equal(["Hello.conditional-suspending-0.wasm", "Hello.core.wasm", "Hello.core2.wasm"], component.CoreModuleFiles);
    }

    [Fact]
    public void AMissingComponentModuleStopsThePublish()
    {
        var failure = Assert.Throws<ServiceHostException>(() =>
            new TranspiledComponentReader(new Lister("Hello.core.wasm")).Read("/gen", "Hello"));
        Assert.Equal("jco wrote no Hello.js in /gen.", failure.Message);
    }

    [Fact]
    public void AComponentWithoutCoreModulesStopsThePublish()
    {
        var failure = Assert.Throws<ServiceHostException>(() =>
            new TranspiledComponentReader(new Lister("Hello.js", "Other.core.wasm")).Read("/gen", "Hello"));
        Assert.Equal("jco wrote no core module for Hello in /gen.", failure.Message);
    }

    [Fact]
    public void AnyOtherFileStopsThePublish()
    {
        var failure = Assert.Throws<ServiceHostException>(() =>
            new TranspiledComponentReader(new Lister("Hello.core.wasm", "Hello.core/nested.wasm", "Hello.d.ts", "Hello.js"))
                .Read("/gen", "Hello"));
        Assert.Equal("jco wrote files the Worker does not expect in /gen: Hello.core/nested.wasm, Hello.d.ts.", failure.Message);
    }
}
