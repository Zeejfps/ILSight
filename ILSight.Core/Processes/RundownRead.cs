// Adapted from ILSpy's Processes/NettraceRundownReader
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

using System.Diagnostics;

namespace ILSight.Core.Processes;

/// <summary>What reading a rundown's trace came to: the modules it reports, or why it could not be read.</summary>
internal abstract record RundownRead
{
    private RundownRead()
    {
    }

    public sealed record Read(IReadOnlyList<RundownModule> Modules) : RundownRead;

    public sealed record Corrupt(string Detail) : RundownRead;

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Read, TResult> read, Func<Corrupt, TResult> corrupt)
    {
        return this switch
        {
            Read c => read(c),
            Corrupt c => corrupt(c),
            _ => throw new UnreachableException($"RundownRead.Match does not handle {GetType().Name}."),
        };
    }
}
