using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.BookmarkArea;
using ILSight.MetadataArea;

namespace ILSight;

/// <summary>Reaches the app's error reports and the open assemblies off the UI thread, for the navigation
/// handlers, <see cref="GoToBookmarkHandler"/>, <see cref="ShowReopenedNodesHandler"/> and
/// <see cref="AnalyzeReferenceHandler"/>.</summary>
public sealed class NavigationInfrastructure(ErrorLog errors, AssemblyList assemblies, BrowseSession session, OpenRequests opens)
    : NavigateHandler.IPorts, ShowInMetadataHandler.IPorts, ShowInApiReferenceHandler.IPorts, OpenAndShowIdHandler.IPorts,
        GoToHistoryEntryHandler.IPorts, GoToBookmarkHandler.IPorts, ShowReopenedNodesHandler.IPorts, AnalyzeReferenceHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    /// <summary>Looks in the open assemblies on the thread pool and comes back on the UI thread, with whatever the
    /// lookup threw as <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<Lookup<BrowseNode>>> LookUpReferenceAsync(CodeReference.Navigable reference, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => NodeLookup.FindAsync(assemblies, session, reference, ct), ct));
    }

    /// <inheritdoc cref="LookUpReferenceAsync"/>
    public async Task<Outcome<Lookup<MetadataTarget>>> LookUpMetadataRowAsync(CodeReference.Navigable reference, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => NodeLookup.FindMetadataRowAsync(assemblies, session, reference, ct), ct));
    }

    /// <inheritdoc cref="LookUpReferenceAsync"/>
    public async Task<Outcome<Lookup<BrowseNode>>> LookUpPlaceAsync(NodePath place, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => NodeLookup.FindAsync(assemblies, session, place, ct), ct));
    }

    /// <summary>Looks among the listed assemblies on the thread pool, opening nothing, and comes back on the UI thread,
    /// with whatever the lookup threw as <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<Lookup<BrowseNode>>> LookUpListedPlaceAsync(NodePath place, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => NodeLookup.FindListedAsync(assemblies, session, place, ct), ct));
    }

    /// <summary>Looks among the listed assemblies on the thread pool and comes back on the UI thread, with whatever
    /// the lookup threw as <see cref="Outcome{T}.Failed"/>, except the cancellation of <paramref name="ct"/>, which it
    /// lets through.</summary>
    public async Task<Outcome<List<Lookup<BrowseNode>>>> LookUpListedPlacesAsync(IReadOnlyList<NodePath?> places, CancellationToken ct)
    {
        var found = await Background.Wait(Task.Run(() => FindListedPlacesAsync(places, ct), ct));
        ct.ThrowIfCancellationRequested();
        return found;
    }

    private async Task<List<Lookup<BrowseNode>>> FindListedPlacesAsync(IReadOnlyList<NodePath?> places, CancellationToken ct)
    {
        var found = new List<Lookup<BrowseNode>>(places.Count);
        foreach (var place in places)
        {
            found.Add(await FindListedPlaceAsync(place, ct));
        }
        return found;
    }

    private async Task<Lookup<BrowseNode>> FindListedPlaceAsync(NodePath? place, CancellationToken ct)
    {
        if (place is null)
        {
            return new Lookup<BrowseNode>.NotFound();
        }

        return await NodeLookup.FindListedAsync(assemblies, session, place, ct);
    }

    /// <summary>Looks on the thread pool and comes back on the UI thread, with whatever the lookup threw as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<Lookup<string>>> LookUpApiReferencePageAsync(CodeReference.Entity reference, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => AssemblyListing.FindApiReferencePageAsync(assemblies, reference, ct), ct));
    }

    /// <summary>Opens and looks on the thread pool, the opening cancelled with the opens under way now, and comes
    /// back on the UI thread, with whatever that threw as <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<Lookup<BrowseNode>>> OpenAndFindIdAsync(IReadOnlyList<AssemblyPath> paths, string idString, CancellationToken ct)
    {
        var opening = opens.Token;
        return await Background.Wait(Task.Run(() => OpenAndLookUpIdAsync(paths, idString, opening), ct));
    }

    private async Task<Lookup<BrowseNode>> OpenAndLookUpIdAsync(IReadOnlyList<AssemblyPath> paths, string idString, CancellationToken opening)
    {
        var opened = new List<LoadedAssembly>();
        foreach (var path in paths)
        {
            opened.Add(AssemblyListing.Open(assemblies, path, opening));
        }

        var scope = Navigation.ListIdScope(assemblies, opened);
        return await NodeLookup.FindByIdAsync(session, idString, scope, opening);
    }
}
