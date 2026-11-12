using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using static ILSight.Settings;
using static ILSight.ScreenLayout;

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
        var d = AppStateDriver.Opened(_temp.Settings());
        var screen = d.Ui;
        OpenPanel(screen, DockView.Analyzer);
        var analyzer = screen.Layout.GroupOf(DockPanels.Analyzer);
        Assert.NotNull(analyzer);
        screen.Layout.Dock(DockPanels.Bookmarks, analyzer, DockEdge.Center);
        d.NextFrame();
        d.AppState.SettingsSaving.Save(d.AppState);

        var next = new AppStateDriver(_temp.Settings());
        var layout = next.Ui.Layout;
        var active = next.AppState.Tabs.Active;
        Assert.NotNull(active);
        var documents = layout.GroupOf(DocumentPanels.GetPanel(next.Ui.Documents, active));
        Assert.NotNull(documents);
        var views = layout.GroupOf(DockPanels.Analyzer);
        Assert.NotNull(views);
        var split = ParentOf(layout.Root, views);
        Assert.NotNull(split);

        Assert.Same(views, layout.GroupOf(DockPanels.Bookmarks));
        Assert.Equal(Direction.Vertical, split.Direction);
        Assert.Same(documents, split.First);
        Assert.Same(views, split.Second);
        Assert.Single(next.AppState.Tabs.Open);
    }

    [Fact]
    public void An_open_search_panel_is_still_open_where_it_was_docked_after_a_restart()
    {
        var d = new AppStateDriver(_temp.Settings());
        var screen = d.Ui;
        OpenSearch(screen);
        OpenPanel(screen, DockView.Analyzer);
        var analyzer = screen.Layout.GroupOf(DockPanels.Analyzer);
        Assert.NotNull(analyzer);
        screen.Layout.Dock(DockPanels.Search, analyzer, DockEdge.Center);
        d.NextFrame();
        d.AppState.SettingsSaving.Save(d.AppState);

        var next = new AppStateDriver(_temp.Settings()).Ui;

        Assert.True(next.Layout.Contains(DockPanels.Search));
        Assert.Same(next.Layout.GroupOf(DockPanels.Analyzer), next.Layout.GroupOf(DockPanels.Search));
    }

    [Fact]
    public void The_symbols_being_analyzed_are_analyzed_again_after_a_restart()
    {
        var first = AppStateDriver.Opened(_temp.Settings());
        first.Analyze(first.Fixture("Outline").Children.Single(c => c.Text.StartsWith("First()")));
        first.Analyze(first.Fixture("Greeter"));
        first.Pump();

        var next = new AppStateDriver(_temp.Settings());
        next.RunUntil(() => !next.AppState.IsFindingAnalyses);

        Assert.Equal(["First() : int", "Greeter"], next.AppState.Analyzer.Found().Select(r => r.Symbol.Label));
    }
}
