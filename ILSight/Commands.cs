namespace ILSight;

[Edge]
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

    /// <summary>Asks for what <paramref name="load"/> started loading in its tab, if anything, to be brought in once
    /// the frame is drawn (<see cref="StartDecompiling(AppState, DecompileTicket?)"/>): the draw points the tab in the
    /// frame of its input, and only the decompile, which takes longer than a frame, waits for the edge. Allocates
    /// nothing when no load started.</summary>
    public static void DecompileLater(UiState uiState, DecompileTicket? load)
    {
        if (load is null)
        {
            return;
        }

        uiState.AppCommands.Enqueue(CreateDecompile(load));
    }

    private static AppCommand CreateDecompile(DecompileTicket load)
    {
        return appState =>
        {
            StartDecompiling(appState, load);
        };
    }

    /// <summary>Asks for the comparison <paramref name="run"/> started, if any, to be run once the frame is drawn
    /// (<see cref="StartComparing"/>): the draw changed the comparison in the frame of its input, and only comparing,
    /// which takes longer than a frame, waits for the edge. Allocates nothing when none started.</summary>
    public static void CompareLater(UiState uiState, ComparisonRun? run)
    {
        if (run is null)
        {
            return;
        }

        uiState.AppCommands.Enqueue(CreateCompare(run));
    }

    private static AppCommand CreateCompare(ComparisonRun run)
    {
        return appState =>
        {
            StartComparing(appState, run);
        };
    }

    /// <summary>Starts running the comparison <paramref name="run"/> (<see cref="CompareAssembliesHandler"/>) now,
    /// counted in the app's background work until it ends, on a token that a newer comparison of it and closing its tab
    /// cancel too.</summary>
    public static void StartComparing(AppState appState, ComparisonRun run)
    {
        appState.Workflows.Start(nameof(CompareAssembliesHandler), ct => appState.CompareAssembliesHandler.HandleAsync(run, ct), run.Work.Token);
    }

    /// <summary>Asks for <paramref name="start"/> to be done to the app state and <paramref name="subject"/> once the
    /// frame is drawn, and for the work it started, if any, to be run then as <paramref name="run"/>, counted in the
    /// app's background work as <paramref name="name"/> until it ends, on a token that what
    /// <paramref name="cancelledOnPurposeBy"/> picks of that work cancels too: the canceller the state change made,
    /// such as a diagram it started reading cards for, which closing it cancels (R009). Nothing runs when
    /// <paramref name="start"/> started nothing. Pass <c>static</c> lambdas, handing them what they act on as
    /// <paramref name="subject"/>, so that nothing is allocated unless this is called.</summary>
    public static void StartLater<T, TWork>(UiState uiState, string name, T subject, Func<AppState, T, TWork?> start,
        Func<AppState, T, TWork, CancellationToken, Task> run, Func<TWork, CancellationToken> cancelledOnPurposeBy) where TWork : class
    {
        uiState.AppCommands.Enqueue(CreateStartLater(name, subject, start, run, cancelledOnPurposeBy));
    }

    private static AppCommand CreateStartLater<T, TWork>(string name, T subject, Func<AppState, T, TWork?> start,
        Func<AppState, T, TWork, CancellationToken, Task> run, Func<TWork, CancellationToken> cancelledOnPurposeBy) where TWork : class
    {
        return appState =>
        {
            var work = start(appState, subject);
            if (work is null)
            {
                return;
            }

            appState.Workflows.Start(name, ct => run(appState, subject, work, ct), cancelledOnPurposeBy(work));
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

    /// <summary>Starts bringing in each of <paramref name="loads"/>, as <see cref="StartDecompiling(AppState, DecompileTicket?)"/>
    /// does: each on a token of its own, which newer work on its tab cancels.</summary>
    public static void StartDecompiling(AppState appState, List<DecompileTicket> loads)
    {
        foreach (var load in loads)
        {
            StartDecompiling(appState, load);
        }
    }

    /// <summary>Starts each of <paramref name="loads"/> now: each node decompiled as
    /// <see cref="StartDecompiling(AppState, DecompileTicket?)"/> does, and the cards of each reopened diagram read
    /// (<see cref="ReopenDiagramHandler"/>) on a token that closing the diagram cancels too.</summary>
    public static void StartLoads(AppState appState, TabLoads loads)
    {
        loads.Switch(load => StartDecompiling(appState, load), read => StartReadingCards(appState, read), lookup => StartFindingNodes(appState, lookup),
            run => StartComparing(appState, run));
    }

    /// <summary>Starts finding the nodes <paramref name="lookup"/>'s reopened tabs wait for
    /// (<see cref="ShowReopenedNodesHandler"/>) as a workflow of its own, then each load it points a tab at.</summary>
    private static void StartFindingNodes(AppState appState, ReopenedNodesLookup lookup)
    {
        StartShowing(appState, nameof(ShowReopenedNodesHandler), ct => appState.ShowReopenedNodesHandler.HandleAsync(appState, lookup, ct));
    }

    private static void StartReadingCards(AppState appState, SavedDiagramRead read)
    {
        appState.Workflows.Start(nameof(ReopenDiagramHandler), ct => appState.ReopenDiagramHandler.HandleAsync(appState.Reopened, read, ct), read.Diagram.Token);
    }

    /// <summary>Starts <paramref name="show"/> now, as <see cref="WorkflowRunner.Start"/> does, then the load it
    /// pointed a tab at, if any (<see cref="StartDecompiling(AppState, DecompileTicket?)"/>): the workflow ends where
    /// the tab shows it loading, since newer work on the tab cancels the load and not the workflow.</summary>
    public static void StartShowing(AppState appState, string name, Func<CancellationToken, Task<DecompileTicket?>> show)
    {
        appState.Workflows.Start(name, async ct => StartDecompiling(appState, await show(ct)));
    }

    /// <summary>As <see cref="StartShowing(AppState, string, Func{CancellationToken, Task{DecompileTicket?}})"/>, for
    /// a workflow that points several tabs, then starts each load.</summary>
    public static void StartShowing(AppState appState, string name, Func<CancellationToken, Task<List<DecompileTicket>>> show)
    {
        appState.Workflows.Start(name, async ct => StartDecompiling(appState, await show(ct)));
    }

    /// <summary>As <see cref="StartShowing(AppState, string, Func{CancellationToken, Task{DecompileTicket?}})"/>, for
    /// a workflow that returns the loads it started in tabs of every kind, each started once it ends
    /// (<see cref="StartLoads"/>).</summary>
    public static void StartShowing(AppState appState, string name, Func<CancellationToken, Task<TabLoads>> show)
    {
        appState.Workflows.Start(name, async ct => StartLoads(appState, await show(ct)));
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

    /// <summary>As <see cref="Start(UiState, string, AppWorkflow)"/>, for a workflow that points a tab at what it is to
    /// show and returns the load it started there, which is started once the workflow ends
    /// (<see cref="StartShowing(AppState, string, Func{CancellationToken, Task{DecompileTicket?}})"/>). Pass a
    /// <c>static</c> lambda.</summary>
    public static void Start(UiState uiState, string name, Func<AppState, CancellationToken, Task<DecompileTicket?>> show)
    {
        uiState.AppCommands.Enqueue(CreateShowing(name, show));
    }

    /// <summary>As <see cref="Start(UiState, string, Func{AppState, CancellationToken, Task{DecompileTicket?}})"/>, over
    /// the app state and <paramref name="subject"/>. Pass a <c>static</c> lambda, handing it what it acts on as
    /// <paramref name="subject"/>, so that nothing is allocated unless this is called.</summary>
    public static void Start<T>(UiState uiState, string name, T subject, Func<AppState, T, CancellationToken, Task<DecompileTicket?>> show)
    {
        uiState.AppCommands.Enqueue(CreateShowing(name, subject, show));
    }

    /// <summary>As <see cref="Start{T}(UiState, string, T, Func{AppState, T, CancellationToken, Task{DecompileTicket?}})"/>,
    /// for a workflow that points several tabs and returns each load it started.</summary>
    public static void Start<T>(UiState uiState, string name, T subject, Func<AppState, T, CancellationToken, Task<List<DecompileTicket>>> show)
    {
        uiState.AppCommands.Enqueue(CreateShowing(name, subject, show));
    }

    /// <summary>As <see cref="Start(UiState, string, Func{AppState, CancellationToken, Task{DecompileTicket?}})"/>, for
    /// a workflow that returns the loads it started in tabs of every kind
    /// (<see cref="StartShowing(AppState, string, Func{CancellationToken, Task{TabLoads}})"/>).</summary>
    public static void Start(UiState uiState, string name, Func<AppState, CancellationToken, Task<TabLoads>> show)
    {
        uiState.AppCommands.Enqueue(CreateShowing(name, show));
    }

    /// <summary>As <see cref="Start(UiState, string, Func{AppState, CancellationToken, Task{TabLoads}})"/>, over the
    /// app state and <paramref name="subject"/>. Pass a <c>static</c> lambda, handing it what it acts on as
    /// <paramref name="subject"/>, so that nothing is allocated unless this is called.</summary>
    public static void Start<T>(UiState uiState, string name, T subject, Func<AppState, T, CancellationToken, Task<TabLoads>> show)
    {
        uiState.AppCommands.Enqueue(CreateShowing(name, subject, show));
    }

    /// <summary>As <see cref="Start{T}(UiState, string, T, Func{AppState, T, CancellationToken, Task})"/>, for a
    /// workflow that <paramref name="cancelledOnPurposeBy"/> cancels on purpose, such as the token of the work it was
    /// started as, which newer work or closing a dialog cancels: its token fires with it too, and ending cancelled
    /// by it is not a failure.</summary>
    public static void Start<T>(UiState uiState, string name, T subject, Func<AppState, T, CancellationToken, Task> workflow, CancellationToken cancelledOnPurposeBy)
    {
        uiState.AppCommands.Enqueue(CreateStart(name, subject, workflow, cancelledOnPurposeBy));
    }

    private static AppCommand CreateShowing(string name, Func<AppState, CancellationToken, Task<DecompileTicket?>> show)
    {
        return appState =>
        {
            StartShowing(appState, name, ct => show(appState, ct));
        };
    }

    private static AppCommand CreateShowing<T>(string name, T subject, Func<AppState, T, CancellationToken, Task<DecompileTicket?>> show)
    {
        return appState =>
        {
            StartShowing(appState, name, ct => show(appState, subject, ct));
        };
    }

    private static AppCommand CreateShowing<T>(string name, T subject, Func<AppState, T, CancellationToken, Task<List<DecompileTicket>>> show)
    {
        return appState =>
        {
            StartShowing(appState, name, ct => show(appState, subject, ct));
        };
    }

    private static AppCommand CreateShowing(string name, Func<AppState, CancellationToken, Task<TabLoads>> show)
    {
        return appState =>
        {
            StartShowing(appState, name, ct => show(appState, ct));
        };
    }

    private static AppCommand CreateShowing<T>(string name, T subject, Func<AppState, T, CancellationToken, Task<TabLoads>> show)
    {
        return appState =>
        {
            StartShowing(appState, name, ct => show(appState, subject, ct));
        };
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
