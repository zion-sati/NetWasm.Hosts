// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace NetWasm.Hosts.Publishing;

/// <summary>Collects the host modules a Worker imports.</summary>
public interface IHostModuleCollector
{
    /// <summary>The entry module and every module it imports, directly or not, found in the directories.</summary>
    ImmutableArray<HostModule> Collect(string entryName, ImmutableArray<string> directories);
}

/// <summary>
/// The Worker's host modules sit in one directory and import each other by "./name.mjs", statically: a module is
/// looked up by name in every directory, and one that is missing, in two directories, imported from elsewhere or
/// imported dynamically stops the publish, since the Worker would fail where the publish passed.
/// </summary>
public sealed partial class HostModuleCollector(ITextFileReader reader) : IHostModuleCollector
{
    public ImmutableArray<HostModule> Collect(string entryName, ImmutableArray<string> directories)
    {
        var found = new SortedDictionary<string, HostModule>(StringComparer.Ordinal);
        var pending = new Stack<(string Name, string Importer)>([(entryName, "the Worker")]);
        while (pending.TryPop(out var next))
        {
            if (found.ContainsKey(next.Name))
            {
                continue;
            }
            var (module, text) = Find(next.Name, next.Importer, directories);
            found.Add(next.Name, module);
            if (DynamicImport().IsMatch(text))
            {
                throw new ServiceHostException($"The host module {module.SourcePath} imports a module dynamically, which a Worker cannot.");
            }
            foreach (Match import in StaticImport().Matches(text))
            {
                var specifier = import.Groups["specifier"].Value;
                if (!specifier.StartsWith("./", StringComparison.Ordinal) || specifier.IndexOf('/', 2) >= 0)
                {
                    throw new ServiceHostException(
                        $"The host module {module.SourcePath} imports '{specifier}', which is not a host module beside it.");
                }
                pending.Push((specifier[2..], module.SourcePath));
            }
        }
        return [.. found.Values];
    }

    private (HostModule Module, string Text) Find(string name, string importer, ImmutableArray<string> directories)
    {
        var matches = directories
            .Select(directory => Path.Combine(directory, name))
            .Select(path => (Path: path, Text: reader.Read(path)))
            .Where(candidate => candidate.Text is not null)
            .ToArray();
        return matches.Length switch
        {
            1 => (new HostModule(name, matches[0].Path), matches[0].Text!),
            0 => throw new ServiceHostException(
                $"{importer} imports the host module {name}, which is in none of {string.Join(", ", directories)}."),
            _ => throw new ServiceHostException(
                $"The host module {name} is in more than one directory: {string.Join(", ", matches.Select(match => match.Path))}."),
        };
    }

    // import ... from "x" and export ... from "x" (their lists may span lines), and import "x".
    [GeneratedRegex("""^\s*(?:(?:import|export)\s[^;(]*?\bfrom|import)\s*["'](?<specifier>[^"']+)["']""", RegexOptions.Multiline)]
    private static partial Regex StaticImport();

    [GeneratedRegex("""\bimport\s*\(""")]
    private static partial Regex DynamicImport();
}
