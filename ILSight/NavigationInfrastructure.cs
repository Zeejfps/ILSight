using ILSight.Core;
using ILSight.Core.Metadata;

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
    public async Task<Outcome<BrowseNode?>> LookUpReferenceAsync(CodeReference.Navigable reference, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => NodeLookup.Find(assemblies, session, reference), ct));
    }

    /// <inheritdoc cref="LookUpReferenceAsync"/>
    public async Task<Outcome<MetadataTarget?>> LookUpMetadataRowAsync(CodeReference.Navigable reference, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => NodeLookup.FindMetadataRow(assemblies, session, reference), ct));
    }

    /// <inheritdoc cref="LookUpReferenceAsync"/>
    public async Task<Outcome<BrowseNode?>> LookUpPlaceAsync(NodePath place, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => NodeLookup.Find(assemblies, session, place), ct));
    }

    /// <summary>Looks among the listed assemblies on the thread pool and comes back on the UI thread, with whatever
    /// the lookup threw as <see cref="Outcome{T}.Failed"/>, except the cancellation of <paramref name="ct"/>, which it
    /// lets through.</summary>
    public async Task<Outcome<List<BrowseNode?>>> LookUpListedPlacesAsync(IReadOnlyList<NodePath?> places, CancellationToken ct)
    {
        var found = await Background.Wait(Task.Run(() => LookUpListedPlaces(places), ct));
        ct.ThrowIfCancellationRequested();
        return found;
    }

    private List<BrowseNode?> LookUpListedPlaces(IReadOnlyList<NodePath?> places)
    {
        return places
            .Select(LookUpListedPlace)
            .ToList();
    }

    private BrowseNode? LookUpListedPlace(NodePath? place)
    {
        if (place is null)
        {
            return null;
        }

        return NodeLookup.FindListed(assemblies, session, place);
    }

    /// <summary>Looks on the thread pool and comes back on the UI thread, with whatever the lookup threw as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<string?>> LookUpApiReferencePageAsync(CodeReference.Entity reference, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => AssemblyListing.FindApiReferencePage(assemblies, reference), ct));
    }

    /// <summary>Opens and looks on the thread pool, the opening cancelled with the opens under way now, and comes
    /// back on the UI thread, with whatever that threw as <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<BrowseNode?>> OpenAndFindIdAsync(IReadOnlyList<AssemblyPath> paths, string idString, CancellationToken ct)
    {
        var opening = opens.Token;
        return await Background.Wait(Task.Run(() => OpenAndFindId(paths, idString, opening), ct));
    }

    private BrowseNode? OpenAndFindId(IReadOnlyList<AssemblyPath> paths, string idString, CancellationToken opening)
    {
        var opened = new List<LoadedAssembly>();
        foreach (var path in paths)
        {
            opened.Add(AssemblyListing.Open(assemblies, path, opening));
        }

        var scope = Navigation.ListIdScope(assemblies, opened);
        return NodeLookup.FindById(session, idString, scope);
    }
}
