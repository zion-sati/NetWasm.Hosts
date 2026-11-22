// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

namespace NetWasm.Hosts.Publishing;

/// <summary>Where a host's publish puts the shared parts: the transpiled component and the host modules.</summary>
public static class ServiceLayout
{
    public const string ComponentDirectory = "gen";
    public const string HostDirectory = "host";
    public const string HostEntryName = "fetch-service-host.mjs";

    /// <summary>The specifier of a component file, imported from an entry the given number of directories down.</summary>
    public static string ComponentImport(string file, int depth = 0) => Up(depth) + ComponentDirectory + "/" + file;

    public static string HostEntryImport => HostEntryImportFrom(0);

    /// <summary>The specifier of the host entry, imported from an entry the given number of directories down.</summary>
    public static string HostEntryImportFrom(int depth) => Up(depth) + HostDirectory + "/" + HostEntryName;

    private static string Up(int depth) => depth == 0 ? "./" : string.Concat(Enumerable.Repeat("../", depth));
}
