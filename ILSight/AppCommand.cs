namespace ILSight;

/// <summary>What the screen asks of the app state, which <see cref="UiState"/> queues while it draws
/// and <see cref="ILSightApp"/> runs, in the order queued, once the frame is drawn. Made through
/// <see cref="UiState.Later(AppCommand)"/> and its overload, from a <c>static</c> lambda handed what it
/// acts on, so that a view drawn every frame allocates nothing until it asks. Asking for an async workflow
/// queues one too, which starts it (<see cref="Commands.Start(UiState, string, AppWorkflow)"/>), so commands and
/// workflow starts run in the order they were asked for.</summary>
public delegate void AppCommand(AppState appState);

/// <summary>What the screen asks to run in the background over the app state: a workflow that
/// <see cref="Commands.Start(UiState, string, AppWorkflow)"/> queues with the <see cref="AppCommand"/>s, which
/// <see cref="WorkflowRunner"/> starts in its place among them once the frame is drawn. <paramref name="ct"/> fires
/// when the app shuts down.</summary>
public delegate Task AppWorkflow(AppState appState, CancellationToken ct);

public static class Commands
{
    /// <summary>Asks for <paramref name="run"/> to be done to the app state once the frame is drawn.
    /// Pass a <c>static</c> lambda.</summary>
    public static void Later(UiState uiState, AppCommand run)
    {
        uiState.AppCommands.Enqueue(run);
    }

    /// <summary>Asks for <paramref name="run"/> to be done to the app state and <paramref name="subject"/>
    /// once the frame is drawn. Pass a <c>static</c> lambda, handing it what it acts on as
    /// <paramref name="subject"/>, so that nothing is allocated unless this is called.</summary>
    public static void Later<T>(UiState uiState, T subject, Action<AppState, T> run)
    {
        uiState.AppCommands.Enqueue(Create(subject, run));
    }

    /// <summary>A command doing <paramref name="run"/> to <paramref name="subject"/>. It is made here, so
    /// that a caller that only sometimes asks doesn't allocate a closure on the way in every time.</summary>
    public static AppCommand Create<T>(T subject, Action<AppState, T> run)
    {
        return appState =>
        {
            run(appState, subject);
        };
    }

    /// <summary>Asks for <paramref name="workflow"/> to be started over the app state once the frame is drawn, in its
    /// place among the commands queued, and counted in the app's background work as <paramref name="name"/> until it
    /// ends. It runs on the UI thread up to its first await before the next command does. Pass a <c>static</c>
    /// lambda.</summary>
    public static void Start(UiState uiState, string name, AppWorkflow workflow)
    {
        uiState.AppCommands.Enqueue(CreateStart(name, workflow));
    }

    /// <summary>As <see cref="Start(UiState, string, AppWorkflow)"/>, over the app state and
    /// <paramref name="subject"/>. Pass a <c>static</c> lambda, handing it what it acts on as
    /// <paramref name="subject"/>, so that nothing is allocated unless this is called.</summary>
    public static void Start<T>(UiState uiState, string name, T subject, Func<AppState, T, CancellationToken, Task> workflow)
    {
        uiState.AppCommands.Enqueue(CreateStart(name, subject, workflow));
    }

    /// <summary>As <see cref="Start{T}(UiState, string, T, Func{AppState, T, CancellationToken, Task})"/>, for a
    /// workflow that <paramref name="cancelledOnPurposeBy"/> cancels on purpose, such as the token of the work it was
    /// started as, which newer work or closing a dialog cancels: its token fires with it too, and ending cancelled
    /// by it is not a failure.</summary>
    public static void Start<T>(UiState uiState, string name, T subject, Func<AppState, T, CancellationToken, Task> workflow, CancellationToken cancelledOnPurposeBy)
    {
        uiState.AppCommands.Enqueue(CreateStart(name, subject, workflow, cancelledOnPurposeBy));
    }

    private static AppCommand CreateStart(string name, AppWorkflow workflow)
    {
        return appState =>
        {
            appState.Workflows.Start(name, ct => workflow(appState, ct));
        };
    }

    private static AppCommand CreateStart<T>(string name, T subject, Func<AppState, T, CancellationToken, Task> workflow)
    {
        return appState =>
        {
            appState.Workflows.Start(name, ct => workflow(appState, subject, ct));
        };
    }

    private static AppCommand CreateStart<T>(string name, T subject, Func<AppState, T, CancellationToken, Task> workflow, CancellationToken cancelledOnPurposeBy)
    {
        return appState =>
        {
            appState.Workflows.Start(name, ct => workflow(appState, subject, ct), cancelledOnPurposeBy);
        };
    }
}
