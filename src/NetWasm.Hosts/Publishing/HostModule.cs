// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

namespace NetWasm.Hosts.Publishing;

/// <summary>A host module the Worker imports: its file name and where it is copied from.</summary>
public sealed record HostModule(string Name, string SourcePath);
