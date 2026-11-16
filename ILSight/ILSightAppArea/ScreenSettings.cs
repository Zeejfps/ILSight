using ILSight.SettingArea;
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
        uiState.BookmarksPanel.Columns.Restore(saved.BookmarkColumns);
        uiState.SearchPanel.Columns.Restore(saved.SearchColumns);
        uiState.ExportSetup.Columns.Restore(saved.ExportColumns);
        uiState.ProcessPicker.Columns.Restore(saved.ProcessColumns);
        uiState.ProcessPicker.ModuleColumns.Restore(saved.ProcessModuleColumns);
        uiState.NuGetPicker.RestoreSearch(saved.NuGet);
        uiState.NuGetPicker.Window.Restore(saved.NuGetWindow);
        uiState.ExportSetup.Window.Restore(saved.ExportWindow);
        uiState.ProcessPicker.Window.Restore(saved.ProcessWindow);
        uiState.Closer.Window.Restore(saved.CloseWindow);
        uiState.Documents.SkipPanels(uiState.Panels.ListDocumentPanels());
        uiState.KeptView.Keep(CaptureViewSources(uiState));
    }

    /// <summary>What the settings keep of the screen, when that changed since it was last taken; else null.</summary>
    public static ViewSettings? TakeChangedViewSettings(UiState uiState)
    {
        var sources = CaptureViewSources(uiState);
        if (!uiState.KeptView.HasChanged(sources))
        {
            return null;
        }

        return TakeViewSettings(uiState);
    }

    /// <summary>Hands <paramref name="kept"/> what the settings keep of the screen, when that changed since it was last
    /// taken.</summary>
    public static void KeepChangedViewSettings(UiState uiState, KeptScreen kept)
    {
        var view = TakeChangedViewSettings(uiState);
        if (view is null)
        {
            return;
        }

        kept.KeepView(view);
    }

    /// <summary>What the settings keep of the screen as it is now, changed or not, as before the last save.</summary>
    public static ViewSettings TakeViewSettings(UiState uiState)
    {
        uiState.KeptView.Keep(CaptureViewSources(uiState));
        return CreateViewSettings(uiState);
    }

    internal static ViewSources CaptureViewSources(UiState uiState)
    {
        return new ViewSources
        {
            BookmarkColumns = uiState.BookmarksPanel.Columns.Changes,
            SearchColumns = uiState.SearchPanel.Columns.Changes,
            ExportColumns = uiState.ExportSetup.Columns.Changes,
            FoldedOptionGroups = uiState.DecompilerOptionsPanel.Folded.Revision,
            SearchKinds = uiState.Search.Kinds,
            FlagsFilters = uiState.Grids.Filters.Revision,
            ExportWindow = uiState.ExportSetup.Window.Rest.Changes,
            Layout = uiState.Panels.LayoutChanges,
            Display = uiState.Display.Options,
            NuGetSearch = uiState.NuGetPicker.Browser.SearchChanges,
            NuGetWindow = uiState.NuGetPicker.Window.Rest.Changes,
            ProcessWindow = uiState.ProcessPicker.Window.Rest.Changes,
            ProcessColumns = uiState.ProcessPicker.Columns.Changes,
            ProcessModuleColumns = uiState.ProcessPicker.ModuleColumns.Changes,
            CloseWindow = uiState.Closer.Window.Rest.Changes,
            Documents = uiState.Documents.Revision,
        };
    }

    private static ViewSettings CreateViewSettings(UiState uiState)
    {
        return new ViewSettings
        {
            BookmarkColumns = uiState.BookmarksPanel.Columns.Weights.ToList(),
            SearchColumns = uiState.SearchPanel.Columns.Weights.ToList(),
            ExportColumns = uiState.ExportSetup.Columns.Weights.ToList(),
            FoldedOptionGroups = uiState.DecompilerOptionsPanel.Folded.ListInOrder(),
            SearchKinds = uiState.Search.Kinds,
            FlagsFilters = uiState.Grids.Filters.ListSaved(),
            ExportWindow = uiState.ExportSetup.Window.Bounds,
            Layout = uiState.Panels.CopyArrangement(),
            Display = uiState.Display.Options,
            NuGet = uiState.NuGetPicker.Browser.KeptSearch,
            NuGetWindow = uiState.NuGetPicker.Window.Bounds,
            ProcessWindow = uiState.ProcessPicker.Window.Bounds,
            ProcessColumns = uiState.ProcessPicker.Columns.Weights.ToList(),
            ProcessModuleColumns = uiState.ProcessPicker.ModuleColumns.Weights.ToList(),
            CloseWindow = uiState.Closer.Window.Bounds,
            Tabs = uiState.Documents.SavePanels(),
        };
    }
}
