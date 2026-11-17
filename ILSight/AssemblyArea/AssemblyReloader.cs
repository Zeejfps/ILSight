using ILSight.Core;
using ILSight.AssemblyListArea;

namespace ILSight.AssemblyArea;

/// <summary>Reads the assemblies of the list again: one of them, with a PDB or a target framework picked for it,
/// or all of them from disk, then showing each tab's place in them again. The workflows return once the files are
/// read, with the loads they started in the tabs for the edge to start, and reach the files and the user's error
/// reports through <see cref="IPorts"/>. Each takes the parts of the app state it uses, and how code is decompiled as
/// its owner (<see cref="DecompilerChoice"/>), read once the files are read, as it is then.</summary>
public sealed class AssemblyReloader(AssemblyReloader.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user something went wrong.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Tells the user, where they acted, what was refused and why.</summary>
        void ShowRefusal(Refusal refusal);

        /// <summary>Whether the PDB at <paramref name="pdbPath"/> can be read for <paramref name="assembly"/>, checked
        /// without holding up the screen; the assembly closed before it was read comes back as
        /// <see cref="PdbCheckOutcome.Closed"/>, a check that failed as <see cref="PdbCheckOutcome.Failed"/>.</summary>
        Task<PdbCheckOutcome> CheckPdbAsync(LoadedModule assembly, string pdbPath, CancellationToken ct);

        /// <summary>Reads the file of <paramref name="item"/> again, its nodes loaded as far as the item's are, and
        /// finds the nodes at <paramref name="places"/> in it, without holding up the screen. The list keeps the
        /// item until it is replaced. What that threw comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<ReopenedFile>> ReadAgainAsync(LoadedAssembly item, IReadOnlyList<NodePath> places, CancellationToken ct);

        /// <summary>Lists the files at <paramref name="paths"/> in the open list, as work of the list's generation
        /// <paramref name="work"/>, in order, those that fail to load as failed, then has the nodes at
        /// <paramref name="expanded"/> load their children and finds the nodes at <paramref name="places"/>. Done
        /// without holding up the screen; a lookup the library failed as <see cref="Outcome{T}.Failed"/>. Stopped once
        /// <paramref name="ct"/> fires (the edge links the reload's canceller and the generation's ending into it), and
        /// a file met once the generation ended throws the cancellation of its ending; each let through.</summary>
        Task<Outcome<ReloadedNodes>> ReopenFilesAsync(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded, IReadOnlyList<NodePath> places,
            GenerationWork work, CancellationToken ct);

        /// <summary>Releases the modules <paramref name="removed"/> took off the list, each once nothing reads it.</summary>
        void ReleaseModules(RemovedModules removed);
    }

    /// <summary>Checks that the PDB at <paramref name="pdbPath"/> can be read for <paramref name="assembly"/>,
    /// then has the assembly read it in place of the one it finds itself, kept with the list, and reopens the
    /// assembly to show it. Reports why when it cannot be read, and says why when the assembly was closed. Returns the
    /// loads that started in the tabs.</summary>
    public async Task<List<DecompileTicket>> UsePdbAsync(OpenListSession session, Reopens reopens, DecompilerChoice decompiler, LoadedModule assembly,
        string pdbPath, CancellationToken ct)
    {
        var checking = await ports.CheckPdbAsync(assembly, pdbPath, ct);
        if (checking is PdbCheckOutcome.Closed closed)
        {
            ports.ShowRefusal(new Refusal.PdbNotRead { Pdb = Path.GetFileName(pdbPath), Assembly = closed.Assembly });
            return [];
        }

        var check = checking switch
        {
            PdbCheckOutcome.Checked c => c.Check,
            PdbCheckOutcome.Closed or PdbCheckOutcome.Failed => PdbCheck.Unreadable,
        };
        if (check == PdbCheck.Unreadable)
        {
            ports.ReportError(new AppProblem.PdbUnusable
            {
                PdbPath = pdbPath,
                Assembly = assembly.Name,
                Check = check,
            });
            return [];
        }

        if (check != PdbCheck.Matches)
        {
            ports.ShowRefusal(new Refusal.PdbUnusable
            {
                PdbPath = pdbPath,
                Assembly = assembly.Name,
                Check = check,
            });
            return [];
        }
        var assemblies = session.Assemblies;
        if (!assemblies.IsListed(assembly))
        {
            return [];
        }

        assemblies.Pdbs.Pick(assembly.FilePath, pdbPath);
        return await ReopenAssemblyAsync(session, reopens, decompiler, assembly, ct);
    }

    /// <summary>Resolves <paramref name="assembly"/>'s references against the framework
    /// <paramref name="framework"/> names, such as <c>.NETCoreApp,Version=v8.0</c>, or, when it is
    /// null, against the one the assembly was built for; kept with the list, and the assembly reopened
    /// to show it. Does nothing when that is the framework already in use. Returns the loads that started in the
    /// tabs.</summary>
    public Task<List<DecompileTicket>> SetTargetFrameworkAsync(OpenListSession session, Reopens reopens, DecompilerChoice decompiler, LoadedModule assembly,
        TargetFrameworkId? framework, CancellationToken ct)
    {
        var assemblies = session.Assemblies;
        if (!assemblies.IsListed(assembly))
        {
            return Task.FromResult<List<DecompileTicket>>([]);
        }

        if (IsInUse(assembly, framework))
        {
            return Task.FromResult<List<DecompileTicket>>([]);
        }

        PickTargetFramework(assemblies.TargetFrameworks, assembly, framework);
        return ReopenAssemblyAsync(session, reopens, decompiler, assembly, ct);
    }

    // A null framework is the one the assembly was built for.
    private static bool IsInUse(LoadedModule assembly, TargetFrameworkId? framework)
    {
        if (!assembly.IsTargetFrameworkPicked)
        {
            return framework == null;
        }

        return assembly.TargetFramework == framework;
    }

    private static void PickTargetFramework(AssemblyPicks<TargetFrameworkId> frameworks, LoadedAssembly assembly, TargetFrameworkId? framework)
    {
        if (framework == null)
        {
            frameworks.Forget(assembly.FilePath);
            return;
        }
        frameworks.Pick(assembly.FilePath, framework);
    }

    private async Task<List<DecompileTicket>> ReopenAssemblyAsync(OpenListSession session, Reopens reopens, DecompilerChoice decompiler,
        LoadedAssembly assembly, CancellationToken ct)
    {
        var item = assembly.ListedItem;
        var ticket = reopens.Start(item.FilePath);
        var tabs = ListTabsShowing(session.Navigation, decompiler.Setup.Language, item);
        var places = tabs.ConvertAll(t => t.Location);
        var reopened = await ports.ReadAgainAsync(item, places, ct);
        return FinishReopen(reopens, session, decompiler.Setup, item, reopened, ticket, tabs);
    }

    private static List<ReloadingTab> ListTabsShowing(Navigation navigation, DecompilationLanguage language, LoadedAssembly item)
    {
        var showing = new List<ReloadingTab>();
        foreach (var tab in navigation.OpenTabs)
        {
            var location = tab.Location;
            if (location is null)
            {
                continue;
            }

            if (!IsPathOf(item, location))
            {
                continue;
            }

            showing.Add(new ReloadingTab { Tab = tab, Location = location, Version = tab.Version, Position = FindPositionToKeep(tab, language) });
        }
        return showing;
    }

    private static bool IsPathOf(LoadedAssembly item, NodePath path)
    {
        return path.FilePath.Equals(item.FilePath);
    }

    private List<DecompileTicket> FinishReopen(Reopens reopens, OpenListSession session, DecompilerSetup decompiler, LoadedAssembly item,
        Outcome<ReopenedFile> outcome, int ticket, List<ReloadingTab> tabs)
    {
        if (!reopens.TryFinish(item.FilePath, ticket))
        {
            ReleaseUnlisted(outcome);
            return [];
        }

        return outcome switch
        {
            Outcome<ReopenedFile>.Ok ok => ShowReopened(session, session.Navigation, decompiler, item, ok.Value, tabs),
            Outcome<ReopenedFile>.Failed failed => ReportNotReopened(item, failed.Error),
        };
    }

    private void ReleaseUnlisted(Outcome<ReopenedFile> outcome)
    {
        if (outcome is Outcome<ReopenedFile>.Ok read)
        {
            ports.ReleaseModules(read.Value.Fresh.ListModules());
        }
    }

    private List<DecompileTicket> ReportNotReopened(LoadedAssembly item, Exception error)
    {
        ports.ReportError(new AppProblem.AssemblyNotReloaded { Assembly = item.Name, Error = error });
        return [];
    }

    private List<DecompileTicket> ShowReopened(OpenListSession session, Navigation navigation, DecompilerSetup decompiler, LoadedAssembly item,
        ReopenedFile reopened, List<ReloadingTab> tabs)
    {
        var loads = new List<DecompileTicket>();
        var isReplaced = session.TryReplaceAssembly(item, reopened.Fresh, out var removed);
        ports.ReleaseModules(removed);
        if (!isReplaced)
        {
            return loads;
        }

        for (var i = 0; i < tabs.Count; i++)
        {
            ShowAgain(navigation, decompiler, tabs[i], reopened.Places[i], loads);
        }
        return loads;
    }

    /// <summary>Begins reloading every assembly of the open list from disk, as a reload started
    /// (<see cref="Reload.Start"/>): remembers the list's files, has each tab showing them let go of its node, and
    /// clears the list, which ends its generation and begins the one the files are read again in. Returns what the
    /// reopen needs, with that generation's work, for the edge to start the reopen under
    /// (<see cref="RefreshAssembliesAsync"/>).</summary>
    public ReloadBegun BeginReload(SavedAssemblyLists lists, OpenListSession session, DecompilerChoice decompiler)
    {
        AssemblyLists.RememberOpenAssemblies(lists, session);
        var intent = session.Navigation.BeginRequest();

        var paths = lists.ActivePaths.ToList();
        var tabs = ReleaseShownTabs(session.Navigation, session.Assemblies, decompiler.Setup.Language);
        var removed = session.ClearAssemblies();
        ports.ReleaseModules(removed);
        return new ReloadBegun { Paths = paths, Tabs = tabs, Intent = intent, Work = session.Assemblies.Work };
    }

    /// <summary>Reads again the files the reload <paramref name="begun"/> cleared from the list, as work of the
    /// generation its clear began, as the reload <paramref name="ticket"/> started, then commands the view to expand the
    /// tree at <paramref name="expanded"/>, the paths of the nodes it had expanded, and shows each tab's place again,
    /// decompiled from the fresh files, and what the tabs reopened from the open list's saved documents showed. Returns
    /// the loads that started in the tabs. The edge that starts it links the ticket's canceller (a newer reload, a clear
    /// of the list) and the generation's ending into <paramref name="ct"/>, asking each, and settles it when it fails
    /// (<see cref="FailReload"/>).</summary>
    public async Task<TabLoads> RefreshAssembliesAsync(OpenListSession session, Queue<ViewCommand> viewCommands, DecompilerChoice decompiler,
        IReadOnlyList<NodePath> expanded, ReloadTicket ticket, ReloadBegun begun, CancellationToken ct)
    {
        var places = begun.Tabs.Select(static t => t.Location).ToList();
        var reloaded = await ports.ReopenFilesAsync(begun.Paths, expanded, places, begun.Work, ct);
        return FinishReload(session, viewCommands, decompiler.Setup, reloaded, ticket, begun.Intent, begun.Tabs);
    }

    // Each tab lets go of its node so that its place is found again in what the reload reads; a tab whose assembly was
    // closed keeps what it shows, as no file is read for it.
    private static List<ReloadingTab> ReleaseShownTabs(Navigation navigation, AssemblyList assemblies, DecompilationLanguage language)
    {
        var reloading = new List<ReloadingTab>();
        foreach (var tab in navigation.OpenTabs)
        {
            var location = tab.Location;
            if (location is null)
            {
                continue;
            }

            if (IsKeptClosed(tab, assemblies, location))
            {
                continue;
            }

            var position = FindPositionToKeep(tab, language);
            tab.Load.ShowReloading(location);
            reloading.Add(new ReloadingTab { Tab = tab, Location = location, Version = tab.Version, Position = position });
        }
        return reloading;
    }

    private static bool IsKeptClosed(DocumentTab tab, AssemblyList assemblies, NodePath location)
    {
        if (tab.Load.ClosedAssembly is null)
        {
            return false;
        }

        return !assemblies.IsListed(location.FilePath);
    }

    private static ViewPosition? FindPositionToKeep(DocumentTab tab, DecompilationLanguage language)
    {
        return Navigation.KeepPosition(tab.Load.Position, tab.Load.Language, language);
    }

    private TabLoads FinishReload(OpenListSession session, Queue<ViewCommand> viewCommands, DecompilerSetup decompiler, Outcome<ReloadedNodes> outcome,
        ReloadTicket ticket, int intent, IReadOnlyList<ReloadingTab> tabs)
    {
        if (!session.Reload.TryFinish(ticket))
        {
            return TabLoads.None;
        }

        return outcome switch
        {
            Outcome<ReloadedNodes>.Ok ok => ShowReloaded(session, viewCommands, decompiler, ok.Value, intent, tabs),
            Outcome<ReloadedNodes>.Failed failed => ShowNotReloaded(session, failed.Error),
        };
    }

    private TabLoads ShowNotReloaded(OpenListSession session, Exception error)
    {
        var reopened = ShowReloadFailed(session, error);
        ports.ReportError(new AppProblem.AssembliesNotReloaded(error));
        return reopened;
    }

    /// <summary>Ends the reload <paramref name="ticket"/> was given for when its workflow failed with
    /// <paramref name="error"/> (the edge's settle, which reports it), unless another reload started since: each tab
    /// still reloading says its file couldn't be read, and why, keeping its place for the next reload. Returns the loads
    /// the tabs reopened from the open list's saved documents started.</summary>
    public static TabLoads FailReload(OpenListSession session, ReloadTicket ticket, Exception error)
    {
        if (!session.Reload.TryFinish(ticket))
        {
            return TabLoads.None;
        }

        return ShowReloadFailed(session, error);
    }

    /// <summary>Ends the reload <paramref name="ticket"/> was given for when its reopen ended quietly, cancelled on
    /// purpose (a stop, a newer reload, the end of the generation it reads in): each of <paramref name="tabs"/> still
    /// showing what this reload left it showing, reloading, stops waiting and says what ended it, read from the list as
    /// it stands, never gone, as no lookup ran: its assembly closed when the list no longer holds it (Close All, the list
    /// cleared), else the reload stopped, its place kept for the next; a tab a newer reload shows reloading is that
    /// reload's. The reload ends, unless a newer one or a stop replaced it (whose tabs, shown again, are theirs). Returns
    /// the loads the tabs reopened from the open list's saved documents started, when the reload ended here.</summary>
    public static TabLoads EndQuietly(OpenListSession session, ReloadTicket ticket, IReadOnlyList<ReloadingTab> tabs)
    {
        foreach (var reloading in tabs)
        {
            if (IsStillShowing(session.Navigation, reloading))
            {
                reloading.Tab.Load.ShowNotReloaded(reloading.Location, WhatEnded(session.Assemblies, reloading.Location));
            }
        }

        if (!session.Reload.TryFinish(ticket))
        {
            return TabLoads.None;
        }

        return SavedDocuments.ShowReopenedDocuments(session.Navigation, session.Reopened, session.Assemblies);
    }

    private static ReloadEnd WhatEnded(AssemblyList assemblies, NodePath location)
    {
        return assemblies.IsListed(location.FilePath) ? ReloadEnd.Stopped : ReloadEnd.AssemblyClosed;
    }

    private static TabLoads ShowReloadFailed(OpenListSession session, Exception error)
    {
        foreach (var tab in session.Navigation.OpenTabs)
        {
            if (tab.Content is TabContent.Reloading reloading)
            {
                tab.Load.ShowLookupFailed(reloading.Location, error);
            }
        }

        return SavedDocuments.ShowReopenedDocuments(session.Navigation, session.Reopened, session.Assemblies);
    }

    private TabLoads ShowReloaded(OpenListSession session, Queue<ViewCommand> viewCommands, DecompilerSetup decompiler, ReloadedNodes reloaded, int intent,
        IReadOnlyList<ReloadingTab> tabs)
    {
        viewCommands.Enqueue(new ViewCommand.ExpandNodes(reloaded.Expanded));
        var loads = new List<DecompileTicket>();
        for (var i = 0; i < tabs.Count; i++)
        {
            ReshowTab(session.Navigation, viewCommands, decompiler, tabs[i], reloaded.Places[i], intent, loads);
        }

        var reopened = SavedDocuments.ShowReopenedDocuments(session.Navigation, session.Reopened, session.Assemblies);
        var retriedAnalyses = Analysis.BeginRetrying(session.Analyzer, session.Assemblies);
        return TabLoads.None.With(loads).With(reopened).With(retriedAnalyses);
    }

    private void ReshowTab(Navigation navigation, Queue<ViewCommand> viewCommands, DecompilerSetup decompiler, ReloadingTab reloading,
        Outcome<Lookup<BrowseNode>> place, int intent, List<DecompileTicket> loads)
    {
        var node = ShowAgain(navigation, decompiler, reloading, place, loads);
        if (node is null)
        {
            return;
        }

        if (reloading.Tab == navigation.ActiveTab && navigation.IsCurrent(intent))
        {
            viewCommands.Enqueue(new ViewCommand.RevealNode(node));
        }
    }

    // The node shown again, null when it is not. A place whose lookup failed is reported once, its tab saying so, and
    // why, where it is shown.
    private BrowseNode? ShowAgain(Navigation navigation, DecompilerSetup decompiler, ReloadingTab reloading, Outcome<Lookup<BrowseNode>> outcome,
        List<DecompileTicket> loads)
    {
        return outcome switch
        {
            Outcome<Lookup<BrowseNode>>.Ok ok => ShowFoundAgain(navigation, decompiler, reloading, ok.Value, loads),
            Outcome<Lookup<BrowseNode>>.Failed failed => ShowLookupFailed(navigation, reloading, failed.Error),
        };
    }

    private BrowseNode? ShowLookupFailed(Navigation navigation, ReloadingTab reloading, Exception error)
    {
        ports.ReportError(LookupFailures.OfPlace(reloading.Location, new PlaceLookupStep.ReloadingTab(), error));
        if (IsStillShowing(navigation, reloading))
        {
            reloading.Tab.Load.ShowLookupFailed(reloading.Location, error);
        }

        return null;
    }

    private BrowseNode? ShowFoundAgain(Navigation navigation, DecompilerSetup decompiler, ReloadingTab reloading, Lookup<BrowseNode> place,
        List<DecompileTicket> loads)
    {
        if (!IsStillShowing(navigation, reloading))
        {
            return null;
        }

        var tab = reloading.Tab;
        switch (place)
        {
            case Lookup<BrowseNode>.Found found:
                return Restore(navigation, decompiler, reloading, found.Value, loads);
            case Lookup<BrowseNode>.NotFound:
                ReleaseGoneTab(tab, reloading.Location);
                break;
            case Lookup<BrowseNode>.Unreadable unreadable:
                ReleaseUnreadableTab(tab, reloading.Location, unreadable.Module);
                break;
        }

        return null;
    }

    private static BrowseNode Restore(Navigation navigation, DecompilerSetup decompiler, ReloadingTab reloading, BrowseNode node,
        List<DecompileTicket> loads)
    {
        var load = navigation.Restore(reloading.Tab, node, decompiler, reloading.Position);
        if (load is not null)
        {
            loads.Add(load);
        }

        return node;
    }

    // The tab is still open, showing what the reload left it showing.
    private static bool IsStillShowing(Navigation navigation, ReloadingTab reloading)
    {
        return navigation.IsOpen(reloading.Tab) && reloading.Tab.Version == reloading.Version;
    }

    private void ReleaseGoneTab(DocumentTab tab, NodePath location)
    {
        // The tab says it is gone, where it was: no error of its own (BE2).
        tab.Load.ShowGone(location);
    }

    private void ReleaseUnreadableTab(DocumentTab tab, NodePath location, UnreadableModule unreadable)
    {
        tab.Load.ShowUnreadable(location, unreadable);
        ports.ReportError(AppProblem.ModuleUnreadable.FromModule(unreadable));
    }
}
