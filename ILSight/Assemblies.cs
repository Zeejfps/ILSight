using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ILSight.Core;
using static ILSight.AssemblyLists;
using static ILSight.Decompilation;
using static ILSight.Navigation;
using static ILSight.SavedDocuments;
using static ILSight.Settings;

namespace ILSight;

/// <summary>Opening, closing and reloading the assemblies in the list. The workflows return once what they started
/// is done, and reach the files and the user's error reports through <see cref="IPorts"/>.</summary>
public static class Assemblies
{
    public interface IPorts
    {
        /// <summary>Tells the user something went wrong.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Whether there is a file, rather than a folder or nothing, at <paramref name="path"/>.</summary>
        bool IsFile(string path);

        /// <summary>Lists the files at <paramref name="paths"/> in the open list, in order, those that fail to load
        /// as failed, and gives the last one listed, or null for none. Done without holding up the screen, and
        /// stopped between files once <paramref name="stop"/> fires; what that threw comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<LoadedAssembly?>> OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, CancellationToken stop, CancellationToken ct);

        /// <summary>Lists the assemblies <paramref name="assembly"/> references that can be found in the open list,
        /// as <see cref="OpenFilesAsync"/> lists files.</summary>
        Task<Outcome<List<LoadedModule>>> OpenReferencesAsync(LoadedModule assembly, CancellationToken stop, CancellationToken ct);

        /// <summary>Whether the PDB at <paramref name="pdbPath"/> can be read for <paramref name="assembly"/>, checked
        /// without holding up the screen; a check that failed comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<PdbCheck>> CheckPdbAsync(LoadedModule assembly, string pdbPath, CancellationToken ct);

        /// <summary>Reads the file of <paramref name="item"/> again, its nodes loaded as far as the item's are, and
        /// finds the nodes at <paramref name="places"/> in it, without holding up the screen. The list keeps the
        /// item until it is replaced. What that threw comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<Reopened>> ReadAgainAsync(LoadedAssembly item, IReadOnlyList<NodePath> places, CancellationToken ct);

        /// <summary>Lists the files at <paramref name="paths"/> in the open list, as <see cref="OpenFilesAsync"/>
        /// does, then has the nodes at <paramref name="expanded"/> load their children and finds the nodes at
        /// <paramref name="places"/>, stopping once <paramref name="stop"/> fires.</summary>
        Task<Outcome<Reloaded>> ReopenFilesAsync(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded, IReadOnlyList<NodePath> places,
            CancellationToken stop, CancellationToken ct);
    }

    /// <summary>Opens the files, then shows the last one opened, unless something else was asked for
    /// meanwhile. Reports why opening failed, unless the opens were cancelled.</summary>
    public static async Task OpenAssembliesAsync(AppState appState, IEnumerable<AssemblyPath> paths, IPorts ports, CancellationToken ct)
    {
        var files = paths.ToArray();
        if (files.Length == 0)
        {
            return;
        }

        var ticket = appState.Intent.Begin();
        var opened = await ports.OpenFilesAsync(files, appState.Opens.Token, ct);
        ShowOpened(appState, opened, ticket, ports);
    }

    private static void ShowOpened(AppState appState, Outcome<LoadedAssembly?> outcome, int ticket, IPorts ports)
    {
        switch (outcome)
        {
            case Outcome<LoadedAssembly?>.Ok ok when ok.Value is not null:
                if (!appState.Intent.IsStale(ticket))
                {
                    ShowNode(appState, BrowseSessions.EnsureRoot(appState.Session, ok.Value));
                }

                break;
            case Outcome<LoadedAssembly?>.Ok:
                break;
            case Outcome<LoadedAssembly?>.Failed failed:
                ReportOpenFailure(ports, failed.Error);
                break;
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Reports why opening failed, unless the opening was cancelled.</summary>
    private static void ReportOpenFailure(IPorts ports, Exception error)
    {
        var problem = FindOpenProblem(error);
        if (problem is null)
        {
            return;
        }

        ports.ReportError(problem);
    }

    /// <summary>What to tell the user about opening that failed with <paramref name="error"/>; null when the
    /// opening was cancelled, which is no failure.</summary>
    internal static AppProblem.OpenFailed? FindOpenProblem(Exception error)
    {
        if (error is OperationCanceledException)
        {
            return null;
        }

        return new AppProblem.OpenFailed(error);
    }

    /// <summary>Opens the file at <paramref name="path"/>, as a file dialog or a download names it; nothing
    /// when it is no absolute path.</summary>
    public static Task OpenAssemblyAsync(AppState appState, string path, IPorts ports, CancellationToken ct)
    {
        return OpenFilesAtAsync(appState, [path], ports, ct);
    }

    /// <summary>Opens the files among <paramref name="paths"/>, dropped on the window, as
    /// <see cref="OpenAssembliesAsync"/> does; folders are left out.</summary>
    public static Task OpenDroppedFilesAsync(AppState appState, IReadOnlyList<string> paths, IPorts ports, CancellationToken ct)
    {
        var files = paths
            .Where(ports.IsFile)
            .ToList();
        return OpenFilesAtAsync(appState, files, ports, ct);
    }

    private static Task OpenFilesAtAsync(AppState appState, IEnumerable<string> paths, IPorts ports, CancellationToken ct)
    {
        var files = paths
            .Select(AssemblyPath.Parse)
            .OfType<AssemblyPath>();
        return OpenAssembliesAsync(appState, files, ports, ct);
    }

    /// <summary>Opens the assemblies <paramref name="assembly"/> references that can be found, cancelled with
    /// <paramref name="opens"/> under way now. Reports why opening failed, unless they were cancelled.</summary>
    public static async Task OpenDependenciesAsync(OpenRequests opens, LoadedModule assembly, IPorts ports, CancellationToken ct)
    {
        var opened = await ports.OpenReferencesAsync(assembly, opens.Token, ct);
        if (opened is Outcome<List<LoadedModule>>.Failed failed)
        {
            ReportOpenFailure(ports, failed.Error);
        }
    }

    /// <summary>Checks that the PDB at <paramref name="pdbPath"/> can be read for <paramref name="assembly"/>,
    /// then has the assembly read it in place of the one it finds itself, kept with the list, and reopens the
    /// assembly to show it. Reports why when it cannot be read.</summary>
    public static async Task UsePdbAsync(AppState appState, LoadedModule assembly, string pdbPath, IPorts ports, CancellationToken ct)
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
        await ReopenAssemblyAsync(appState, assembly, ports, ct);
    }

    /// <summary>Resolves <paramref name="assembly"/>'s references against the framework
    /// <paramref name="framework"/> names, such as <c>.NETCoreApp,Version=v8.0</c>, or, when it is
    /// null, against the one the assembly was built for; kept with the list, and the assembly reopened
    /// to show it. Does nothing when that is the framework already in use.</summary>
    public static Task SetTargetFrameworkAsync(AppState appState, LoadedModule assembly, TargetFrameworkId? framework, IPorts ports,
        CancellationToken ct)
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
        return ReopenAssemblyAsync(appState, assembly, ports, ct);
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
    private static async Task ReopenAssemblyAsync(AppState appState, LoadedAssembly assembly, IPorts ports, CancellationToken ct)
    {
        var item = assembly.ListedItem;
        var ticket = appState.Reopens.Start(item.FilePath);
        var tabs = ListTabsShowing(appState, item);
        var places = tabs.ConvertAll(t => t.Location);
        var reopened = await ports.ReadAgainAsync(item, places, ct);
        FinishReopen(appState, item, reopened, ticket, tabs, ports);
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

    private static void FinishReopen(AppState appState, LoadedAssembly item, Outcome<Reopened> outcome, int ticket, List<ReloadingTab> tabs,
        IPorts ports)
    {
        if (!appState.Reopens.Finish(item.FilePath, ticket))
        {
            return;
        }

        SaveSettings(appState);
        switch (outcome)
        {
            case Outcome<Reopened>.Ok ok:
                ShowReopened(appState, item, ok.Value, tabs, ports);
                break;
            case Outcome<Reopened>.Failed failed:
                ports.ReportError(new AppProblem.AssemblyNotReloaded(item.Name, failed.Error));
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void ShowReopened(AppState appState, LoadedAssembly item, Reopened reopened, List<ReloadingTab> tabs, IPorts ports)
    {
        if (!AssemblyListing.Replace(appState.Assemblies, item, reopened.Item))
        {
            return;
        }

        for (var i = 0; i < tabs.Count; i++)
        {
            ShowAgain(appState, tabs[i], reopened.Places[i], ports);
        }
    }

    /// <summary>Takes <paramref name="items"/> off <paramref name="list"/>; one no longer on it is passed over.</summary>
    public static void CloseAssemblies(AssemblyList list, IReadOnlyList<LoadedAssembly> items)
    {
        foreach (var item in items)
        {
            AssemblyListing.CloseAssembly(list, item);
        }
    }

    /// <summary>Takes the files that failed to load off <paramref name="list"/>.</summary>
    public static void CloseFailedAssemblies(AssemblyList list)
    {
        var failed = list.Listed
            .OfType<FailedAssembly>()
            .ToList();
        foreach (var assembly in failed)
        {
            AssemblyListing.CloseAssembly(list, assembly);
        }
    }

    /// <summary>Closes every assembly of the open list, stopping its reload; tabs still waiting for it to
    /// reopen their places show what is left of them.</summary>
    public static void CloseAllAssemblies(AppState appState)
    {
        appState.Intent.Begin();
        appState.Opens.Cancel();
        appState.Reload.Stop();
        ClearAssemblies(appState);
        appState.Assemblies.Pdbs.ForgetAll();
        appState.Assemblies.TargetFrameworks.ForgetAll();
        ShowReopenedDocuments(appState);
    }

    private static void ClearAssemblies(AppState appState)
    {
        AssemblyListing.CloseAll(appState.Assemblies);
        appState.Analyzer.ClearRoots();
    }

    /// <summary>Reloads every assembly of the open list from disk, then commands the view to expand the tree at
    /// <paramref name="expanded"/>, the paths of the nodes it had expanded, and shows each tab's place again,
    /// decompiled from the fresh files.</summary>
    public static async Task RefreshAssembliesAsync(AppState appState, IReadOnlyList<NodePath> expanded, IPorts ports, CancellationToken ct)
    {
        RememberOpenAssemblies(appState);
        var intent = appState.Intent.Begin();
        var ticket = appState.Reload.Start();

        var paths = appState.Lists.ActivePaths.ToList();
        var tabs = ReleaseShownTabs(appState);
        var places = tabs.ConvertAll(t => t.Location);
        ClearAssemblies(appState);

        var reloaded = await ports.ReopenFilesAsync(paths, expanded, places, ticket.Token, ct);
        FinishReload(appState, reloaded, ticket, intent, tabs, ports);
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

    private static void FinishReload(AppState appState, Outcome<Reloaded> outcome, ReloadTicket ticket, int intent, List<ReloadingTab> tabs,
        IPorts ports)
    {
        if (!appState.Reload.Finish(ticket))
        {
            return;
        }

        switch (outcome)
        {
            case Outcome<Reloaded>.Ok ok:
                ShowReloaded(appState, ok.Value, intent, tabs, ports);
                break;
            case Outcome<Reloaded>.Failed failed:
                ShowReopenedDocuments(appState);
                ports.ReportError(new AppProblem.AssembliesNotReloaded(failed.Error));
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void ShowReloaded(AppState appState, Reloaded reloaded, int intent, List<ReloadingTab> tabs, IPorts ports)
    {
        appState.ViewCommands.Enqueue(new ViewCommand.ExpandNodes(reloaded.Expanded));
        for (var i = 0; i < tabs.Count; i++)
        {
            ReshowTab(appState, tabs[i], reloaded.Places[i], intent, ports);
        }

        ShowReopenedDocuments(appState);
        SaveSettings(appState);
    }

    private static void ReshowTab(AppState appState, ReloadingTab reloading, BrowseNode? node, int intent, IPorts ports)
    {
        if (!ShowAgain(appState, reloading, node, ports))
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
    private static bool ShowAgain(AppState appState, ReloadingTab reloading, [NotNullWhen(true)] BrowseNode? node, IPorts ports)
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
            ReleaseGoneTab(tab, reloading.Location, ports);
            return false;
        }
        DecompileIntoTab(appState, tab, node, new Arrival(reloading.Position));
        return true;
    }

    private static void ReleaseGoneTab(DocumentTab tab, NodePath location, IPorts ports)
    {
        Tabs.ShowGone(tab, location);
        ports.ReportError(new AppProblem.GoneAfterReload(location));
    }

    /// <summary>Opens <paramref name="paths"/> as a reload does, so that another reload or clearing the list
    /// stops it, and the settings are saved once it is done.</summary>
    internal static async Task OpenAssembliesAsReloadAsync(AppState appState, IReadOnlyList<AssemblyPath> paths, IPorts ports, CancellationToken ct)
    {
        var ticket = appState.Reload.Start();
        var opened = await ports.OpenFilesAsync(paths, ticket.Token, ct);
        FinishOpeningAsReload(appState, opened, ticket, ports);
    }

    private static void FinishOpeningAsReload(AppState appState, Outcome<LoadedAssembly?> outcome, ReloadTicket ticket, IPorts ports)
    {
        if (!appState.Reload.Finish(ticket))
        {
            return;
        }

        ShowReopenedDocuments(appState);
        switch (outcome)
        {
            case Outcome<LoadedAssembly?>.Ok:
                SaveSettings(appState);
                break;
            case Outcome<LoadedAssembly?>.Failed failed:
                ports.ReportError(new AppProblem.OpenFailed(failed.Error));
                break;
            default:
                throw new UnreachableException();
        }
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
