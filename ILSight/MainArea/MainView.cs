using System.Diagnostics;
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
using ILSight.DisplayOptionArea;
using ILSight.NuGetArea;
using ILSight.SearchArea;
using ILSight.AssemblyArea;
using static ILSight.DocumentPanels;

namespace ILSight.MainArea;

/// <summary>The whole window: menu bar, toolbar, tree | documents and search, status bar, and the
/// dialogs over them.</summary>
public static class MainView
{
    public static void Render(Ui<UiState> ui, AppState appState)
    {
        using var screen = ui.Screen();
        Shortcuts.Handle(ui, appState);
        OpenDropped(ui);
        using (ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f }))
        {
            MainMenuView.Render(ui, appState);
            ui.Separator();
            ToolbarView.Render(ui, appState);
            ui.Separator();
            Panels(ui, appState);
            ui.Separator();
            StatusBarView.Render(ui, appState);
        }
        FrameworkPickerView.Render(ui, appState.Navigation, appState.Assemblies);
        ComparisonPickerView.Render(ui, appState.Navigation, appState.Assemblies);
        NuGetPickerView.Render(ui);
        ProcessPickerView.Render(ui, appState.Navigation);
        CloseAssembliesDialogView.Render(ui, appState.Assemblies);
        AssemblyListsDialogView.Render(ui, appState.Lists, appState.ListSession, appState.View);
        AssemblyListDeleteDialogView.Render(ui, appState.Lists, appState.ListSession, appState.View);
        BookmarkImportDialogView.Render(ui, appState.Imports);
        LayoutResetDialogView.Render(ui);
        DisplayOptionsResetDialogView.Render(ui);
        ExportDialogView.Render(ui, appState.DecompilerOptions);
        ErrorDialogView.Render(ui, appState.Errors);
        AboutDialogView.Render(ui, appState);
    }

    /// <summary>Opens the files dropped on the window.</summary>
    private static void OpenDropped(Ui<UiState> ui)
    {
        var dropped = ui.Input.TakeDroppedFiles();
        if (dropped.Count == 0)
        {
            return;
        }

        Commands.Start(ui.State, nameof(AssemblyOpening.OpenDroppedFilesAsync), dropped, static (a, paths, ct) => a.AssemblyOpening.OpenDroppedFilesAsync(a, paths, ct));
    }

    private static void Panels(Ui<UiState> ui, AppState appState)
    {
        var t = ui.State.Strings;
        UpdateSearches(ui, appState);
        if (IsLayoutEmpty(ui.State))
        {
            DocumentsView.Hint(ui, t.ViewEmptyHint);
            return;
        }
        DockSpace(ui, appState);
        SyncAfterDocuments(ui, appState);
        RemoveReleasedImages(ui);
    }

    /// <summary>Runs the view commands the features queued while the documents were drawn, then asks for
    /// the tab the user last pressed, docked or brought to front to become the active one.</summary>
    private static void SyncAfterDocuments(Ui<UiState> ui, AppState appState)
    {
        QueuedCommands.RunViewCommands(ui.State, appState);
        SyncActiveTab(ui.State, appState.Navigation, appState.Decompiler);
    }

    private static void UpdateSearches(Ui<UiState> ui, AppState appState)
    {
        var shown = ui.State.Layout.Contains(DockPanels.Search);
        UpdateSearch(ui, ui.State.Search, appState.Assemblies, shown);
        UpdateSearch(ui, ui.State.Omnibar.Search, appState.Assemblies, ui.State.Omnibar.IsSearching);
    }

    private static void UpdateSearch(Ui<UiState> ui, Search search, AssemblyList assemblies, bool shown)
    {
        var wait = Searches.Update(search, assemblies, ui.RealTime, shown);
        if (wait > 0)
        {
            ui.RequestUpdateIn(wait);
        }
    }

    private static bool IsLayoutEmpty(UiState screen)
    {
        return !screen.Layout.HasPanels();
    }

    private static void DockSpace(Ui<UiState> ui, AppState appState)
    {
        var t = ui.State.Strings;
        DockTabMenu? tabMenu;
        string? doubleClicked;
        using (var dock = ui.DockSpace("dock", ui.State.Layout, (ui.State, appState, t), static (s, panel) => Tab(s.State, s.appState, s.t, panel),
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
        TabMenuView.Render(ui, appState.Navigation);
        TabMenuView.KeepOpenOnDoubleClick(ui, doubleClicked);
    }

    private static DockTab Tab(UiState screen, AppState appState, Strings t, string panel)
    {
        var view = DockPanels.ViewOf(panel);
        if (view is not null)
        {
            return DockPanels.Tab(t, screen.Icons, view.Value);
        }

        var tab = DocumentPanels.FindTab(screen.Documents, panel);
        if (tab is not null)
        {
            return DocumentsView.Tab(screen, appState.Navigation, tab);
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
                TreePaneView.Render(ui, appState.Navigation, appState.Assemblies, appState.Decompiler);
                break;
            case DockView.Search:
                SearchPanelView.Render(ui, appState.Navigation, appState.Decompiler);
                break;
            case DockView.Bookmarks:
                BookmarksPanelView.Render(ui, appState.Bookmarks, appState.Assemblies);
                break;
            case DockView.Analyzer:
                AnalyzerPanelView.Render(ui, appState.Navigation, appState.Analyzer);
                break;
            case DockView.DecompilerOptions:
                DecompilerOptionsPanelView.Render(ui, appState.DecompilerOptions);
                break;
            case DockView.DisplayOptions:
                DisplayOptionsPanelView.Render(ui, appState);
                break;
            case DockView.Exports:
                ExportsPanelView.Render(ui, appState.Export, appState.FinishedExports);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void Document(Ui<UiState> ui, AppState appState, string panel)
    {
        var tab = DocumentPanels.FindTab(ui.State.Documents, panel);
        if (tab is null)
        {
            return;
        }

        DocumentsView.Render(ui, appState.Navigation, appState.Session, appState.Assemblies, appState.Decompiler,
            appState.Bookmarks, appState.Tooltips, tab);
    }

    private static void RemoveReleasedImages(Ui<UiState> ui)
    {
        var released = DocumentPanels.TakeReleasedImages(ui.State.Documents);
        for (var i = 0; i < released.Count; i++)
        {
            ui.RemoveImage(released[i]);
        }
    }
}
