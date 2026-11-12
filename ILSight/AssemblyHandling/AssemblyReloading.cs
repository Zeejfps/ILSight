using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ILSight.Core;
using static ILSight.AssemblyListKeeping.AssemblyLists;
using static ILSight.Decompilation;
using static ILSight.SavedDocuments;

namespace ILSight.AssemblyHandling;

/// <summary>Reading the assemblies of the list again: one of them, with a PDB or a target framework picked for it,
/// or all of them from disk, then showing each tab's place in them again. The workflows return once the files are
/// read, with the loads they started in the tabs for the edge to start, and reach the files and the user's error
/// reports through <see cref="IPorts"/>.</summary>
public sealed class AssemblyReloading(AssemblyReloading.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user something went wrong.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Whether the PDB at <paramref name="pdbPath"/> can be read for <paramref name="assembly"/>, checked
        /// without holding up the screen; a check that failed comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<PdbCheck>> CheckPdbAsync(LoadedModule assembly, string pdbPath, CancellationToken ct);

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
    }

    /// <summary>Checks that the PDB at <paramref name="pdbPath"/> can be read for <paramref name="assembly"/>,
    /// then has the assembly read it in place of the one it finds itself, kept with the list, and reopens the
    /// assembly to show it. Reports why when it cannot be read. Returns the loads that started in the tabs.</summary>
    public async Task<List<DecompileTicket>> UsePdbAsync(AppState appState, LoadedModule assembly, string pdbPath, CancellationToken ct)
    {
        var checking = await ports.CheckPdbAsync(assembly, pdbPath, ct);
        var check = checking.Match(static ok => ok.Value, static _ => PdbCheck.Unreadable);
        if (check != PdbCheck.Matches)
        {
            ports.ReportError(new AppProblem.PdbUnusable(pdbPath, assembly.Name, check));
            return [];
        }
        if (!AssemblyListing.IsListed(appState.Assemblies, assembly))
        {
            return [];
        }

        appState.Assemblies.Pdbs.Pick(assembly.FilePath, pdbPath);
        return await ReopenAssemblyAsync(appState, assembly, ct);
    }

    /// <summary>Resolves <paramref name="assembly"/>'s references against the framework
    /// <paramref name="framework"/> names, such as <c>.NETCoreApp,Version=v8.0</c>, or, when it is
    /// null, against the one the assembly was built for; kept with the list, and the assembly reopened
    /// to show it. Does nothing when that is the framework already in use. Returns the loads that started in the
    /// tabs.</summary>
    public Task<List<DecompileTicket>> SetTargetFrameworkAsync(AppState appState, LoadedModule assembly, TargetFrameworkId? framework, CancellationToken ct)
    {
        if (!AssemblyListing.IsListed(appState.Assemblies, assembly))
        {
            return Task.FromResult<List<DecompileTicket>>([]);
        }

        if (IsInUse(assembly, framework))
        {
            return Task.FromResult<List<DecompileTicket>>([]);
        }

        PickTargetFramework(appState.Assemblies.TargetFrameworks, assembly, framework);
        return ReopenAssemblyAsync(appState, assembly, ct);
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
    private async Task<List<DecompileTicket>> ReopenAssemblyAsync(AppState appState, LoadedAssembly assembly, CancellationToken ct)
    {
        var item = assembly.ListedItem;
        var ticket = appState.Reopens.Start(item.FilePath);
        var tabs = ListTabsShowing(appState, item);
        var places = tabs.ConvertAll(t => t.Location);
        var reopened = await ports.ReadAgainAsync(item, places, ct);
        return FinishReopen(appState, item, reopened, ticket, tabs);
    }

    private static List<ReloadingTab> ListTabsShowing(AppState appState, LoadedAssembly item)
    {
        var showing = new List<ReloadingTab>();
        foreach (var tab in appState.Navigation.OpenTabs)
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

            showing.Add(new ReloadingTab { Tab = tab, Location = location, Version = tab.Version, Position = FindPositionToKeep(appState, tab) });
        }
        return showing;
    }

    private static bool IsPathOf(LoadedAssembly item, NodePath path)
    {
        return path.FilePath.Equals(item.FilePath);
    }

    /// <summary>Lists what the reopen read, unless another one started since, and shows each tab's place in it
    /// again, or reports why it failed. Returns the loads it started in the tabs.</summary>
    private List<DecompileTicket> FinishReopen(AppState appState, LoadedAssembly item, Outcome<ReopenedFile> outcome, int ticket, List<ReloadingTab> tabs)
    {
        if (!appState.Reopens.TryFinish(item.FilePath, ticket))
        {
            return [];
        }

        switch (outcome)
        {
            case Outcome<ReopenedFile>.Ok ok:
                return ShowReopened(appState, item, ok.Value, tabs);
            case Outcome<ReopenedFile>.Failed failed:
                ports.ReportError(new AppProblem.AssemblyNotReloaded(item.Name, failed.Error));
                return [];
            default:
                throw new UnreachableException();
        }
    }

    private List<DecompileTicket> ShowReopened(AppState appState, LoadedAssembly item, ReopenedFile reopened, List<ReloadingTab> tabs)
    {
        var loads = new List<DecompileTicket>();
        if (!AssemblyListing.TryReplace(appState.Assemblies, item, reopened.Item))
        {
            return loads;
        }

        for (var i = 0; i < tabs.Count; i++)
        {
            TryShowAgain(appState, tabs[i], reopened.Places[i], loads);
        }
        return loads;
    }

    /// <summary>Reloads every assembly of the open list from disk, then commands the view to expand the tree at
    /// <paramref name="expanded"/>, the paths of the nodes it had expanded, and shows each tab's place again,
    /// decompiled from the fresh files, and what the tabs reopened from the open list's saved documents showed.
    /// Returns the loads that started in the tabs.</summary>
    public async Task<TabLoads> RefreshAssembliesAsync(AppState appState, IReadOnlyList<NodePath> expanded, CancellationToken ct)
    {
        RememberOpenAssemblies(appState.Lists, appState.ListSession);
        var intent = appState.Navigation.BeginRequest();
        var ticket = appState.Reload.Start();

        var paths = appState.Lists.ActivePaths.ToList();
        var tabs = ReleaseShownTabs(appState);
        var places = tabs.ConvertAll(t => t.Location);
        OpenAssemblies.CloseAll(appState.Assemblies, appState.Analyzer);

        var reloaded = await ports.ReopenFilesAsync(paths, expanded, places, ticket.Token, ct);
        return FinishReload(appState, reloaded, ticket, intent, tabs);
    }

    private static List<ReloadingTab> ReleaseShownTabs(AppState appState)
    {
        var reloading = new List<ReloadingTab>();
        foreach (var tab in appState.Navigation.OpenTabs)
        {
            var location = tab.Location;
            if (location is null)
            {
                continue;
            }

            var position = FindPositionToKeep(appState, tab);
            Tabs.ShowReloading(tab, location);
            reloading.Add(new ReloadingTab { Tab = tab, Location = location, Version = tab.Version, Position = position });
        }
        return reloading;
    }

    private static ViewPosition? FindPositionToKeep(AppState appState, DocumentTab tab)
    {
        return Navigation.KeepPosition(tab.Position, tab.Language, appState.Language);
    }

    /// <summary>Shows what the reload found, unless another one started since, or reports why it failed. Returns
    /// the loads it started in the tabs.</summary>
    private TabLoads FinishReload(AppState appState, Outcome<ReloadedNodes> outcome, ReloadTicket ticket, int intent, List<ReloadingTab> tabs)
    {
        if (!appState.Reload.TryFinish(ticket))
        {
            return TabLoads.None;
        }

        switch (outcome)
        {
            case Outcome<ReloadedNodes>.Ok ok:
                return ShowReloaded(appState, ok.Value, intent, tabs);
            case Outcome<ReloadedNodes>.Failed failed:
                var reopened = ShowReopenedDocuments(appState.Navigation, appState.Reopened, appState.Assemblies);
                ports.ReportError(new AppProblem.AssembliesNotReloaded(failed.Error));
                return reopened;
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Shows each tab's place again and what the tabs reopened from the saved documents showed, the nodes
    /// those wait for still to be found. Returns the loads it started in the tabs, the lookup of those nodes among
    /// them.</summary>
    private TabLoads ShowReloaded(AppState appState, ReloadedNodes reloaded, int intent, List<ReloadingTab> tabs)
    {
        appState.ViewCommands.Enqueue(new ViewCommand.ExpandNodes(reloaded.Expanded));
        var loads = new List<DecompileTicket>();
        for (var i = 0; i < tabs.Count; i++)
        {
            ReshowTab(appState, tabs[i], reloaded.Places[i], intent, loads);
        }

        var reopened = ShowReopenedDocuments(appState.Navigation, appState.Reopened, appState.Assemblies);
        return TabLoads.None.With(loads).With(reopened);
    }

    private void ReshowTab(AppState appState, ReloadingTab reloading, BrowseNode? node, int intent, List<DecompileTicket> loads)
    {
        if (!TryShowAgain(appState, reloading, node, loads))
        {
            return;
        }

        if (reloading.Tab == appState.Navigation.ActiveTab && appState.Navigation.IsCurrent(intent))
        {
            appState.ViewCommands.Enqueue(new ViewCommand.RevealNode(node));
        }
    }

    /// <summary>Shows <paramref name="node"/>, the tab's place found again, in the tab, or that its place
    /// is gone, unless the tab was closed or shows something else now. True when it shows the node, adding the
    /// load it started in the tab to <paramref name="loads"/>.</summary>
    private bool TryShowAgain(AppState appState, ReloadingTab reloading, [NotNullWhen(true)] BrowseNode? node, List<DecompileTicket> loads)
    {
        var tab = reloading.Tab;
        if (!appState.Navigation.IsOpen(tab))
        {
            return false;
        }

        if (tab.Version != reloading.Version)
        {
            return false;
        }

        if (node is null)
        {
            ReleaseGoneTab(tab, reloading.Location);
            return false;
        }
        var load = appState.Navigation.Restore(tab, node, appState.Language, appState.DecompilerOptions, reloading.Position);
        if (load is not null)
        {
            loads.Add(load);
        }

        return true;
    }

    private void ReleaseGoneTab(DocumentTab tab, NodePath location)
    {
        Tabs.ShowGone(tab, location);
        ports.ReportError(new AppProblem.GoneAfterReload(location));
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
