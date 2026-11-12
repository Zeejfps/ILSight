namespace ILSight.Exporting;

/// <summary>A look at the disk <see cref="ExportFileChecks.StartCheck"/> asked for, for
/// <see cref="CheckExportFilesHandler"/> to make: the folder and key file the export dialog names, as typed. Dropped,
/// the dialog waits for what the disk says about them for good, and the export can't start.</summary>
[MustUse]
public sealed class ExportFileCheck
{
    public required string Folder { get; init; }

    public required string KeyFile { get; init; }

    /// <summary>Whether this check is of <paramref name="folder"/> and <paramref name="keyFile"/>, as typed.</summary>
    public bool IsOf(string folder, string keyFile)
    {
        return Folder == folder && KeyFile == keyFile;
    }
}
