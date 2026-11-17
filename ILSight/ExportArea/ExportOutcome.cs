namespace ILSight.ExportArea;

/// <summary>What the work of an export came to: what it wrote, that it stopped because its turn was cancelled, that the
/// assembly it reads was closed before it read it, or the error it failed with.</summary>
public union ExportOutcome<T>(ExportOutcome<T>.Done, ExportOutcome<T>.Stopped, ExportOutcome<T>.Closed, ExportOutcome<T>.Failed)
{
    public sealed record Done(T Value);

    /// <summary>The export was cancelled before its work ended.</summary>
    public sealed record Stopped;

    /// <summary>The assembly the export reads, <see cref="Assembly"/> (its file name), was closed before the work read it
    /// (the read's own answer), so nothing was written: a refusal, never said as cancelled nor counted a failure.</summary>
    public sealed record Closed(string Assembly);

    public sealed record Failed(Exception Error);
}
