using ILSight.Core;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;

namespace ILSight.AssemblyArea;

/// <summary>Loads the open list's assemblies, and the tabs showing them, where no command asks for it: the list the app
/// starts with, its saved analyses found as they open; the list again when the options that apply as assemblies are
/// read change; and the tabs whose assembly was closed once it is listed again.</summary>
[Edge]
public sealed class AssemblyListLoader
{
    private readonly AppState _appState;

    /// <param name="appState">The app state whose open list this loads.</param>
    internal AssemblyListLoader(AppState appState)
    {
        _appState = appState;
    }

    /// <summary>Starts opening the active list's assemblies <paramref name="start"/> began reopening, and finding its
    /// saved analyses' symbols. None waits for another. A switch or a clear since stopped them: they read nothing
    /// then.</summary>
    public void StartOpeningStartupList(ListStart start)
    {
        var reopening = start.Reopening;
        Commands.StartOpeningAsReload(_appState, nameof(OpenStartupAssembliesAsync), reopening, ct => OpenStartupAssembliesAsync(reopening, ct));
        Commands.StartRestoringAnalyses(_appState, start.Analyses);
    }

    /// <summary>When the options that apply as assemblies are read changed, reads the open list's assemblies again,
    /// keeping the nodes expanded in <paramref name="tree"/> expanded. Their change ends the list's generation, as a
    /// clear does: the opens under way commit nothing after it, and are cancelled.</summary>
    public void ReloadListWhenLoadOptionsChange(BrowseTree tree)
    {
        if (!AssemblyListing.TryChangeLoadOptions(_appState.ListSession.Assemblies, _appState.Decompiler.Setup.Options))
        {
            return;
        }

        // Started before the frame draws, so that it draws them reloading.
        Commands.StartReloading(_appState, tree.ListExpandedPaths());
    }

    /// <summary>Revives each tab whose assembly was closed and is listed again (<see cref="SavedDocuments.ReviveClosedTabs"/>),
    /// its view in <paramref name="documents"/>, and starts what it loads.</summary>
    public void ReviveClosedTabsListedAgain(DocumentViews documents)
    {
        // Asked first, so the frames with no closed tab listed again allocate nothing.
        if (!SavedDocuments.HasClosedTabListedAgain(_appState.ListSession.Navigation, _appState.ListSession.Assemblies))
        {
            return;
        }

        var revivedLoads = SavedDocuments.ReviveClosedTabs(_appState.ListSession, documents);
        Commands.StartLoads(_appState, revivedLoads);
    }

    // As a reload, so that the list is saved whole until they are all open (files that are gone or fail to load stay
    // listed as failed). Returns the loads that started in the tabs reopened from the list's saved documents.
    private Task<TabLoads> OpenStartupAssembliesAsync(ListReopening reopening, CancellationToken ct)
    {
        return _appState.OpenAssembliesAsReloadHandler.HandleAsync(_appState.ListSession, reopening.Paths, reopening.Reload, reopening.Work, ct);
    }
}
