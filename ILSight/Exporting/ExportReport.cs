namespace ILSight.Exporting;

/// <summary>What one export did: which it was, how it ended, with the files it wrote and what went wrong,
/// how long it took, and whether its folder was there when it ended.</summary>
public sealed record ExportReport(
    ExportRun Run,
    ExportJob Job,
    ExportEnd End,
    TimeSpan Elapsed,
    bool FolderExists)
{
    public string Name => Job.Name;

    public string Folder => Job.Folder;
}
