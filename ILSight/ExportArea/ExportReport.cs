namespace ILSight.ExportArea;

/// <summary>What one export did: which it was, how it ended, with the files it wrote and what went wrong,
/// how long it took, and whether its folder was there when it ended.</summary>
public sealed record ExportReport
{
    public required ExportRun Run { get; init; }

    public required ExportJob Job { get; init; }

    public required ExportEnd End { get; init; }

    public required TimeSpan Elapsed { get; init; }

    public required bool FolderExists { get; init; }

    public string Name => Job.Name;

    public string Folder => Job.Folder;
}
