// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: LicenseRef-NetWasm-Community-1.0

using System.Collections.Immutable;

namespace NetWasm.Hosts.Publishing;

/// <summary>A variable the project declares for the component's environment.</summary>
public sealed record EnvironmentVariable(string Name, string Value);

/// <summary>What one publish asks for.</summary>
/// <param name="ComponentDirectory">jco's output for the service component.</param>
/// <param name="ComponentName">The base name jco gave the component's files.</param>
/// <param name="HostingDirectories">The directories the host modules come from, NetWasm.Hosting's first.</param>
/// <param name="Hosts">The hosts to publish for, by name.</param>
/// <param name="AssemblyName">The project's assembly name.</param>
/// <param name="Variables">The variables the project declares.</param>
/// <param name="Settings">The hosts' own settings, by MSBuild property name.</param>
public sealed record ServicePublishRequest(
    string ComponentDirectory,
    string ComponentName,
    ImmutableArray<string> HostingDirectories,
    ImmutableArray<string> Hosts,
    string AssemblyName,
    ImmutableArray<EnvironmentVariable> Variables,
    ImmutableDictionary<string, string> Settings);

/// <summary>What a host's writer has to work with: the component, the declared variables and the settings.</summary>
public sealed record ServiceHostInput(
    TranspiledComponent Component,
    string AssemblyName,
    ImmutableArray<EnvironmentVariable> Variables,
    ImmutableDictionary<string, string> Settings);

/// <summary>The files one host's publish holds, each at its path relative to the host's directory.</summary>
public sealed record HostPackage(string Host, ImmutableArray<PackageFile> Files);

/// <summary>A file of a host's publish.</summary>
public abstract record PackageFile(string RelativePath);

/// <summary>A file copied unchanged.</summary>
public sealed record CopiedFile(string RelativePath, string SourcePath) : PackageFile(RelativePath);

/// <summary>A file the publish writes.</summary>
public sealed record GeneratedFile(string RelativePath, string Text) : PackageFile(RelativePath);

/// <summary>A publish input no host can be published from; the message says which and why.</summary>
public sealed class ServiceHostException(string message) : Exception(message);
