// Adapted from ILSpy's Processes/ProcessExplorer, DiagnosticsPortScanner and RunningDotNetProcess
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

namespace ILSight.Core.Processes;

/// <summary>What a runtime says of itself over its diagnostics endpoint. A runtime older than .NET 6 names
/// neither its entry assembly nor its version.</summary>
public sealed record ProcessInfo
{
    public required string? CommandLine { get; init; }

    /// <summary>As the runtime names it, e.g. <c>arm64</c>.</summary>
    public required string? Architecture { get; init; }

    /// <summary>The entry assembly's simple name, e.g. <c>MyApp</c>.</summary>
    public required string? EntryAssemblyName { get; init; }

    /// <summary>The runtime's version, e.g. <c>10.0.0</c>.</summary>
    public required string? RuntimeVersion { get; init; }
}
