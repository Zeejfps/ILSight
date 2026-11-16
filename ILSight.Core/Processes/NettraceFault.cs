using System.Diagnostics;

namespace ILSight.Core.Processes;

/// <summary>Why a process's rundown trace can't be read, as found by ILSight's own reader
/// (<see cref="NettraceRundown"/>): a cause the app words in each language. How the data is malformed (which tag, which
/// size) is the diagnostic, carried by the exception, never told apart here.</summary>
public abstract record NettraceFault
{
    private NettraceFault()
    {
    }

    /// <summary>The trace is of a Nettrace version newer than the reader knows.</summary>
    public sealed record VersionNewer(int Version) : NettraceFault;

    /// <summary>The answer doesn't start as a Nettrace trace does.</summary>
    public sealed record NotNettrace : NettraceFault;

    /// <summary>The trace ends before what it says is in it.</summary>
    public sealed record CutShort : NettraceFault;

    /// <summary>A tag, size or value in the trace is none a Nettrace writer writes.</summary>
    public sealed record Malformed : NettraceFault;

    public TResult Match<TResult>(Func<VersionNewer, TResult> versionNewer, Func<NotNettrace, TResult> notNettrace,
        Func<CutShort, TResult> cutShort, Func<Malformed, TResult> malformed)
    {
        return this switch
        {
            VersionNewer v => versionNewer(v),
            NotNettrace n => notNettrace(n),
            CutShort c => cutShort(c),
            Malformed m => malformed(m),
            _ => throw new UnreachableException($"NettraceFault.Match does not handle {GetType().Name}."),
        };
    }
}
