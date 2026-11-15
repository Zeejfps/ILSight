using ILSight.Core;
using ILSight.AssemblyArea;

namespace ILSight.AssemblyListArea;

/// <summary>Reaches the app's error reports, the file system and the open assemblies off the UI thread, for
/// <see cref="AssemblyOpening"/>, <see cref="AssemblyReloading"/> and <see cref="OpenAssembliesAsReloadHandler"/>.</summary>
public sealed class AssemblyListInfrastructure(ErrorLog errors, RefusalLine refusals, AssemblyList assemblies, BrowseSession session)
    : AssemblyOpening.IPorts, AssemblyReloading.IPorts, OpenAssembliesAsReloadHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    public void ShowRefusal(Refusal refusal)
    {
        refusals.Show(refusal);
    }

    public void ReleaseModules(RemovedModules removed)
    {
        LoadedAssemblies.Release(removed);
    }

    public bool IsFile(string path)
    {
        return File.Exists(path);
    }

    /// <summary>Opens on the thread pool, as work of <paramref name="work"/>'s generation, and comes back on the UI
    /// thread. A file the library can't read is listed as failed
    /// (<see cref="AssemblyListing.Open(AssemblyList, AssemblyPath, GenerationWork, CancellationToken)"/>); one met once
    /// the generation ended throws the cancellation of its ending. The cancellation of <paramref name="ct"/>, which the
    /// edge links to a newer reload, a clear of the list and the generation's ending, and anything else, are let
    /// through.</summary>
    public async Task<LoadedAssembly?> OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, GenerationWork work, CancellationToken ct)
    {
        var last = await Background.Wait(Task.Run(() => ListFiles(paths, work, ct), ct));
        ct.ThrowIfCancellationRequested();
        return last;
    }

    /// <summary>Opens as <see cref="OpenFilesAsync"/> does, and gives the root node of the last file listed, which
    /// opening shows.</summary>
    async Task<BrowseNode?> AssemblyOpening.IPorts.OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, GenerationWork work, CancellationToken ct)
    {
        var root = await Background.Wait(Task.Run(() => OpenFilesAndFindRootAsync(paths, work, ct), ct));
        ct.ThrowIfCancellationRequested();
        return root;
    }

    private async Task<BrowseNode?> OpenFilesAndFindRootAsync(IReadOnlyList<AssemblyPath> paths, GenerationWork work, CancellationToken ct)
    {
        var last = ListFiles(paths, work, ct);
        if (last is null)
        {
            return null;
        }

        return await BrowseSessions.EnsureRootAsync(session, last, ct);
    }

    /// <summary>Lists them on the thread pool and comes back on the UI thread, a reference the library failed to read
    /// as <see cref="Outcome{T}.Failed"/>, as the core answers it; cancelled as <see cref="OpenFilesAsync"/> is.</summary>
    public async Task<Outcome<List<LoadedModule>>> OpenReferencesAsync(LoadedModule assembly, GenerationWork work, CancellationToken ct)
    {
        var listed = await Background.Wait(Task.Run(() => AssemblyListing.ListReferences(assemblies, assembly, work, ct), ct));
        ct.ThrowIfCancellationRequested();
        return listed;
    }

    /// <summary>Checks on the thread pool, under a reading of the assembly, and comes back on the UI thread, with
    /// a file it could not read (<see cref="FileReadFailures"/>) as <see cref="PdbChecking.Failed"/>; an assembly closed before answers
    /// <see cref="PdbChecking.Closed"/>.</summary>
    public async Task<PdbChecking> CheckPdbAsync(LoadedModule assembly, string pdbPath, CancellationToken ct)
    {
        using var reading = LoadedAssemblies.TryBeginReading(assembly);
        if (reading is null)
        {
            return new PdbChecking.Closed { Assembly = assembly.FilePath.FileName };
        }

        var checking = await FileReadFailures.CatchAsync(Background.Wait(Task.Run(() => PdbFiles.Check(assembly, pdbPath), ct)), ct);
        return checking.Match<PdbChecking>(static ok => new PdbChecking.Checked { Check = ok.Value }, static failed => new PdbChecking.Failed { Error = failed.Error });
    }

    /// <summary>Reads on the thread pool and comes back on the UI thread, a file that can no longer be read as
    /// <see cref="Outcome{T}.Failed"/>, as the core answers it; the cancellation of <paramref name="ct"/>, and
    /// anything else, are let through.</summary>
    public async Task<Outcome<ReopenedFile>> ReadAgainAsync(LoadedAssembly item, IReadOnlyList<NodePath> places, CancellationToken ct)
    {
        var reading = Task.Run(() => ReadItemAgainAsync(item, places, ct), ct);
        try
        {
            var read = await Background.Wait(reading);
            ct.ThrowIfCancellationRequested();
            return read;
        }
        catch (OperationCanceledException)
        {
            ReleaseIfRead(reading);
            throw;
        }
    }

    // A read that ended before the cancellation that drops its answer holds a fresh file nothing lists.
    private static void ReleaseIfRead(Task<Outcome<ReopenedFile>> reading)
    {
        var ended = reading;
        if (ended.IsCompletedSuccessfully && ended.Result is Outcome<ReopenedFile>.Ok read)
        {
            LoadedAssemblies.Release(read.Value.Fresh.ListModules());
        }
    }

    /// <summary>Opens as <see cref="OpenFilesAsync"/> does, as work of <paramref name="work"/>'s generation, then finds the
    /// nodes; a lookup the library failed fails the reopen, as the core answers it. Cancelled as
    /// <see cref="OpenFilesAsync"/> is, the edge linking the reload's canceller and the generation's ending.</summary>
    public async Task<Outcome<ReloadedNodes>> ReopenFilesAsync(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded,
        IReadOnlyList<NodePath> places, GenerationWork work, CancellationToken ct)
    {
        var reopened = await Background.Wait(Task.Run(() => ReopenAndFindNodesAsync(paths, expanded, places, work, ct), ct));
        ct.ThrowIfCancellationRequested();
        return reopened;
    }

    private LoadedAssembly? ListFiles(IEnumerable<AssemblyPath> paths, GenerationWork work, CancellationToken ct)
    {
        LoadedAssembly? last = null;
        foreach (var path in paths)
        {
            ct.ThrowIfCancellationRequested();
            last = AssemblyListing.Open(assemblies, path, work, ct);
        }
        return last;
    }

    // The fresh file is this read's until the answer holding it is made: a read that fails or is cancelled on its way
    // releases it.
    private async Task<Outcome<ReopenedFile>> ReadItemAgainAsync(LoadedAssembly item, IReadOnlyList<NodePath> places, CancellationToken ct)
    {
        var read = AssemblyListing.ReadAgain(assemblies, item);
        if (read is not Outcome<FreshItem>.Ok { Value: var fresh })
        {
            return new Outcome<ReopenedFile>.Failed(((Outcome<FreshItem>.Failed)read).Error);
        }

        var handedOver = false;
        try
        {
            var reopened = new ReopenedFile { Fresh = fresh, Places = await FindPlacesAgainAsync(item, fresh, places, ct) };
            handedOver = true;
            return new Outcome<ReopenedFile>.Ok(reopened);
        }
        finally
        {
            if (!handedOver)
            {
                LoadedAssemblies.Release(fresh.ListModules());
            }
        }
    }

    private async Task<Outcome<Lookup<BrowseNode>>[]> FindPlacesAgainAsync(LoadedAssembly item, FreshItem fresh, IReadOnlyList<NodePath> places, CancellationToken ct)
    {
        var old = await BrowseSessions.EnsureRootAsync(session, item, ct);
        await EnsureChildrenAsLoadedAsync(old, await BrowseSessions.EnsureRootAsync(session, fresh.Item, ct), ct);
        var placeNodes = new Outcome<Lookup<BrowseNode>>[places.Count];
        for (var i = 0; i < placeNodes.Length; i++)
        {
            placeNodes[i] = await BrowseSessions.FindNodeAsync(session, fresh.Item, places[i], ct);
        }
        return placeNodes;
    }

    // So that the tree can show the fresh nodes as it showed the old ones.
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

    // A lookup of an expanded node the library failed fails the reopen, with why; each tab's place is its own.
    private async Task<Outcome<ReloadedNodes>> ReopenAndFindNodesAsync(IReadOnlyList<AssemblyPath> paths, IReadOnlyList<NodePath> expanded,
        IReadOnlyList<NodePath> places, GenerationWork work, CancellationToken ct)
    {
        ListFiles(paths, work, ct);
        var expandedNodes = await GetExpandedNodesAsync(expanded, ct);
        if (expandedNodes is Outcome<List<BrowseNode>>.Failed expandedFailed)
        {
            return new Outcome<ReloadedNodes>.Failed(expandedFailed.Error);
        }

        var placeNodes = await FindNodesAsync(places, ct);
        return new Outcome<ReloadedNodes>.Ok(new ReloadedNodes { Expanded = ((Outcome<List<BrowseNode>>.Ok)expandedNodes).Value, Places = placeNodes });
    }

    private async Task<Outcome<List<BrowseNode>>> GetExpandedNodesAsync(IReadOnlyList<NodePath> expanded, CancellationToken ct)
    {
        var nodes = new List<BrowseNode>();
        foreach (var path in expanded)
        {
            ct.ThrowIfCancellationRequested();
            var lookup = await NodeLookup.FindToExpandAsync(assemblies, session, path, ct);
            if (lookup is Outcome<Lookup<BrowseNode>>.Failed failed)
            {
                return new Outcome<List<BrowseNode>>.Failed(failed.Error);
            }

            if (lookup is not Outcome<Lookup<BrowseNode>>.Ok { Value: Lookup<BrowseNode>.Found listed })
            {
                // A node that is gone, or in a module that could not be read, or under children that failed to
                // build, each of which shows that where the tree lists it, is not shown expanded.
                continue;
            }

            var node = listed.Value;
            var built = await BrowseSessions.GetChildrenAsync(session, node, ct);
            if (built is not ChildrenState.Ready)
            {
                // A node whose children failed to build has none to show expanded.
                continue;
            }

            nodes.Add(node);
        }
        return new Outcome<List<BrowseNode>>.Ok(nodes);
    }

    // Each place's lookup its own outcome.
    private async Task<Outcome<Lookup<BrowseNode>>[]> FindNodesAsync(IReadOnlyList<NodePath> places, CancellationToken ct)
    {
        var found = new Outcome<Lookup<BrowseNode>>[places.Count];
        for (var i = 0; i < found.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            found[i] = await NodeLookup.FindAsync(assemblies, session, places[i], ct);
        }
        return found;
    }
}
