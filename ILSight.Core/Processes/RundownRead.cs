// Adapted from ILSpy's Processes/NettraceRundownReader
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

namespace ILSight.Core.Processes;

/// <summary>What reading a rundown's trace came to: the modules it reports, or why it could not be read.</summary>
internal abstract record RundownRead
{
    private RundownRead()
    {
    }

    public sealed record Read(IReadOnlyList<RundownModule> Modules) : RundownRead;

    public sealed record Corrupt(string Detail) : RundownRead;
}
