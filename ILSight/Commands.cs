namespace ILSight;

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

    /// <summary>Asks for <paramref name="show"/> to be done to the app state and <paramref name="subject"/> once the
    /// frame is drawn, pointing a tab at what it is to show, and for the load it started in the tab, if any, to be
    /// started then (<see cref="StartDecompiling"/>). Pass a <c>static</c> lambda, handing it what it acts on as
    /// <paramref name="subject"/>, so that nothing is allocated unless this is called.</summary>
    public static void ShowLater<T>(UiState uiState, T subject, Func<AppState, T, DecompileTicket?> show)
    {
        uiState.AppCommands.Enqueue(CreateShow(subject, show));
    }

    private static AppCommand CreateShow<T>(T subject, Func<AppState, T, DecompileTicket?> show)
    {
        return appState =>
        {
            StartDecompiling(appState, show(appState, subject));
        };
    }

    /// <summary>Starts bringing in what <paramref name="load"/> started loading in its tab
    /// (<see cref="DecompileIntoTabHandler"/>) now, counted in the app's background work until it ends, on a token
    /// that newer work on the tab and closing it cancel too; nothing when no load started.</summary>
    public static void StartDecompiling(AppState appState, DecompileTicket? load)
    {
        if (load is null)
        {
            return;
        }

        appState.Workflows.Start(nameof(DecompileIntoTabHandler), ct => appState.DecompileIntoTabHandler.HandleAsync(appState.ViewCommands, load, ct),
            load.Work.Token);
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
