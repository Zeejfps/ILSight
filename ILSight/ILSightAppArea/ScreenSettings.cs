using ZGF.Gui.Immediate;
using ILSight.ExportArea;
using ILSight.MetadataGridArea;
using ILSight.MetadataArea;

namespace ILSight.ILSightAppArea;

/// <summary>What the settings keep of the screen: taking it when it changed, to be saved with the app state's settings.</summary>
public static class ScreenSettings
{
    /// <summary>Restores into <paramref name="uiState"/> what the settings kept of the screen besides its layout,
    /// display options, folded groups and filters, which it was created with: the search kinds, the columns, the
    /// NuGet search, the dialogs' windows and the panels the saved documents were docked as; then takes it all as
    /// unchanged since.</summary>
    /// <param name="saved">What the settings kept of the screen when they were last saved.</param>
    public static void RestoreScreen(UiState uiState, ViewSettings saved)
    {
        uiState.Search.LookFor(saved.SearchKinds);
        uiState.BookmarkColumns.Restore(saved.BookmarkColumns);
        uiState.SearchColumns.Restore(saved.SearchColumns);
        uiState.ExportColumns.Restore(saved.ExportColumns);
        uiState.ProcessColumns.Restore(saved.ProcessColumns);
        uiState.ProcessModuleColumns.Restore(saved.ProcessModuleColumns);
        uiState.NuGetPicker.RestoreSearch(saved.NuGet);
        DialogWindows.Restore(uiState.NuGetPicker.Window, saved.NuGetWindow);
        DialogWindows.Restore(uiState.ExportSetup.Window, saved.ExportWindow);
        DialogWindows.Restore(uiState.ProcessPicker.Window, saved.ProcessWindow);
        DialogWindows.Restore(uiState.Closer.Window, saved.CloseWindow);
        DocumentPanels.SkipPanels(uiState.Documents, Docking.ListDocumentPanels(uiState.MainLayout));
        uiState.KeptView = CaptureViewSources(uiState);
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
        return new ViewSources
        {
            BookmarkColumns = uiState.BookmarkColumns.Changes,
            SearchColumns = uiState.SearchColumns.Changes,
            ExportColumns = uiState.ExportColumns.Changes,
            FoldedOptionGroups = uiState.FoldedOptionGroups.Revision,
            SearchKinds = uiState.Search.Kinds,
            FlagsFilters = uiState.MetadataFilters.Revision,
            ExportWindow = uiState.ExportSetup.Window.Changes,
            Layout = uiState.Layout.Changes,
            Display = uiState.Display,
            NuGetSearch = uiState.NuGetPicker.Browser.SearchChanges,
            NuGetWindow = uiState.NuGetPicker.Window.Changes,
            ProcessWindow = uiState.ProcessPicker.Window.Changes,
            ProcessColumns = uiState.ProcessColumns.Changes,
            ProcessModuleColumns = uiState.ProcessModuleColumns.Changes,
            CloseWindow = uiState.Closer.Window.Changes,
            Documents = uiState.Documents.Revision,
        };
    }

    private static ViewSettings CreateViewSettings(UiState uiState)
    {
        return new ViewSettings
        {
            BookmarkColumns = uiState.BookmarkColumns.Weights.ToList(),
            SearchColumns = uiState.SearchColumns.Weights.ToList(),
            ExportColumns = uiState.ExportColumns.Weights.ToList(),
            FoldedOptionGroups = OptionFolds.ListInOrder(uiState.FoldedOptionGroups),
            SearchKinds = uiState.Search.Kinds,
            FlagsFilters = MetadataGridWidget.ListSaved(uiState.MetadataFilters),
            ExportWindow = uiState.ExportSetup.Window.Bounds,
            Layout = uiState.Layout.CopyArrangement(),
            Display = uiState.Display,
            NuGet = uiState.NuGetPicker.Browser.KeptSearch,
            NuGetWindow = uiState.NuGetPicker.Window.Bounds,
            ProcessWindow = uiState.ProcessPicker.Window.Bounds,
            ProcessColumns = uiState.ProcessColumns.Weights.ToList(),
            ProcessModuleColumns = uiState.ProcessModuleColumns.Weights.ToList(),
            CloseWindow = uiState.Closer.Window.Bounds,
            Tabs = DocumentPanels.SavePanels(uiState.Documents),
        };
    }
}
