using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;

namespace ILSight.App.Tests;

public sealed class DockingTests
{
    [Fact]
    public void Each_tab_opened_has_an_id_no_tab_had_before_even_once_one_closed()
    {
        var tabs = new DocumentTabs();
        var first = tabs.AddNew();
        var second = tabs.AddNew();

        tabs.Remove(second);
        var third = tabs.AddNew();

        Assert.Equal(3, new[] { first.Id, second.Id, third.Id }.Distinct().Count());
    }

    [Fact]
    public async Task A_view_docks_as_a_tab_beside_a_document_and_the_document_stays_the_active_tab()
    {
        var app = AppDriver.Opened();
        var greeterNode = await app.FixtureAsync("Greeter");
        app.Show(greeterNode);
        var greeter = app.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(greeter);
        var greeterPanel = app.Ui.Documents.GetPanel(greeter);

        app.DockWidget.Dock(DockPanels.Assemblies, greeterPanel, DockEdge.Center);
        app.Run(3);

        Assert.Equal([greeterPanel, DockPanels.Assemblies], app.Arrangement.GroupOf(greeterPanel)?.Panels);
        Assert.Same(greeter, app.AppState.ListSession.Navigation.ActiveTab);
        Assert.True(app.Frames.Drew("Greeter"));
        Assert.True(app.Frames.Drew(AppStateDriver.FixturesNamespace));
    }

    [Fact]
    public void The_assemblies_view_closes_like_a_tab_and_comes_back_on_the_left_from_the_View_menu()
    {
        var app = AppDriver.Opened();
        app.Run(2);

        app.DockWidget.Close(DockPanels.Assemblies);
        app.Run(2);
        var closed = !app.Arrangement.Contains(DockPanels.Assemblies);
        app.Frames.ClickOn(Selector.WithText("View"));
        app.Frames.ClickOn(Selector.WithText("Panels"));
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Assemblies"));
        app.Frames.Frame(2);

        Assert.True(closed);
        var layout = app.Arrangement;
        var split = Assert.IsType<DockSplit>(layout.Root);
        Assert.Same(layout.GroupOf(DockPanels.Assemblies), split.First);
        Assert.True(app.Frames.Drew("0.0.0.0 · net11.0"));
    }

    [Fact]
    public void View_opens_the_search_panel_and_holds_back_and_forward_with_no_navigate_menu()
    {
        var app = new AppDriver();
        app.Run(2);
        Assert.False(app.Frames.Drew("Navigate"));

        app.Frames.ClickOn(Selector.WithText("View"));
        Assert.True(app.Frames.Drew("Back"));
        Assert.True(app.Frames.Drew("Forward"));
        app.Frames.ClickOn(Selector.WithText("Panels"));
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Search"));
        app.Frames.Frame(2);

        Assert.True(app.Arrangement.Contains(DockPanels.Search));
        Assert.True(app.Frames.Drew("Search…"));
    }

    [Fact]
    public async Task Closing_the_last_document_frees_its_area_and_the_next_one_opens_right_of_the_views()
    {
        var d = AppStateDriver.Opened();

        var active = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(active);
        d.AppState.ListSession.Navigation.CloseTab(active);
        d.NextFrame();
        var left = Assert.IsType<DockGroup>(d.Arrangement.Root).Panels.ToList();
        var greeter = await d.FixtureAsync("Greeter");
        d.Show(greeter);

        Assert.Equal([DockPanels.Assemblies], left);
        var layout = d.Arrangement;
        var split = Assert.IsType<DockSplit>(layout.Root);
        var shown = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(shown);
        Assert.Same(layout.GroupOf(d.Ui.Documents.GetPanel(shown)), split.Second);
        Assert.Equal("Greeter", shown.Node?.Text);
    }

    [Fact]
    public void With_everything_closed_a_hint_points_to_the_View_menu()
    {
        var app = new AppDriver();
        var appState = app.AppState;
        app.DockWidget.Close(DockPanels.Assemblies);
        var active = appState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(active);
        appState.ListSession.Navigation.CloseTab(active);

        app.Run(2);

        Assert.True(app.Frames.Drew("Everything is closed. Open the assemblies from the View menu."));
    }

    [Fact]
    public async Task Reset_layout_in_the_View_menu_once_confirmed_puts_the_assemblies_left_of_every_open_document_and_closes_the_rest()
    {
        var app = AppDriver.Opened();
        var d = app.Driver;
        var greeter = await d.FixtureAsync("Greeter");
        d.Show(greeter);
        var farewellNode = await d.FixtureAsync("Farewell");
        d.Show(farewellNode, inNewTab: true);
        var farewell = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(farewell);
        app.DockWidget.DockAtRim(app.Ui.Documents.GetPanel(farewell), DockEdge.Bottom);
        app.DockWidget.Float(DockPanels.Assemblies, new UiRect(40f, 40f, 300f, 300f));
        d.Ui.Panels.Open(DockView.Bookmarks);
        app.Run(2);

        app.Frames.ClickOn(Selector.WithText("View"));
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Reset layout"));
        app.Frames.Frame(2);
        Assert.True(app.Arrangement.Contains(DockPanels.Bookmarks));
        app.Frames.ClickOn(Selector.WithText("Reset"));
        app.Frames.Frame(2);

        var layout = d.Arrangement;
        var split = Assert.IsType<DockSplit>(layout.Root);
        Assert.Equal([DockPanels.Assemblies], Assert.IsType<DockGroup>(split.First).Panels);
        var documents = Assert.IsType<DockGroup>(split.Second);
        Assert.Equal(d.AppState.ListSession.Navigation.OpenTabs.Select(tab => d.Ui.Documents.GetPanel(tab)).Order(), documents.Panels.Order());
        Assert.Equal(app.Ui.Documents.GetPanel(farewell), documents.Active);
        Assert.Same(farewell, d.AppState.ListSession.Navigation.ActiveTab);
        Assert.Empty(layout.Windows);
        Assert.False(layout.Contains(DockPanels.Bookmarks));
    }
}
