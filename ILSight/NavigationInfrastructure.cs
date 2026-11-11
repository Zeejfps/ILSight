using ILSight.Core;

namespace ILSight;

/// <summary>Reaches the app's error reports, and the open assemblies off the UI thread, for the navigation
/// handlers.</summary>
public sealed class NavigationInfrastructure(ErrorLog errors, AssemblyList assemblies, BrowseSession session, OpenRequests opens)
    : NavigateHandler.IPorts, ShowInMetadataHandler.IPorts, ShowInApiReferenceHandler.IPorts, OpenAndShowIdHandler.IPorts,
        GoToHistoryEntryHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
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
