using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ILSight.Core;
using static ILSight.AssemblyLists;
using static ILSight.Decompilation;
using static ILSight.SavedDocuments;
using static ILSight.Settings;

namespace ILSight;

/// <summary>Reading the assemblies of the list again: one of them, with a PDB or a target framework picked for it,
/// or all of them from disk, then showing each tab's place in them again. The workflows return once what they
/// started is done, and reach the files and the user's error reports through <see cref="IPorts"/>.</summary>
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
        Task<Outcome<Reopened>> ReadAgainAsync(LoadedAssembly item, IReadOnlyList<NodePath> places, CancellationToken ct);

        /// <summary>Lists the files at <paramref name="paths"/> in the open list, in order, those that fail to load
        /// as failed, then has the nodes at <paramref name="expanded"/> load their children and finds the nodes at
        /// <paramref name="places"/>. Done without holding up the screen, and stopped once <paramref name="stop"/>
        /// fires; what that threw comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<Reloaded>> ReopenFilesAsync(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded, IReadOnlyList<NodePath> places,
            CancellationToken stop, CancellationToken ct);
    }

    /// <summary>Checks that the PDB at <paramref name="pdbPath"/> can be read for <paramref name="assembly"/>,
    /// then has the assembly read it in place of the one it finds itself, kept with the list, and reopens the
    /// assembly to show it. Reports why when it cannot be read.</summary>
    public async Task UsePdbAsync(AppState appState, LoadedModule assembly, string pdbPath, CancellationToken ct)
    {
        var checking = await ports.CheckPdbAsync(assembly, pdbPath, ct);
        var check = checking.Match(static ok => ok.Value, static _ => PdbCheck.Unreadable);
        if (check != PdbCheck.Matches)
        {
            ports.ReportError(new AppProblem.PdbUnusable(pdbPath, assembly.Name, check));
            return;
        }
        if (!AssemblyListing.IsListed(appState.Assemblies, assembly))
        {
            return;
        }

        appState.Assemblies.Pdbs.Pick(assembly.FilePath, pdbPath);
        await ReopenAssemblyAsync(appState, assembly, ct);
    }

    /// <summary>Resolves <paramref name="assembly"/>'s references against the framework
    /// <paramref name="framework"/> names, such as <c>.NETCoreApp,Version=v8.0</c>, or, when it is
    /// null, against the one the assembly was built for; kept with the list, and the assembly reopened
    /// to show it. Does nothing when that is the framework already in use.</summary>
    public Task SetTargetFrameworkAsync(AppState appState, LoadedModule assembly, TargetFrameworkId? framework, CancellationToken ct)
    {
        if (!AssemblyListing.IsListed(appState.Assemblies, assembly))
        {
            return Task.CompletedTask;
        }

        if (IsInUse(assembly, framework))
        {
            return Task.CompletedTask;
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
    /// they were, then lists it where the item is and shows each tab's place in it again. The other assemblies
    /// stay as they are.</summary>
    private async Task ReopenAssemblyAsync(AppState appState, LoadedAssembly assembly, CancellationToken ct)
    {
        var item = assembly.ListedItem;
        var ticket = appState.Reopens.Start(item.FilePath);
        var tabs = ListTabsShowing(appState, item);
        var places = tabs.ConvertAll(t => t.Location);
        var reopened = await ports.ReadAgainAsync(item, places, ct);
        FinishReopen(appState, item, reopened, ticket, tabs);
    }

    private static List<ReloadingTab> ListTabsShowing(AppState appState, LoadedAssembly item)
    {
        var showing = new List<ReloadingTab>();
        foreach (var tab in appState.Tabs.Open)
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

    private void FinishReopen(AppState appState, LoadedAssembly item, Outcome<Reopened> outcome, int ticket, List<ReloadingTab> tabs)
    {
        if (!appState.Reopens.TryFinish(item.FilePath, ticket))
        {
            return;
        }

        SaveSettings(appState);
        switch (outcome)
        {
            case Outcome<Reopened>.Ok ok:
                ShowReopened(appState, item, ok.Value, tabs);
                break;
            case Outcome<Reopened>.Failed failed:
                ports.ReportError(new AppProblem.AssemblyNotReloaded(item.Name, failed.Error));
                break;
            default:
                throw new UnreachableException();
        }
    }

    private void ShowReopened(AppState appState, LoadedAssembly item, Reopened reopened, List<ReloadingTab> tabs)
    {
        if (!AssemblyListing.TryReplace(appState.Assemblies, item, reopened.Item))
        {
            return;
        }

        for (var i = 0; i < tabs.Count; i++)
        {
            TryShowAgain(appState, tabs[i], reopened.Places[i]);
        }
    }

    /// <summary>Reloads every assembly of the open list from disk, then commands the view to expand the tree at
    /// <paramref name="expanded"/>, the paths of the nodes it had expanded, and shows each tab's place again,
    /// decompiled from the fresh files.</summary>
    public async Task RefreshAssembliesAsync(AppState appState, IReadOnlyList<NodePath> expanded, CancellationToken ct)
    {
        RememberOpenAssemblies(appState);
        var intent = appState.Intent.Begin();
        var ticket = appState.Reload.Start();

        var paths = appState.Lists.ActivePaths.ToList();
        var tabs = ReleaseShownTabs(appState);
        var places = tabs.ConvertAll(t => t.Location);
        Assemblies.ClearAssemblies(appState);

        var reloaded = await ports.ReopenFilesAsync(paths, expanded, places, ticket.Token, ct);
        FinishReload(appState, reloaded, ticket, intent, tabs);
    }

    private static List<ReloadingTab> ReleaseShownTabs(AppState appState)
    {
        var reloading = new List<ReloadingTab>();
        foreach (var tab in appState.Tabs.Open)
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

    private void FinishReload(AppState appState, Outcome<Reloaded> outcome, ReloadTicket ticket, int intent, List<ReloadingTab> tabs)
    {
        if (!appState.Reload.TryFinish(ticket))
        {
            return;
        }

        switch (outcome)
        {
            case Outcome<Reloaded>.Ok ok:
                ShowReloaded(appState, ok.Value, intent, tabs);
                break;
            case Outcome<Reloaded>.Failed failed:
                ShowReopenedDocuments(appState);
                ports.ReportError(new AppProblem.AssembliesNotReloaded(failed.Error));
                break;
            default:
                throw new UnreachableException();
        }
    }

    private void ShowReloaded(AppState appState, Reloaded reloaded, int intent, List<ReloadingTab> tabs)
    {
        appState.ViewCommands.Enqueue(new ViewCommand.ExpandNodes(reloaded.Expanded));
        for (var i = 0; i < tabs.Count; i++)
        {
            ReshowTab(appState, tabs[i], reloaded.Places[i], intent);
        }

        ShowReopenedDocuments(appState);
        SaveSettings(appState);
    }

    private void ReshowTab(AppState appState, ReloadingTab reloading, BrowseNode? node, int intent)
    {
        if (!TryShowAgain(appState, reloading, node))
        {
            return;
        }

        if (reloading.Tab == appState.Tabs.Active && appState.Intent.IsCurrent(intent))
        {
            appState.ViewCommands.Enqueue(new ViewCommand.RevealNode(node));
        }
    }

    /// <summary>Shows <paramref name="node"/>, the tab's place found again, in the tab, or that its place
    /// is gone, unless the tab was closed or shows something else now. True when it shows the node.</summary>
    private bool TryShowAgain(AppState appState, ReloadingTab reloading, [NotNullWhen(true)] BrowseNode? node)
    {
        var tab = reloading.Tab;
        if (!Tabs.IsOpen(appState.Tabs, tab))
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
        DecompileIntoTab(appState, tab, node, new Arrival(reloading.Position));
        return true;
    }

    private void ReleaseGoneTab(DocumentTab tab, NodePath location)
    {
        Tabs.ShowGone(tab, location);
        ports.ReportError(new AppProblem.GoneAfterReload(location));
    }

    /// <summary>The nodes a reload found again: those the tree had expanded, with their children loaded, and
    /// each tab's place, null where it is gone, in the order the places were asked for.</summary>
    public sealed record Reloaded
    {
        public required List<BrowseNode> Expanded { get; init; }

        public required BrowseNode?[] Places { get; init; }
    }

    /// <summary>A file of the list read again: the item to list in place of the one before, and each tab's place
    /// in it, null where it is gone, in the order the places were asked for.</summary>
    public sealed record Reopened
    {
        public required LoadedAssembly Item { get; init; }

        public required BrowseNode?[] Places { get; init; }
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
