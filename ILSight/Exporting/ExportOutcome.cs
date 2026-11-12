using System.Diagnostics;

namespace ILSight.Exporting;

/// <summary>What the work of an export came to: what it wrote, that it stopped because its turn was cancelled, or the
/// error it failed with.</summary>
public abstract record ExportOutcome<T>
{
    private ExportOutcome()
    {
    }

    public sealed record Done(T Value) : ExportOutcome<T>;

    /// <summary>The export was cancelled before its work ended.</summary>
    public sealed record Stopped : ExportOutcome<T>;

    public sealed record Failed(Exception Error) : ExportOutcome<T>;

    public TResult Match<TResult>(Func<Done, TResult> done, Func<Stopped, TResult> stopped, Func<Failed, TResult> failed)
    {
        return this switch
        {
            Done d => done(d),
            Stopped s => stopped(s),
            Failed f => failed(f),
            _ => throw new UnreachableException($"ExportOutcome.Match does not handle {GetType().Name}."),
        };
    }
}
