using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using static ILSight.Docking;
using static ILSight.Settings;
using static ILSight.ScreenLayout;

namespace ILSight.App.Tests;

public sealed class DockingTests
{
    [Fact]
    public void A_view_docks_as_a_tab_beside_a_document_and_the_document_stays_the_active_tab()
    {
        var app = AppDriver.Opened();
        app.Show(app.Fixture("Greeter"));
        var greeter = app.AppState.Tabs.Active!;
        var greeterPanel = DocumentPanels.GetPanel(app.Ui.Documents, greeter);
        var layout = app.Ui.Layout;

        layout.Dock(DockPanels.Assemblies, layout.GroupOf(greeterPanel)!, DockEdge.Center);
        app.Run(3);

        Assert.Equal([greeterPanel, DockPanels.Assemblies], layout.GroupOf(greeterPanel)!.Panels);
        Assert.Same(greeter, app.AppState.Tabs.Active);
        Assert.True(app.Frames.Drew("Greeter"));
        Assert.True(app.Frames.Drew(AppStateDriver.FixturesNamespace));
    }

    [Fact]
    public void The_assemblies_view_closes_like_a_tab_and_comes_back_on_the_left_from_the_View_menu()
    {
        var app = AppDriver.Opened();
        var layout = app.Ui.Layout;
        app.Run(2);

        layout.Close(DockPanels.Assemblies);
        app.Run(2);
        var closed = !layout.Contains(DockPanels.Assemblies);
        app.Frames.ClickOn(Selector.WithText("View"));
        app.Frames.ClickOn(Selector.WithText("Panels"));
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Assemblies")).Frame(2);

        Assert.True(closed);
        var split = Assert.IsType<DockSplit>(layout.Root);
        Assert.Same(layout.GroupOf(DockPanels.Assemblies), split.First);
        Assert.True(app.Frames.Drew("0.0.0.0 · net10.0"));
    }

    [Fact]
    public void View_opens_the_search_panel_and_holds_back_and_forward_with_no_navigate_menu()
    {
        var app = new AppDriver();
        var layout = app.Ui.Layout;
        app.Run(2);
        Assert.False(app.Frames.Drew("Navigate"));

        app.Frames.ClickOn(Selector.WithText("View"));
        Assert.True(app.Frames.Drew("Back"));
        Assert.True(app.Frames.Drew("Forward"));
        app.Frames.ClickOn(Selector.WithText("Panels"));
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Search")).Frame(2);

        Assert.True(layout.Contains(DockPanels.Search));
        Assert.True(app.Frames.Drew("Search…"));
    }

    [Fact]
    public void Closing_the_last_document_frees_its_area_and_the_next_one_opens_right_of_the_views()
    {
        var d = AppStateDriver.Opened();
        var layout = d.Ui.Layout;

        CloseTab(d.AppState, d.AppState.Tabs.Active!);
        d.NextFrame();
        var left = Assert.IsType<DockGroup>(layout.Root).Panels.ToList();
        d.Show(d.Fixture("Greeter"));

        Assert.Equal([DockPanels.Assemblies], left);
        var split = Assert.IsType<DockSplit>(layout.Root);
        Assert.Same(layout.GroupOf(DocumentPanels.GetPanel(d.Ui.Documents, d.AppState.Tabs.Active!)), split.Second);
        Assert.Equal("Greeter", d.AppState.Tabs.Active!.Node!.Text);
    }

    [Fact]
    public void With_everything_closed_a_hint_points_to_the_View_menu()
    {
        var app = new AppDriver();
        var appState = app.AppState;
        app.Ui.Layout.Close(DockPanels.Assemblies);
        CloseTab(appState, appState.Tabs.Active!);

        app.Run(2);

        Assert.True(app.Frames.Drew("Everything is closed. Open the assemblies from the View menu."));
    }

    [Fact]
    public void Reset_layout_in_the_View_menu_once_confirmed_puts_the_assemblies_left_of_every_open_document_and_closes_the_rest()
    {
        var app = AppDriver.Opened();
        var d = app.Driver;
        d.Show(d.Fixture("Greeter"));
        d.Show(d.Fixture("Farewell"), inNewTab: true);
        var farewell = d.AppState.Tabs.Active!;
        var layout = d.Ui.Layout;
        layout.Dock(DocumentPanels.GetPanel(app.Ui.Documents, farewell), layout.Root, DockEdge.Bottom);
        layout.Float(DockPanels.Assemblies, new UiRect(40f, 40f, 300f, 300f));
        OpenPanel(d.Ui, DockView.Bookmarks);
        app.Run(2);

        app.Frames.ClickOn(Selector.WithText("View"));
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Reset layout")).Frame(2);
        Assert.True(layout.Contains(DockPanels.Bookmarks));
        app.Frames.ClickOn(Selector.WithText("Reset")).Frame(2);

        var split = Assert.IsType<DockSplit>(layout.Root);
        Assert.Equal([DockPanels.Assemblies], Assert.IsType<DockGroup>(split.First).Panels);
        var documents = Assert.IsType<DockGroup>(split.Second);
        Assert.Equal(d.AppState.Tabs.Open.Select(tab => DocumentPanels.GetPanel(d.Ui.Documents, tab)).Order(), documents.Panels.Order());
        Assert.Equal(DocumentPanels.GetPanel(app.Ui.Documents, farewell), documents.Active);
        Assert.Same(farewell, d.AppState.Tabs.Active);
        Assert.Empty(layout.Windows);
        Assert.False(layout.Contains(DockPanels.Bookmarks));
    }
}

public sealed class LayoutResetTests
{
    [Fact]
    public void Cancelling_the_reset_question_leaves_the_layout_as_it_was()
    {
        var app = new AppDriver();
        var screen = app.Ui;
        OpenPanel(screen, DockView.Bookmarks);
        app.Run(2);

        app.Frames.ClickOn(Selector.WithText("View"));
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Reset layout")).Frame(2);
        Assert.True(screen.ConfirmingLayoutReset);
        app.Frames.ClickOn(Selector.WithText("Cancel")).Frame(2);

        Assert.False(screen.ConfirmingLayoutReset);
        Assert.True(screen.Layout.Contains(DockPanels.Bookmarks));
    }
}

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
        screen.Layout.Dock(DockPanels.Bookmarks, screen.Layout.GroupOf(DockPanels.Analyzer)!, DockEdge.Center);
        d.NextFrame();
        SaveSettings(d.AppState);

        var next = new AppStateDriver(_temp.Settings());
        var layout = next.Ui.Layout;
        var documents = layout.GroupOf(DocumentPanels.GetPanel(next.Ui.Documents, next.AppState.Tabs.Active!))!;
        var views = layout.GroupOf(DockPanels.Analyzer)!;
        var split = ParentOf(layout.Root, views)!;

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
        screen.Layout.Dock(DockPanels.Search, screen.Layout.GroupOf(DockPanels.Analyzer)!, DockEdge.Center);
        d.NextFrame();
        SaveSettings(d.AppState);

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

public sealed class DockLayoutReadTests
{
    private static DockLayoutJson BuildSplitLayout(int first, int second, int nextId)
    {
        return new DockLayoutJson
        {
            Root = new DockSplitJson
            {
                Id = 1,
                First = new DockGroupJson { Id = first, Panels = [DockPanels.Assemblies] },
                Second = new DockGroupJson { Id = second, Panels = [DockPanels.Search] },
            },
            Windows = [],
            NextId = nextId,
        };
    }

    [Fact]
    public void A_layout_with_two_parts_under_one_id_reads_as_ids_repeated()
    {
        var read = DockJson.ToLayout(BuildSplitLayout(2, 2, nextId: 3));

        Assert.IsType<DockLayoutRead.IdsRepeated>(read);
    }

    [Fact]
    public void A_window_with_the_id_of_a_node_reads_as_ids_repeated()
    {
        var json = BuildSplitLayout(2, 3, nextId: 4);
        json.Windows = [new DockWindowJson { Id = 3, Root = new DockGroupJson { Id = 4, Panels = [DockPanels.Bookmarks] } }];

        var read = DockJson.ToLayout(json);

        Assert.IsType<DockLayoutRead.IdsRepeated>(read);
    }

    [Fact]
    public void A_layout_holding_a_part_as_null_reads_as_parts_missing()
    {
        var json = BuildSplitLayout(2, 3, nextId: 4);
        json.Windows = [null];

        var read = DockJson.ToLayout(json);

        Assert.IsType<DockLayoutRead.PartsMissing>(read);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(0)]
    public void A_next_id_at_or_below_an_id_in_use_is_raised_above_the_highest(int nextId)
    {
        var json = BuildSplitLayout(2, 3, nextId);
        json.Windows = [new DockWindowJson { Id = 5, Root = new DockGroupJson { Id = 4, Panels = [DockPanels.Bookmarks] } }];

        var read = DockJson.ToLayout(json);

        Assert.Equal(6, Assert.IsType<DockLayoutRead.Parsed>(read).Layout.NextId);
    }

    [Fact]
    public void A_next_id_above_every_id_in_use_is_kept()
    {
        var read = DockJson.ToLayout(BuildSplitLayout(2, 3, nextId: 9));

        Assert.Equal(9, Assert.IsType<DockLayoutRead.Parsed>(read).Layout.NextId);
    }

    [Fact]
    public void A_layout_whose_parts_have_no_ids_yet_numbers_from_one()
    {
        var read = DockJson.ToLayout(new DockLayoutJson { NextId = 0 });

        Assert.Equal(1, Assert.IsType<DockLayoutRead.Parsed>(read).Layout.NextId);
    }
}
