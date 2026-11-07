using ILSight.Core;

namespace ILSight;

/// <summary>What the settings keep of the screen: taking it when it changed, to be saved with the app state's settings.</summary>
public static class ScreenSettings
{
    /// <summary>The screen as the settings left it.</summary>
    /// <param name="saved">What the settings kept of the screen when they were last saved.</param>
    /// <param name="reportError">Tells the user that something done in the background failed.</param>
    /// <param name="session">The browse tree the tree and searches show nodes of.</param>
    /// <param name="runtime">What this machine has installed.</param>
    public static UiState CreateScreen(ViewSettings saved, Action<AppProblem> reportError, BrowseSession session, InstalledRuntime runtime)
    {
        var appCommands = new Queue<AppCommand>();
        var search = Searches.CreateSearch(reportError, session);
        search.Kinds = saved.SearchKinds;
        var uiState = new UiState
        {
            AppCommands = appCommands,
            Tree = new BrowseTree { Session = session, ReportError = reportError },
            FoldedOptionGroups = new FoldedGroups { Folded = new HashSet<string>(saved.FoldedOptionGroups ?? []) },
            Search = search,
            Omnibar = new Omnibar { Search = Searches.CreateSearch(reportError, session) },
            MetadataFilters = MetadataGridWidget.CreateFilters(saved.FlagsFilters),
            Runtime = runtime,
            Picker = new FrameworkPicker { ReportError = reportError, ListAssemblies = () => RuntimeAssemblies.ListAssemblies(runtime) },
            NuGetPicker = new NuGetPicker { ReportError = reportError },
            Display = saved.Display ?? DisplayOptions.Default,
            Analyzer = new AnalyzerRows { AppCommands = appCommands },
            MainLayout = new MainLayout { Dock = DockPanels.Restore(saved.Layout) },
        };
        uiState.BookmarkColumns.Restore(saved.BookmarkColumns);
        uiState.SearchColumns.Restore(saved.SearchColumns);
        uiState.ExportColumns.Restore(saved.ExportColumns);
        NuGetPicking.RestoreSearch(uiState.NuGetPicker, saved.NuGet);
        DialogWindows.Restore(uiState.NuGetPicker.Window, saved.NuGetWindow);
        DialogWindows.Restore(uiState.ExportSetup.Window, saved.ExportWindow);
        ScreenLayout.ConnectLayout(uiState);
        DocumentPanels.SkipPanels(uiState.Documents, Docking.ListDocumentPanels(uiState.MainLayout));
        uiState.KeptView = CaptureViewSources(uiState);
        return uiState;
    }

    /// <summary>What the settings keep of the screen, when that changed since it was last taken; else null.</summary>
    public static ViewSettings? TakeChangedViewSettings(UiState uiState)
    {
        var sources = CaptureViewSources(uiState);
        if (sources == uiState.KeptView)
            return null;
        return TakeViewSettings(uiState);
    }

    /// <summary>What the settings keep of the screen as it is now, changed or not, as before the last save.</summary>
    public static ViewSettings TakeViewSettings(UiState uiState)
    {
        uiState.KeptView = CaptureViewSources(uiState);
        return CreateViewSettings(uiState);
    }

    internal static ViewSources CaptureViewSources(UiState uiState)
    {
        return new ViewSources(
            uiState.BookmarkColumns.Changes,
            uiState.SearchColumns.Changes,
            uiState.ExportColumns.Changes,
            uiState.FoldedOptionGroups.Revision,
            uiState.Search.Kinds,
            uiState.MetadataFilters.Revision,
            uiState.ExportSetup.Window.Changes,
            uiState.Layout.Changes,
            uiState.Display,
            uiState.NuGetPicker.SearchChanges,
            uiState.NuGetPicker.Window.Changes,
            uiState.Documents.Revision);
    }

    private static ViewSettings CreateViewSettings(UiState uiState)
    {
        return new ViewSettings(
            uiState.BookmarkColumns.Weights.ToList(),
            uiState.SearchColumns.Weights.ToList(),
            uiState.ExportColumns.Weights.ToList(),
            OptionFolds.ListInOrder(uiState.FoldedOptionGroups),
            uiState.Search.Kinds,
            MetadataGridWidget.ListSaved(uiState.MetadataFilters),
            uiState.ExportSetup.Window.Bounds,
            uiState.Layout.CopyArrangement(),
            uiState.Display,
            NuGetPicking.GetKeptSearch(uiState.NuGetPicker),
            uiState.NuGetPicker.Window.Bounds,
            DocumentPanels.SavePanels(uiState.Documents));
    }
}
