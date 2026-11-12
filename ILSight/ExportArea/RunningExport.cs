using ILSight.Core;

namespace ILSight.ExportArea;

/// <summary>The export that runs: how far it has got, and whether it was asked to stop and has not stopped yet.</summary>
public sealed record RunningExport
{
    public required QueuedExport Export { get; init; }

    public required ExportProgress Progress { get; init; }

    public required bool IsCancelling { get; init; }
}
