using ILSight.Core.Processes;

namespace ILSight.ProcessBrowsing;

/// <summary>How far looking for the running processes has got.</summary>
public abstract record ProcessScan
{
    private ProcessScan()
    {
    }

    public sealed record NotStarted : ProcessScan;

    public sealed record Scanning : ProcessScan;

    public sealed record Scanned(IReadOnlyList<RunningProcess> Processes) : ProcessScan;

    public sealed record Failed(string Error) : ProcessScan;
}
