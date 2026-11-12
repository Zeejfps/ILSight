using ILSight.Core;
using static ILSight.SavedDocuments;

namespace ILSight;

/// <summary>What the open assembly list holds while it is open, which switching to another list changes together:
/// its assemblies, the analyses, the tabs and their history, the tabs reopened from its saved documents, its reload
/// and its opens. The lists and delete dialogs and the toolbar's list menu take it to switch lists in the frame of
/// their input, without the rest of the app's state. It keeps a rule across the assemblies and the analyses (an
/// analysis is of an assembly on the list), so it holds the list itself privately and closes assemblies by its own
/// methods: what it hands out (<see cref="Assemblies"/>) reads the list and opens onto it, but closes nothing. The
/// tabs (<see cref="Navigation"/>), the reopened tabs, the reload and the opens keep rules of their own, and none of
/// their members makes a part another list's, so it hands them out and their readers use them directly.</summary>
public sealed class OpenListSession
{
    private readonly ClosableAssemblyList _assemblies;

    private readonly AnalyzerTree _analyzer;

    /// <param name="assemblies">The list itself, which only the session closes.</param>
    /// <param name="analyzer">The symbols being analyzed and what the analyses found.</param>
    public OpenListSession(ClosableAssemblyList assemblies, AnalyzerTree analyzer)
    {
        _assemblies = assemblies;
        _analyzer = analyzer;
    }

    /// <summary>The open list's assemblies, to read and to open onto; closing them is the session's.</summary>
    public AssemblyList Assemblies => _assemblies.List;

    /// <summary>The symbols being analyzed and what the analyses found.</summary>
    public AnalyzerTree Analyzer => _analyzer;

    /// <summary>The open tabs, the history of what they showed and the newest thing asked for.</summary>
    public required Navigation Navigation { get; init; }

    /// <summary>The tabs reopened from the open list's saved documents that wait for what they showed.</summary>
    public required ReopenedTabs Reopened { get; init; }

    public required Reload Reload { get; init; }

    public required OpenRequests Opens { get; init; }

    /// <summary>Takes <paramref name="items"/> off the list, and the analyses of them with them; one no longer on it is
    /// passed over.</summary>
    public void CloseAssemblies(IReadOnlyList<LoadedAssembly> items)
    {
        foreach (var item in items)
        {
            _assemblies.Close(item);
            _analyzer.RemoveAnalysesOf(item.ListedItem);
        }
    }

    /// <summary>Takes the files that failed to load off the list.</summary>
    public void CloseFailedAssemblies()
    {
        var failed = Assemblies.Listed
            .OfType<FailedAssembly>()
            .ToList<LoadedAssembly>();
        CloseAssemblies(failed);
    }

    /// <summary>Takes every assembly off the list, and every analysis with them, as reading them all again
    /// does.</summary>
    public void ClearAssemblies()
    {
        _assemblies.CloseAll();
        _analyzer.ClearRoots();
    }

    /// <summary>Closes every assembly of the open list and the analyses of them, stopping its reload and its opens;
    /// tabs still waiting for it to reopen their places show what is left of them. Returns the loads that started in
    /// those tabs, for the edge that asked for this to start.</summary>
    public TabLoads CloseAllAssemblies()
    {
        Navigation.BeginRequest();
        Opens.Cancel();
        Reload.Stop();
        ClearAssemblies();
        Assemblies.Pdbs.ForgetAll();
        Assemblies.TargetFrameworks.ForgetAll();
        return ShowReopenedDocuments(Navigation, Reopened, Assemblies);
    }
}
