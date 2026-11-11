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
/// reports how far it has got. It ends its own turn with <see cref="EndTurn"/>.</summary>
public sealed record ExportStart : ExportTurn
{
    private readonly Action _endTurn;

    internal ExportStart(ExportRun run, IProgress<ExportProgress> progress, Action endTurn, CancellationToken token)
    {
        Run = run;
        Token = token;
        Progress = progress;
        _endTurn = endTurn;
    }

    public ExportRun Run { get; }

    public CancellationToken Token { get; }

    public IProgress<ExportProgress> Progress { get; }

    /// <summary>Ends this export's turn, when it is still the running one, and starts the next
    /// waiting export. Ending it again does nothing.</summary>
    public void EndTurn()
    {
        _endTurn();
    }
}

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
    private readonly List<Turn> _waiting = [];
    private Active? _running;
    private int _lastRun;

    public bool IsRunning => _running is not null;

    /// <summary>The running export, or null when none runs.</summary>
    public RunningExport? Running => _running?.Shown;

    /// <summary>How many exports wait for the running one to end.</summary>
    public int WaitingCount => _waiting.Count;

    /// <summary>The waiting exports, next first.</summary>
    public IEnumerable<QueuedExport> Waiting => _waiting.Select(static t => t.Export);

    /// <summary>Queues <paramref name="job"/>, which has one part until it reports otherwise. The task
    /// completes when its turn comes: at once when nothing runs. A started export ends its turn with
    /// <see cref="ExportStart.EndTurn"/>.</summary>
    public Task<ExportTurn> EnterAsync(ExportJob job)
    {
        _lastRun++;
        var started = new TaskCompletionSource<ExportTurn>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiting.Add(new Turn(new QueuedExport(new ExportRun(_lastRun), job), started));
        if (!IsRunning)
        {
            StartNext();
        }

        return started.Task;
    }

    /// <summary>Asks the running export to stop; the waiting ones still get their turn.</summary>
    public void CancelRunning()
    {
        var running = _running;
        if (running is null)
        {
            return;
        }

        running.Shown = running.Shown with { IsCancelling = true };
        running.Cancel.Cancel();
    }

    /// <summary>Takes the waiting export <paramref name="run"/> out of the queue.</summary>
    public void Withdraw(ExportRun run)
    {
        var index = _waiting.FindIndex(t => t.Export.Run == run);
        if (index < 0)
        {
            return;
        }

        var turn = _waiting[index];
        _waiting.RemoveAt(index);
        turn.Started.SetResult(new ExportWithdrawn());
    }

    private void StartNext()
    {
        var turn = _waiting[0];
        _waiting.RemoveAt(0);
        var export = turn.Export;
        var run = export.Run;
        var active = new Active { Shown = new RunningExport(export, new ExportProgress(export.Job.Name, 1, 1, null), false) };
        _running = active;
        var progress = new Progress<ExportProgress>(p => TakeProgress(run, p));
        turn.Started.SetResult(new ExportStart(run, progress, () => EndTurn(run), active.Cancel.Token));
    }

    /// <summary>Ends the turn of the export <paramref name="run"/>, when it is the running one, and
    /// starts the next.</summary>
    private void EndTurn(ExportRun run)
    {
        var active = FindActive(run);
        if (active is null)
        {
            return;
        }

        active.Cancel.Dispose();
        _running = null;
        if (_waiting.Count == 0)
        {
            return;
        }

        StartNext();
    }

    private void TakeProgress(ExportRun run, ExportProgress progress)
    {
        var active = FindActive(run);
        if (active is null)
        {
            return;
        }

        var shown = active.Shown;
        var furthest = FindFurthest(shown.Progress.Fraction, progress.Fraction);
        active.Shown = shown with { Progress = progress with { Fraction = furthest } };
    }

    /// <summary>The further of two fractions of one export: reports sent from several threads can
    /// arrive out of order, and the bar must not step back.</summary>
    private static float? FindFurthest(float? shown, float? reported)
    {
        if (shown is null)
        {
            return reported;
        }

        if (reported is null)
        {
            return shown;
        }

        return Math.Max(shown.Value, reported.Value);
    }

    /// <summary>The running export's state when <paramref name="run"/> is the running one; null otherwise.</summary>
    private Active? FindActive(ExportRun run)
    {
        var running = _running;
        if (running is null)
        {
            return null;
        }

        if (running.Shown.Export.Run != run)
        {
            return null;
        }

        return running;
    }

    private sealed record Turn(QueuedExport Export, TaskCompletionSource<ExportTurn> Started);

    /// <summary>The running export as shown, with the source of the token that cancels it.</summary>
    private sealed class Active
    {
        public CancellationTokenSource Cancel { get; } = new();

        public required RunningExport Shown { get; set; }
    }
}
