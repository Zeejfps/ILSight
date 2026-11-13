using ILSight.Core;

namespace ILSight.ExportArea;

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

    /// <summary>Queues <paramref name="job"/>, which has one part until it reports otherwise. Its turn comes at once
    /// when nothing runs, else when the exports queued before it have ended. A started export ends its turn with
    /// <see cref="ExportTurn.Started.EndTurn"/>.</summary>
    public ExportTicket Enter(ExportJob job)
    {
        _lastRun++;
        var run = new ExportRun(_lastRun);
        var started = new TaskCompletionSource<ExportTurn>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiting.Add(new Turn { Export = new QueuedExport { Run = run, Job = job }, Started = started });
        if (!IsRunning)
        {
            StartNext();
        }

        return new ExportTicket { Run = run, Job = job, Turn = started.Task };
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
        turn.Started.SetResult(new ExportTurn.Withdrawn());
    }

    private void StartNext()
    {
        var turn = _waiting[0];
        _waiting.RemoveAt(0);
        var export = turn.Export;
        var run = export.Run;
        var active = new Active
        {
            Shown = new RunningExport
            {
                Export = export,
                Progress = new ExportProgress
                {
                    Name = export.Job.Name,
                    Part = 1,
                    Parts = 1,
                    Fraction = null,
                },
                IsCancelling = false,
            },
        };
        _running = active;
        var progress = new Progress<ExportProgress>(p => TakeProgress(run, p));
        turn.Started.SetResult(new ExportTurn.Started(run, progress, () => EndTurn(run), active.Cancel.Token));
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

    private sealed record Turn
    {
        public required QueuedExport Export { get; init; }

        public required TaskCompletionSource<ExportTurn> Started { get; init; }
    }

    /// <summary>The running export as shown, with the source of the token that cancels it.</summary>
    private sealed class Active
    {
        public CancellationTokenSource Cancel { get; } = new();

        public required RunningExport Shown { get; set; }
    }
}
