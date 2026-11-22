// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;

namespace NetWasm.Hosts.Publishing;

/// <summary>Lists the files under a directory.</summary>
public interface IDirectoryFileLister
{
    /// <summary>Every file under the directory, by its path relative to it with '/' separators, in ordinal order.</summary>
    ImmutableArray<string> List(string directory);
}

/// <summary>Reads a text file.</summary>
public interface ITextFileReader
{
    /// <summary>The file's text, or null when there is no such file.</summary>
    string? Read(string path);
}

/// <summary>Lists the files on disk.</summary>
public sealed class DirectoryFileLister : IDirectoryFileLister
{
    public ImmutableArray<string> List(string directory) =>
        [.. Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)];
}

/// <summary>Reads files on disk.</summary>
public sealed class TextFileReader : ITextFileReader
{
    public string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
}
