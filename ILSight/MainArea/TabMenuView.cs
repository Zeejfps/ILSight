using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.CommandArea;

namespace ILSight.MainArea;

/// <summary>The context menu of a document's tab: keeping the preview tab open, and closing the tab, the
/// other documents of its group, all of them, or those to its left or right.</summary>
internal static class TabMenuView
{
    private const string MenuId = "tab-menu";

    /// <summary>Opens the menu on the tab right-clicked, when it is a document's.</summary>
    public static void OpenOnRightClick(Ui<UiState> ui, DockTabMenu? request)
    {
        if (request is null)
        {
            return;
        }

        var tab = ui.State.Documents.FindTab(request.Value.Panel);
        if (tab is null)
        {
            return;
        }

        ui.State.TabMenuTarget = tab;
        ui.OpenContextMenu(MenuId, request.Value.At);
    }

    /// <summary>Keeps the tab double-clicked open, when it is a document's.</summary>
    public static void KeepOpenOnDoubleClick(Ui<UiState> ui, string? panel)
    {
        if (panel is null)
        {
            return;
        }

        var tab = ui.State.Documents.FindTab(panel);
        if (tab is null)
        {
            return;
        }

        Commands.Later(ui.State, tab, static (a, kept) => a.Navigation.KeepTabOpen(kept));
    }

    public static void Render(Ui<UiState> ui, Navigation navigation)
    {
        using var menu = ui.ContextMenu(MenuId);
        if (!menu.IsOpen)
        {
            return;
        }

        var tab = ui.State.TabMenuTarget;
        if (tab is null)
        {
            return;
        }

        if (!DocumentPanels.HasView(ui.State.Documents, tab))
        {
            return;
        }

        TabMenu(ui, navigation, tab);
    }

    private static void TabMenu(Ui<UiState> ui, Navigation navigation, DocumentTab tab)
    {
        var t = ui.State.Strings;
        var screen = ui.State;
        var group = DocumentPanels.ListTabsInGroup(screen, tab);
        var index = group.IndexOf(tab);
        KeepOpenItem(ui, navigation, tab);
        if (ui.MenuItem(t.TabClose, Shortcuts.CloseTab, id: "close"))
        {
            DocumentPanels.CloseTab(screen, tab);
        }

        if (ui.MenuItem(t.TabCloseOthers, enabled: group.Count > 1, id: "close-others"))
        {
            DocumentPanels.CloseOtherTabs(screen, tab);
        }

        if (ui.MenuItem(t.TabCloseAll, id: "close-all"))
        {
            DocumentPanels.CloseTabsInGroup(screen, tab);
        }

        ui.MenuSeparator();
        if (ui.MenuItem(t.TabCloseLeft, enabled: index > 0, id: "close-left"))
        {
            DocumentPanels.CloseTabsLeftOf(screen, tab);
        }

        if (ui.MenuItem(t.TabCloseRight, enabled: HasTabsRightOf(group, index), id: "close-right"))
        {
            DocumentPanels.CloseTabsRightOf(screen, tab);
        }
    }

    private static bool HasTabsRightOf(List<DocumentTab> group, int index)
    {
        return index < group.Count - 1;
    }

    private static void KeepOpenItem(Ui<UiState> ui, Navigation navigation, DocumentTab tab)
    {
        if (tab != navigation.PreviewTab)
        {
            return;
        }

        if (ui.MenuItem(ui.State.Strings.TabKeepOpen, id: "keep-open"))
        {
            Commands.Later(ui.State, tab, static (a, kept) => a.Navigation.KeepTabOpen(kept));
        }

        ui.MenuSeparator();
    }
}
