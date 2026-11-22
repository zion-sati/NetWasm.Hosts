// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

namespace NetWasm.Hosts.Publishing;

/// <summary>Where a host's publish puts the shared parts: the transpiled component and the host modules.</summary>
public static class ServiceLayout
{
    public const string ComponentDirectory = "gen";
    public const string HostDirectory = "host";
    public const string HostEntryName = "fetch-service-host.mjs";

    public static string ComponentImport(string file) => "./" + ComponentDirectory + "/" + file;

    public static string HostEntryImport => "./" + HostDirectory + "/" + HostEntryName;
}
