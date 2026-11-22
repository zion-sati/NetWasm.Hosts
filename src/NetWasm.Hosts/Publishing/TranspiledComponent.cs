// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;

namespace NetWasm.Hosts.Publishing;

/// <summary>A component as jco transpiled it: its JavaScript module and the core modules that module loads.</summary>
public sealed record TranspiledComponent(string JavaScriptFile, ImmutableArray<string> CoreModuleFiles);
