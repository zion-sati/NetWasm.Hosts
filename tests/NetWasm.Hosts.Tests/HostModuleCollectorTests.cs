// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using NetWasm.Hosts.Publishing;

namespace NetWasm.Hosts.Tests;

public sealed class HostModuleCollectorTests
{
    private sealed class Files(Dictionary<string, string> files) : ITextFileReader
    {
        public string? Read(string path) => files.GetValueOrDefault(path);
    }

    private static HostModuleCollector Collector(Dictionary<string, string> files) => new(new Files(files));

    [Fact]
    public void TheEntryAndEveryModuleItImportsAreCollectedFromBothDirectories()
    {
        var modules = Collector(new()
        {
            ["/host/entry.mjs"] = """
                // Reads from "the request" and imports nothing else here.
                import { a } from "./a.mjs";
                import {
                  b,
                } from './b.mjs';
                export { c } from "./c.mjs";
                import "./d.mjs";
                const text = "import from";
                """,
            ["/netwasm/a.mjs"] = "import { b } from \"./b.mjs\";\nexport function a() { return b(); }\n",
            ["/netwasm/b.mjs"] = "import { a } from \"./a.mjs\";\nexport const b = () => a;\n",
            ["/netwasm/c.mjs"] = "export const c = 1;\n",
            ["/host/d.mjs"] = "globalThis.d = true;\n",
        }).Collect("entry.mjs", ["/netwasm", "/host"]);

        Assert.Equal(
            [
                new HostModule("a.mjs", "/netwasm/a.mjs"),
                new HostModule("b.mjs", "/netwasm/b.mjs"),
                new HostModule("c.mjs", "/netwasm/c.mjs"),
                new HostModule("d.mjs", "/host/d.mjs"),
                new HostModule("entry.mjs", "/host/entry.mjs"),
            ],
            modules);
    }

    [Fact]
    public void AModuleInNoDirectoryStopsThePublish()
    {
        var failure = Assert.Throws<ServiceHostException>(() => Collector(new()
        {
            ["/host/entry.mjs"] = "import { gone } from \"./gone.mjs\";\n",
        }).Collect("entry.mjs", ["/netwasm", "/host"]));
        Assert.Equal("/host/entry.mjs imports the host module gone.mjs, which is in none of /netwasm, /host.", failure.Message);
    }

    [Fact]
    public void AMissingEntryNamesTheWorkerAsItsImporter()
    {
        var failure = Assert.Throws<ServiceHostException>(() => Collector([]).Collect("entry.mjs", ["/host"]));
        Assert.StartsWith("the Worker imports the host module entry.mjs", failure.Message);
    }

    [Fact]
    public void AModuleInTwoDirectoriesStopsThePublish()
    {
        var failure = Assert.Throws<ServiceHostException>(() => Collector(new()
        {
            ["/netwasm/entry.mjs"] = "",
            ["/host/entry.mjs"] = "",
        }).Collect("entry.mjs", ["/netwasm", "/host"]));
        Assert.Equal("The host module entry.mjs is in more than one directory: /netwasm/entry.mjs, /host/entry.mjs.", failure.Message);
    }

    [Fact]
    public void ADynamicImportStopsThePublish()
    {
        var failure = Assert.Throws<ServiceHostException>(() => Collector(new()
        {
            ["/host/entry.mjs"] = "const sha = await import (\"./sha.mjs\");\n",
        }).Collect("entry.mjs", ["/host"]));
        Assert.Equal("The host module /host/entry.mjs imports a module dynamically, which a Worker cannot.", failure.Message);
    }

    [Theory]
    [InlineData("node:fs")]
    [InlineData("../outside.mjs")]
    [InlineData("./nested/module.mjs")]
    public void AnImportFromOutsideTheHostDirectoryStopsThePublish(string specifier)
    {
        var failure = Assert.Throws<ServiceHostException>(() => Collector(new()
        {
            ["/host/entry.mjs"] = $"import x from \"{specifier}\";\n",
        }).Collect("entry.mjs", ["/host"]));
        Assert.Equal($"The host module /host/entry.mjs imports '{specifier}', which is not a host module beside it.", failure.Message);
    }
}
