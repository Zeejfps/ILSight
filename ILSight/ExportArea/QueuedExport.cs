namespace ILSight.ExportArea;

/// <summary>An export in the queue: <see cref="Run"/> tells it apart from every other export of
/// the session, through waiting, running and finishing.</summary>
public sealed record QueuedExport(ExportRun Run, ExportJob Job);
