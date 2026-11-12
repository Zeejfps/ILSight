using ILSight.Core;
using ILSight.Core.Processes;

namespace ILSight;

/// <summary>The "open from running process" dialog as its widgets bind it: whether it is open, where its window
/// is and the filter typed; and <see cref="Browser"/>, the running .NET processes it found and the assemblies the
/// selected one has loaded, which keeps its own rules.</summary>
public sealed class ProcessPicker
{
    public bool IsOpen;

    /// <summary>Where the dialog's window is, kept between runs.</summary>
    public DialogWindow Window { get; } = new();

    /// <summary>What the processes are filtered by: their name, id or entry assembly.</summary>
    public string Filter = "";

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

    /// <summary>Takes in the filter as typed; returns the process whose modules are to be listed now, or null.
    /// See <see cref="ProcessBrowser.Update"/>.</summary>
    public RunningProcess? Update()
    {
        return Browser.Update(Filter);
    }
}

/// <summary>How far looking for the running processes has got.</summary>
public abstract record ProcessScan
{
    private ProcessScan()
    {
    }

    public sealed record NotStarted : ProcessScan;

    public sealed record Scanning : ProcessScan;

    public sealed record Scanned(IReadOnlyList<RunningProcess> Processes) : ProcessScan;

    public sealed record Failed(string Error) : ProcessScan;
}

/// <summary>How far listing the selected process's modules has got. Each case past <see cref="None"/> names
/// its process, and those that ended name its entry assembly, which a failed listing can still find from the
/// command line.</summary>
public abstract record ModuleScan
{
    private ModuleScan()
    {
    }

    /// <summary>No process is selected.</summary>
    public sealed record None : ModuleScan;

    public sealed record Loading(RunningProcess Process) : ModuleScan;

    public sealed record Loaded(RunningProcess Process, IReadOnlyList<RunningModule> Modules, AssemblyPath? EntryAssembly) : ModuleScan;

    /// <summary>The process could not be asked: it is gone, refused, or didn't answer in time.</summary>
    public sealed record Unreachable(RunningProcess Process, string Detail, AssemblyPath? EntryAssembly) : ModuleScan;

    /// <summary>The process answered with what could not be read.</summary>
    public sealed record Unreadable(RunningProcess Process, string Detail, AssemblyPath? EntryAssembly) : ModuleScan;
}
