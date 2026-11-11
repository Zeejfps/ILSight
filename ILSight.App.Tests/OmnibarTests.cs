using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ZGF.KeyboardModule;
using static ILSight.CodeDisplay;

namespace ILSight.App.Tests;

public sealed class OmnibarTests
{
    private const string FixturesAssembly = "ILSight.App.Tests";

    /// <summary>The app showing the fixture type <paramref name="type"/> in its active tab.</summary>
    private static AppDriver Showing(string type)
    {
        var app = AppDriver.Opened();
        app.Settle(2);
        app.Show(app.Fixture(type), 2);
        app.Settle(2);
        return app;
    }

    private static IReadOnlyList<Crumb> ActiveCrumbs(AppDriver app)
    {
        var tab = app.AppState.Tabs.Active!;
        return DocumentPanels.GetView(app.Ui.Documents, tab).Breadcrumb.Crumbs;
    }

    /// <summary>The crumb reading <paramref name="label"/>, in whichever document's bar shows it.</summary>
    private static SemanticNode FindCrumb(AppDriver app, string label)
    {
        return app.Frames.Snapshot().Nodes.Single(n => IsPart(n, "crumb") && n.SelfAndDescendants().Any(c => c.Label == label));
    }

    private static bool IsPart(SemanticNode node, string name)
    {
        var path = node.Path;
        if (path == null)
        {
            return false;
        }

        return path.Segments[^1] is TestSegment.Named named && named.Name == name;
    }

    /// <summary>Clicks the crumb reading <paramref name="label"/>.</summary>
    private static UiFrames ClickCrumb(AppDriver app, string label)
    {
        return app.Frames.ClickOn(new Selector.ByPath(FindCrumb(app, label).Path!));
    }

    /// <summary>Clicks the arrow after the crumb reading <paramref name="label"/>, which shares its depth.</summary>
    private static void ClickArrowAfter(AppDriver app, string label)
    {
        var crumb = FindCrumb(app, label).Path!.Segments;
        app.Frames.ClickOn(GetPath([.. crumb.Take(crumb.Count - 1), new TestSegment.Named("children")])).Frame(2);
    }

    /// <summary>Starts a search in the active bar with Ctrl+L and types <paramref name="query"/>, waiting
    /// until more than <paramref name="moreThan"/> matches are suggested.</summary>
    private static void SearchFor(AppDriver app, string query, int moreThan = 0)
    {
        var omnibar = app.Ui.Omnibar;
        app.Frames.Key(Shortcuts.OmnibarSearch).Frame(2);
        app.Frames.Type(query).Frame();
        app.RunOut();
        Assert.False(omnibar.Search.IsBusy);
        Assert.True(omnibar.Search.ShowsQuery);
        Assert.True(omnibar.SuggestionCount > moreThan, $"{omnibar.SuggestionCount} suggestions");
    }

    [Fact]
    public void A_document_shows_the_path_to_its_node_from_the_assembly_over_its_code()
    {
        var app = Showing("Greeter");

        var labels = ActiveCrumbs(app)
            .Select(c => c.Label)
            .ToList();

        Assert.Equal([FixturesAssembly, AppStateDriver.FixturesNamespace, "Greeter"], labels);
        FindCrumb(app, FixturesAssembly);
        FindCrumb(app, "Greeter");
    }

    [Fact]
    public void With_nested_namespaces_the_path_steps_through_each_enclosing_namespace()
    {
        var app = Showing("Greeter");

        SetDisplayOptions(app.Ui, app.Ui.Display with { NestedNamespaces = true });
        app.Run(2);
        var labels = ActiveCrumbs(app)
            .Select(c => c.Label)
            .ToList();

        Assert.Equal([FixturesAssembly, "ILSight", "App", "Tests", "Fixtures", "Greeter"], labels);
    }

    [Fact]
    public void Clicking_a_crumb_shows_its_node_in_the_preview_tab_as_a_pick_in_the_tree_does()
    {
        var app = Showing("Greeter");

        ClickCrumb(app, AppStateDriver.FixturesNamespace).Frame(2);
        app.Settle(2);

        Assert.Same(app.Node(AppStateDriver.FixturesNamespace), app.AppState.Tabs.Active!.Node);
        Assert.Same(app.AppState.Tabs.Active, app.AppState.Tabs.Preview);
        Assert.Same(app.Node(AppStateDriver.FixturesNamespace), app.Ui.Tree.View.Selected);
    }

    [Fact]
    public void The_arrow_after_a_crumb_lists_its_children_with_the_next_crumb_selected_and_picking_one_shows_it()
    {
        var app = Showing("Greeter");

        ClickArrowAfter(app, AppStateDriver.FixturesNamespace);
        var menu = app.Ui.Omnibar.Menu;
        Assert.NotNull(menu);
        var selected = menu.Selection.Item;
        app.Frames.ClickOn(GetCrumbChild("document-1", "Farewell")).Frame(2);
        app.Settle(2);

        Assert.Same(app.Fixture("Greeter"), selected);
        Assert.Null(app.Ui.Omnibar.Menu);
        Assert.Same(app.Fixture("Farewell"), app.AppState.Tabs.Active!.Node);
    }

    [Fact]
    public void Typing_in_the_arrows_filter_lists_the_children_whose_name_holds_it_and_Enter_shows_the_first()
    {
        var app = Showing("Greeter");

        ClickArrowAfter(app, AppStateDriver.FixturesNamespace);
        var menu = app.Ui.Omnibar.Menu;
        Assert.NotNull(menu);
        app.Frames.Type("arew").Frame(2);
        var shown = menu.Shown.ToList();
        app.Frames.Key(KeyboardKey.Enter).Frame(2);
        app.Settle(2);

        Assert.Equal([app.Fixture("Farewell")], shown);
        Assert.Same(app.Fixture("Farewell"), app.AppState.Tabs.Active!.Node);
    }

    [Fact]
    public void Ctrl_L_turns_the_active_bar_into_a_search_whose_Enter_shows_the_best_match_and_brings_the_path_back()
    {
        var app = Showing("Greeter");
        var omnibar = app.Ui.Omnibar;

        SearchFor(app, "Farewell");
        var searchedIn = omnibar.SearchingPanel;
        app.Frames.Key(KeyboardKey.Enter).Frame(2);
        app.Settle(2);

        Assert.Equal(DocumentPanels.GetPanel(app.Ui.Documents, app.AppState.Tabs.Active!), searchedIn);
        Assert.Same(app.Fixture("Farewell"), app.AppState.Tabs.Active!.Node);
        Assert.False(omnibar.IsSearching);
        FindCrumb(app, "Farewell");
    }

    [Fact]
    public void Down_walks_the_suggestions_and_Escape_puts_the_path_back()
    {
        var app = Showing("Greeter");
        var omnibar = app.Ui.Omnibar;
        SearchFor(app, "Gree", moreThan: 1);

        app.Frames.Key(KeyboardKey.DownArrow).Frame();
        var first = omnibar.SelectedSuggestion;
        app.Frames.Key(KeyboardKey.DownArrow).Frame();
        var second = omnibar.SelectedSuggestion;
        app.Frames.Key(KeyboardKey.Escape).Frame(2);

        Assert.Same(omnibar.Search.Rows[0], first);
        Assert.Same(omnibar.Search.Rows[1], second);
        Assert.False(omnibar.IsSearching);
        Assert.Same(app.Fixture("Greeter"), app.AppState.Tabs.Active!.Node);
        FindCrumb(app, "Greeter");
    }

    [Fact]
    public void Clicking_a_suggestion_shows_it()
    {
        var app = Showing("Greeter");
        SearchFor(app, "Farewell");

        app.Frames.ClickOn(GetSuggestion("document-1", "Farewell")).Frame(2);
        app.Settle(2);

        Assert.Same(app.Fixture("Farewell"), app.AppState.Tabs.Active!.Node);
        Assert.False(app.Ui.Omnibar.IsSearching);
    }

    [Fact]
    public void The_search_button_at_the_end_of_the_bar_starts_a_search_there()
    {
        var app = Showing("Greeter");

        app.Frames.ClickOn(Selector.AtPath("dock/document-1/search")).Frame(2);

        Assert.True(OmnibarView.IsSearchingIn(app.Ui.Omnibar, DocumentPanels.GetPanel(app.Ui.Documents, app.AppState.Tabs.Active!)));
        Assert.True(app.Frames.WantsTextInput);
    }

    [Fact]
    public void With_the_bar_turned_off_no_path_shows_and_Ctrl_L_opens_the_search_panel()
    {
        var app = Showing("Greeter");

        SetDisplayOptions(app.Ui, app.Ui.Display with { ShowOmnibar = false });
        app.Run(2);
        var searchIcon = BuiltInGlyphs.Text(BuiltInGlyphs.Icons.SearchField);
        var searchButtons = app.Frames.TextRects()
            .Count(t => t.Text == searchIcon);
        app.Frames.Key(Shortcuts.OmnibarSearch).Frame(2);

        Assert.Equal(1, searchButtons);
        Assert.False(app.Ui.Omnibar.IsSearching);
        Assert.True(app.Ui.Layout.Contains(DockPanels.Search));
    }

    [Fact]
    public void Ctrl_M_then_Ctrl_L_in_the_code_still_folds_everything_rather_than_searching()
    {
        var app = Showing("Greeter");
        var view = DocumentPanels.GetView(app.Ui.Documents, app.AppState.Tabs.Active!);

        app.Frames.Click(700f, 400f).Frame();
        app.Frames.Key(KeyboardKey.M, KeyModifiers.Control).Frame();
        app.Frames.Key(KeyboardKey.L, KeyModifiers.Control).Frame(2);

        Assert.False(app.Ui.Omnibar.IsSearching);
        Assert.True(view.Code.Folds.AnyCollapsed);
    }
}
