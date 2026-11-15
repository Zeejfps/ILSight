using ZGF.Gui.Immediate;
using ILSight.DocumentViewArea;
using ZGF.Gui.Immediate.Widgets;
using ILSight.CommandArea;

namespace ILSight.MainArea;

/// <summary>The context menu of a document's tab: keeping the preview tab open, and closing the tab, the
/// other documents of its group, all of them, or those to its left or right.</summary>
internal static class TabMenuView
{
    private const string MenuId = "tab-menu";

    /// <summary>Opens the menu on the tab right-clicked, when it is a document's.</summary>
    public static void OpenOnRightClick(Ui ui, DocumentViews documents, TabMenu tabMenu, DockTabMenu? request)
    {
        if (request is null)
        {
            return;
        }

        var tab = documents.FindTab(request.Value.Panel);
        if (tab is null)
        {
            return;
        }

        tabMenu.TargetMenu(tab);
        ui.OpenContextMenu(MenuId, request.Value.At);
    }

    /// <summary>Keeps the tab double-clicked open, when it is a document's.</summary>
    public static void KeepOpenOnDoubleClick(Ui ui, DocumentViews documents, IAppCommandWriter commands, string? panel)
    {
        if (panel is null)
        {
            return;
        }

        var tab = documents.FindTab(panel);
        if (tab is null)
        {
            return;
        }

        Commands.Later(commands, tab, static (a, kept) => a.ListSession.Navigation.KeepTabOpen(kept));
    }

    public static void Render(Ui ui, Appearance appearance, IAppCommandWriter commands, Panels panels, DocumentViews documents, TabMenu tabMenu, Navigation navigation)
    {
        using var menu = ui.ContextMenu(MenuId);
        if (!menu.IsOpen)
        {
            return;
        }

        var tab = tabMenu.Target;
        if (tab is null)
        {
            return;
        }

        if (!documents.HasView(tab))
        {
            return;
        }

        TabMenu(ui, appearance, commands, panels, navigation, tab);
    }

    private static void TabMenu(Ui ui, Appearance appearance, IAppCommandWriter commands, Panels panels, Navigation navigation, DocumentTab tab)
    {
        var t = appearance.Strings;
        var group = panels.ListTabsInGroup(tab);
        var index = group.IndexOf(tab);
        KeepOpenItem(ui, appearance, commands, navigation, tab);
        if (ui.MenuItem(t.TabClose, Shortcuts.CloseTab, id: "close"))
        {
            panels.CloseTab(tab);
        }

        if (ui.MenuItem(t.TabCloseOthers, enabled: group.Count > 1, id: "close-others"))
        {
            panels.CloseOtherTabs(tab);
        }

        if (ui.MenuItem(t.TabCloseAll, id: "close-all"))
        {
            panels.CloseTabsInGroup(tab);
        }

        ui.MenuSeparator();
        if (ui.MenuItem(t.TabCloseLeft, enabled: index > 0, id: "close-left"))
        {
            panels.CloseTabsLeftOf(tab);
        }

        if (ui.MenuItem(t.TabCloseRight, enabled: HasTabsRightOf(group, index), id: "close-right"))
        {
            panels.CloseTabsRightOf(tab);
        }
    }

    private static bool HasTabsRightOf(List<DocumentTab> group, int index)
    {
        return index < group.Count - 1;
    }

    private static void KeepOpenItem(Ui ui, Appearance appearance, IAppCommandWriter commands, Navigation navigation, DocumentTab tab)
    {
        if (tab != navigation.PreviewTab)
        {
            return;
        }

        if (ui.MenuItem(appearance.Strings.TabKeepOpen, id: "keep-open"))
        {
            Commands.Later(commands, tab, static (a, kept) => a.ListSession.Navigation.KeepTabOpen(kept));
        }

        ui.MenuSeparator();
    }
}
