using ILSight.Core;
using ILSight.CodeTooltipping;
using ILSight.AssemblyListArea;
using ILSight.Searching;
using ILSight.Diagramming;
using ILSight.AssemblyArea;
using ILSight.MetadataArea;

namespace ILSight.Commanding;

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

    /// <summary>Asks for <paramref name="reference"/> to be looked up once the frame is drawn, for the request
    /// <paramref name="lookup"/> began, and shown where it leads (<see cref="NavigateHandler"/>), then the load that
    /// started there: the draw began the request in the frame of its input (the app says it is resolving), and only
    /// the lookup, which takes longer than a frame, waits for the edge. Nothing when <paramref name="lookup"/> is
    /// null.</summary>
    public static void FollowLater(UiState uiState, CodeReference.Navigable reference, ResolveTicket? lookup)
    {
        if (lookup is null)
        {
            return;
        }

        Start(uiState, nameof(NavigateHandler), (reference, lookup), static (a, s, ct) => a.NavigateHandler.HandleAsync(a, s.reference, s.lookup, ct));
    }

    /// <summary>As <see cref="FollowLater"/>, showing the metadata table row that defines what
    /// <paramref name="reference"/> points at (<see cref="ShowInMetadataHandler"/>).</summary>
    public static void ShowInMetadataLater(UiState uiState, CodeReference.Navigable reference, ResolveTicket lookup)
    {
        Start(uiState, nameof(ShowInMetadataHandler), (reference, lookup), static (a, s, ct) => a.ShowInMetadataHandler.HandleAsync(a, s.reference, s.lookup, ct));
    }

    /// <summary>Asks for what <paramref name="reference"/> points at to be analyzed once the frame is drawn: the analyzer
    /// opens with a root that shows as loading (<see cref="AnalyzerPanelView.Analyze(UiState, AnalyzerTree, CodeReference.Navigable)"/>),
    /// and what the reference points at is found and analyzed then (<see cref="StartAnalyzing"/>).</summary>
    public static void AnalyzeLater(UiState uiState, CodeReference.Navigable reference)
    {
        uiState.AppCommands.Enqueue(CreateAnalyze(uiState, reference));
    }

    private static AppCommand CreateAnalyze(UiState uiState, CodeReference.Navigable reference)
    {
        return appState =>
        {
            StartAnalyzing(appState, AnalyzerPanelView.Analyze(uiState, appState.Analyzer, reference));
        };
    }

    /// <summary>Starts finding what the reference of <paramref name="pending"/> points at and analyzing it in its root
    /// (<see cref="AnalyzeReferenceHandler"/>) now, counted in the app's background work until it ends.</summary>
    public static void StartAnalyzing(AppState appState, PendingAnalysis pending)
    {
        appState.Workflows.Start(nameof(AnalyzeReferenceHandler), ct => appState.AnalyzeReferenceHandler.HandleAsync(appState.Analyzer, appState.ViewCommands, pending, ct));
    }

    /// <summary>Starts running the analysis <paramref name="run"/> is for (<see cref="RunAnalysisHandler"/>) now,
    /// counted in the app's background work until it ends, on a token that cancelling the analysis cancels too;
    /// nothing when no run started.</summary>
    public static void StartAnalysis(AppState appState, AnalysisRun? run)
    {
        if (run is null)
        {
            return;
        }

        appState.Workflows.Start(nameof(RunAnalysisHandler), ct => appState.RunAnalysisHandler.HandleAsync(appState.Analyzer, run, ct), run.Work.Token);
    }

    /// <summary>Starts finding the symbols of the analyses <paramref name="restore"/> waits for
    /// (<see cref="RestoreAnalysesHandler"/>) now, counted in the app's background work until it ends; nothing when none
    /// waits.</summary>
    public static void StartRestoringAnalyses(AppState appState, AnalysesRestore? restore)
    {
        if (restore is null)
        {
            return;
        }

        appState.Workflows.Start(nameof(RestoreAnalysesHandler),
            ct => appState.RestoreAnalysesHandler.HandleAsync(appState.Analyzer, appState.Assemblies, appState.ViewCommands, restore, ct));
    }

    /// <summary>Asks for the namespaces of each assembly <paramref name="expansion"/> expanded in <paramref name="tree"/>
    /// to be expanded once the frame is drawn (<see cref="StartExpandingNamespaces"/>): the draw expanded the assemblies
    /// in the frame of its input, and only building their children, which takes longer than a frame, waits for the
    /// edge.</summary>
    public static void ExpandNamespacesLater(UiState uiState, BrowseTree tree, NamespaceExpansion expansion)
    {
        uiState.AppCommands.Enqueue(CreateExpandNamespaces(tree, expansion));
    }

    private static AppCommand CreateExpandNamespaces(BrowseTree tree, NamespaceExpansion expansion)
    {
        return appState =>
        {
            StartExpandingNamespaces(appState, tree, expansion);
        };
    }

    /// <summary>Starts building the children of each assembly <paramref name="expansion"/> expanded and expanding the
    /// namespaces among them (<see cref="ExpandNamespacesHandler"/>) now, a workflow per assembly, each counted in the
    /// app's background work until it ends.</summary>
    public static void StartExpandingNamespaces(AppState appState, BrowseTree tree, NamespaceExpansion expansion)
    {
        foreach (var root in expansion.Roots)
        {
            appState.Workflows.Start(nameof(ExpandNamespacesHandler), ct => appState.ExpandNamespacesHandler.HandleAsync(tree, expansion, root, ct));
        }
    }

    /// <summary>Starts building the children <paramref name="tree"/> asked for while it drew
    /// (<see cref="BuildChildrenHandler"/>) now, a workflow per node, each counted in the app's background work until it
    /// ends. Allocates nothing while nothing is asked for.</summary>
    public static void StartBuildingChildren(AppState appState, BrowseTree tree)
    {
        if (!tree.Builds.HasAsked)
        {
            return;
        }

        foreach (var node in tree.Builds.TakeAsked())
        {
            StartBuildingChildren(appState, tree, node);
        }
    }

    /// <summary>A method of its own, so that the frames that start nothing allocate no closure.</summary>
    private static void StartBuildingChildren(AppState appState, BrowseTree tree, BrowseNode node)
    {
        appState.Workflows.Start(nameof(BuildChildrenHandler), ct => appState.BuildChildrenHandler.HandleAsync(tree, node, ct));
    }

    /// <summary>Starts the runs <paramref name="search"/> asked for while the screen drew (<see cref="RunSearchHandler"/>)
    /// now, each counted in the app's background work until it ends, on a token that a new query cancels too.
    /// Allocates nothing while nothing is asked for.</summary>
    public static void StartSearching(AppState appState, Search search)
    {
        if (!search.Runs.HasAsked)
        {
            return;
        }

        foreach (var run in search.Runs.TakeAsked())
        {
            StartSearchRun(appState, search, run);
        }
    }

    /// <summary>A method of its own, so that the frames that start nothing allocate no closure.</summary>
    private static void StartSearchRun(AppState appState, Search search, SearchRun run)
    {
        appState.Workflows.Start(nameof(RunSearchHandler), ct => appState.RunSearchHandler.HandleAsync(search, run, ct), run.Work.Token);
    }

    /// <summary>Starts looking up the tooltips <paramref name="tooltips"/> asked for while the code drew
    /// (<paramref name="handler"/>) now, a workflow per reference, each counted in the app's background work until it
    /// ends. Allocates nothing while nothing is asked for.</summary>
    public static void StartLookingUpTooltips(WorkflowRunner workflows, LookUpTooltipHandler handler, CodeTooltips tooltips)
    {
        if (!tooltips.HasAsked)
        {
            return;
        }

        foreach (var lookup in tooltips.TakeAsked())
        {
            StartLookingUp(workflows, handler, tooltips, lookup);
        }
    }

    /// <summary>A method of its own, so that the frames that start nothing allocate no closure.</summary>
    private static void StartLookingUp(WorkflowRunner workflows, LookUpTooltipHandler handler, CodeTooltips tooltips, TooltipLookup lookup)
    {
        workflows.Start(nameof(LookUpTooltipHandler), ct => handler.HandleAsync(tooltips, lookup, ct));
    }

    /// <summary>Asks for the files of the request <paramref name="opening"/> began to be opened once the frame is drawn
    /// (<see cref="AssemblyOpening"/>), then the load of the last one shown: the input began the request in its own
    /// frame, and only opening the files, which takes longer than a frame, waits for the edge. Nothing when
    /// <paramref name="opening"/> is null.</summary>
    public static void OpenFilesLater(UiState uiState, FileOpening? opening)
    {
        if (opening is null)
        {
            return;
        }

        Start(uiState, nameof(AssemblyOpening.OpenAssembliesAsync), opening, static (a, o, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, o, ct));
    }

    /// <summary>Asks for the assemblies of the list <paramref name="opening"/> began opening to be opened once the frame
    /// is drawn (<see cref="AssemblyLists.OpenListAsync"/>), then the loads that started in the tabs: the draw switched
    /// the list in the frame of its input, and only opening its files, which takes longer than a frame, waits for the
    /// edge. Nothing when <paramref name="opening"/> is null.</summary>
    public static void OpenListLater(UiState uiState, ListOpening? opening)
    {
        if (opening is null)
        {
            return;
        }

        uiState.AppCommands.Enqueue(CreateOpenList(opening));
    }

    private static AppCommand CreateOpenList(ListOpening opening)
    {
        return appState =>
        {
            StartOpeningList(appState, opening);
        };
    }

    /// <summary>Starts the loads closing the list before left (the lookup of the tabs it left waiting), opening the
    /// assemblies of the list <paramref name="opening"/> began opening (<see cref="AssemblyLists.OpenListAsync"/>), then
    /// the loads that started in the tabs, and beside it the finding of the list's analyses
    /// (<see cref="StartRestoringAnalyses"/>), each now and none waiting for another; nothing when it is null.</summary>
    public static void StartOpeningList(AppState appState, ListOpening? opening)
    {
        if (opening is null)
        {
            return;
        }

        StartLoads(appState, opening.Closing);
        StartShowing(appState, nameof(AssemblyLists.OpenListAsync), ct => AssemblyLists.OpenListAsync(appState, opening, ct));
        StartRestoringAnalyses(appState, opening.Analyses);
    }

    /// <summary>Asks for what going to a history entry left, <paramref name="move"/>, to be started once the frame is
    /// drawn (<see cref="StartMoving"/>): the draw went to the entry in the frame of its input, and only the load or the
    /// lookup, which take longer than a frame, wait for the edge. Allocates nothing when it left nothing.</summary>
    public static void MoveLater(UiState uiState, HistoryMove? move)
    {
        if (move is null)
        {
            return;
        }

        uiState.AppCommands.Enqueue(CreateMove(move));
    }

    private static AppCommand CreateMove(HistoryMove move)
    {
        return appState =>
        {
            StartMoving(appState, move);
        };
    }

    /// <summary>Starts what going to a history entry left, <paramref name="move"/>, now: the load of the node the tab
    /// shows again (<see cref="StartDecompiling(AppState, DecompileTicket?)"/>), or the lookup of the place the entry
    /// recorded (<see cref="GoToHistoryEntryHandler"/>) as a workflow, then the load it points the tab at.</summary>
    public static void StartMoving(AppState appState, HistoryMove move)
    {
        move.Switch(load => StartDecompiling(appState, load),
            lookup => StartShowing(appState, nameof(GoToHistoryEntryHandler), ct => appState.GoToHistoryEntryHandler.HandleAsync(appState, lookup, ct)));
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
            run => StartComparing(appState, run), restore => StartRestoringAnalyses(appState, restore));
    }

    /// <summary>Starts looking for the ready-made lists this machine offers, when the lists dialog's menu asked for them
    /// while it drew, off the UI thread (it reads the installed frameworks' folders), as a workflow of its own; the
    /// presets found go to <paramref name="scan"/>.</summary>
    public static void StartScanningPresets(WorkflowRunner workflows, PresetScan scan)
    {
        if (!scan.TryStart())
        {
            return;
        }

        StartScan(workflows, scan);
    }

    /// <summary>Starts the scan <see cref="StartScanningPresets"/> took; apart, so a frame that starts none makes no
    /// closure.</summary>
    private static void StartScan(WorkflowRunner workflows, PresetScan scan)
    {
        workflows.Start(nameof(AssemblyListPresets.FindInstalled), async ct => scan.Take(await Task.Run(AssemblyListPresets.FindInstalled, ct)));
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
