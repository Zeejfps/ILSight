using System.Diagnostics;
using ILSight.CommandArea;

namespace ILSight.App.Tests;

public sealed class WorkflowRunnerTests
{
    /// <summary>Waits, without holding up the thread the workflows' continuations may need, until the work
    /// <paramref name="work"/> counts has ended.</summary>
    internal static async Task WaitUntilEndedAsync(BackgroundWork work)
    {
        var clock = Stopwatch.StartNew();
        while (work.Outstanding.Count > 0)
        {
            if (clock.Elapsed > Patience)
            {
                throw new TimeoutException($"Still running: {string.Join(", ", work.Outstanding)}.");
            }

            await Task.Delay(1);
        }
    }

    private static Exception GetReportedError(ErrorLog errors)
    {
        var entry = Assert.Single(errors.Entries);
        return Assert.IsType<AppProblem.Unexpected>(entry.Problem).Error;
    }

    [Fact]
    public async Task Start_WhileTheWorkflowRuns_CountsItUnderItsName()
    {
        var work = new BackgroundWork();
        var runner = new WorkflowRunner(work, new ErrorLog());
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        runner.Start("waiting", _ => finish.Task);
        var whileRunning = work.Outstanding;
        finish.SetResult();
        await WaitUntilEndedAsync(work);

        Assert.Equal(["waiting"], whileRunning);
        Assert.Empty(work.Outstanding);
    }

    [Fact]
    public async Task Start_WorkflowThrowsAfterAwaiting_ReportsItOnceAndEndsTheWork()
    {
        var work = new BackgroundWork();
        var errors = new ErrorLog();
        var runner = new WorkflowRunner(work, errors);

        runner.Start("failing", static async _ =>
        {
            await Task.Yield();
            throw new InvalidOperationException("after the await");
        });
        await WaitUntilEndedAsync(work);

        Assert.Equal("after the await", Assert.IsType<InvalidOperationException>(GetReportedError(errors)).Message);
    }

    [Fact]
    public async Task Start_WorkflowThrowsBeforeAwaiting_ReportsItRatherThanThrowing()
    {
        var work = new BackgroundWork();
        var errors = new ErrorLog();
        var runner = new WorkflowRunner(work, errors);

        runner.Start("failing at once", static _ => throw new InvalidOperationException("at once"));
        await WaitUntilEndedAsync(work);

        Assert.Equal("at once", Assert.IsType<InvalidOperationException>(GetReportedError(errors)).Message);
    }

    [Fact]
    public async Task CancelAll_WhileAWorkflowRuns_CancelsItsTokenAndReportsNothing()
    {
        var work = new BackgroundWork();
        var errors = new ErrorLog();
        var runner = new WorkflowRunner(work, errors);

        runner.Start("waiting for shutdown", static ct => Task.Delay(Timeout.Infinite, ct));
        runner.CancelAll();
        await WaitUntilEndedAsync(work);

        Assert.Empty(errors.Entries);
    }

    [Fact]
    public async Task Start_AfterCancelAll_StartsWithItsTokenCancelled()
    {
        var work = new BackgroundWork();
        var errors = new ErrorLog();
        var runner = new WorkflowRunner(work, errors);
        var given = CancellationToken.None;

        runner.CancelAll();
        runner.Start("too late", ct =>
        {
            given = ct;
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });
        await WaitUntilEndedAsync(work);

        Assert.True(given.IsCancellationRequested);
        Assert.Empty(errors.Entries);
    }

    [Fact]
    public async Task Start_CancelledWhileTheAppRuns_ReportsItAsAFailure()
    {
        var work = new BackgroundWork();
        var errors = new ErrorLog();
        var runner = new WorkflowRunner(work, errors);

        runner.Start("timing out", static async _ =>
        {
            await Task.Yield();
            throw new TaskCanceledException("timed out");
        });
        await WaitUntilEndedAsync(work);

        Assert.Equal("timed out", Assert.IsType<TaskCanceledException>(GetReportedError(errors)).Message);
    }

    [Fact]
    public async Task Start_SupersededByNewerWork_EndsCancelledAndReportsNothing()
    {
        var work = new BackgroundWork();
        var errors = new ErrorLog();
        var runner = new WorkflowRunner(work, errors);
        var latest = new LatestWork();
        var given = CancellationToken.None;

        runner.Start("older search", ct =>
        {
            given = ct;
            return Task.Delay(Timeout.Infinite, ct);
        }, latest.Start().Token);
#pragma warning disable RL0807 // the newer work only cancels the older one here: nothing runs under its ticket
        latest.Start();
#pragma warning restore RL0807
        await WaitUntilEndedAsync(work);

        Assert.True(given.IsCancellationRequested);
        Assert.Empty(errors.Entries);
    }

    [Fact]
    public async Task Start_DialogClosed_EndsCancelledAndReportsNothing()
    {
        var work = new BackgroundWork();
        var errors = new ErrorLog();
        var runner = new WorkflowRunner(work, errors);
        using var dialog = new CancellationTokenSource();

        runner.Start("fetching for the dialog", static ct => Task.Delay(Timeout.Infinite, ct), dialog.Token);
        await dialog.CancelAsync();
        await WaitUntilEndedAsync(work);

        Assert.Empty(errors.Entries);
    }

    [Fact]
    public async Task Start_ClosedBeforeTheEdgeStartedIt_StartsWithItsTokenCancelledAndReportsNothing()
    {
        var work = new BackgroundWork();
        var errors = new ErrorLog();
        var runner = new WorkflowRunner(work, errors);
        using var dialog = new CancellationTokenSource();
        await dialog.CancelAsync();
        var given = CancellationToken.None;

        runner.Start("queued, then closed", ct =>
        {
            given = ct;
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }, dialog.Token);
        await WaitUntilEndedAsync(work);

        Assert.True(given.IsCancellationRequested);
        Assert.Empty(errors.Entries);
    }

    [Fact]
    public async Task Start_TimedOutWhileNothingCancelledItOnPurpose_ReportsItAsAFailure()
    {
        var work = new BackgroundWork();
        var errors = new ErrorLog();
        var runner = new WorkflowRunner(work, errors);
        using var dialog = new CancellationTokenSource();
        var latest = new LatestWork();

        runner.Start("timing out", static async _ =>
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
            await Task.Delay(Timeout.Infinite, timeout.Token);
        }, dialog.Token, latest.Start().Token);
        await WaitUntilEndedAsync(work);

        Assert.IsType<TaskCanceledException>(GetReportedError(errors));
    }

    [Fact]
    public async Task Start_WorkflowEnded_DisposesItsTokenSourceAndLetsGoOfTheShutdown()
    {
        var work = new BackgroundWork();
        var errors = new ErrorLog();
        var runner = new WorkflowRunner(work, errors);
        var given = CancellationToken.None;

        runner.Start("ending at once", ct =>
        {
            given = ct;
            return Task.CompletedTask;
        });
        await WaitUntilEndedAsync(work);
        runner.CancelAll();

        Assert.True(given.CanBeCanceled);
        Assert.Throws<ObjectDisposedException>(() => given.WaitHandle);
        Assert.False(given.IsCancellationRequested);
    }

    [Fact]
    public async Task StartUntilShutdown_WhileTheWorkflowRuns_IsNotCountedAndShutdownEndsItUnreported()
    {
        var work = new BackgroundWork();
        var errors = new ErrorLog();
        var runner = new WorkflowRunner(work, errors);
        var given = CancellationToken.None;
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        runner.StartUntilShutdown(async ct =>
        {
            given = ct;
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            finally
            {
                ended.SetResult();
            }
        });
        var whileRunning = work.Outstanding;
        runner.CancelAll();
        await ended.Task.WaitAsync(Patience);

        Assert.Empty(whileRunning);
        Assert.True(given.IsCancellationRequested);
        Assert.Empty(errors.Entries);
    }

    [Fact]
    public void StartUntilShutdown_WorkflowThrows_ReportsItRatherThanThrowing()
    {
        var errors = new ErrorLog();
        var runner = new WorkflowRunner(new BackgroundWork(), errors);

        runner.StartUntilShutdown(static _ => throw new InvalidOperationException("at once"));

        Assert.Equal("at once", Assert.IsType<InvalidOperationException>(GetReportedError(errors)).Message);
    }

    [Fact]
    public void Start_QueuedAmongCommands_StartsInTheOrderAskedWithTheAppsShutdownToken()
    {
        var driver = new AppStateDriver();
        var seen = new SeenWorkflow();

        Commands.Later(driver.Ui, seen, static (_, s) => s.Steps.Add("command before"));
        Commands.Start(driver.Ui, "waiting for shutdown", seen, static (_, s, ct) =>
        {
            s.Steps.Add("workflow");
            s.Token = ct;
            return Task.Delay(Timeout.Infinite, ct);
        });
        Commands.Later(driver.Ui, seen, static (_, s) => s.Steps.Add("command after"));
        var askedBeforeTheFrameEnds = seen.Steps.Count;
        driver.NextFrame();
        var startedInOrder = seen.Steps.ToList();
        driver.AppState.Workflows.CancelAll();
        driver.Settle();

        Assert.Equal(0, askedBeforeTheFrameEnds);
        Assert.Equal(["command before", "workflow", "command after"], startedInOrder);
        Assert.True(seen.Token.IsCancellationRequested);
        Assert.Empty(driver.AppState.Errors.Entries);
    }

    /// <summary>What a workflow started through the commands did, and the token it was given.</summary>
    private sealed class SeenWorkflow
    {
        public List<string> Steps { get; } = [];

        public CancellationToken Token { get; set; }
    }
}
