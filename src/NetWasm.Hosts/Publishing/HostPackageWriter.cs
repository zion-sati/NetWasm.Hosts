// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;

namespace NetWasm.Hosts.Publishing;

/// <summary>Writes the hosts' publishes.</summary>
public interface IHostPackageWriter
{
    /// <summary>Replaces each host's directory under the root with its files; returns their full paths.</summary>
    ImmutableArray<string> Write(string root, ImmutableArray<HostPackage> packages);
}

/// <summary>Writes to disk, each host into &lt;root&gt;/&lt;host&gt;, a directory the publish owns, so no file of an earlier publish stays.</summary>
public sealed class HostPackageWriter : IHostPackageWriter
{
    public ImmutableArray<string> Write(string root, ImmutableArray<HostPackage> packages)
    {
        var written = ImmutableArray.CreateBuilder<string>();
        foreach (var package in packages)
        {
            var directory = Path.Combine(root, package.Host);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
            foreach (var file in package.Files)
            {
                var path = Path.GetFullPath(Path.Combine(directory, file.RelativePath));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                switch (file)
                {
                    case CopiedFile copied:
                        File.Copy(copied.SourcePath, path);
                        break;
                    case GeneratedFile generated:
                        File.WriteAllText(path, generated.Text);
                        break;
                    default:
                        throw new NotSupportedException($"A file of kind {file.GetType().Name} cannot be written.");
                }
                written.Add(path);
            }
        }
        return written.ToImmutable();
    }
}
