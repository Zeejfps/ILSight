using ZGF.Gui.Immediate;
using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class TabMenuTests
{
    private static readonly string[] Types = ["Greeter", "Farewell", "Outline"];

    /// <summary>The fixture assembly's tab, then a tab for each of <see cref="Types"/> beside it, in one group.</summary>
    private static async Task<(AppDriver App, BrowseNode Assembly)> OpenTabsAsync()
    {
        var app = AppDriver.Opened(width: 1600f);
        app.Settle(2);
        var active = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(active);
        var assembly = active.Node;
        Assert.NotNull(assembly);
        foreach (var type in Types)
        {
            app.Show(await app.FixtureAsync(type), 2, inNewTab: true);
        }

        app.Settle(2);
        return (app, assembly);
    }

    private static List<string> TitlesInGroup(AppDriver app)
    {
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        return DocumentPanels.ListTabsInGroup(app.Ui, tab)
            .Select(t =>
            {
                Assert.NotNull(t.Node);
                return NodeText.Name(t.Node);
            })
            .ToList();
    }

    private static void PickFromTabMenu(AppDriver app, string title, string item)
    {
        app.Frames.ClickOn(Targets.GetTab(title), PointerButton.Right).Frame();
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, item)).Frame(2);
        app.Settle(2);
    }

    [Fact]
    public async Task Closing_to_the_right_then_the_others_leaves_only_the_tab_right_clicked()
    {
        var (app, assembly) = await OpenTabsAsync();
        Assert.Equal([NodeText.Name(assembly), .. Types], TitlesInGroup(app));

        PickFromTabMenu(app, "Greeter", "Close all to the right");

        Assert.Equal([NodeText.Name(assembly), "Greeter"], TitlesInGroup(app));
        Assert.Equal(2, app.AppState.Navigation.OpenTabs.Count);

        PickFromTabMenu(app, "Greeter", "Close others");

        Assert.Equal(["Greeter"], TitlesInGroup(app));
        Assert.Single(app.AppState.Navigation.OpenTabs);
    }

    [Fact]
    public async Task Closing_to_the_left_keeps_the_tab_right_clicked_and_those_after_it()
    {
        var (app, _) = await OpenTabsAsync();

        PickFromTabMenu(app, "Farewell", "Close all to the left");

        Assert.Equal(["Farewell", "Outline"], TitlesInGroup(app));
    }

    [Fact]
    public async Task Closing_all_closes_the_documents_of_the_group_and_leaves_the_other_panels_docked_there()
    {
        var (app, _) = await OpenTabsAsync();
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var group = app.Ui.Layout.GroupOf(DocumentPanels.GetPanel(app.Ui.Documents, tab));
        Assert.NotNull(group);
        group.Panels.Add(DockPanels.Bookmarks);
        app.Run(2);

        DocumentPanels.CloseTabsInGroup(app.Ui, tab);
        app.Settle(2);

        Assert.Empty(app.AppState.Navigation.OpenTabs);
        Assert.Same(group, app.Ui.Layout.GroupOf(DockPanels.Bookmarks));
    }

    [Fact]
    public void The_menu_offers_nothing_to_close_beside_a_tab_alone_in_its_group()
    {
        var app = AppDriver.Opened(width: 1600f);
        app.Settle(2);
        var active = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(active);
        var node = active.Node;
        Assert.NotNull(node);
        var title = NodeText.Name(node);

        app.Frames.ClickOn(Targets.GetTab(title), PointerButton.Right).Frame();

        Assert.True(app.Frames.Drew("Close others"));
        Assert.True(app.Frames.Drew(Shortcuts.CloseTab.Label));
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Close others")).Frame(2);
        Assert.Single(app.AppState.Navigation.OpenTabs);
    }
}
