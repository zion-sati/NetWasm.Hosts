// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using NetWasm.Hosts.Publishing;

namespace NetWasm.Hosts.Tests;

// The file system adapters, against a temporary directory.
public sealed class FileSystemTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("netwasm-hosts-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Write(string relativePath, string text)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void TheListerGivesEveryFileByItsRelativePathInOrdinalOrder()
    {
        Write("b.js", "");
        Write("A.wasm", "");
        Write("sub/c.d.ts", "");

        Assert.Equal(["A.wasm", "b.js", "sub/c.d.ts"], new DirectoryFileLister().List(_root));
    }

    [Fact]
    public void TheReaderGivesAFilesTextOrNullForNoFile()
    {
        var path = Write("a.mjs", "export {};\n");

        Assert.Equal("export {};\n", new TextFileReader().Read(path));
        Assert.Null(new TextFileReader().Read(Path.Combine(_root, "missing.mjs")));
    }

    [Fact]
    public void TheWriterReplacesEachHostsDirectoryWithItsFiles()
    {
        var source = Write("source/Hello.core.wasm", "core");
        Write("publish/cloudflare/stale.mjs", "old");
        Write("publish/Hello.wasm", "component");
        var root = Path.Combine(_root, "publish");

        var written = new HostPackageWriter().Write(root,
        [
            new("cloudflare", [new GeneratedFile("worker.mjs", "worker"), new CopiedFile("gen/Hello.core.wasm", source)]),
            new("deno", [new GeneratedFile("main.mjs", "main")]),
        ]);

        Assert.Equal(
            [Path.Combine(root, "cloudflare", "worker.mjs"), Path.Combine(root, "cloudflare", "gen", "Hello.core.wasm"), Path.Combine(root, "deno", "main.mjs")],
            written);
        Assert.Equal("worker", File.ReadAllText(Path.Combine(root, "cloudflare", "worker.mjs")));
        Assert.Equal("core", File.ReadAllText(Path.Combine(root, "cloudflare", "gen", "Hello.core.wasm")));
        Assert.Equal("main", File.ReadAllText(Path.Combine(root, "deno", "main.mjs")));
        Assert.False(File.Exists(Path.Combine(root, "cloudflare", "stale.mjs")));
        Assert.Equal("component", File.ReadAllText(Path.Combine(root, "Hello.wasm")));
    }

    [Fact]
    public void AnEmbeddedModuleExportsItsSourcesBytesInBase64()
    {
        var source = Path.Combine(_root, "Hello.core.wasm");
        File.WriteAllBytes(source, [0x00, 0x61, 0x73, 0x6d, 0xff]);
        var root = Path.Combine(_root, "publish");

        var written = new HostPackageWriter().Write(root, [new("netlify", [new EmbeddedModuleFile("gen/Hello.core.wasm.mjs", source)])]);

        Assert.Equal([Path.Combine(root, "netlify", "gen", "Hello.core.wasm.mjs")], written);
        Assert.Equal("export default \"AGFzbf8=\";\n", File.ReadAllText(written[0]));
    }

    private sealed record UnknownFile(string RelativePath) : PackageFile(RelativePath);

    [Fact]
    public void TheWriterRefusesAFileKindItDoesNotKnow()
    {
        var failure = Assert.Throws<NotSupportedException>(() =>
            new HostPackageWriter().Write(Path.Combine(_root, "out"), [new("deno", [new UnknownFile("x")])]));
        Assert.Equal("A file of kind UnknownFile cannot be written.", failure.Message);
    }
}
