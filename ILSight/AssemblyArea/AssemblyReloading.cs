using System.Diagnostics.CodeAnalysis;
using ILSight.Core;
using static ILSight.AssemblyListArea.AssemblyLists;
using static ILSight.Decompilation;
using static ILSight.SavedDocuments;

namespace ILSight.AssemblyArea;

/// <summary>Reading the assemblies of the list again: one of them, with a PDB or a target framework picked for it,
/// or all of them from disk, then showing each tab's place in them again. The workflows return once the files are
/// read, with the loads they started in the tabs for the edge to start, and reach the files and the user's error
/// reports through <see cref="IPorts"/>. Each takes the parts of the app state it uses, and how code is decompiled as
/// its owner (<see cref="DecompilerChoice"/>), read once the files are read, as it is then.</summary>
public sealed class AssemblyReloading(AssemblyReloading.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user something went wrong.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Tells the user, where they acted, what was refused and why.</summary>
        void ShowRefusal(Refusal refusal);

        /// <summary>Whether the PDB at <paramref name="pdbPath"/> can be read for <paramref name="assembly"/>, checked
        /// without holding up the screen; the assembly closed before it was read comes back as
        /// <see cref="PdbChecking.Closed"/>, a check that failed as <see cref="PdbChecking.Failed"/>.</summary>
        Task<PdbChecking> CheckPdbAsync(LoadedModule assembly, string pdbPath, CancellationToken ct);

        /// <summary>Reads the file of <paramref name="item"/> again, its nodes loaded as far as the item's are, and
        /// finds the nodes at <paramref name="places"/> in it, without holding up the screen. The list keeps the
        /// item until it is replaced. What that threw comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<ReopenedFile>> ReadAgainAsync(LoadedAssembly item, IReadOnlyList<NodePath> places, CancellationToken ct);

        /// <summary>Lists the files at <paramref name="paths"/> in the open list, in order, those that fail to load
        /// as failed, then has the nodes at <paramref name="expanded"/> load their children and finds the nodes at
        /// <paramref name="places"/>. Done without holding up the screen, and stopped once <paramref name="stop"/>
        /// fires; what that threw comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<ReloadedNodes>> ReopenFilesAsync(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded, IReadOnlyList<NodePath> places,
            CancellationToken stop, CancellationToken ct);

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
        if (checking is PdbChecking.Closed closed)
        {
            ports.ShowRefusal(new Refusal.PdbNotRead { Pdb = Path.GetFileName(pdbPath), Assembly = closed.Assembly });
            return [];
        }

        var check = checking.Match(static c => c.Check, static _ => PdbCheck.Unreadable, static _ => PdbCheck.Unreadable);
        if (check != PdbCheck.Matches)
        {
            ports.ReportError(new AppProblem.PdbUnusable
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

    /// <summary>Whether <paramref name="framework"/>, null for the one the assembly was built for, is
    /// what its references are resolved against now.</summary>
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

    /// <summary>Reads the item of the list holding <paramref name="assembly"/> again, its nodes loaded as far as
    /// they were, then lists it where the item is and shows each tab's place in it again, returning the loads that
    /// started in the tabs. The other assemblies stay as they are.</summary>
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

    /// <summary>Lists what the reopen read, unless another one started since, and shows each tab's place in it
    /// again, or reports why it failed. Returns the loads it started in the tabs.</summary>
    private List<DecompileTicket> FinishReopen(Reopens reopens, OpenListSession session, DecompilerSetup decompiler, LoadedAssembly item,
        Outcome<ReopenedFile> outcome, int ticket, List<ReloadingTab> tabs)
    {
        if (!reopens.TryFinish(item.FilePath, ticket))
        {
            return [];
        }

        return outcome.Match(
            ok => ShowReopened(session, session.Navigation, decompiler, item, ok.Value, tabs),
            failed => ReportNotReopened(item, failed.Error));
    }

    /// <summary>Reports why <paramref name="item"/> could not be read again. Returns no loads: none started.</summary>
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
            TryShowAgain(navigation, decompiler, tabs[i], reopened.Places[i], loads, out _);
        }
        return loads;
    }

    /// <summary>Reloads every assembly of the open list from disk, then commands the view to expand the tree at
    /// <paramref name="expanded"/>, the paths of the nodes it had expanded, and shows each tab's place again,
    /// decompiled from the fresh files, and what the tabs reopened from the open list's saved documents showed.
    /// Returns the loads that started in the tabs.</summary>
    public async Task<TabLoads> RefreshAssembliesAsync(SavedAssemblyLists lists, OpenListSession session, Queue<ViewCommand> viewCommands,
        DecompilerChoice decompiler, IReadOnlyList<NodePath> expanded, CancellationToken ct)
    {
        RememberOpenAssemblies(lists, session);
        var intent = session.Navigation.BeginRequest();
        var ticket = session.Reload.Start();

        var paths = lists.ActivePaths.ToList();
        var tabs = ReleaseShownTabs(session.Navigation, session.Assemblies, decompiler.Setup.Language);
        var places = tabs.ConvertAll(t => t.Location);
        ports.ReleaseModules(session.ClearAssemblies());

        var reloaded = await ports.ReopenFilesAsync(paths, expanded, places, ticket.Token, ct);
        return FinishReload(session, viewCommands, decompiler.Setup, reloaded, ticket, intent, tabs);
    }

    /// <summary>Lets go of the node each tab shows, to find its place again in what the reload reads; a tab whose
    /// assembly was closed (<see cref="TabLoad.ShowClosed"/>) keeps what it shows, reading nothing, unless its file
    /// is on <paramref name="assemblies"/> again, where its place is found as any other's.</summary>
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

    /// <summary>Whether <paramref name="tab"/>, at <paramref name="location"/>, shows a node whose assembly was closed
    /// and whose file <paramref name="assemblies"/> doesn't list again.</summary>
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

    /// <summary>Shows what the reload found, unless another one started since, or reports why it failed. Returns
    /// the loads it started in the tabs.</summary>
    private TabLoads FinishReload(OpenListSession session, Queue<ViewCommand> viewCommands, DecompilerSetup decompiler, Outcome<ReloadedNodes> outcome,
        ReloadTicket ticket, int intent, List<ReloadingTab> tabs)
    {
        if (!session.Reload.TryFinish(ticket))
        {
            return TabLoads.None;
        }

        return outcome.Match(
            ok => ShowReloaded(session, viewCommands, decompiler, ok.Value, intent, tabs),
            failed => ShowNotReloaded(session, failed.Error));
    }

    /// <summary>Shows what the tabs reopened from the saved documents showed, the nodes those wait for still to be
    /// found, and reports why the reload failed. Returns the loads it started in the tabs.</summary>
    private TabLoads ShowNotReloaded(OpenListSession session, Exception error)
    {
        var reopened = ShowReopenedDocuments(session.Navigation, session.Reopened, session.Assemblies);
        ports.ReportError(new AppProblem.AssembliesNotReloaded(error));
        return reopened;
    }

    /// <summary>Shows each tab's place again and what the tabs reopened from the saved documents showed, the nodes
    /// those wait for still to be found, and has the restored analyses that failed looked for again in what was read.
    /// Returns the loads it started in the tabs, the lookup of those nodes and of those analyses among them.</summary>
    private TabLoads ShowReloaded(OpenListSession session, Queue<ViewCommand> viewCommands, DecompilerSetup decompiler, ReloadedNodes reloaded, int intent,
        List<ReloadingTab> tabs)
    {
        viewCommands.Enqueue(new ViewCommand.ExpandNodes(reloaded.Expanded));
        var loads = new List<DecompileTicket>();
        for (var i = 0; i < tabs.Count; i++)
        {
            ReshowTab(session.Navigation, viewCommands, decompiler, tabs[i], reloaded.Places[i], intent, loads);
        }

        var reopened = ShowReopenedDocuments(session.Navigation, session.Reopened, session.Assemblies);
        return TabLoads.None.With(loads).With(reopened).With(Analysis.BeginRetrying(session.Analyzer, session.Assemblies));
    }

    private void ReshowTab(Navigation navigation, Queue<ViewCommand> viewCommands, DecompilerSetup decompiler, ReloadingTab reloading,
        Lookup<BrowseNode> place, int intent, List<DecompileTicket> loads)
    {
        if (!TryShowAgain(navigation, decompiler, reloading, place, loads, out var node))
        {
            return;
        }

        if (reloading.Tab == navigation.ActiveTab && navigation.IsCurrent(intent))
        {
            viewCommands.Enqueue(new ViewCommand.RevealNode(node));
        }
    }

    /// <summary>Shows the node <paramref name="place"/> found, the tab's place found again, in the tab, or that its
    /// place is gone, or in a module that could not be read, unless the tab was closed or shows something else now.
    /// True when it shows the <paramref name="node"/>, adding the load it started in the tab to
    /// <paramref name="loads"/>.</summary>
    private bool TryShowAgain(Navigation navigation, DecompilerSetup decompiler, ReloadingTab reloading, Lookup<BrowseNode> place,
        List<DecompileTicket> loads, [NotNullWhen(true)] out BrowseNode? node)
    {
        node = null;
        var tab = reloading.Tab;
        if (!navigation.IsOpen(tab))
        {
            return false;
        }

        if (tab.Version != reloading.Version)
        {
            return false;
        }

        node = place.Match<BrowseNode?>(
            static found => found.Value,
            _ =>
            {
                ReleaseGoneTab(tab, reloading.Location);
                return null;
            },
            unreadable =>
            {
                ReleaseUnreadableTab(tab, reloading.Location, unreadable.Module);
                return null;
            });
        if (node is null)
        {
            return false;
        }

        var load = navigation.Restore(tab, node, decompiler, reloading.Position);
        if (load is not null)
        {
            loads.Add(load);
        }

        return true;
    }

    private void ReleaseGoneTab(DocumentTab tab, NodePath location)
    {
        tab.Load.ShowGone(location);
        ports.ReportError(new AppProblem.GoneAfterReload(location));
    }

    private void ReleaseUnreadableTab(DocumentTab tab, NodePath location, UnreadableModule unreadable)
    {
        tab.Load.ShowUnreadable(location, unreadable);
        ports.ReportError(AppProblem.ModuleUnreadable.FromModule(unreadable));
    }

    /// <summary>A tab whose place is being found again, as it was when that started.</summary>
    private sealed record ReloadingTab
    {
        public required DocumentTab Tab { get; init; }

        public required NodePath Location { get; init; }

        public required int Version { get; init; }

        public required ViewPosition? Position { get; init; }
    }
}
