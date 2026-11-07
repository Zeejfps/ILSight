using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Processes;

namespace ILSight;

/// <summary>The "open from running process" dialog: the running .NET processes, looked for each time it
/// opens and filtered by name, id or entry assembly, and the assemblies the selected one has loaded.</summary>
public sealed class ProcessPicker
{
    /// <summary>Tells the user that looking for the processes failed.</summary>
    public required Action<AppProblem> ReportError { get; init; }

    /// <summary>Lists the running .NET processes.</summary>
    public required Func<CancellationToken, Task<IReadOnlyList<RunningProcess>>> FindProcesses { get; init; }

    /// <summary>Lists the modules a process has loaded.</summary>
    public required Func<ProcessId, CancellationToken, Task<ModuleListing>> FindModules { get; init; }

    /// <summary>Where the dialog's window is, kept between runs.</summary>
    public DialogWindow Window { get; } = new();

    public bool IsOpen;
    public bool FocusFilter;
    public string Filter = "";
    public string? AppliedFilter;

    public ProcessScan Scan = new ProcessScan.NotStarted();

    /// <summary>Counts the scans started, so that only the latest one's result is kept.</summary>
    public int ScanRequest;

    /// <summary>The processes the filter shows, in the order shown.</summary>
    public readonly List<RunningProcess> Filtered = new();

    /// <summary>The process whose modules are shown, as a row of <see cref="Filtered"/>.</summary>
    public ListSelection ProcessSelection { get; } = new();

    public ModuleScan Modules = new ModuleScan.None();

    /// <summary>Counts the module listings started, so that only the latest one's result is kept.</summary>
    public int ModuleRequest;

    /// <summary>Cancels the module listing running, when another process is picked or the dialog closes.</summary>
    public CancellationTokenSource ModuleWork = new();

    public MultiSelection<RunningModule> ModuleSelection { get; } = new();
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
