using ILSight.Core;
using ILSight.Core.Processes;

namespace ILSight;

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
