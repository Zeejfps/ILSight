using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.SettingArea;

namespace ILSight.App.Tests;

public sealed class LayoutPersistenceTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    private static DockSplit? ParentOf(DockNode node, DockNode target)
    {
        return node switch
        {
            DockSplit split when split.First == target || split.Second == target => split,
            DockSplit split => ParentOf(split.First, target) ?? ParentOf(split.Second, target),
            _ => null,
        };
    }

    [Fact]
    public void Views_docked_below_the_documents_are_still_below_them_after_a_restart()
    {
        var app = new AppDriver(settings: _temp.Settings());
        var d = app.Driver;
        d.OpenFixtures();
        d.Ui.Panels.Open(DockView.Analyzer);
        app.DockWidget.Dock(DockPanels.Bookmarks, DockPanels.Analyzer, DockEdge.Center);
        app.Run();
        var write = d.AppState.SettingsSaver.Save(d.AppState);
        Assert.IsType<SettingsWriteResult.Written>(write);

        var next = AppStateDriver.Started(_temp.Settings());
        var layout = next.Arrangement;
        var active = next.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(active);
        var documents = layout.GroupOf(next.Ui.Documents.GetPanel(active));
        Assert.NotNull(documents);
        var views = layout.GroupOf(DockPanels.Analyzer);
        Assert.NotNull(views);
        var split = ParentOf(layout.Root, views);
        Assert.NotNull(split);

        Assert.Same(views, layout.GroupOf(DockPanels.Bookmarks));
        Assert.Equal(Direction.Vertical, split.Direction);
        Assert.Same(documents, split.First);
        Assert.Same(views, split.Second);
        Assert.Single(next.AppState.ListSession.Navigation.OpenTabs);
    }

    [Fact]
    public void An_open_search_panel_is_still_open_where_it_was_docked_after_a_restart()
    {
        var app = new AppDriver(settings: _temp.Settings());
        var d = app.Driver;
        d.Ui.Panels.OpenSearch();
        d.Ui.Panels.Open(DockView.Analyzer);
        app.DockWidget.Dock(DockPanels.Search, DockPanels.Analyzer, DockEdge.Center);
        app.Run();
        var write = d.AppState.SettingsSaver.Save(d.AppState);
        Assert.IsType<SettingsWriteResult.Written>(write);

        var next = AppStateDriver.Started(_temp.Settings()).Arrangement;

        Assert.True(next.Contains(DockPanels.Search));
        Assert.Same(next.GroupOf(DockPanels.Analyzer), next.GroupOf(DockPanels.Search));
    }

    [Fact]
    public async Task The_symbols_being_analyzed_are_analyzed_again_after_a_restart()
    {
        var first = AppStateDriver.Opened(_temp.Settings());
        var outline = await first.FixtureAsync("Outline");
        first.Analyze((await outline.ListChildrenAsync()).Single(c => c.Text.StartsWith("First()")));
        first.Analyze(await first.FixtureAsync("Greeter"));
        first.Pump();

        var next = AppStateDriver.Started(_temp.Settings());
        next.Settle();

        Assert.Equal(["First() : int", "Greeter"], next.AppState.ListSession.Analyzer.Found().Select(r => r.Symbol.Label));
    }
}
