using ZGF.Gui.Immediate;
using ILSight.Commanding;

namespace ILSight;

/// <summary>The edge where the workflows the screen asks for through
/// <see cref="Commands.Start(UiState, string, AppWorkflow)"/> and the app's start run, and the only code that starts
/// them. Each runs with a token of its own, linked to the app shutting down and to whatever else may cancel it on
/// purpose (the newer work that supersedes it, the dialog that closes it); is counted in the app's background work
/// until it ends unless it runs until shutdown; and has an exception it ends with reported once, unless it is the
/// cancellation one of those asked for. A cancellation none of them asked for, such as a timeout, is reported.
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
        work.Track(name, RunAsync(workflow, link, link.Token));
    }

    /// <summary>As <see cref="Start"/>, for a workflow that runs until the app shuts down, such as watching a
    /// folder. It is kept here rather than counted in the background work, which a harness waiting for the UI
    /// to be ready waits out.</summary>
    public void StartUntilShutdown(Func<CancellationToken, Task> workflow)
    {
        _untilShutdown.RemoveAll(static running => running.IsCompleted);
        var link = Link([]);
        _untilShutdown.Add(RunAsync(workflow, link, link.Token));
    }

    /// <summary>Cancels the workflows running, and any started later, as the app shuts down. Their ending
    /// cancelled is not a failure, so it is not reported.</summary>
    public void CancelAll()
    {
        _shutdown.Cancel();
    }

    /// <summary>A token source of the workflow's own, cancelled with the app's shutdown and with each of
    /// <paramref name="cancelledOnPurposeBy"/>.</summary>
    private CancellationTokenSource Link(ReadOnlySpan<CancellationToken> cancelledOnPurposeBy)
    {
        return CancellationTokenSource.CreateLinkedTokenSource([_shutdown.Token, .. cancelledOnPurposeBy]);
    }

    /// <summary>Runs <paramref name="workflow"/> on <paramref name="ct"/>, the token of <paramref name="link"/>,
    /// which it lets go of once the workflow ends.</summary>
    private async Task RunAsync(Func<CancellationToken, Task> workflow, IDisposable link, CancellationToken ct)
    {
        using (link)
        {
            try
            {
                await workflow(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Shut down, superseded or closed: someone asked for it.
                return;
            }
            catch (Exception ex)
            {
                Errors.Report(errors, new AppProblem.Unexpected(ex));
            }
        }
    }
}
