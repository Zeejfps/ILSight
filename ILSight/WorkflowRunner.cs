using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>The edge where the workflows the screen asks for through
/// <see cref="Commands.Start(UiState, string, AppWorkflow)"/> run, and the only code that starts them. Each runs
/// with a token of its own that fires when the app shuts down, is counted in the app's background work until it
/// ends, and has an exception it ends with reported once, unless it is the cancellation shutting down asked for.
/// Lives on the UI thread, as the app state that holds it does.</summary>
public sealed class WorkflowRunner(BackgroundWork work, ErrorLog errors)
{
    private readonly CancellationTokenSource _shutdown = new();

    /// <summary>Starts <paramref name="workflow"/> now, on this thread, counted in the background work as
    /// <paramref name="name"/> until it ends. Its token fires when the app shuts down; one started after
    /// <see cref="CancelAll"/> starts with it cancelled. An exception it ends with, even before its first await,
    /// is reported as <see cref="AppProblem.Unexpected"/> rather than thrown.</summary>
    public void Start(string name, Func<CancellationToken, Task> workflow)
    {
        work.Track(name, RunAsync(workflow, _shutdown.Token));
    }

    /// <summary>Cancels the workflows running, and any started later, as the app shuts down. Their ending
    /// cancelled is not a failure, so it is not reported.</summary>
    public void CancelAll()
    {
        _shutdown.Cancel();
    }

    private async Task RunAsync(Func<CancellationToken, Task> workflow, CancellationToken shutdown)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        try
        {
            await workflow(linked.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !shutdown.IsCancellationRequested)
        {
            Errors.Report(errors, new AppProblem.Unexpected(ex));
        }
    }
}
