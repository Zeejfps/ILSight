// Adapted from ILSpy's Processes/ProcessExplorer, DiagnosticsPortScanner and RunningDotNetProcess
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

using System.Diagnostics;

namespace ILSight.Core.ProcessArea;

/// <summary>What asking a process for its modules came to.</summary>
public abstract record ModuleListing
{
    private ModuleListing()
    {
    }

    /// <summary>The modules, sorted by name.</summary>
    public sealed record Listed(IReadOnlyList<RunningModule> Modules) : ModuleListing;

    /// <summary>The process could not be asked: it is gone, refused, or didn't answer in time.</summary>
    public sealed record Unreachable(UnreachableCause Cause) : ModuleListing;

    /// <summary>The process answered with what could not be read.</summary>
    public sealed record Unreadable : ModuleListing
    {
        /// <summary>Why, the cause the app words in each language.</summary>
        public required NettraceFault Fault { get; init; }

        /// <summary>What the reader threw, its message the English diagnostic.</summary>
        public required Exception Error { get; init; }
    }

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Listed, TResult> listed, Func<Unreachable, TResult> unreachable,
        Func<Unreadable, TResult> unreadable)
    {
        return this switch
        {
            Listed c => listed(c),
            Unreachable c => unreachable(c),
            Unreadable c => unreadable(c),
            _ => throw new UnreachableException($"ModuleListing.Match does not handle {GetType().Name}."),
        };
    }
}
