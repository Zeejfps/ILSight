using ZGF.Gui.Immediate.Widgets;
using ILSight.Core.Processes;

namespace ILSight.ProcessArea;

/// <summary>The "open from running process" dialog as its widgets bind it: whether it is open, where its window
/// is and the filter typed; and <see cref="Browser"/>, the running .NET processes it found and the assemblies the
/// selected one has loaded, which keeps its own rules. Its owner is the dialog, open once at a time, so it is held once
/// (<see cref="UiState"/>); its window and its two tables' columns are kept with the settings (the settings' save reads
/// them here); were the dialog shown in a second place, each would keep its own.</summary>
public sealed class ProcessPicker
{
    public bool IsOpen;

    /// <summary>Where the dialog's window is, kept between runs.</summary>
    public DialogWindow Window { get; } = new();

    /// <summary>What the processes are filtered by: their name, id or entry assembly.</summary>
    public string Filter = "";

    /// <summary>The columns of the running processes: name, id, entry assembly and runtime.</summary>
    public TableColumns Columns { get; } = new(
        new TableColumn(2f, 80f),
        new TableColumn(1f, 48f),
        new TableColumn(2f, 80f),
        new TableColumn(1f, 56f));

    /// <summary>The columns of the assemblies the selected process loaded: name and folder.</summary>
    public TableColumns ModuleColumns { get; } = new(
        new TableColumn(2f, 100f),
        new TableColumn(3f, 80f));

    /// <summary>The processes found, those the filter shows, the one selected and its modules.</summary>
    public required ProcessBrowser Browser { get; init; }

    /// <summary>Opens the dialog, its filter taking focus, and starts looking for the processes again, as they
    /// come and go: the dialog draws it scanning from this frame on. Returns the work
    /// <see cref="ProcessBrowser.ScanAsync"/> scans as. Closing is only clearing <see cref="IsOpen"/>, as the
    /// window's close box does: <see cref="Browser"/> stops what the dialog started once it is told so.</summary>
    public WorkTicket Open()
    {
        IsOpen = true;
        Browser.FocusFilter();
        return Browser.StartScan();
    }
}
