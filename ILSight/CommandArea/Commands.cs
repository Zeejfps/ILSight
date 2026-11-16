using ILSight.Core;
using ILSight.BookmarkArea;
using ILSight.Core.Analysis;
using ILSight.CodeTooltipArea;
using ILSight.AssemblyListArea;
using ILSight.SearchArea;
using ILSight.DiagramArea;
using ILSight.AssemblyArea;
using ILSight.MetadataArea;
using ILSight.MetadataGridArea;

namespace ILSight.CommandArea;

[Edge]
public static class Commands
{
    /// <summary>Asks for <paramref name="run"/> to be done to the app state once the frame is drawn.
    /// Pass a <c>static</c> lambda.</summary>
    public static void Later(IAppCommandWriter commands, AppCommand run)
    {
        commands.Enqueue(run);
    }

    /// <summary>Asks for <paramref name="run"/> to be done to the app state and <paramref name="subject"/>
    /// once the frame is drawn. Pass a <c>static</c> lambda, handing it what it acts on as
    /// <paramref name="subject"/>, so that nothing is allocated unless this is called.</summary>
    public static void Later<T>(IAppCommandWriter commands, T subject, Action<AppState, T> run)
    {
        commands.Enqueue(AppCommandFactory.Create(subject, run));
    }

    /// <summary>Tells the user <paramref name="refusal"/> once the frame is drawn, as a command they asked for that is
    /// unavailable now says why where they acted (the refusal line), never as a failure.</summary>
    public static void RefuseLater(IAppCommandWriter commands, Refusal refusal)
    {
        Later(commands, refusal, static (a, r) => a.Refusals.Show(r));
    }

    /// <summary>Asks for what <paramref name="load"/> started loading in its tab, if anything, to be brought in once
    /// the frame is drawn (<see cref="StartDecompiling(AppState, DecompileTicket?)"/>): the draw points the tab in the
    /// frame of its input, and only the decompile, which takes longer than a frame, waits for the edge. Allocates
    /// nothing when no load started.</summary>
    public static void DecompileLater(IAppCommandWriter commands, DecompileTicket? load)
    {
        if (load is null)
        {
            return;
        }

        commands.Enqueue(CreateDecompile(load));
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
    public static void FollowLater(IAppCommandWriter commands, CodeReference.Navigable reference, ResolveTicket? lookup)
    {
        if (lookup is null)
        {
            return;
        }

        StartResolving(commands, nameof(NavigateHandler), (reference, lookup), lookup,
            static (a, s, ct) => a.NavigateHandler.HandleAsync(a.ListSession.Navigation, a.Decompiler, s.reference, s.lookup, ct));
    }

    // A lookup the user asked for, which the app says it is resolving until it ends: when its workflow fails, the edge
    // reports it and the request stops resolving (Navigation.FailResolving).
    private static void StartResolving<T>(IAppCommandWriter commands, string name, T subject, ResolveTicket lookup,
        Func<AppState, T, CancellationToken, Task<DecompileTicket?>> show)
    {
        commands.Enqueue(appState => StartShowingResolved(appState, name, lookup, ct => show(appState, subject, ct)));
    }

    private static void StartShowingResolved(AppState appState, string name, ResolveTicket lookup, Func<CancellationToken, Task<DecompileTicket?>> show)
    {
        appState.Workflows.StartSettling(name, async ct => StartDecompiling(appState, await show(ct)), _ => appState.ListSession.Navigation.FailResolving(lookup));
    }

    /// <summary>As <see cref="FollowLater"/>, showing the metadata table row that defines what
    /// <paramref name="reference"/> points at (<see cref="ShowInMetadataHandler"/>).</summary>
    public static void ShowInMetadataLater(IAppCommandWriter commands, CodeReference.Navigable reference, ResolveTicket lookup)
    {
        StartResolving(commands, nameof(ShowInMetadataHandler), (reference, lookup), lookup,
            static (a, s, ct) => a.ShowInMetadataHandler.HandleAsync(a.ListSession.Navigation, a.Decompiler, s.reference, s.lookup, ct));
    }

    /// <summary>Asks for what <paramref name="reference"/> points at to be analyzed once the frame is drawn: the analyzer
    /// opens with a root that shows as loading (<see cref="AnalyzerPanelView.ShowAnalyzing(Panels, AnalyzerTree, CodeReference.Navigable)"/>),
    /// and what the reference points at is found and analyzed then (<see cref="StartAnalyzing"/>).</summary>
    public static void AnalyzeLater(IAppCommandWriter commands, Panels panels, CodeReference.Navigable reference)
    {
        Later(commands, (panels, reference), static (a, s) => StartAnalyzing(a, AnalyzerPanelView.ShowAnalyzing(s.panels, a.ListSession.Analyzer, s.reference)));
    }

    /// <summary>Asks for <paramref name="symbol"/> to be analyzed once the frame is drawn (<see cref="Analyze"/>).</summary>
    public static void AnalyzeLater(IAppCommandWriter commands, Panels panels, AnalyzerRows rows, AnalysisSymbol symbol)
    {
        Later(commands, (panels, rows, symbol), static (a, s) => Analyze(a, s.panels, s.rows, s.symbol));
    }

    /// <summary>Asks for what <paramref name="facts"/> were read from to be analyzed once the frame is drawn
    /// (<see cref="Analyze"/>): the analyses it offers are composed by the app's table then, in the command, so a bug in
    /// the table is reported once at the command's edge, never in the draw that offered "Analyze".</summary>
    public static void AnalyzeLater(IAppCommandWriter commands, Panels panels, AnalyzerRows rows, AnalysisFacts facts)
    {
        Later(commands, (panels, rows, facts), static (a, s) => Analyze(a, s.panels, s.rows, s.facts.Symbol));
    }

    /// <summary>Analyzes <paramref name="symbol"/>: the analyzer opens, if it is not, with the root analyzing it
    /// selected (<see cref="AnalyzerPanelView.ShowAnalysis"/>). When the list doesn't hold its assembly, a root shows as
    /// loading while the assembly is listed, as following a reference into it lists it, and the analysis lands then
    /// (<see cref="StartAnalyzing"/>); when nothing names its definition to list it by, the user is told why.</summary>
    public static void Analyze(AppState appState, Panels panels, AnalyzerRows rows, AnalysisSymbol symbol)
    {
        appState.ListSession.Analyzer.AddRoot(symbol).Switch(
            analyzing => AnalyzerPanelView.ShowAnalysis(panels, rows, analyzing.Root),
            listing => StartAnalyzing(appState, AnalyzerPanelView.ShowListing(panels, listing.Pending)),
            refused => appState.Refusals.Show(refused.Why));
    }

    /// <summary>Starts finding what the reference of <paramref name="pending"/> points at and analyzing it in its root
    /// (<see cref="AnalyzeReferenceHandler"/>) now, counted in the app's background work until it ends.</summary>
    public static void StartAnalyzing(AppState appState, PendingAnalysis pending)
    {
        appState.Workflows.StartSettling(nameof(AnalyzeReferenceHandler),
            ct => appState.AnalyzeReferenceHandler.HandleAsync(appState.ListSession.Analyzer, appState.ViewCommands, pending, ct),
            error => appState.ListSession.Analyzer.FailUnresolved(pending.Root, pending.Reference.Name, error.Message));
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

        StartRunningAnalysis(appState, run);
    }

    // A method of its own, so that the calls that start nothing allocate no closure.
    private static void StartRunningAnalysis(AppState appState, AnalysisRun run)
    {
        appState.Workflows.StartSettling(nameof(RunAnalysisHandler), ct => appState.RunAnalysisHandler.HandleAsync(appState.ListSession.Analyzer, run, ct),
            error => Analysis.FinishRun(appState.ListSession.Analyzer, run, error.Message), run.Work.Token);
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

        StartFindingAnalysesSymbols(appState, restore);
    }

    // A method of its own, so that the calls that start nothing allocate no closure.
    private static void StartFindingAnalysesSymbols(AppState appState, AnalysesRestore restore)
    {
        appState.Workflows.StartSettling(nameof(RestoreAnalysesHandler),
            ct => appState.RestoreAnalysesHandler.HandleAsync(appState.ListSession.Analyzer, appState.ListSession.Assemblies, appState.ViewCommands, restore, ct),
            error => Analysis.FailRoots(appState.ListSession.Analyzer, restore.Roots, error.Message));
    }

    /// <summary>Asks for the namespaces of each assembly <paramref name="expansion"/> expanded in <paramref name="tree"/>
    /// to be expanded once the frame is drawn (<see cref="StartExpandingNamespaces"/>): the draw expanded the assemblies
    /// in the frame of its input, and only building their children, which takes longer than a frame, waits for the
    /// edge.</summary>
    public static void ExpandNamespacesLater(IAppCommandWriter commands, BrowseTree tree, NamespaceExpansion expansion)
    {
        commands.Enqueue(CreateExpandNamespaces(tree, expansion));
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
            appState.Workflows.StartSettling(nameof(ExpandNamespacesHandler), ct => appState.ExpandNamespacesHandler.HandleAsync(tree, expansion, root, ct),
                error => SettleFailedBuild(appState.Session, tree, root, error));
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

    // A method of its own, so that the frames that start nothing allocate no closure.
    private static void StartBuildingChildren(AppState appState, BrowseTree tree, BrowseNode node)
    {
        appState.Workflows.StartSettling(nameof(BuildChildrenHandler), ct => appState.BuildChildrenHandler.HandleAsync(tree, node, ct),
            error => SettleFailedBuild(appState.Session, tree, node, error));
    }

    // A build whose workflow failed shows one row saying why under its node, which a reload asks for again.
    internal static void SettleFailedBuild(BrowseSession session, BrowseTree tree, BrowseNode node, Exception error)
    {
        session.FailChildren(node, error);
        tree.Builds.Finish(node);
        tree.View.Refresh();
    }

    /// <summary>Starts reading the type system of each module listed since the last frame, ahead of its first use,
    /// which takes the same read's outcome (<see cref="LoadedAssemblies.ReadTypeSystemAheadAsync"/>): a read the library
    /// fails on ends with nothing to report, its first use showing why. Each is counted in the app's background work
    /// until it ends. Allocates nothing while nothing is asked for.</summary>
    public static void StartReadingTypeSystems(WorkflowRunner workflows, AssemblyList assemblies)
    {
        if (!assemblies.HasTypeSystemReadsAsked)
        {
            return;
        }

        foreach (var module in assemblies.TakeTypeSystemReadsAsked())
        {
            StartReadingTypeSystem(workflows, module);
        }
    }

    // A method of its own, so that the frames that start nothing allocate no closure.
    private static void StartReadingTypeSystem(WorkflowRunner workflows, LoadedModule module)
    {
        workflows.Start(nameof(LoadedAssemblies.ReadTypeSystemAheadAsync), ct => LoadedAssemblies.ReadTypeSystemAheadAsync(module, ct));
    }

    /// <summary>Starts the runs <paramref name="results"/> asked for while the screen drew (<see cref="RunSearchHandler"/>)
    /// now, each counted in the app's background work until it ends, on a token that a new query cancels too.
    /// Allocates nothing while nothing is asked for.</summary>
    public static void StartSearching(AppState appState, SearchResults results)
    {
        if (!results.HasAskedRuns)
        {
            return;
        }

        foreach (var run in results.TakeAskedRuns())
        {
            StartSearchRun(appState, results, run);
        }
    }

    // A method of its own, so that the frames that start nothing allocate no closure.
    private static void StartSearchRun(AppState appState, SearchResults results, SearchRun run)
    {
        appState.Workflows.StartSettling(nameof(RunSearchHandler), ct => appState.RunSearchHandler.HandleAsync(results, run, ct), error => results.FailRun(run, error),
            run.Work.Token);
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

    // A method of its own, so that the frames that start nothing allocate no closure.
    private static void StartLookingUp(WorkflowRunner workflows, LookUpTooltipHandler handler, CodeTooltips tooltips, TooltipLookup lookup)
    {
        workflows.StartSettling(nameof(LookUpTooltipHandler), ct => handler.HandleAsync(tooltips, lookup, ct),
            _ => tooltips.Finish(lookup, new Lookup<CodeTooltip>.NotFound()));
    }

    /// <summary>Starts the sorts and filters the metadata grids asked for while the screen drew
    /// (<see cref="ArrangeMetadataGridHandler"/>), each counted in the app's background work until it ends. Allocates
    /// nothing while nothing is asked for.</summary>
    public static void StartArrangingGrids(WorkflowRunner workflows, ArrangeMetadataGridHandler handler, MetadataGridViews views)
    {
        if (!views.HasAsked)
        {
            return;
        }

        foreach (var ask in views.TakeAsked())
        {
            StartArranging(workflows, handler, ask);
        }
    }

    // A method of its own, so that the frames that start nothing allocate no closure.
    private static void StartArranging(WorkflowRunner workflows, ArrangeMetadataGridHandler handler, MetadataGridViewAsk ask)
    {
        workflows.StartSettling(nameof(ArrangeMetadataGridHandler), ct => handler.HandleAsync(ask, ct), error => ask.State.FailArranging(ask.Key, error));
    }

    /// <summary>Starts reading the pages of paged metadata grids the views asked for while the screen drew
    /// (<see cref="ReadMetadataPageHandler"/>), a workflow per page, each counted in the app's background work until it
    /// ends. Allocates nothing while nothing is asked for.</summary>
    public static void StartReadingGridPages(WorkflowRunner workflows, ReadMetadataPageHandler handler, MetadataGridPages pages)
    {
        if (!pages.HasAsked)
        {
            return;
        }

        foreach (var ask in pages.TakeAsked())
        {
            StartReadingPage(workflows, handler, ask);
        }
    }

    // A method of its own, so that the frames that start nothing allocate no closure.
    private static void StartReadingPage(WorkflowRunner workflows, ReadMetadataPageHandler handler, MetadataGridPageAsk ask)
    {
        workflows.StartSettling(nameof(ReadMetadataPageHandler), ct => handler.HandleAsync(ask, ct), error => ask.Grid.FailPage(ask.Page, error));
    }

    /// <summary>Asks for the files of the request <paramref name="opening"/> began to be opened once the frame is drawn
    /// (<see cref="AssemblyOpening"/>), then the load of the last one shown: the input began the request in its own
    /// frame, and only opening the files, which takes longer than a frame, waits for the edge. Nothing when
    /// <paramref name="opening"/> is null.</summary>
    public static void OpenFilesLater(IAppCommandWriter commands, FileOpening? opening)
    {
        if (opening is null)
        {
            return;
        }

        Later(commands, opening, static (a, o) => StartOpening(a, o));
    }

    // A request the input began opening: when its workflow fails or ends quietly, the app no longer says it is opening
    // its files. Newer opens and a clear of the list cancel it: it is work of the list's generation now.
    private static void StartOpening(AppState appState, FileOpening opening)
    {
        var work = appState.ListSession.Assemblies.Work;
        StartShowingSettled(appState, nameof(AssemblyOpening.OpenAssembliesAsync), ct => appState.AssemblyOpening.OpenAssembliesAsync(appState, opening, work, ct),
            () => appState.ListSession.Navigation.EndOpening(opening.Intent), work.Ending);
    }

    /// <summary>Asks for the files among <paramref name="dropped"/> to be opened once the frame is drawn
    /// (<see cref="AssemblyOpening.OpenDroppedFilesAsync"/>), then the load of the last one shown, cancelled with the
    /// opens under way; when its workflow fails or ends quietly, the app no longer says it is opening them.</summary>
    public static void OpenDroppedLater(IAppCommandWriter commands, IReadOnlyList<string> dropped)
    {
        commands.Enqueue(appState => StartOpeningDropped(appState, dropped));
    }

    private static void StartOpeningDropped(AppState appState, IReadOnlyList<string> dropped)
    {
        var work = appState.ListSession.Assemblies.Work;
        StartShowingSettled(appState, nameof(AssemblyOpening.OpenDroppedFilesAsync), ct => appState.AssemblyOpening.OpenDroppedFilesAsync(appState, dropped, work, ct),
            () => appState.ListSession.Navigation.FailOpening(), work.Ending);
    }

    /// <summary>Asks for the assemblies <paramref name="assembly"/> references to be opened once the frame is drawn
    /// (<see cref="AssemblyOpening.OpenDependenciesAsync"/>), cancelled with the opens under way then.</summary>
    public static void OpenDependenciesLater(IAppCommandWriter commands, LoadedModule assembly)
    {
        StartLater(commands, nameof(AssemblyOpening.OpenDependenciesAsync), assembly, static (a, _) => a.ListSession.Assemblies.Work,
            static (a, m, work, ct) => a.AssemblyOpening.OpenDependenciesAsync(m, work, ct), static work => work.Ending);
    }

    /// <summary>Asks for every assembly of the open list to be reloaded once the frame is drawn
    /// (<see cref="StartReloading"/>).</summary>
    public static void ReloadLater(IAppCommandWriter commands, List<NodePath> expanded)
    {
        Later(commands, expanded, static (a, e) => StartReloading(a, e));
    }

    /// <summary>Starts reloading every assembly of the open list now: its clear (<see cref="AssemblyReloading.BeginReload"/>),
    /// which begins the generation the files are read again in, then the reopen under that generation's work
    /// (<see cref="AssemblyReloading.RefreshAssembliesAsync"/>), cancelled by a newer reload or a clear of the list
    /// through the reload's ticket and by any end of that generation through its ending, then the loads it started.
    /// When either workflow fails, the reload ends and the tabs it left reloading say why
    /// (<see cref="AssemblyReloading.FailReload"/>); when the reopen ends quietly, the reload ends as well, unless a
    /// newer one or a stop replaced it, and the tabs it left reloading stop waiting
    /// (<see cref="AssemblyReloading.EndQuietly"/>).</summary>
    public static void StartReloading(AppState appState, IReadOnlyList<NodePath> expanded)
    {
        var session = appState.ListSession;
        var ticket = session.Reload.Start();
        appState.Workflows.StartSettling(nameof(AssemblyReloading.BeginReload),
            _ =>
            {
                BeginReload(appState, expanded, ticket);
                return Task.CompletedTask;
            },
            error => StartLoads(appState, AssemblyReloading.FailReload(session, ticket, error)), ticket.Token);
    }

    // The clear ends there, handing back the generation it began; the reopen is started under its work.
    private static void BeginReload(AppState appState, IReadOnlyList<NodePath> expanded, ReloadTicket ticket)
    {
        var begun = appState.AssemblyReloading.BeginReload(appState.Lists, appState.ListSession, appState.Decompiler);
        StartReopeningReloaded(appState, expanded, ticket, begun);
    }

    private static void StartReopeningReloaded(AppState appState, IReadOnlyList<NodePath> expanded, ReloadTicket ticket, ReloadBegun begun)
    {
        var session = appState.ListSession;
        appState.Workflows.StartSettlingEveryEnd(nameof(AssemblyReloading.RefreshAssembliesAsync),
            async ct => StartLoads(appState, await appState.AssemblyReloading.RefreshAssembliesAsync(session, appState.ViewCommands, appState.Decompiler,
                expanded, ticket, begun, ct)),
            error => StartLoads(appState, AssemblyReloading.FailReload(session, ticket, error)),
            () => StartLoads(appState, AssemblyReloading.EndQuietly(session, ticket, begun.Tabs)), ticket.Token, begun.Work.Ending);
    }

    /// <summary>As <see cref="Start{T}(IAppCommandWriter, string, T, Func{AppState, T, CancellationToken, Task{List{DecompileTicket}}})"/>,
    /// for a reopen of <paramref name="assembly"/>'s file: when its workflow fails, the file is no longer shown
    /// reopening (<see cref="Reopens.Stop"/>).</summary>
    public static void StartReopening<T>(IAppCommandWriter commands, string name, LoadedModule assembly, T subject,
        Func<AppState, T, CancellationToken, Task<List<DecompileTicket>>> show)
    {
        var path = assembly.ListedItem.FilePath;
        commands.Enqueue(appState => appState.Workflows.StartSettling(name,
            async ct => StartDecompiling(appState, await show(appState, subject, ct)), _ => appState.Reopens.Stop(path)));
    }

    /// <summary>Asks for the assemblies of the list <paramref name="opening"/> began opening to be opened once the frame
    /// is drawn (<see cref="StartOpeningList"/>), then the loads that started in the tabs: the draw switched
    /// the list in the frame of its input, and only opening its files, which takes longer than a frame, waits for the
    /// edge. Nothing when <paramref name="opening"/> is null.</summary>
    public static void OpenListLater(IAppCommandWriter commands, ListOpening? opening)
    {
        if (opening is null)
        {
            return;
        }

        commands.Enqueue(CreateOpenList(opening));
    }

    private static AppCommand CreateOpenList(ListOpening opening)
    {
        return appState =>
        {
            StartOpeningList(appState, opening);
        };
    }

    /// <summary>Starts the loads closing the list before left (the lookup of the tabs it left waiting), opening the
    /// assemblies of the list <paramref name="opening"/> began opening, as the reload it holds, then
    /// the loads that started in the tabs, and beside it the finding of the list's analyses
    /// (<see cref="StartRestoringAnalyses"/>), each now and none waiting for another; nothing when it is null.</summary>
    public static void StartOpeningList(AppState appState, ListOpening? opening)
    {
        if (opening is null)
        {
            return;
        }

        FinishClosing(appState, opening.Closing);
        StartReopening(appState, opening.Reopening);
        StartRestoringAnalyses(appState, opening.Analyses);
    }

    private static void StartReopening(AppState appState, ListReopening reopening)
    {
        var session = appState.ListSession;
        var handler = appState.OpenAssembliesAsReloadHandler;
        StartOpeningAsReload(appState, nameof(OpenAssembliesAsReloadHandler), reopening,
            ct => handler.HandleAsync(session, reopening.Paths, reopening.Reload, reopening.Work, ct));
    }

    /// <summary>Starts <paramref name="open"/>, the reopening of a list's assemblies as <paramref name="reopening"/>
    /// began it, now, then the loads it started, cancelled by a newer reload or a clear of the list through its ticket,
    /// and by any end of the generation it opens in through that generation's ending, each asked; when its workflow
    /// fails or ends quietly, the reopening ends unfinished (<see cref="OpenAssembliesAsReloadHandler.EndUnfinished"/>),
    /// unless a newer reload or a stop replaced it.</summary>
    public static void StartOpeningAsReload(AppState appState, string name, ListReopening reopening, Func<CancellationToken, Task<TabLoads>> open)
    {
        var session = appState.ListSession;
        appState.Workflows.StartSettlingEveryEnd(name, async ct => StartLoads(appState, await open(ct)),
            _ => StartLoads(appState, OpenAssembliesAsReloadHandler.EndUnfinished(session, reopening.Reload)),
            () => StartLoads(appState, OpenAssembliesAsReloadHandler.EndUnfinished(session, reopening.Reload)), reopening.Reload.Token,
            reopening.Work.Ending);
    }

    /// <summary>Asks for what going to a history entry left, <paramref name="move"/>, to be started once the frame is
    /// drawn (<see cref="StartMoving"/>): the draw went to the entry in the frame of its input, and only the load or the
    /// lookup, which take longer than a frame, wait for the edge. Allocates nothing when it left nothing.</summary>
    public static void MoveLater(IAppCommandWriter commands, HistoryMove? move)
    {
        if (move is null)
        {
            return;
        }

        commands.Enqueue(CreateMove(move));
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
    /// recorded (<see cref="GoToHistoryEntryHandler"/>) as a workflow, then the load it points the tab at, or the read
    /// of the cards of a diagram made again (<see cref="ReopenDiagramHandler"/>) on a token closing it cancels too, or
    /// the run of a comparison made again (<see cref="StartComparing"/>).</summary>
    public static void StartMoving(AppState appState, HistoryMove move)
    {
        move.Switch(load => StartDecompiling(appState, load),
            lookup => StartShowingResolved(appState, nameof(GoToHistoryEntryHandler), lookup.Ticket,
                ct => appState.GoToHistoryEntryHandler.HandleAsync(appState.ListSession.Navigation, appState.Decompiler, lookup, ct)),
            read => StartReadingPlace(appState, read), run => StartComparing(appState, run));
    }

    private static void StartReadingPlace(AppState appState, DiagramPlaceRead read)
    {
        appState.Workflows.StartSettling(nameof(ReopenDiagramHandler), ct => appState.ReopenDiagramHandler.ShowPlaceAsync(read, ct),
            error => read.Diagram.FailReading(read.Read, error.Message), read.Diagram.Token);
    }

    /// <summary>Asks for the comparison <paramref name="run"/> started, if any, to be run once the frame is drawn
    /// (<see cref="StartComparing"/>): the draw changed the comparison in the frame of its input, and only comparing,
    /// which takes longer than a frame, waits for the edge. Allocates nothing when none started.</summary>
    public static void CompareLater(IAppCommandWriter commands, ComparisonRun? run)
    {
        if (run is null)
        {
            return;
        }

        commands.Enqueue(CreateCompare(run));
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
        appState.Workflows.StartSettling(nameof(CompareAssembliesHandler), ct => appState.CompareAssembliesHandler.HandleAsync(run, ct),
            _ => run.Comparison.FailWork(run.Work, run.Left.NameAndVersion, run.Right.NameAndVersion), run.Work.Token);
    }

    /// <summary>Asks for <paramref name="start"/> to be done to the app state and <paramref name="subject"/> once the
    /// frame is drawn, and for the work it started, if any, to be run then as <paramref name="run"/>, counted in the
    /// app's background work as <paramref name="name"/> until it ends, on a token that what
    /// <paramref name="cancelledOnPurposeBy"/> picks of that work cancels too: the canceller the state change made,
    /// such as a diagram it started reading cards for, which closing it cancels (R009). Nothing runs when
    /// <paramref name="start"/> started nothing. Pass <c>static</c> lambdas, handing them what they act on as
    /// <paramref name="subject"/>, so that nothing is allocated unless this is called.</summary>
    public static void StartLater<T, TWork>(IAppCommandWriter commands, string name, T subject, Func<AppState, T, TWork?> start,
        Func<AppState, T, TWork, CancellationToken, Task> run, Func<TWork, CancellationToken> cancelledOnPurposeBy) where TWork : class
    {
        commands.Enqueue(CreateStartLater(name, subject, start, run, cancelledOnPurposeBy));
    }

    /// <summary>As <see cref="StartLater{T, TWork}"/>, for work whose start recorded a pending state: one that ends with an
    /// unexpected exception, reported, has <paramref name="settle"/> settle that state with the work it was for
    /// (<see cref="WorkflowRunner.StartSettling"/>).</summary>
    public static void StartSettlingLater<T, TWork>(IAppCommandWriter commands, string name, T subject, Func<AppState, T, TWork?> start,
        Func<AppState, T, TWork, CancellationToken, Task> run, Func<TWork, CancellationToken> cancelledOnPurposeBy, Action<T, TWork, Exception> settle)
        where TWork : class
    {
        commands.Enqueue(CreateStartSettlingLater(name, subject, start, run, cancelledOnPurposeBy, settle));
    }

    private static AppCommand CreateStartSettlingLater<T, TWork>(string name, T subject, Func<AppState, T, TWork?> start,
        Func<AppState, T, TWork, CancellationToken, Task> run, Func<TWork, CancellationToken> cancelledOnPurposeBy, Action<T, TWork, Exception> settle)
        where TWork : class
    {
        return appState =>
        {
            var work = start(appState, subject);
            if (work is null)
            {
                return;
            }

            StartSettlingWork(appState, name, subject, work, run, cancelledOnPurposeBy, settle);
        };
    }

    // A method of its own, so that the calls that start nothing allocate no closure.
    private static void StartSettlingWork<T, TWork>(AppState appState, string name, T subject, TWork work,
        Func<AppState, T, TWork, CancellationToken, Task> run, Func<TWork, CancellationToken> cancelledOnPurposeBy, Action<T, TWork, Exception> settle)
        where TWork : class
    {
        appState.Workflows.StartSettling(name, ct => run(appState, subject, work, ct), error => settle(subject, work, error), cancelledOnPurposeBy(work));
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

            StartWork(appState, name, subject, work, run, cancelledOnPurposeBy);
        };
    }

    // A method of its own, so that the calls that start nothing allocate no closure.
    private static void StartWork<T, TWork>(AppState appState, string name, T subject, TWork work,
        Func<AppState, T, TWork, CancellationToken, Task> run, Func<TWork, CancellationToken> cancelledOnPurposeBy) where TWork : class
    {
        appState.Workflows.Start(name, ct => run(appState, subject, work, ct), cancelledOnPurposeBy(work));
    }

    /// <summary>Goes to the line <paramref name="bookmark"/> marks once the frame is drawn (<see cref="StartGoingToBookmark"/>).</summary>
    public static void GoToBookmarkLater(IAppCommandWriter commands, Bookmark bookmark)
    {
        Later(commands, bookmark, static (a, b) => StartGoingToBookmark(a, b));
    }

    /// <summary>Begins the request to go to the line <paramref name="bookmark"/> marks, which the app says it is
    /// resolving until it ends, and starts finding its place (<see cref="GoToBookmarkHandler"/>), then the load it
    /// starts in the tab; when that workflow fails, the edge reports it and the request stops resolving
    /// (<see cref="Navigation.FailResolving"/>), unless a newer one began.</summary>
    public static void StartGoingToBookmark(AppState appState, Bookmark bookmark)
    {
        var lookup = appState.ListSession.Navigation.BeginResolving(inNewTab: false);
        StartShowingResolved(appState, nameof(GoToBookmarkHandler), lookup,
            ct => appState.GoToBookmarkHandler.HandleAsync(appState.ListSession.Navigation, appState.Bookmarks, appState.Decompiler, bookmark, lookup, ct));
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

        StartBringingIn(appState, load);
    }

    // A method of its own, so that the calls that start nothing allocate no closure.
    private static void StartBringingIn(AppState appState, DecompileTicket load)
    {
        var firstGridRows = appState.FirstGridRows;
        var notes = DocumentNoteTexts.For(appState.Locale);
        // A load that fails unexpectedly shows failed in its tab, rather than loading for good.
        appState.Workflows.StartSettling(nameof(DecompileIntoTabHandler),
            ct => appState.DecompileIntoTabHandler.HandleAsync(appState.ViewCommands, load, notes, firstGridRows, ct),
            error => load.Load.TryShowFailed(load.Work, error.Message), load.Work.Token);
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

    /// <summary>Takes <paramref name="items"/> off the open list (<see cref="OpenListSession.CloseAssemblies"/>) and has
    /// the modules taken off released, each once nothing reads it.</summary>
    public static void CloseAssemblies(AppState appState, IReadOnlyList<LoadedAssembly> items)
    {
        LoadedAssemblies.Release(appState.ListSession.CloseAssemblies(items));
    }

    /// <summary>Takes the files that failed to load off the open list
    /// (<see cref="OpenListSession.CloseFailedAssemblies"/>), released as every close's modules are.</summary>
    public static void CloseFailedAssemblies(AppState appState)
    {
        LoadedAssemblies.Release(appState.ListSession.CloseFailedAssemblies());
    }

    /// <summary>Closes every assembly of the open list (<see cref="OpenListSession.CloseAllAssemblies"/>), then starts
    /// the loads that started in its tabs and has the modules taken off released.</summary>
    public static void CloseAllAssemblies(AppState appState)
    {
        FinishClosing(appState, appState.ListSession.CloseAllAssemblies());
    }

    /// <summary>Starts the loads closing the list started in its tabs, and has the modules it took off released, each
    /// once nothing reads it.</summary>
    public static void FinishClosing(AppState appState, ListClosing closing)
    {
        StartLoads(appState, closing.Loads);
        LoadedAssemblies.Release(closing.Removed);
    }

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

    // Apart, so that a frame that starts no scan makes no closure.
    private static void StartScan(WorkflowRunner workflows, PresetScan scan)
    {
        workflows.StartSettling(nameof(AssemblyListPresets.FindInstalled), async ct => scan.Take(await Task.Run(AssemblyListPresets.FindInstalled, ct)),
            _ => scan.Fail());
    }

    private static void StartFindingNodes(AppState appState, ReopenedNodesLookup lookup)
    {
        appState.Workflows.StartSettling(nameof(ShowReopenedNodesHandler),
            async ct => StartDecompiling(appState, await appState.ShowReopenedNodesHandler.HandleAsync(appState.ListSession.Reopened, appState.ListSession.Navigation,
                appState.Decompiler, appState.ViewCommands, lookup, ct)),
            error => ShowReopenedNodesHandler.ShowLookupFailed(appState.ListSession.Reopened, appState.ListSession.Navigation, lookup.Nodes, error));
    }

    private static void StartReadingCards(AppState appState, SavedDiagramRead read)
    {
        appState.Workflows.StartSettling(nameof(ReopenDiagramHandler), ct => appState.ReopenDiagramHandler.HandleAsync(appState.ListSession.Reopened, read, ct),
            error => SettleCardsRead(appState.ListSession.Reopened, read, error), read.Diagram.Token);
    }

    private static void SettleCardsRead(ReopenedTabs reopened, SavedDiagramRead read, Exception error)
    {
        read.Diagram.FailReading(read.Read, error.Message);
        SavedDocuments.TryFinishWaiting(reopened, read.Reopened);
    }

    /// <summary>As <see cref="StartShowing(AppState, string, Func{CancellationToken, Task{DecompileTicket?}})"/>, with
    /// <paramref name="endPending"/> ending the pending state it began when its workflow fails, which the edge reports,
    /// or ends cancelled on purpose by one of <paramref name="cancelledOnPurposeBy"/>.</summary>
    public static void StartShowingSettled(AppState appState, string name, Func<CancellationToken, Task<DecompileTicket?>> show, Action endPending,
        params ReadOnlySpan<CancellationToken> cancelledOnPurposeBy)
    {
        appState.Workflows.StartSettlingEveryEnd(name, async ct => StartDecompiling(appState, await show(ct)), _ => endPending(), endPending, cancelledOnPurposeBy);
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
    public static void Start(IAppCommandWriter commands, string name, AppWorkflow workflow)
    {
        commands.Enqueue(CreateStart(name, workflow));
    }

    /// <summary>As <see cref="Start(IAppCommandWriter, string, AppWorkflow)"/>, over the app state and
    /// <paramref name="subject"/>. Pass a <c>static</c> lambda, handing it what it acts on as
    /// <paramref name="subject"/>, so that nothing is allocated unless this is called.</summary>
    public static void Start<T>(IAppCommandWriter commands, string name, T subject, Func<AppState, T, CancellationToken, Task> workflow)
    {
        commands.Enqueue(CreateStart(name, subject, workflow));
    }

    /// <summary>As <see cref="Start(IAppCommandWriter, string, AppWorkflow)"/>, for a workflow that points a tab at what it is to
    /// show and returns the load it started there, which is started once the workflow ends
    /// (<see cref="StartShowing(AppState, string, Func{CancellationToken, Task{DecompileTicket?}})"/>). Pass a
    /// <c>static</c> lambda.</summary>
    public static void Start(IAppCommandWriter commands, string name, Func<AppState, CancellationToken, Task<DecompileTicket?>> show)
    {
        commands.Enqueue(CreateShowing(name, show));
    }

    /// <summary>As <see cref="Start(IAppCommandWriter, string, Func{AppState, CancellationToken, Task{DecompileTicket?}})"/>, over
    /// the app state and <paramref name="subject"/>. Pass a <c>static</c> lambda, handing it what it acts on as
    /// <paramref name="subject"/>, so that nothing is allocated unless this is called.</summary>
    public static void Start<T>(IAppCommandWriter commands, string name, T subject, Func<AppState, T, CancellationToken, Task<DecompileTicket?>> show)
    {
        commands.Enqueue(CreateShowing(name, subject, show));
    }

    /// <summary>As <see cref="Start{T}(IAppCommandWriter, string, T, Func{AppState, T, CancellationToken, Task{DecompileTicket?}})"/>,
    /// for a workflow that points several tabs and returns each load it started.</summary>
    public static void Start<T>(IAppCommandWriter commands, string name, T subject, Func<AppState, T, CancellationToken, Task<List<DecompileTicket>>> show)
    {
        commands.Enqueue(CreateShowing(name, subject, show));
    }

    /// <summary>As <see cref="Start(IAppCommandWriter, string, Func{AppState, CancellationToken, Task{DecompileTicket?}})"/>, for
    /// a workflow that returns the loads it started in tabs of every kind
    /// (<see cref="StartShowing(AppState, string, Func{CancellationToken, Task{TabLoads}})"/>).</summary>
    public static void Start(IAppCommandWriter commands, string name, Func<AppState, CancellationToken, Task<TabLoads>> show)
    {
        commands.Enqueue(CreateShowing(name, show));
    }

    /// <summary>As <see cref="Start(IAppCommandWriter, string, Func{AppState, CancellationToken, Task{TabLoads}})"/>, over the
    /// app state and <paramref name="subject"/>. Pass a <c>static</c> lambda, handing it what it acts on as
    /// <paramref name="subject"/>, so that nothing is allocated unless this is called.</summary>
    public static void Start<T>(IAppCommandWriter commands, string name, T subject, Func<AppState, T, CancellationToken, Task<TabLoads>> show)
    {
        commands.Enqueue(CreateShowing(name, subject, show));
    }

    /// <summary>As <see cref="Start{T}(IAppCommandWriter, string, T, Func{AppState, T, CancellationToken, Task})"/>, for a
    /// workflow that <paramref name="cancelledOnPurposeBy"/> cancels on purpose, such as the token of the work it was
    /// started as, which newer work or closing a dialog cancels: its token fires with it too, and ending cancelled
    /// by it is not a failure.</summary>
    public static void Start<T>(IAppCommandWriter commands, string name, T subject, Func<AppState, T, CancellationToken, Task> workflow, CancellationToken cancelledOnPurposeBy)
    {
        commands.Enqueue(CreateStart(name, subject, workflow, cancelledOnPurposeBy));
    }

    /// <summary>As <see cref="Start{T}(IAppCommandWriter, string, T, Func{AppState, T, CancellationToken, Task}, CancellationToken)"/>,
    /// for a workflow that began a pending state on <paramref name="subject"/>: when it fails, <paramref name="settle"/>
    /// ends that state, failed where it is shown, and the edge reports the failure once
    /// (<see cref="WorkflowRunner.StartSettling"/>).</summary>
    public static void StartSettling<T>(IAppCommandWriter commands, string name, T subject, Func<AppState, T, CancellationToken, Task> workflow,
        Action<AppState, T, Exception> settle, CancellationToken cancelledOnPurposeBy = default)
    {
        commands.Enqueue(appState => appState.Workflows.StartSettling(name, ct => workflow(appState, subject, ct),
            error => settle(appState, subject, error), cancelledOnPurposeBy));
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
