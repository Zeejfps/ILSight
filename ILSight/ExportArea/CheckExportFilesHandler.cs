
namespace ILSight.ExportArea;

/// <summary>Looks at what the export dialog names on the disk through <see cref="IPorts"/>, without holding up the
/// screen: whether the folder already holds files, which the export may replace, and whether a key file is named that
/// is not there, which keeps the export from starting.</summary>
public sealed class CheckExportFilesHandler(CheckExportFilesHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Whether the folder at <paramref name="folder"/> is there and holds any file or folder, looked at
        /// without holding up the screen.</summary>
        Task<bool> HasEntriesAsync(string folder, CancellationToken ct);

        /// <summary>Whether the file at <paramref name="file"/> is there, looked at without holding up the
        /// screen.</summary>
        Task<bool> IsFileThereAsync(string file, CancellationToken ct);
    }

    /// <summary>Looks at what <paramref name="check"/> asks about and hands it to <paramref name="checks"/>: a folder
    /// that is not a full path holds nothing, and no key file named is none missing.</summary>
    public async Task HandleAsync(ExportFileChecks checks, ExportFileCheck check, CancellationToken ct)
    {
        var folder = check.Folder.Trim();
        var keyFile = check.KeyFile.Trim();
        var holdsFiles = false;
        if (Path.IsPathFullyQualified(folder))
        {
            holdsFiles = await ports.HasEntriesAsync(folder, ct);
        }

        var isKeyFileMissing = false;
        if (keyFile.Length > 0)
        {
            isKeyFileMissing = !await ports.IsFileThereAsync(keyFile, ct);
        }

        checks.Finish(check, new ExportFileFacts { FolderHoldsFiles = holdsFiles, IsKeyFileMissing = isKeyFileMissing });
    }
}
