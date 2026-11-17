using ILSight.Core;
using ILSight.Core.ProcessArea;

namespace ILSight.ProcessArea;

/// <summary>How far listing the selected process's modules has got. Each case past <see cref="None"/> names
/// its process, and those that ended name its entry assembly, which a failed listing can still find from the
/// command line.</summary>
public union ModuleScan(ModuleScan.None, ModuleScan.Loading, ModuleScan.Loaded, ModuleScan.Unreachable,
    ModuleScan.Unreadable)
{
    /// <summary>No process is selected.</summary>
    public sealed record None;

    public sealed record Loading(RunningProcess Process);

    public sealed record Loaded
    {
        public required RunningProcess Process { get; init; }

        public required IReadOnlyList<RunningModule> Modules { get; init; }

        public required AssemblyPath? EntryAssembly { get; init; }
    }

    /// <summary>The process could not be asked: it is gone, refused, or didn't answer in time.</summary>
    public sealed record Unreachable
    {
        public required RunningProcess Process { get; init; }

        /// <summary>Why, the cause the picker words in the app's language.</summary>
        public required UnreachableCause Cause { get; init; }

        public required AssemblyPath? EntryAssembly { get; init; }
    }

    /// <summary>The process answered with what could not be read.</summary>
    public sealed record Unreadable
    {
        public required RunningProcess Process { get; init; }

        /// <summary>Why, the cause the picker words in the app's language.</summary>
        public required NettraceFault Fault { get; init; }

        /// <summary>What the reader threw, its message the English diagnostic.</summary>
        public required Exception Error { get; init; }

        public required AssemblyPath? EntryAssembly { get; init; }
    }
}
