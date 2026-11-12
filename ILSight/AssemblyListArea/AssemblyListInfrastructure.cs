using ILSight.Core;
using ILSight.AssemblyArea;

namespace ILSight.AssemblyListArea;

/// <summary>Reaches the app's error reports, the file system and the open assemblies off the UI thread, for
/// <see cref="AssemblyOpening"/>, <see cref="AssemblyReloading"/> and <see cref="OpenAssembliesAsReloadHandler"/>.</summary>
public sealed class AssemblyListInfrastructure(ErrorLog errors, AssemblyList assemblies, BrowseSession session)
    : AssemblyOpening.IPorts, AssemblyReloading.IPorts, OpenAssembliesAsReloadHandler.IPorts
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

    /// <summary>Opens as <see cref="OpenFilesAsync"/> does, and gives the root node of the last file listed, which
    /// opening shows.</summary>
    async Task<Outcome<BrowseNode?>> AssemblyOpening.IPorts.OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, CancellationToken stop, CancellationToken ct)
    {
        var opened = await OpenFilesAsync(paths, stop, ct);
        return opened.Match<Outcome<BrowseNode?>>(ok => new Outcome<BrowseNode?>.Ok(FindRoot(ok.Value)), static failed => new Outcome<BrowseNode?>.Failed(failed.Error));
    }

    private BrowseNode? FindRoot(LoadedAssembly? item)
    {
        if (item is null)
        {
            return null;
        }

        return BrowseSessions.EnsureRoot(session, item);
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
        return await Background.Wait(Task.Run(() => PdbFiles.Check(assembly, pdbPath), ct));
    }

    /// <summary>Reads on the thread pool and comes back on the UI thread, with whatever the reading threw as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<ReopenedFile>> ReadAgainAsync(LoadedAssembly item, IReadOnlyList<NodePath> places, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => ReadAgain(item, places), ct));
    }

    /// <inheritdoc cref="OpenFilesAsync"/>
    public async Task<Outcome<ReloadedNodes>> ReopenFilesAsync(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded,
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

    private ReopenedFile ReadAgain(LoadedAssembly item, IReadOnlyList<NodePath> places)
    {
        var fresh = AssemblyListing.ReadAgain(assemblies, item);
        EnsureChildrenAsLoaded(BrowseSessions.EnsureRoot(session, item), BrowseSessions.EnsureRoot(session, fresh));
        var placeNodes = places
            .Select(p => BrowseSessions.FindNode(session, fresh, p))
            .ToArray();
        return new ReopenedFile { Item = fresh, Places = placeNodes };
    }

    /// <summary>Loads the children of each node under <paramref name="fresh"/> whose counterpart under
    /// <paramref name="old"/> has them loaded, so that the tree can show the fresh nodes as it showed
    /// the old ones.</summary>
    private void EnsureChildrenAsLoaded(BrowseNode old, BrowseNode fresh)
    {
        var oldChildren = session.PeekChildren(old);
        if (oldChildren is not ChildrenState.Ready loaded)
        {
            return;
        }

        BrowseSessions.GetChildren(session, fresh);
        foreach (var child in loaded.Children)
        {
            var counterpart = session.FindLoadedChildLike(fresh, child);
            if (counterpart is not null)
            {
                EnsureChildrenAsLoaded(child, counterpart);
            }
        }
    }

    private ReloadedNodes ReopenFiles(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded, IReadOnlyList<NodePath> places,
        CancellationToken stop)
    {
        OpenFiles(paths, stop);
        var expandedNodes = GetExpandedNodes(expanded, stop);
        var placeNodes = FindNodes(places, stop);
        return new ReloadedNodes { Expanded = expandedNodes, Places = placeNodes };
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
