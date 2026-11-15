using System.Diagnostics;

namespace ILSight.ExportArea;

/// <summary>What the work of an export came to: what it wrote, that it stopped because its turn was cancelled, that the
/// assembly it reads was closed before it read it, or the error it failed with.</summary>
public abstract record ExportOutcome<T>
{
    private ExportOutcome()
    {
    }

    public sealed record Done(T Value) : ExportOutcome<T>;

    /// <summary>The export was cancelled before its work ended.</summary>
    public sealed record Stopped : ExportOutcome<T>;

    /// <summary>The assembly the export reads, <see cref="Assembly"/> (its file name), was closed before the work read it
    /// (the read's own answer), so nothing was written: a refusal, never said as cancelled nor counted a failure.</summary>
    public sealed record Closed(string Assembly) : ExportOutcome<T>;

    public sealed record Failed(Exception Error) : ExportOutcome<T>;

    public TResult Match<TResult>(Func<Done, TResult> done, Func<Stopped, TResult> stopped, Func<Closed, TResult> closed, Func<Failed, TResult> failed)
    {
        return this switch
        {
            Done d => done(d),
            Stopped s => stopped(s),
            Closed c => closed(c),
            Failed f => failed(f),
            _ => throw new UnreachableException($"ExportOutcome.Match does not handle {GetType().Name}."),
        };
    }
}
