using ILSight.CommandArea;

namespace ILSight.AssemblyArea;

/// <summary>Loads the open list's assemblies where no command asks for it: the list the app starts with, its saved
/// analyses found as they open.</summary>
public sealed class AssemblyListLoading
{
    private readonly AppState _appState;

    /// <param name="appState">The app state whose open list this loads.</param>
    internal AssemblyListLoading(AppState appState)
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

    // As a reload, so that the list is saved whole until they are all open (files that are gone or fail to load stay
    // listed as failed). Returns the loads that started in the tabs reopened from the list's saved documents.
    private Task<TabLoads> OpenStartupAssembliesAsync(ListReopening reopening, CancellationToken ct)
    {
        return _appState.OpenAssembliesAsReloadHandler.HandleAsync(_appState.ListSession, reopening.Paths, reopening.Reload, reopening.Work, ct);
    }
}
