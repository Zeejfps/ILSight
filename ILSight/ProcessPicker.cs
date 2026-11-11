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
