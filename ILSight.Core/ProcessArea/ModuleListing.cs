// Adapted from ILSpy's Processes/ProcessExplorer, DiagnosticsPortScanner and RunningDotNetProcess
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

namespace ILSight.Core.ProcessArea;

/// <summary>What asking a process for its modules came to.</summary>
public union ModuleListing(ModuleListing.Listed, ModuleListing.Unreachable, ModuleListing.Unreadable)
{
    /// <summary>The modules, sorted by name.</summary>
    public sealed record Listed(IReadOnlyList<RunningModule> Modules);

    /// <summary>The process could not be asked: it is gone, refused, or didn't answer in time.</summary>
    public sealed record Unreachable(UnreachableCause Cause);

    /// <summary>The process answered with what could not be read.</summary>
    public sealed record Unreadable
    {
        /// <summary>Why, the cause the app words in each language.</summary>
        public required NettraceFault Fault { get; init; }

        /// <summary>What the reader threw, its message the English diagnostic.</summary>
        public required Exception Error { get; init; }
    }
}
