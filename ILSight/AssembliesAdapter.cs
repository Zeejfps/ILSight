using ILSight.Core;

namespace ILSight;

/// <summary>Reaches the app's error reports, the file system, and the open assemblies off the UI thread, for
/// <see cref="Assemblies"/>.</summary>
public sealed class AssembliesAdapter(ErrorLog errors, AssemblyList assemblies, BrowseSession session) : Assemblies.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    public bool IsFile(string path)
    {
        return File.Exists(path);
    }

    /// <summary>Opens on the thread pool and comes back on the UI thread, with whatever the opening threw as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<LoadedAssembly?>> OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, CancellationToken stop, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => OpenFiles(paths, stop), ct));
    }

    /// <inheritdoc cref="OpenFilesAsync"/>
    public async Task<Outcome<List<LoadedModule>>> OpenReferencesAsync(LoadedModule assembly, CancellationToken stop, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => AssemblyListing.ListReferences(assemblies, assembly, stop), ct));
    }

    /// <summary>Checks on the thread pool and comes back on the UI thread, with whatever the check threw as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<PdbCheck>> CheckPdbAsync(LoadedModule assembly, string pdbPath, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => LoadedAssemblies.CheckPdb(assembly, pdbPath), ct));
    }

    /// <summary>Reads on the thread pool and comes back on the UI thread, with whatever the reading threw as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<Assemblies.Reopened>> ReadAgainAsync(LoadedAssembly item, IReadOnlyList<NodePath> places, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => ReadAgain(item, places), ct));
    }

    /// <inheritdoc cref="OpenFilesAsync"/>
    public async Task<Outcome<Assemblies.Reloaded>> ReopenFilesAsync(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded,
        IReadOnlyList<NodePath> places, CancellationToken stop, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => ReopenFiles(paths, expanded, places, stop), ct));
    }

    /// <summary>Lists the files, those that fail to load as failed, and returns the last.</summary>
    private LoadedAssembly? OpenFiles(IEnumerable<AssemblyPath> paths, CancellationToken stop)
    {
        LoadedAssembly? last = null;
        foreach (var path in paths)
        {
            stop.ThrowIfCancellationRequested();
            last = AssemblyListing.Open(assemblies, path, stop);
        }
        return last;
    }

    private Assemblies.Reopened ReadAgain(LoadedAssembly item, IReadOnlyList<NodePath> places)
    {
        var fresh = AssemblyListing.ReadAgain(assemblies, item);
        EnsureChildrenAsLoaded(BrowseSessions.EnsureRoot(session, item), BrowseSessions.EnsureRoot(session, fresh));
        var placeNodes = places
            .Select(p => BrowseSessions.FindNode(session, fresh, p))
            .ToArray();
        return new Assemblies.Reopened { Item = fresh, Places = placeNodes };
    }

    /// <summary>Loads the children of each node under <paramref name="fresh"/> whose counterpart under
    /// <paramref name="old"/> has them loaded, so that the tree can show the fresh nodes as it showed
    /// the old ones.</summary>
    private void EnsureChildrenAsLoaded(BrowseNode old, BrowseNode fresh)
    {
        var oldChildren = BrowseSessions.PeekChildren(session, old);
        if (oldChildren is not ChildrenState.Ready loaded)
        {
            return;
        }

        BrowseSessions.GetChildren(session, fresh);
        foreach (var child in loaded.Children)
        {
            var counterpart = BrowseSessions.FindLoadedChildLike(session, fresh, child);
            if (counterpart is not null)
            {
                EnsureChildrenAsLoaded(child, counterpart);
            }
        }
    }

    private Assemblies.Reloaded ReopenFiles(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded, IReadOnlyList<NodePath> places,
        CancellationToken stop)
    {
        OpenFiles(paths, stop);
        var expandedNodes = GetExpandedNodes(expanded, stop);
        var placeNodes = FindNodes(places, stop);
        return new Assemblies.Reloaded { Expanded = expandedNodes, Places = placeNodes };
    }

    private List<BrowseNode> GetExpandedNodes(IReadOnlyList<NodePath> expanded, CancellationToken stop)
    {
        var nodes = new List<BrowseNode>();
        foreach (var path in expanded)
        {
            stop.ThrowIfCancellationRequested();
            var node = NodeLookup.Find(assemblies, session, path);
            if (node is null)
            {
                continue;
            }

            BrowseSessions.GetChildren(session, node);
            nodes.Add(node);
        }
        return nodes;
    }

    private BrowseNode?[] FindNodes(IReadOnlyList<NodePath> places, CancellationToken stop)
    {
        var found = new BrowseNode?[places.Count];
        for (var i = 0; i < found.Length; i++)
        {
            stop.ThrowIfCancellationRequested();
            found[i] = NodeLookup.Find(assemblies, session, places[i]);
        }
        return found;
    }
}
