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
        Shortcuts.Handle(ui, appState);
        OpenDropped(ui, ui.State.AppCommands);
        using (ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f }))
        {
            MainMenuView.Render(ui, appState);
            ui.Separator();
            ToolbarView.Render(ui, appState.Lists, appState.ListSession, appState.KeptScreen.View, appState.FinishedExports, appState.Decompiler);
            ui.Separator();
            Panels(ui, appState);
            ui.Separator();
            StatusBarView.Render(ui, appState);
        }
        FrameworkPickerView.Render(ui, appState.ListSession.Navigation, appState.ListSession.Assemblies);
        ComparisonPickerView.Render(ui, ui.State.ComparisonPicker, ui.State.Appearance, ui.State.AppCommands, appState.ListSession.Navigation, appState.ListSession.Assemblies);
        NuGetPickerView.Render(ui);
        ProcessPickerView.Render(ui, appState.ListSession.Navigation);
        CloseAssembliesDialogView.Render(ui, ui.State.Closer, ui.State.Appearance, ui.State.AppCommands, appState.ListSession.Assemblies);
        AssemblyListsDialogView.Render(ui, appState.Lists, appState.ListSession, appState.KeptScreen.View);
        AssemblyListDeleteDialogView.Render(ui, ui.State.Appearance, ui.State.ListsDialog, ui.State.AppCommands, appState.Lists, appState.ListSession, appState.KeptScreen.View);
        BookmarkImportDialogView.Render(ui, ui.State.Appearance, ui.State.AppCommands, appState.Imports);
        LayoutResetDialogView.Render(ui);
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
        UpdateSearches(ui, ui.State.MainLayout, ui.State.Search, ui.State.Omnibar, appState.ListSession.Assemblies);
        if (IsLayoutEmpty(ui.State.MainLayout.Dock))
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
        DocumentPanels.SyncActiveTab(ui.State, appState.ListSession.Navigation, appState.Decompiler.Setup);
    }

    private static void UpdateSearches(Ui ui, MainLayout mainLayout, Search search, Omnibar omnibar, AssemblyList assemblies)
    {
        var shown = mainLayout.Dock.Contains(DockPanels.Search);
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

    private static bool IsLayoutEmpty(DockLayout layout)
    {
        return !layout.HasPanels();
    }

    private static void DockSpace(Ui<UiState> ui, AppState appState)
    {
        var t = ui.State.Appearance.Strings;
        DockTabMenu? tabMenu;
        string? doubleClicked;
        using (var dock = ui.DockSpace("dock", ui.State.MainLayout.Dock, (ui.State, appState.ListSession.Navigation, t), static (s, panel) => Tab(s.State, s.Navigation, s.t, panel),
                   tabListTooltip: t.DockAllTabs))
        {
            while (dock.Next(out var panel))
            {
                Panel(ui, appState, panel);
            }

            tabMenu = dock.TabMenu;
            doubleClicked = dock.TabDoubleClicked;
        }
        TabMenuView.OpenOnRightClick(ui, tabMenu);
        TabMenuView.Render(ui, appState.ListSession.Navigation);
        TabMenuView.KeepOpenOnDoubleClick(ui, ui.State.Documents, ui.State.AppCommands, doubleClicked);
    }

    private static DockTab Tab(UiState screen, Navigation navigation, Strings t, string panel)
    {
        var view = DockPanels.ViewOf(panel);
        if (view is not null)
        {
            return DockPanels.Tab(t, screen.Appearance.Icons, view.Value);
        }

        var tab = screen.Documents.FindTab(panel);
        if (tab is not null)
        {
            return DocumentsView.Tab(screen, navigation, tab);
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
                TreePaneView.Render(ui, appState.ListSession.Navigation, appState.ListSession.Assemblies, appState.Decompiler.Setup);
                break;
            case DockView.Search:
                SearchPanelView.Render(ui, ui.State.Appearance, ui.State.Search, ui.State.AppCommands, ui.State.SearchColumns, appState.ListSession.Navigation, appState.Decompiler.Setup);
                break;
            case DockView.Bookmarks:
                BookmarksPanelView.Render(ui, ui.State.Appearance, ui.State.BookmarksPanel, ui.State.AppCommands, ui.State.BookmarkColumns, appState.Bookmarks, appState.ListSession.Assemblies);
                break;
            case DockView.Analyzer:
                AnalyzerPanelView.Render(ui, appState.ListSession.Navigation, appState.ListSession.Analyzer);
                break;
            case DockView.DecompilerOptions:
                DecompilerOptionsPanelView.Render(ui, appState.Decompiler.Setup.Options);
                break;
            case DockView.DisplayOptions:
                DisplayOptionsPanelView.Render(ui, appState);
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

        DocumentsView.Render(ui, appState.ListSession.Navigation, appState.Session, appState.ListSession.Assemblies, appState.Decompiler.Setup,
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
