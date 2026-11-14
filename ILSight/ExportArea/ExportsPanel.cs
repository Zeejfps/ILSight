namespace ILSight.ExportArea;

/// <summary>What the exports panel shows of the exports: the one selected, and the one its context menu was
/// opened on. One that is gone is as none.</summary>
public sealed class ExportsPanel
{
    /// <summary>What Enter, Delete and Ctrl+C act on.</summary>
    public ExportRun? Selected { get; private set; }

    /// <summary>The export the panel's context menu was opened on, or null.</summary>
    public ExportRun? MenuTarget { get; private set; }

    public void Select(ExportRun run)
    {
        Selected = run;
    }

    /// <summary>Opens the context menu's actions on <paramref name="run"/>.</summary>
    public void TargetMenu(ExportRun run)
    {
        MenuTarget = run;
    }
}
