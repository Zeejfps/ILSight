using System.Diagnostics;
using ILSight.OmnibarArea;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.ExportArea;
using ILSight.BookmarkArea;
using ILSight.ComparisonPickerArea;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;
using ILSight.ProcessArea;
using ILSight.FrameworkArea;
using ILSight.AssemblyListArea;
using ILSight.NuGetArea;
using ILSight.SearchArea;
using ILSight.AssemblyArea;

namespace ILSight.MainArea;

/// <summary>The whole window: menu bar, toolbar, tree | documents and search, status bar, and the
/// dialogs over them.</summary>
public static class MainView
{
    public static void Render(Ui<UiState> ui, AppState appState)
    {
        using var screen = ui.Screen();
        Shortcuts.Handle(ui, ui.State.Appearance, ui.State.AppCommands, ui.State.Panels, ui.State.Display, ui.State.Omnibar, ui.State.Tree, ui.State.ExportSetup, appState);
        OpenDropped(ui, ui.State.AppCommands);
        using (ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f }))
        {
            MainMenuView.Render(ui, ui.State.Appearance, ui.State.AppCommands, ui.State.Panels, ui.State.LayoutReset, ui.State.About, ui.State.Tree, ui.State.Display, ui.State.ErrorDialog, ui.State.Picker, ui.State.NuGetPicker, ui.State.ReadNuGetSetupHandler, ui.State.ProcessPicker, ui.State.ExportSetup, appState);
            ui.Separator();
            ToolbarView.Render(ui, ui.State.Appearance, ui.State.AppCommands, ui.State.Panels, ui.State.ListsDialog, appState.Lists, appState.ListSession, appState.KeptScreen.View, appState.FinishedExports, appState.Decompiler);
            ui.Separator();
            Panels(ui, appState);
            ui.Separator();
            StatusBarView.Render(ui, ui.State.Appearance, ui.State.AppCommands, ui.State.Panels, ui.State.Tree, ui.State.ErrorDialog, ui.State.RefusalDetails, appState);
        }
        FrameworkPickerView.Render(ui, ui.State.Appearance, ui.State.AppCommands, ui.State.Picker, appState.ListSession.Navigation, appState.ListSession.Assemblies);
        ComparisonPickerView.Render(ui, ui.State.ComparisonPicker, ui.State.Appearance, ui.State.AppCommands, appState.ListSession.Navigation, appState.ListSession.Assemblies);
        NuGetPickerView.Render(ui, ui.State.Appearance, ui.State.AppCommands, ui.State.NuGetPicker, ui.State.DownloadNuGetPackageHandler);
        ProcessPickerView.Render(ui, ui.State.Appearance, ui.State.AppCommands, ui.State.ProcessColumns, ui.State.ProcessModuleColumns, ui.State.ProcessPicker, appState.ListSession.Navigation);
        CloseAssembliesDialogView.Render(ui, ui.State.Closer, ui.State.Appearance, ui.State.AppCommands, appState.ListSession.Assemblies);
        AssemblyListsDialogView.Render(ui, ui.State.Appearance, ui.State.AppCommands, ui.State.ListsDialog, ui.State.Presets, appState.Lists, appState.ListSession, appState.KeptScreen.View);
        AssemblyListDeleteDialogView.Render(ui, ui.State.Appearance, ui.State.ListsDialog, ui.State.AppCommands, appState.Lists, appState.ListSession, appState.KeptScreen.View);
        BookmarkImportDialogView.Render(ui, ui.State.Appearance, ui.State.AppCommands, appState.Imports);
        LayoutResetDialogView.Render(ui, ui.State.Appearance, ui.State.Panels, ui.State.LayoutReset);
        DisplayOptionsResetDialogView.Render(ui, ui.State.Appearance, ui.State.Display);
        ExportDialogView.Render(ui, ui.State.ExportSetup, ui.State.Appearance, ui.State.ExportColumns, ui.State.AppCommands, appState.Decompiler.Setup.Options);
        ErrorDialogView.Render(ui, ui.State.Appearance, ui.State.ErrorDialog, ui.State.Display, ui.State.AppCommands, appState.Errors);
        AboutDialogView.Render(ui, ui.State.About, ui.State.Appearance, ui.State.AppCommands, ui.State.ShippedLicenses, appState);
    }

    private static void OpenDropped(Ui ui, IAppCommandWriter commands)
    {
        var dropped = ui.Input.TakeDroppedFiles();
        if (dropped.Count == 0)
        {
            return;
        }

        Commands.OpenDroppedLater(commands, dropped);
    }

    private static void Panels(Ui<UiState> ui, AppState appState)
    {
        var t = ui.State.Appearance.Strings;
        UpdateSearches(ui, ui.State.Panels, ui.State.Search, ui.State.Omnibar, appState.ListSession.Assemblies);
        if (!ui.State.Panels.HasPanels)
        {
            DocumentsView.Hint(ui, t.ViewEmptyHint);
            return;
        }
        DockSpace(ui, appState);
        SyncAfterDocuments(ui, appState);
        RemoveReleasedImages(ui, ui.State.Documents);
    }

    private static void SyncAfterDocuments(Ui<UiState> ui, AppState appState)
    {
        QueuedCommands.RunViewCommands(ui.State, appState);
        ui.State.Panels.SyncActiveTab(appState.ListSession.Navigation, appState.Decompiler.Setup);
    }

    private static void UpdateSearches(Ui ui, Panels panels, Search search, Omnibar omnibar, AssemblyList assemblies)
    {
        var shown = panels.IsOpen(DockPanels.Search);
        UpdateSearch(ui, search, assemblies, shown);
        UpdateSearch(ui, omnibar.Search, assemblies, omnibar.IsSearching);
    }

    private static void UpdateSearch(Ui ui, Search search, AssemblyList assemblies, bool shown)
    {
        var wait = search.Update(assemblies, TimeSpan.FromSeconds(ui.RealTime), shown);
        if (wait > TimeSpan.Zero)
        {
            ui.RequestUpdateIn(wait.TotalSeconds);
        }
    }

    private static void DockSpace(Ui<UiState> ui, AppState appState)
    {
        var t = ui.State.Appearance.Strings;
        DockTabMenu? tabMenu;
        string? doubleClicked;
        using (var dock = ui.State.Panels.DrawDockSpace(ui, "dock", (ui.State.Appearance, ui.State.Documents, ui.State.Tree, appState.ListSession.Navigation),
                   static (s, panel) => Tab(s.Appearance, s.Documents, s.Tree, s.Navigation, panel),
                   tabListTooltip: t.DockAllTabs))
        {
            while (dock.Next(out var panel))
            {
                Panel(ui, appState, panel);
            }

            tabMenu = dock.TabMenu;
            doubleClicked = dock.TabDoubleClicked;
        }
        TabMenuView.OpenOnRightClick(ui, ui.State.Documents, ui.State.TabMenu, tabMenu);
        TabMenuView.Render(ui, ui.State.Appearance, ui.State.AppCommands, ui.State.Panels, ui.State.Documents, ui.State.TabMenu, appState.ListSession.Navigation);
        TabMenuView.KeepOpenOnDoubleClick(ui, ui.State.Documents, ui.State.AppCommands, doubleClicked);
    }

    private static DockTab Tab(Appearance appearance, DocumentViews documents, BrowseTree tree, Navigation navigation, string panel)
    {
        var t = appearance.Strings;
        var view = DockPanels.ViewOf(panel);
        if (view is not null)
        {
            return DockPanels.Tab(t, appearance.Icons, view.Value);
        }

        var tab = documents.FindTab(panel);
        if (tab is not null)
        {
            return DocumentsView.Tab(appearance, tree, navigation, tab);
        }

        return new DockTab(t.DocumentNewTab);
    }

    private static void Panel(Ui<UiState> ui, AppState appState, string panel)
    {
        var view = DockPanels.ViewOf(panel);
        if (view is null)
        {
            Document(ui, appState, panel);
            return;
        }
        View(ui, appState, view.Value);
    }

    private static void View(Ui<UiState> ui, AppState appState, DockView view)
    {
        switch (view)
        {
            case DockView.Assemblies:
                TreePaneView.Render(ui, ui.State.Appearance, ui.State.AppCommands, ui.State.Panels, ui.State.Tree, ui.State.Analyzer, ui.State.Display, ui.State.Search, ui.State.ComparisonPicker, ui.State.Closer, ui.State.ExportSetup, appState.ListSession.Navigation, appState.ListSession.Assemblies, appState.Decompiler.Setup);
                break;
            case DockView.Search:
                SearchPanelView.Render(ui, ui.State.Appearance, ui.State.Search, ui.State.AppCommands, ui.State.SearchColumns, appState.ListSession.Navigation, appState.Decompiler.Setup);
                break;
            case DockView.Bookmarks:
                BookmarksPanelView.Render(ui, ui.State.Appearance, ui.State.BookmarksPanel, ui.State.AppCommands, ui.State.BookmarkColumns, appState.Bookmarks, appState.ListSession.Assemblies);
                break;
            case DockView.Analyzer:
                AnalyzerPanelView.Render(ui, ui.State.Appearance, ui.State.AppCommands, ui.State.Panels, ui.State.Analyzer, appState.ListSession.Navigation, appState.ListSession.Analyzer);
                break;
            case DockView.DecompilerOptions:
                DecompilerOptionsPanelView.Render(ui, ui.State.Appearance, ui.State.AppCommands, ui.State.DecompilerOptionsPanel, ui.State.FoldedOptionGroups, appState.Decompiler.Setup.Options);
                break;
            case DockView.DisplayOptions:
                DisplayOptionsPanelView.Render(ui, ui.State.Appearance, ui.State.AppCommands, ui.State.Shell, ui.State.DisplayOptionsPanel, ui.State.Display, ui.State.CodeFonts, appState);
                break;
            case DockView.Exports:
                ExportsPanelView.Render(ui, ui.State.Appearance, ui.State.AppCommands, ui.State.ExportsPanel, appState.Export, appState.FinishedExports);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void Document(Ui<UiState> ui, AppState appState, string panel)
    {
        var tab = ui.State.Documents.FindTab(panel);
        if (tab is null)
        {
            return;
        }

        DocumentsView.Render(ui, ui.State.Appearance, ui.State.AppCommands, ui.State.Documents, ui.State.Tree, ui.State.Panels, ui.State.Display, ui.State.BookmarksPanel, ui.State.Omnibar, ui.State.Grids, appState.ListSession.Navigation, appState.Session, appState.ListSession.Assemblies, appState.Decompiler.Setup,
            appState.Bookmarks, appState.Tooltips, tab);
    }

    private static void RemoveReleasedImages(Ui ui, DocumentViews documents)
    {
        var released = documents.TakeReleasedImages();
        for (var i = 0; i < released.Count; i++)
        {
            ui.RemoveImage(released[i]);
        }
    }
}
