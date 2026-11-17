// Adapted from ILSpy's Processes/NettraceRundownReader
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

namespace ILSight.Core.ProcessArea;

/// <summary>What reading a rundown's trace came to: the modules it reports, or why it could not be read.</summary>
internal union RundownRead(RundownRead.Read, RundownRead.Corrupt)
{
    public sealed record Read(IReadOnlyList<RundownModule> Modules);

    public sealed record Corrupt
    {
        public required NettraceFault Fault { get; init; }

        /// <summary>What the reader threw, its message the English diagnostic.</summary>
        public required Exception Error { get; init; }
    }
}
