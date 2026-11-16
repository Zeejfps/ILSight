namespace ILSight.ExportArea;

/// <summary>Looks at the disk for the export dialog's folder and key file (<see cref="CheckExportFilesHandler"/>) as
/// they change, so the dialog says what is there before the export starts.</summary>
[Edge]
public sealed class ExportFileChecker
{
    private readonly WorkflowRunner _workflows;
    private readonly CheckExportFilesHandler _handler;

    /// <param name="workflows">Runs the checks.</param>
    /// <param name="handler">Looks at the disk.</param>
    internal ExportFileChecker(WorkflowRunner workflows, CheckExportFilesHandler handler)
    {
        _workflows = workflows;
        _handler = handler;
    }

    /// <summary>While <paramref name="setup"/>, the dialog, is open, starts looking at the disk for its folder and key
    /// file when they changed since the last look.</summary>
    public void CheckExportFilesWhenFolderChanges(ExportSetup setup)
    {
        if (!setup.IsOpen)
        {
            return;
        }

        var check = setup.Files.StartCheck(setup.Folder, setup.StrongNameKeyFile);
        if (check is null)
        {
            return;
        }

        StartChecking(setup.Files, check);
    }

    // A method of its own, so that the frames that start nothing allocate no closure.
    private void StartChecking(ExportFileChecks files, ExportFileCheck check)
    {
        var handler = _handler;
        _workflows.StartSettling(nameof(CheckExportFilesHandler), ct => handler.HandleAsync(files, check, ct), error => files.Fail(check, error.Message));
    }
}
