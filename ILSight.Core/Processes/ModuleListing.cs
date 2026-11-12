// Adapted from ILSpy's Processes/ProcessExplorer, DiagnosticsPortScanner and RunningDotNetProcess
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

namespace ILSight.Core.Processes;

/// <summary>What asking a process for its modules came to.</summary>
public abstract record ModuleListing
{
    private ModuleListing()
    {
    }

    /// <summary>The modules, sorted by name.</summary>
    public sealed record Listed(IReadOnlyList<RunningModule> Modules) : ModuleListing;

    /// <summary>The process could not be asked: it is gone, refused, or didn't answer in time.</summary>
    public sealed record Unreachable(string Detail) : ModuleListing;

    /// <summary>The process answered with what could not be read.</summary>
    public sealed record Unreadable(string Detail) : ModuleListing;
}
