using ILSight.Core;

namespace ILSight.Exporting;

/// <summary>The export that runs: how far it has got, and whether it was asked to stop and has not stopped yet.</summary>
public sealed record RunningExport(QueuedExport Export, ExportProgress Progress, bool IsCancelling);
