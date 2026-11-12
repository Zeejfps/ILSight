namespace ILSight.ExportArea;

/// <summary>What the disk says about the folder and key file an export names: looked up where the
/// dialog is shown and the export started, so what keeps it from starting is decided without a disk.</summary>
public readonly record struct ExportFileFacts
{
    /// <summary>Whether the folder already holds files, which the export may replace.</summary>
    public required bool FolderHoldsFiles { get; init; }

    /// <summary>Whether a key file is named that is not there.</summary>
    public required bool IsKeyFileMissing { get; init; }
}
