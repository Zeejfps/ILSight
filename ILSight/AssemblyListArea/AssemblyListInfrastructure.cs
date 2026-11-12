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
        return await Background.Wait(Task.Run(() => OpenFilesAndFindRootAsync(paths, stop, ct), ct));
    }

    /// <summary>Lists the files, those that fail to load as failed, and gives the root of the last, or null for
    /// none.</summary>
    private async Task<BrowseNode?> OpenFilesAndFindRootAsync(IReadOnlyList<AssemblyPath> paths, CancellationToken stop, CancellationToken ct)
    {
        var last = OpenFiles(paths, stop);
        if (last is null)
        {
            return null;
        }

        return await BrowseSessions.EnsureRootAsync(session, last, ct);
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
        return await Background.Wait(Task.Run(() => ReadItemAgainAsync(item, places, ct), ct));
    }

    /// <inheritdoc cref="OpenFilesAsync"/>
    public async Task<Outcome<ReloadedNodes>> ReopenFilesAsync(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded,
        IReadOnlyList<NodePath> places, CancellationToken stop, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => ReopenFilesAsync(paths, expanded, places, stop), ct));
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

    private async Task<ReopenedFile> ReadItemAgainAsync(LoadedAssembly item, IReadOnlyList<NodePath> places, CancellationToken ct)
    {
        var fresh = AssemblyListing.ReadAgain(assemblies, item);
        var old = await BrowseSessions.EnsureRootAsync(session, item, ct);
        await EnsureChildrenAsLoadedAsync(old, await BrowseSessions.EnsureRootAsync(session, fresh.Item, ct), ct);
        var placeNodes = new Lookup<BrowseNode>[places.Count];
        for (var i = 0; i < placeNodes.Length; i++)
        {
            placeNodes[i] = await BrowseSessions.FindNodeAsync(session, fresh.Item, places[i], ct);
        }
        return new ReopenedFile { Fresh = fresh, Places = placeNodes };
    }

    /// <summary>Loads the children of each node under <paramref name="fresh"/> whose counterpart under
    /// <paramref name="old"/> has them loaded, so that the tree can show the fresh nodes as it showed
    /// the old ones.</summary>
    private async Task EnsureChildrenAsLoadedAsync(BrowseNode old, BrowseNode fresh, CancellationToken ct)
    {
        var oldChildren = session.PeekChildren(old);
        if (oldChildren is not ChildrenState.Ready loaded)
        {
            return;
        }

        var built = await BrowseSessions.GetChildrenAsync(session, fresh, ct);
        if (built is not ChildrenState.Ready)
        {
            // A fresh node whose children failed to build has none to carry the old ones' over to.
            return;
        }

        foreach (var child in loaded.Children)
        {
            var counterpart = session.FindLoadedChildLike(fresh, child);
            if (counterpart is not null)
            {
                await EnsureChildrenAsLoadedAsync(child, counterpart, ct);
            }
        }
    }

    private async Task<ReloadedNodes> ReopenFilesAsync(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded, IReadOnlyList<NodePath> places,
        CancellationToken stop)
    {
        OpenFiles(paths, stop);
        var expandedNodes = await GetExpandedNodesAsync(expanded, stop);
        var placeNodes = await FindNodesAsync(places, stop);
        return new ReloadedNodes { Expanded = expandedNodes, Places = placeNodes };
    }

    private async Task<List<BrowseNode>> GetExpandedNodesAsync(IReadOnlyList<NodePath> expanded, CancellationToken stop)
    {
        var nodes = new List<BrowseNode>();
        foreach (var path in expanded)
        {
            stop.ThrowIfCancellationRequested();
            var found = await NodeLookup.FindAsync(assemblies, session, path, stop);
            if (found is not Lookup<BrowseNode>.Found listed)
            {
                // A node that is gone, or in a module that could not be read, which shows that where the tree lists
                // it, is not shown expanded.
                continue;
            }

            var node = listed.Value;
            var built = await BrowseSessions.GetChildrenAsync(session, node, stop);
            if (built is not ChildrenState.Ready)
            {
                // A node whose children failed to build has none to show expanded.
                continue;
            }

            nodes.Add(node);
        }
        return nodes;
    }

    private async Task<Lookup<BrowseNode>[]> FindNodesAsync(IReadOnlyList<NodePath> places, CancellationToken stop)
    {
        var found = new Lookup<BrowseNode>[places.Count];
        for (var i = 0; i < found.Length; i++)
        {
            stop.ThrowIfCancellationRequested();
            found[i] = await NodeLookup.FindAsync(assemblies, session, places[i], stop);
        }
        return found;
    }
}
