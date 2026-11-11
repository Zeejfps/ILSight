using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.Core.Processes;

namespace ILSight;

/// <summary>What the settings keep of the screen: taking it when it changed, to be saved with the app state's settings.</summary>
public static class ScreenSettings
{
    /// <summary>The screen as the settings left it.</summary>
    /// <param name="saved">What the settings kept of the screen when they were last saved.</param>
    /// <param name="work">Where the work the screen starts in the background is counted.</param>
    /// <param name="reportError">Tells the user that something done in the background failed.</param>
    /// <param name="session">The browse tree the tree and searches show nodes of.</param>
    /// <param name="runtime">What this machine has installed.</param>
    public static UiState CreateScreen(ViewSettings saved, BackgroundWork work, Action<AppProblem> reportError, BrowseSession session, InstalledRuntime runtime)
    {
        var appCommands = new Queue<AppCommand>();
        var search = Searches.CreateSearch(work, reportError, session);
        search.Kinds = saved.SearchKinds;
        var uiState = new UiState
        {
            AppCommands = appCommands,
            Tree = new BrowseTree { Session = session, Work = work, ReportError = reportError },
            FoldedOptionGroups = new FoldedGroups { Folded = new HashSet<string>(saved.FoldedOptionGroups ?? []) },
            Search = search,
            Omnibar = new Omnibar { Search = Searches.CreateSearch(work, reportError, session) },
            MetadataFilters = MetadataGridWidget.CreateFilters(saved.FlagsFilters),
            Runtime = runtime,
            Picker = new FrameworkPicker { Work = work, ReportError = reportError, ListAssemblies = () => RuntimeAssemblies.ListAssemblies(runtime) },
            NuGetPicker = new NuGetPicker { Work = work, ReportError = reportError },
            ProcessPicker = new ProcessPicker
            {
                Work = work,
                ReportError = reportError,
                FindProcesses = RunningProcesses.FindProcessesAsync,
                FindModules = RunningProcesses.GetModulesAsync,
            },
            Display = saved.Display ?? DisplayOptions.Default,
            Analyzer = new AnalyzerRows { AppCommands = appCommands },
            MainLayout = new MainLayout { Dock = DockPanels.Restore(saved.Layout) },
        };
        uiState.BookmarkColumns.Restore(saved.BookmarkColumns);
        uiState.SearchColumns.Restore(saved.SearchColumns);
        uiState.ExportColumns.Restore(saved.ExportColumns);
        uiState.ProcessColumns.Restore(saved.ProcessColumns);
        uiState.ProcessModuleColumns.Restore(saved.ProcessModuleColumns);
        NuGetPicking.RestoreSearch(uiState.NuGetPicker, saved.NuGet);
        DialogWindows.Restore(uiState.NuGetPicker.Window, saved.NuGetWindow);
        DialogWindows.Restore(uiState.ExportSetup.Window, saved.ExportWindow);
        DialogWindows.Restore(uiState.ProcessPicker.Window, saved.ProcessWindow);
        DialogWindows.Restore(uiState.Closer.Window, saved.CloseWindow);
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
        {
            return null;
        }

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
            uiState.ProcessPicker.Window.Changes,
            uiState.ProcessColumns.Changes,
            uiState.ProcessModuleColumns.Changes,
            uiState.Closer.Window.Changes,
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
            NuGetPicking.FindKeptSearch(uiState.NuGetPicker),
            uiState.NuGetPicker.Window.Bounds,
            uiState.ProcessPicker.Window.Bounds,
            uiState.ProcessColumns.Weights.ToList(),
            uiState.ProcessModuleColumns.Weights.ToList(),
            uiState.Closer.Window.Bounds,
            DocumentPanels.SavePanels(uiState.Documents));
    }
}
