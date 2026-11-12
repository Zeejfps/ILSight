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
        uiState.Search.Kinds = saved.SearchKinds;
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
            uiState.NuGetPicker.Browser.SearchChanges,
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
            uiState.NuGetPicker.Browser.KeptSearch,
            uiState.NuGetPicker.Window.Bounds,
            uiState.ProcessPicker.Window.Bounds,
            uiState.ProcessColumns.Weights.ToList(),
            uiState.ProcessModuleColumns.Weights.ToList(),
            uiState.Closer.Window.Bounds,
            DocumentPanels.SavePanels(uiState.Documents));
    }
}
