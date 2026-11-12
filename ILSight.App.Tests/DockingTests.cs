using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using static ILSight.Docking;
using static ILSight.ScreenLayout;

namespace ILSight.App.Tests;

public sealed class DockingTests
{
    [Fact]
    public void Each_tab_opened_has_an_id_no_tab_had_before_even_once_one_closed()
    {
        var tabs = new DocumentTabs();
        var first = tabs.AddNew();
        var second = tabs.AddNew();

        Tabs.Remove(tabs, second);
        var third = tabs.AddNew();

        Assert.Equal(3, new[] { first.Id, second.Id, third.Id }.Distinct().Count());
    }

    [Fact]
    public void A_view_docks_as_a_tab_beside_a_document_and_the_document_stays_the_active_tab()
    {
        var app = AppDriver.Opened();
        app.Show(app.Fixture("Greeter"));
        var greeter = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(greeter);
        var greeterPanel = DocumentPanels.GetPanel(app.Ui.Documents, greeter);
        var layout = app.Ui.Layout;

        var group = layout.GroupOf(greeterPanel);
        Assert.NotNull(group);
        layout.Dock(DockPanels.Assemblies, group, DockEdge.Center);
        app.Run(3);

        Assert.Equal([greeterPanel, DockPanels.Assemblies], layout.GroupOf(greeterPanel)?.Panels);
        Assert.Same(greeter, app.AppState.Navigation.ActiveTab);
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

        var active = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(active);
        d.AppState.Navigation.CloseTab(active);
        d.NextFrame();
        var left = Assert.IsType<DockGroup>(layout.Root).Panels.ToList();
        d.Show(d.Fixture("Greeter"));

        Assert.Equal([DockPanels.Assemblies], left);
        var split = Assert.IsType<DockSplit>(layout.Root);
        var shown = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(shown);
        Assert.Same(layout.GroupOf(DocumentPanels.GetPanel(d.Ui.Documents, shown)), split.Second);
        Assert.Equal("Greeter", shown.Node?.Text);
    }

    [Fact]
    public void With_everything_closed_a_hint_points_to_the_View_menu()
    {
        var app = new AppDriver();
        var appState = app.AppState;
        app.Ui.Layout.Close(DockPanels.Assemblies);
        var active = appState.Navigation.ActiveTab;
        Assert.NotNull(active);
        appState.Navigation.CloseTab(active);

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
        var farewell = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(farewell);
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
        Assert.Equal(d.AppState.Navigation.OpenTabs.Select(tab => DocumentPanels.GetPanel(d.Ui.Documents, tab)).Order(), documents.Panels.Order());
        Assert.Equal(DocumentPanels.GetPanel(app.Ui.Documents, farewell), documents.Active);
        Assert.Same(farewell, d.AppState.Navigation.ActiveTab);
        Assert.Empty(layout.Windows);
        Assert.False(layout.Contains(DockPanels.Bookmarks));
    }
}
