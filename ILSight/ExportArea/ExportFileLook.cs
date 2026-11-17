namespace ILSight.ExportArea;

/// <summary>How far the look at the disk for the folder and key file the export dialog names has got: being looked at,
/// looked at with what the disk said, or failed, saying why (the folder could not be listed), until it is asked
/// again.</summary>
public union ExportFileLook(ExportFileLook.Checking, ExportFileLook.Checked, ExportFileLook.Failed)
{
    public sealed record Checking;

    public sealed record Checked
    {
        public required ExportFileFacts Facts { get; init; }
    }

    public sealed record Failed
    {
        public required string Why { get; init; }
    }
}
