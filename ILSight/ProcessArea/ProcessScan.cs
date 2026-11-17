using ILSight.Core.ProcessArea;

namespace ILSight.ProcessArea;

/// <summary>How far looking for the running processes has got.</summary>
public union ProcessScan(ProcessScan.NotStarted, ProcessScan.Scanning, ProcessScan.Scanned, ProcessScan.Failed)
{
    public sealed record NotStarted;

    public sealed record Scanning;

    public sealed record Scanned(IReadOnlyList<RunningProcess> Processes);

    public sealed record Failed(string Error);
}
