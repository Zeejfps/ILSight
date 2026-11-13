using ILSight.Core;

namespace ILSight.AssemblyArea;

/// <summary>Opening assemblies into the list: files the user picked or dropped, and the assemblies one references.
/// The workflows return once the files are open, with the load they started in a tab for the edge to start, and
/// reach the files and the user's error reports through <see cref="IPorts"/>.</summary>
public sealed class AssemblyOpening(AssemblyOpening.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user something went wrong.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Tells the user, where they acted, that what they asked for was refused, and why.</summary>
        void ShowRefusal(Refusal refusal);

        /// <summary>Whether there is a file, rather than a folder or nothing, at <paramref name="path"/>.</summary>
        bool IsFile(string path);

        /// <summary>Lists the files at <paramref name="paths"/> in the open list, in order, those that fail to load
        /// as failed, and gives the root node of the last one listed, or null for none. Done without holding up the
        /// screen, and
        /// stopped between files once <paramref name="stop"/> fires; what that threw comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<BrowseNode?>> OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, CancellationToken stop, CancellationToken ct);

        /// <summary>Lists the assemblies <paramref name="assembly"/> references that can be found in the open list,
        /// as <see cref="OpenFilesAsync"/> lists files.</summary>
        Task<Outcome<List<LoadedModule>>> OpenReferencesAsync(LoadedModule assembly, CancellationToken stop, CancellationToken ct);
    }

    /// <summary>Opens the files, then shows the last one opened, unless something else was asked for
    /// meanwhile. Returns the load that started in the tab, for the edge that started this to start under the tab's
    /// work; null when none started. Reports why opening failed, unless the opens were cancelled.</summary>
    public Task<DecompileTicket?> OpenAssembliesAsync(AppState appState, IEnumerable<AssemblyPath> paths, CancellationToken ct)
    {
        var opening = appState.Navigation.BeginOpening(paths);
        if (opening is null)
        {
            return Task.FromResult<DecompileTicket?>(null);
        }

        return OpenAssembliesAsync(appState, opening, ct);
    }

    /// <summary>Opens the files of the request <paramref name="opening"/> began (in the frame it was asked in), then
    /// shows the last one opened, as <see cref="OpenAssembliesAsync(AppState, IEnumerable{AssemblyPath}, CancellationToken)"/>
    /// does.</summary>
    public async Task<DecompileTicket?> OpenAssembliesAsync(AppState appState, FileOpening opening, CancellationToken ct)
    {
        var opened = await ports.OpenFilesAsync(opening.Paths, appState.Opens.Token, ct);
        return ShowOpened(appState, opened, opening.Intent);
    }

    private DecompileTicket? ShowOpened(AppState appState, Outcome<BrowseNode?> outcome, int ticket)
    {
        appState.Navigation.EndOpening(ticket);
        return outcome.Match(
            ok => ShowLastOpened(appState, ok.Value, ticket),
            failed =>
            {
                ReportOpenFailure(failed.Error);
                return null;
            });
    }

    private static DecompileTicket? ShowLastOpened(AppState appState, BrowseNode? node, int ticket)
    {
        if (node is null)
        {
            return null;
        }

        if (appState.Navigation.IsStale(ticket))
        {
            return null;
        }

        return appState.Navigation.ShowNode(node, appState.Decompiler.Setup);
    }

    private void ReportOpenFailure(Exception error)
    {
        var problem = Assemblies.FindOpenProblem(error);
        if (problem is null)
        {
            return;
        }

        ports.ReportError(problem);
    }

    /// <summary>Opens the files among <paramref name="paths"/>, dropped on the window, as
    /// <see cref="OpenAssembliesAsync(AppState, IEnumerable{AssemblyPath}, CancellationToken)"/> does; folders are left out, and a path that isn't a full one is refused, saying
    /// so.</summary>
    public Task<DecompileTicket?> OpenDroppedFilesAsync(AppState appState, IReadOnlyList<string> paths, CancellationToken ct)
    {
        var files = paths
            .Where(ports.IsFile)
            .ToList();
        return OpenFilesAtAsync(appState, files, ct);
    }

    private Task<DecompileTicket?> OpenFilesAtAsync(AppState appState, IEnumerable<string> paths, CancellationToken ct)
    {
        var files = new List<AssemblyPath>();
        foreach (var path in paths)
        {
            var file = AssemblyPath.Parse(path);
            if (file is null)
            {
                ports.ShowRefusal(new Refusal.NotFullPath { Path = path });
                continue;
            }

            files.Add(file);
        }

        return OpenAssembliesAsync(appState, files, ct);
    }

    /// <summary>Opens the assemblies <paramref name="assembly"/> references that can be found, cancelled with
    /// <paramref name="opens"/> under way now. Reports why opening failed, unless they were cancelled.</summary>
    public async Task OpenDependenciesAsync(OpenRequests opens, LoadedModule assembly, CancellationToken ct)
    {
        var opened = await ports.OpenReferencesAsync(assembly, opens.Token, ct);
        if (opened is Outcome<List<LoadedModule>>.Failed failed)
        {
            ReportOpenFailure(failed.Error);
        }
    }
}
