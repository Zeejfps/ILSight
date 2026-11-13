
namespace ILSight.ExportArea;

/// <summary>Looks at what the export dialog names on the disk through <see cref="IPorts"/>, without holding up the
/// screen: whether the folder already holds files, which the export may replace, and whether a key file is named that
/// is not there, which keeps the export from starting.</summary>
public sealed class CheckExportFilesHandler(CheckExportFilesHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Whether the folder at <paramref name="folder"/> is there and holds any file or folder, looked at
        /// without holding up the screen; a folder that could not be listed comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<bool>> LookInFolderAsync(string folder, CancellationToken ct);

        /// <summary>Whether the file at <paramref name="file"/> is there, looked at without holding up the
        /// screen.</summary>
        Task<bool> IsFileThereAsync(string file, CancellationToken ct);
    }

    /// <summary>Looks at what <paramref name="check"/> asks about and hands it to <paramref name="checks"/>: a folder
    /// that is not a full path holds nothing, and no key file named is none missing; a folder that could not be listed
    /// fails the look, saying why.</summary>
    public async Task HandleAsync(ExportFileChecks checks, ExportFileCheck check, CancellationToken ct)
    {
        var folderLook = await LookAtFolderAsync(check.Folder.Trim(), ct);
        await folderLook.Match(
            ok => FinishAsync(checks, check, ok.Value, ct),
            failed => FailAsync(checks, check, failed.Error, ct));
    }

    /// <summary>Whether <paramref name="folder"/> holds files: none for one that is not a full path.</summary>
    private Task<Outcome<bool>> LookAtFolderAsync(string folder, CancellationToken ct)
    {
        if (!Path.IsPathFullyQualified(folder))
        {
            return Task.FromResult<Outcome<bool>>(new Outcome<bool>.Ok(false));
        }

        return ports.LookInFolderAsync(folder, ct);
    }

    /// <summary>Looks at the key file <paramref name="check"/> names, then hands what the disk says to
    /// <paramref name="checks"/>.</summary>
    private async Task FinishAsync(ExportFileChecks checks, ExportFileCheck check, bool holdsFiles, CancellationToken ct)
    {
        var keyFile = check.KeyFile.Trim();
        var isKeyFileMissing = false;
        if (keyFile.Length > 0)
        {
            isKeyFileMissing = !await ports.IsFileThereAsync(keyFile, ct);
        }

        checks.Finish(check, new ExportFileFacts { FolderHoldsFiles = holdsFiles, IsKeyFileMissing = isKeyFileMissing });
    }

    /// <summary>Hands <paramref name="checks"/> that <paramref name="check"/> failed with <paramref name="error"/>; nothing
    /// is left to wait for, so <paramref name="ct"/> stops nothing.</summary>
    private static Task FailAsync(ExportFileChecks checks, ExportFileCheck check, Exception error, CancellationToken ct)
    {
        checks.Fail(check, error.Message);
        return Task.CompletedTask;
    }
}
