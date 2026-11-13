using ILSight.Core;
using static ILSight.SavedDocuments;

namespace ILSight;

/// <summary>What the open assembly list holds while it is open, which switching to another list changes together:
/// its assemblies, the analyses, the tabs and their history, the tabs reopened from its saved documents, its reload
/// and its opens. The lists and delete dialogs and the toolbar's list menu take it to switch lists in the frame of
/// their input, without the rest of the app's state. It keeps rules across the assemblies and the analyses (an
/// analysis is of an assembly on the list) and across the assemblies and the tabs (a tab whose assembly is closed
/// reads nothing), so it holds the list itself privately and closes assemblies by its own methods, which mark the
/// tabs of what they close: what it hands out (<see cref="Assemblies"/>) reads the list and opens onto it, but closes
/// nothing. The tabs (<see cref="Navigation"/>), the reopened tabs, the reload and the opens keep rules of their own,
/// and none of their members makes a part another list's, so it hands them out and their readers use them
/// directly.</summary>
public sealed class OpenListSession
{
    private readonly ClosableAssemblyList _assemblies;

    private readonly AnalyzerTree _analyzer;

    /// <summary>The files the reopening under way asked for, on their way until each lands; none when none runs.</summary>
    private IReadOnlyList<AssemblyPath> _reopening = [];

    /// <param name="assemblies">The list itself, which only the session closes, and whose read side the analyzer it
    /// makes checks each add against.</param>
    public OpenListSession(ClosableAssemblyList assemblies)
    {
        _assemblies = assemblies;
        _analyzer = new AnalyzerTree(assemblies.List);
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

    /// <summary>Uses what the open list of <paramref name="lists"/> picked for its assemblies and begins reopening them
    /// as a reload, the app saying it is reloading, its files on their way (<see cref="AssemblyList.Arriving"/>) and
    /// those of the reopening it stops no longer, with the analyses of them. Returns the files to open and the reload,
    /// for the edge to open (<see cref="AssemblyArea.OpenAssembliesAsReloadHandler"/>, which ends it through
    /// <see cref="TryFinishReopening"/>).</summary>
    public ListReopening BeginReopening(SavedAssemblyLists lists)
    {
        Assemblies.Pdbs.Use(lists.ActivePdbs);
        Assemblies.TargetFrameworks.Use(lists.ActiveFrameworks);
        var paths = lists.ActivePaths.ToList();
        StopReopeningArrivals();
        var reload = Reload.Start();
        _assemblies.BeginArriving(paths);
        _reopening = paths;
        return new ListReopening { Paths = paths, Reload = reload };
    }

    /// <summary>Ends the reopening <paramref name="ticket"/> was given for (<see cref="BeginReopening"/>): what it
    /// asked for that never landed stops arriving, and the analyses of it go. False when another reload started or it
    /// was stopped since, and its result should be ignored: what stopped it took its files off those on their
    /// way.</summary>
    public bool TryFinishReopening(ReloadTicket ticket)
    {
        if (!Reload.TryFinish(ticket))
        {
            return false;
        }

        StopReopeningArrivals();
        return true;
    }

    /// <summary>Takes what the reopening asked for that hasn't landed off those on their way, and the analyses of
    /// it with it, as closing it would.</summary>
    private void StopReopeningArrivals()
    {
        var stopped = _assemblies.StopArriving(_reopening);
        _reopening = [];
        foreach (var path in stopped)
        {
            _analyzer.RemoveAnalysesOf(path);
        }
    }

    /// <summary>Takes <paramref name="items"/> off the list, and the analyses of them with them; one no longer on it is
    /// passed over. The tabs showing what was read from them keep what they show, saying it was closed, and read it no
    /// more (<see cref="ShowTabsClosed"/>). Returns the modules taken off, for the edge to have the I/O side release
    /// them: the close completes here, and promises nothing about the files.</summary>
    public RemovedModules CloseAssemblies(IReadOnlyList<LoadedAssembly> items)
    {
        var removed = RemovedModules.None;
        foreach (var item in items)
        {
            removed = removed.With(_assemblies.Close(item));
            _analyzer.RemoveAnalysesOf(item.ListedItem);
            ShowTabsClosed(item.ListedItem);
        }
        return removed;
    }

    /// <summary>Has each open tab showing a node read from <paramref name="item"/>, an item of the list the user
    /// closed, or out of it, keep what it shows and read it no more (<see cref="TabLoad.ShowClosed"/>), as the rule
    /// across the list and the tabs asks: a tab whose assembly is closed reads nothing; so does each tab showing a
    /// diagram or a comparison that reads it (<see cref="Navigation.ShowDocumentsClosed"/>). Every close covers it; two
    /// changes take an item off the list and are left out on purpose: reading the list again
    /// (<see cref="ClearAssemblies"/>, whose tabs wait to find their places in what it reads), and reading one item
    /// again (<see cref="TryReplaceAssembly"/>, whose tabs are found again in what replaced it).</summary>
    private void ShowTabsClosed(LoadedAssembly item)
    {
        var name = item.FilePath.FileName;
        foreach (var tab in Navigation.OpenTabs)
        {
            var node = tab.Node;
            if (node is null)
            {
                continue;
            }

            if (node.Assembly.ListedItem != item)
            {
                continue;
            }

            tab.Load.ShowClosed(name);
        }

        Navigation.ShowDocumentsClosed(item.FilePath);
    }

    /// <summary>Lists <paramref name="fresh"/>, the listed <paramref name="item"/> read again, in its place, reading
    /// nothing; false when the item is no longer listed. <paramref name="removed"/> is what no longer lists after, for
    /// the edge to have the I/O side release: the item replaced, or the fresh one, not listed.</summary>
    public bool TryReplaceAssembly(LoadedAssembly item, FreshItem fresh, out RemovedModules removed)
    {
        return _assemblies.TryReplace(item, fresh, out removed);
    }

    /// <summary>Takes the files that failed to load off the list. Returns the modules taken off (none: a file that failed
    /// to load holds none), as every close does.</summary>
    public RemovedModules CloseFailedAssemblies()
    {
        var failed = Assemblies.Listed
            .OfType<FailedAssembly>()
            .ToList<LoadedAssembly>();
        return CloseAssemblies(failed);
    }

    /// <summary>Takes every assembly off the list, and every analysis of them, as reading them all again does; a
    /// restored analysis that failed, holding nothing of them, stays as saved, to be looked for again once they are read
    /// (<see cref="Analysis.BeginRetrying"/>). Returns the modules taken off, for the edge to have the I/O side release
    /// them.</summary>
    public RemovedModules ClearAssemblies()
    {
        var removed = TakeAllOff();
        _analyzer.ClearAnalyses();
        return removed;
    }

    private RemovedModules TakeAllOff()
    {
        var removed = _assemblies.CloseAll();
        _reopening = [];
        return removed;
    }

    /// <summary>Closes every assembly of the open list and the analyses of them, stopping its reload and its opens;
    /// tabs still waiting for it to reopen their places show what is left of them, and those showing what was read
    /// from it keep what they show, saying it was closed, and read it no more. Returns the loads that started in those
    /// tabs and the modules taken off, for the edge that asked for this to start and to have released.</summary>
    public ListClosing CloseAllAssemblies()
    {
        Navigation.BeginRequest();
        Opens.Cancel();
        Reload.Stop();
        foreach (var item in Assemblies.Listed)
        {
            ShowTabsClosed(item);
        }

        var removed = TakeAllOff();
        _analyzer.ClearRoots();
        Assemblies.Pdbs.ForgetAll();
        Assemblies.TargetFrameworks.ForgetAll();
        return new ListClosing { Loads = ShowReopenedDocuments(Navigation, Reopened, Assemblies), Removed = removed };
    }
}
