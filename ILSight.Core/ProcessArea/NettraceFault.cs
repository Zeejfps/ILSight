namespace ILSight.Core.ProcessArea;

/// <summary>Why a process's rundown trace can't be read, as found by ILSight's own reader
/// (<see cref="NettraceRundown"/>): a cause the app words in each language. How the data is malformed (which tag, which
/// size) is the diagnostic, carried by the exception, never told apart here.</summary>
public union NettraceFault(NettraceFault.VersionNewer, NettraceFault.NotNettrace, NettraceFault.CutShort,
    NettraceFault.Malformed)
{
    /// <summary>The trace is of a Nettrace version newer than the reader knows.</summary>
    public sealed record VersionNewer(int Version);

    /// <summary>The answer doesn't start as a Nettrace trace does.</summary>
    public sealed record NotNettrace;

    /// <summary>The trace ends before what it says is in it.</summary>
    public sealed record CutShort;

    /// <summary>A tag, size or value in the trace is none a Nettrace writer writes.</summary>
    public sealed record Malformed;
}
