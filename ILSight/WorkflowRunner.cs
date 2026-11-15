using ZGF.Gui.Immediate;
using ILSight.CommandArea;

namespace ILSight;

/// <summary>The edge where the workflows the screen asks for through
/// <see cref="Commands.Start(IAppCommandWriter, string, AppWorkflow)"/> and the app's start run, and the only code that starts
/// them. Each runs with a token of its own, linked to the app shutting down and to whatever else may cancel it on
/// purpose (the newer work that supersedes it, the dialog that closes it); is counted in the app's background work
/// until it ends unless it runs until shutdown; and has an exception it ends with reported once, unless it is the
/// cancellation one of those asked for. Whether one asked is read from each token it linked as well as the linked one:
/// a cancel still running another thread's callbacks has its source cancelled before the link is reached, and work that
/// met what it ended (a stale open) ends as cancelled from that source. A cancellation none of them asked for, such as a
/// timeout, is reported.
/// Lives on the UI thread, as the app state that holds it does.</summary>
[Edge]
public sealed class WorkflowRunner(BackgroundWork work, ErrorLog errors)
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly List<Task> _untilShutdown = [];

    /// <summary>Starts <paramref name="workflow"/> now, on this thread, counted in the background work as
    /// <paramref name="name"/> until it ends. Its token fires when the app shuts down or any of
    /// <paramref name="cancelledOnPurposeBy"/> fires; one started after <see cref="CancelAll"/>, or after one of
    /// those fired, starts with it cancelled. Ending cancelled by any of them is not a failure; an exception it ends
    /// with otherwise, even before its first await, is reported as <see cref="AppProblem.Unexpected"/> rather than
    /// thrown.</summary>
    public void Start(string name, Func<CancellationToken, Task> workflow, params ReadOnlySpan<CancellationToken> cancelledOnPurposeBy)
    {
        var link = Link(cancelledOnPurposeBy);
        work.Track(name, RunAsync(workflow, null, link, [.. cancelledOnPurposeBy], link.Token));
    }

    /// <summary>As <see cref="Start"/>, for a workflow whose start recorded a pending state on the app state (a tab
    /// loading): one that ends with an unexpected exception, reported, has <paramref name="settle"/> settle that state
    /// with it, so that nothing stays pending for good.</summary>
    public void StartSettling(string name, Func<CancellationToken, Task> workflow, Action<Exception> settle,
        params ReadOnlySpan<CancellationToken> cancelledOnPurposeBy)
    {
        var link = Link(cancelledOnPurposeBy);
        work.Track(name, RunAsync(workflow, settle, link, [.. cancelledOnPurposeBy], link.Token));
    }

    /// <summary>As <see cref="Start"/>, for a workflow that runs until the app shuts down, such as watching a
    /// folder. It is kept here rather than counted in the background work, which a harness waiting for the UI
    /// to be ready waits out.</summary>
    public void StartUntilShutdown(Func<CancellationToken, Task> workflow)
    {
        _untilShutdown.RemoveAll(static running => running.IsCompleted);
        var link = Link([]);
        _untilShutdown.Add(RunAsync(workflow, null, link, [], link.Token));
    }

    /// <summary>Cancels the workflows running, and any started later, as the app shuts down. Their ending
    /// cancelled is not a failure, so it is not reported.</summary>
    public void CancelAll()
    {
        _shutdown.Cancel();
    }

    // Whether the app shutting down or one of the tokens linked asked for the cancellation: each asked itself, as the
    // linked token is reached only once a cancel has run the callbacks registered before the link's.
    private bool IsAskedFor(CancellationToken linked, CancellationToken[] cancelledOnPurposeBy)
    {
        if (linked.IsCancellationRequested || _shutdown.IsCancellationRequested)
        {
            return true;
        }

        foreach (var token in cancelledOnPurposeBy)
        {
            if (token.IsCancellationRequested)
            {
                return true;
            }
        }

        return false;
    }

    private CancellationTokenSource Link(ReadOnlySpan<CancellationToken> cancelledOnPurposeBy)
    {
        return CancellationTokenSource.CreateLinkedTokenSource([_shutdown.Token, .. cancelledOnPurposeBy]);
    }

    private async Task RunAsync(Func<CancellationToken, Task> workflow, Action<Exception>? settle, IDisposable link,
        CancellationToken[] cancelledOnPurposeBy, CancellationToken ct)
    {
        using (link)
        {
            try
            {
                await workflow(ct);
            }
            catch (OperationCanceledException) when (IsAskedFor(ct, cancelledOnPurposeBy))
            {
                // Shut down, superseded or closed: someone asked for it.
                return;
            }
            catch (Exception ex)
            {
                Errors.Report(errors, new AppProblem.Unexpected(ex));
                settle?.Invoke(ex);
            }
        }
    }
}
