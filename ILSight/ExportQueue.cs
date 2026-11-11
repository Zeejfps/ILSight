using ILSight.Core;

namespace ILSight;

/// <summary>What an export writes.</summary>
public enum ExportKind
{
    CSharpFile,
    ILFile,
    Project,
    Solution,
    Pdb,
    Pdbs,
    Files,
    Diagram,
}

/// <summary>An export as the user asked for it: what it is named after, what it writes, and the
/// folder it writes into.</summary>
public sealed record ExportJob(string Name, ExportKind Kind, string Folder);

/// <summary>Which export of the session one is, through waiting, running and finishing; given by the
/// queue as the export enters it, numbered from 1 in the order they entered.</summary>
public sealed record ExportRun
{
    internal ExportRun(int number)
    {
        Number = number;
    }

    internal int Number { get; }
}

/// <summary>An export in the queue: <see cref="Run"/> tells it apart from every other export of
/// the session, through waiting, running and finishing.</summary>
public sealed record QueuedExport(ExportRun Run, ExportJob Job);

/// <summary>How an export's turn in the queue goes: it starts, or it was taken out of the queue
/// before its turn and must not run.</summary>
public abstract record ExportTurn
{
    private protected ExportTurn()
    {
    }
}

/// <summary>What an export works with once its turn comes: the token that cancels it and where it
/// reports how far it has got. <see cref="Run"/> tells the queue which export ends.</summary>
public sealed record ExportStart(ExportRun Run, CancellationToken Token, IProgress<ExportProgress> Progress) : ExportTurn;

/// <summary>The export was taken out of the queue before its turn.</summary>
public sealed record ExportWithdrawn : ExportTurn;

/// <summary>The export that runs: how far it has got, and whether it was asked to stop and has not stopped yet.</summary>
public sealed record RunningExport(QueuedExport Export, ExportProgress Progress, bool IsCancelling);

/// <summary>The exports asked for, run one at a time in the order they were asked for. Only the
/// running one can end its turn, and its progress, which arrives on the UI thread after the
/// background reported it, is dropped once that turn has ended. A waiting export taken out of the
/// queue gets a withdrawn turn at once, which ends it without running.</summary>
public sealed class ExportQueue
{
    public readonly List<Turn> Turns = new();
    public Active? ActiveExport;
    public int LastRun;

    public bool IsRunning => ActiveExport != null;

    /// <summary>The running export, or null when none runs.</summary>
    public RunningExport? Running => ActiveExport?.Shown;

    /// <summary>How many exports wait for the running one to end.</summary>
    public int WaitingCount => Turns.Count;

    /// <summary>The waiting exports, next first.</summary>
    public IEnumerable<QueuedExport> Waiting => Turns.Select(static t => t.Export);

    public sealed record Turn(QueuedExport Export, TaskCompletionSource<ExportTurn> Started);

    /// <summary>The running export as shown, with the source of the token that cancels it.</summary>
    public sealed class Active
    {
        public CancellationTokenSource Cancel { get; } = new();

        public required RunningExport Shown { get; set; }
    }
}
