using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Localization;
using static ILSight.Assemblies;
using static ILSight.Docking;
using static ILSight.Navigation;
using static ILSight.DocumentPanels;

namespace ILSight.App.Tests;

public sealed class AppStateTests
{
    [Fact]
    public void A_driver_made_without_a_screen_has_none_to_give()
    {
        var d = AppStateDriver.WithoutScreen();

        Assert.Throws<InvalidOperationException>(() => d.Ui);
        Assert.Throws<InvalidOperationException>(d.NextFrame);
    }

    [Fact]
    public void Showing_a_node_in_a_new_tab_asks_the_view_to_dock_it_beside_the_active_tab_and_reveal_the_node()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var before = d.AppState.Tabs.Active!;
        var greeter = d.Fixture("Greeter");
        d.TakeViewCommands();

        ShowNode(d.AppState, greeter, inNewTab: true);
        var commands = d.TakeViewCommands();

        var tab = d.AppState.Tabs.Active!;
        Assert.NotSame(before, tab);
        Assert.Equal([new ViewCommand.ShowTab(tab, before), new ViewCommand.RevealNode(greeter)], commands);
    }

    [Fact]
    public void Closing_the_active_tab_with_no_screen_leaves_the_pick_of_the_next_to_the_view()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(d.Fixture("Greeter"), inNewTab: true);
        var greeter = d.AppState.Tabs.Active!;

        var closed = CloseTab(d.AppState, greeter);

        Assert.True(closed.WasActive);
        Assert.Null(d.AppState.Tabs.Active);
        Assert.Single(d.AppState.Tabs.Open);
    }

    [Fact]
    public void A_reveal_asked_for_a_node_whose_list_was_cleared_since_is_dropped()
    {
        var d = AppStateDriver.Opened();
        var ns = d.Node(AppStateDriver.FixturesNamespace);
        ShowNode(d.AppState, d.Fixture("Greeter"));

        CloseAllAssemblies(d.AppState);
        d.NextFrame();

        Assert.False(d.Ui.Tree.View.HasSelection);
        Assert.False(d.Ui.Tree.View.IsExpanded(ns));
    }

    [Fact]
    public void Commands_for_nodes_of_a_cleared_list_or_lines_and_rows_a_tab_no_longer_has_are_dropped()
    {
        var d = AppStateDriver.Opened();
        var ns = d.Node(AppStateDriver.FixturesNamespace);
        d.Show(d.Node("Metadata", "Tables").Table("TypeDef"));
        var grid = d.AppState.Tabs.Active!;
        d.Show(d.Fixture("Greeter"), inNewTab: true);
        var code = d.AppState.Tabs.Active!;
        d.NextFrame();
        var lines = code.Document.Lines.Length;
        var table = grid.Grid!;
        var rows = table.RowCount;
        CloseAllAssemblies(d.AppState);
        d.NextFrame();
        var lineBefore = DocumentPanels.GetView(d.Ui.Documents, code).Code.Pending;
        var rowBefore = DocumentPanels.GetGridState(DocumentPanels.GetView(d.Ui.Documents, grid), table).Pending;

        d.AppState.ViewCommands.Enqueue(new ViewCommand.ExpandNodes([ns]));
        d.AppState.ViewCommands.Enqueue(new ViewCommand.RevealLine(code, lines));
        d.AppState.ViewCommands.Enqueue(new ViewCommand.RevealRow(grid, rows));
        d.NextFrame();

        Assert.False(d.Ui.Tree.View.IsExpanded(ns));
        Assert.Equal(lineBefore, DocumentPanels.GetView(d.Ui.Documents, code).Code.Pending);
        Assert.Equal(rowBefore, DocumentPanels.GetGridState(DocumentPanels.GetView(d.Ui.Documents, grid), table).Pending);
    }

    [Fact]
    public void A_row_revealed_before_a_new_view_of_a_tab_is_drawn_stays_to_be_revealed()
    {
        var d = AppStateDriver.Opened();
        d.Show(d.Node("Metadata", "Tables").Table("TypeDef"));
        var grid = d.AppState.Tabs.Active!;
        var documents = d.Ui.Documents;
        var panel = DocumentPanels.GetPanel(documents, grid);
        DocumentPanels.RemoveView(documents, grid);

        DocumentPanels.AddView(documents, grid, panel);
        DocumentPanels.RevealRow(documents, grid, 2);
        d.NextFrame();

        Assert.Equal(new PendingGridScroll.Reveal(2), DocumentPanels.GetGridState(DocumentPanels.GetView(documents, grid), grid.Grid!).Pending);
    }

    [Fact]
    public void Opening_happens_in_the_background_and_shows_the_assembly()
    {
        var d = new AppStateDriver();

        d.Start((a, ct) => a.AssemblyOpening.OpenAssemblyAsync(a, AppStateDriver.FixturePath, ct));

        Assert.Null(Assert.Single(d.AppState.Tabs.Open).Location);
        d.RunUntil(() => d.AppState.Tabs.Active?.Node is not null);
        Assert.Equal(NodeIcon.Assembly, d.AppState.Tabs.Active!.Node!.Icon);
    }

    [Fact]
    public void A_new_tab_opens_in_the_active_tabs_group()
    {
        var d = AppStateDriver.Opened();
        d.Show(d.Fixture("Greeter"));
        d.Show(d.Fixture("Farewell"), inNewTab: true);
        var greeter = d.AppState.Tabs.Open[^2];
        var farewellPanel = DocumentPanels.GetPanel(d.Ui.Documents, d.AppState.Tabs.Active!);
        var documents = d.Ui.Layout;
        documents.Dock(farewellPanel, documents.Root, DockEdge.Right);
        ActivateTab(d.Ui, greeter);
        d.NextFrame();

        d.Show(d.Fixture("Outline"), inNewTab: true);

        var outlinePanel = DocumentPanels.GetPanel(d.Ui.Documents, d.AppState.Tabs.Active!);
        Assert.Same(documents.GroupOf(DocumentPanels.GetPanel(d.Ui.Documents, greeter)), documents.GroupOf(outlinePanel));
        Assert.Equal("Outline", d.AppState.Tabs.Active!.Node!.Text);
    }

    [Fact]
    public void Docking_the_only_tab_beside_itself_moves_it_there_and_leaves_a_new_empty_tab_that_shows_what_is_picked_next()
    {
        var d = AppStateDriver.Opened();
        d.Show(d.Fixture("Greeter"));
        var greeter = d.AppState.Tabs.Active!;
        var greeterPanel = DocumentPanels.GetPanel(d.Ui.Documents, greeter);
        var documents = d.Ui.Layout;

        documents.Dock(greeterPanel, documents.GroupOf(greeterPanel)!, DockEdge.Right);
        SyncActiveTab(d.Ui, d.AppState.Tabs.Active);
        d.NextFrame();

        Assert.Equal(2, d.AppState.Tabs.Open.Count);
        var empty = d.AppState.Tabs.Open.Single(t => t != greeter);
        Assert.Null(empty.Location);
        Assert.NotSame(documents.GroupOf(greeterPanel), documents.GroupOf(DocumentPanels.GetPanel(d.Ui.Documents, empty)));
        Assert.Same(empty, d.AppState.Tabs.Active);

        d.Show(d.Fixture("Farewell"));

        Assert.Equal("Farewell", empty.Node!.Text);
        Assert.Equal("Greeter", greeter.Node!.Text);
    }

    [Fact]
    public void Closing_the_active_tab_activates_the_one_in_front_of_its_group()
    {
        var d = AppStateDriver.Opened();
        d.Show(d.Fixture("Greeter"));
        d.Show(d.Fixture("Farewell"), inNewTab: true);
        var greeter = d.AppState.Tabs.Open[^2];

        CloseTab(d.AppState, d.AppState.Tabs.Active!);
        d.NextFrame();

        Assert.Same(greeter, d.AppState.Tabs.Active);
        Assert.DoesNotContain(d.AppState.Tabs.Open, t => t.Node?.Text == "Farewell");
        var greeterPanel = DocumentPanels.GetPanel(d.Ui.Documents, greeter);
        Assert.Equal([greeterPanel], d.Ui.Layout.GroupOf(greeterPanel)!.Panels);
    }

    [Fact]
    public void A_file_that_cannot_be_opened_is_listed_as_failed_and_its_tab_says_why()
    {
        var d = new AppStateDriver();
        var missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.dll");

        d.Start((a, ct) => a.AssemblyOpening.OpenAssemblyAsync(a, missing, ct));
        d.RunUntil(() => d.AppState.Tabs.Active?.Node is not null);
        d.Settle();

        var failed = Assert.Single(d.AppState.Assemblies.Listed);
        var tab = Assert.Single(d.AppState.Tabs.Open);
        Assert.IsType<FailedAssembly>(failed);
        Assert.Same(failed.Root, tab.Node);
        Assert.Contains(Path.GetFileName(missing), Documents.GetText(tab.Document));
        Assert.Same(failed.Root, Assert.Single(d.Ui.Tree.Roots));
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void Reloading_does_not_overwrite_a_tab_the_user_pointed_elsewhere_meanwhile()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(d.Fixture("Greeter"));
        var farewell = d.Fixture("Farewell");

        d.Start((a, ct) => a.AssemblyReloading.RefreshAssembliesAsync(a, [], ct));
        ShowNode(d.AppState, farewell);
        d.Settle();

        var tab = Assert.Single(d.AppState.Tabs.Open);
        Assert.Same(farewell, tab.Node);
    }

    [Fact]
    public void A_reload_asks_the_view_to_expand_what_it_had_expanded_and_to_reveal_the_active_tabs_node()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(d.Fixture("Greeter"));
        var expanded = new List<NodePath> { BrowseNodes.GetPath(d.Node()), BrowseNodes.GetPath(d.Node(AppStateDriver.FixturesNamespace)) };
        d.TakeViewCommands();

        d.Start((a, ct) => a.AssemblyReloading.RefreshAssembliesAsync(a, expanded, ct));
        d.Settle();
        var commands = d.TakeViewCommands();

        var expand = Assert.Single(commands.OfType<ViewCommand.ExpandNodes>());
        Assert.Equal(expanded, expand.Nodes.Select(n => BrowseNodes.GetPath(n)));
        Assert.Same(d.Node(), expand.Nodes[0]);
        Assert.Contains(new ViewCommand.RevealNode(d.Fixture("Greeter")), commands);
    }

    [Fact]
    public void Reloading_twice_lands_every_tab_on_the_newest_assemblies()
    {
        var d = AppStateDriver.Opened();
        d.Show(d.Fixture("Greeter"));
        d.Show(d.Fixture("Farewell"), inNewTab: true);

        d.Start((a, ct) => a.AssemblyReloading.RefreshAssembliesAsync(a, Trees.ListExpandedPaths(d.Ui.Tree), ct));
        d.Start((a, ct) => a.AssemblyReloading.RefreshAssembliesAsync(a, Trees.ListExpandedPaths(d.Ui.Tree), ct));
        d.Settle();

        Assert.Equal(["Greeter", "Farewell"], d.AppState.Tabs.Open.Select(t => t.Node!.Text));
        Assert.All(d.AppState.Tabs.Open, t => Assert.Contains(t.Node!.Assembly, d.AppState.Assemblies.Assemblies));
        Assert.Single(d.AppState.Assemblies.Assemblies);
    }

    [Fact]
    public void Reloading_keeps_the_tree_expanded_as_it_was()
    {
        var d = AppStateDriver.Opened();
        d.Show(d.Fixture("Greeter"));
        var ns = d.Node(AppStateDriver.FixturesNamespace);
        d.Ui.Tree.View.SetExpanded(d.Fixture("Greeter"), true);

        d.Start((a, ct) => a.AssemblyReloading.RefreshAssembliesAsync(a, Trees.ListExpandedPaths(d.Ui.Tree), ct));
        d.Settle();

        var view = d.Ui.Tree.View;
        Assert.NotSame(ns, d.Node(AppStateDriver.FixturesNamespace));
        Assert.True(view.IsExpanded(d.Node()));
        Assert.True(view.IsExpanded(d.Node(AppStateDriver.FixturesNamespace)));
        Assert.True(view.IsExpanded(d.Fixture("Greeter")));
        Assert.Same(d.Fixture("Greeter"), view.Selected);
    }

    [Fact]
    public void Going_back_after_a_reload_lands_on_the_reloaded_node()
    {
        var d = AppStateDriver.Opened();
        var before = d.Fixture("Greeter");
        d.Show(before);
        d.Show(d.Fixture("Farewell"));

        d.Start((a, ct) => a.AssemblyReloading.RefreshAssembliesAsync(a, Trees.ListExpandedPaths(d.Ui.Tree), ct));
        d.Settle();
        d.Start(GoBackAsync);
        d.Settle();

        var shown = d.AppState.Tabs.Active!.Node!;
        Assert.Empty(d.AppState.Errors.Entries.Select(e => e.Words().FullText));
        Assert.Equal("Greeter", shown.Text);
        Assert.NotSame(before, shown);
        Assert.Same(d.Fixture("Greeter"), shown);
        Assert.Same(shown, d.Ui.Tree.View.Selected);
    }

    [Fact]
    public void Going_back_to_what_the_tab_shows_returns_to_where_it_was_left_without_decompiling_again()
    {
        var d = AppStateDriver.Opened();
        d.Show(d.Fixture("Greeter"));
        d.Show(d.Fixture("Farewell"), inNewTab: true);
        ActivateTab(d.Ui, d.AppState.Tabs.Open[0]);
        d.Settle();
        var tab = d.AppState.Tabs.Active!;
        var document = tab.Document;

        d.Start(GoBackAsync);
        d.NextFrame();

        var view = DocumentPanels.GetView(d.Ui.Documents, tab).Code;
        Assert.False(tab.IsBusy);
        Assert.Same(document, tab.Document);
        Assert.IsType<PendingScroll.Restore>(view.Pending);
    }

    [Fact]
    public void A_navigation_result_does_not_override_a_newer_choice()
    {
        var d = AppStateDriver.Opened();
        d.Show(d.Fixture("Greeter"));
        var document = d.AppState.Tabs.Active!.Document;
        var (line, column) = document.GetPosition("Name;");
        var reference = Assert.IsAssignableFrom<CodeReference.Navigable>(document.SpanAt(line, column).Reference);
        var farewell = d.Fixture("Farewell");

        d.Start((a, ct) => a.NavigateHandler.HandleAsync(a, reference, inNewTab: false, ct));
        ShowNode(d.AppState, farewell);
        d.Start((a, ct) => a.NavigateHandler.HandleAsync(a, reference, inNewTab: true, ct));
        ActivateTab(d.Ui, d.AppState.Tabs.Open[0]);
        d.NextFrame();
        d.Settle();

        var tab = Assert.Single(d.AppState.Tabs.Open);
        Assert.Same(farewell, tab.Node);
        Assert.False(d.AppState.IsResolving);
    }

    [Fact]
    public void A_navigation_lands_in_the_tab_it_was_asked_from()
    {
        var d = AppStateDriver.Opened();
        d.Show(d.Fixture("Greeter"));
        var document = d.AppState.Tabs.Active!.Document;
        var (line, column) = document.GetPosition("Name;");

        var reference = Assert.IsAssignableFrom<CodeReference.Navigable>(document.SpanAt(line, column).Reference);
        d.Start((a, ct) => a.NavigateHandler.HandleAsync(a, reference, inNewTab: false, ct));
        d.Settle();

        var tab = Assert.Single(d.AppState.Tabs.Open);
        Assert.Equal("Name : string", tab.Node!.Text);
        Assert.Same(tab.Node, d.Ui.Tree.View.Selected);
    }

    [Fact]
    public void A_document_is_only_scrolled_to_a_definition_it_has()
    {
        var d = AppStateDriver.Opened();
        var tab = d.AppState.Tabs.Active!;

        var view = DocumentPanels.GetView(d.Ui.Documents, tab).Code;

        ShowNode(d.AppState, d.Node(AppStateDriver.FixturesNamespace));
        d.NextFrame();
        Assert.IsNotType<PendingScroll.Reveal>(view.Pending);
        d.Settle();
        Assert.IsNotType<PendingScroll.Reveal>(view.Pending);

        d.Show(d.Fixture("Greeter"));
        var reveal = Assert.IsType<PendingScroll.Reveal>(view.Pending);
        Assert.Equal(Documents.FindDefinitionLine(tab.Document, tab.Node!), reveal.Line);
    }

    [Fact]
    public void Search_keeps_its_results_and_selection_when_navigation_opens_an_assembly()
    {
        var d = AppStateDriver.Opened();
        d.Show(d.Fixture("Greeter"));
        var search = d.Ui.Search;
        search.Query = "IsLeapYear";
        Searches.Update(search, d.AppState.Assemblies, 0, true);
        Searches.Update(search, d.AppState.Assemblies, 1, true);
        d.Settle();
        var rows = search.Rows.ToArray();
        Assert.Single(rows);
        Searches.Select(search, search.Rows[0]);
        var selected = search.SelectedRow!.Node;

        var document = d.AppState.Tabs.Active!.Document;
        var (line, column) = document.GetPosition("Length");
        var reference = Assert.IsAssignableFrom<CodeReference.Navigable>(document.SpanAt(line, column).Reference);
        d.Start((a, ct) => a.NavigateHandler.HandleAsync(a, reference, inNewTab: true, ct));
        d.Settle();
        Assert.Equal(2, d.AppState.Assemblies.Assemblies.Count);
        Searches.Update(search, d.AppState.Assemblies, 2, true);
        d.Settle();

        Assert.All(rows, r => Assert.Contains(r, search.Rows));
        Assert.True(search.Rows.Count > rows.Length);
        Assert.Same(selected, search.SelectedRow!.Node);
    }

    [Fact]
    public void Search_drops_hits_in_a_closed_assembly_and_keeps_the_selection()
    {
        var d = AppStateDriver.Opened();
        var greeter = d.Fixture("Greeter");
        var document = greeter.Decompile(DecompilationLanguage.CSharp);
        var (line, column) = document.GetPosition("Length");
        Assert.NotNull(d.AppState.Assemblies.FindNode(Assert.IsAssignableFrom<CodeReference.Navigable>(document.SpanAt(line, column).Reference)));
        var search = d.Ui.Search;
        search.Query = "IsLeapYear";
        Searches.Update(search, d.AppState.Assemblies, 0, true);
        Searches.Update(search, d.AppState.Assemblies, 1, true);
        d.Settle();
        Assert.Contains(search.Rows, r => r.Node.Assembly != greeter.Assembly);
        Searches.Select(search, search.Rows.First(r => r.Node.Assembly == greeter.Assembly));
        var selected = search.SelectedRow!.Node;

        AssemblyListing.CloseAssembly(d.AppState.Assemblies, d.AppState.Assemblies.Assemblies.Single(a => a.Name == "System.Private.CoreLib"));
        Searches.Update(search, d.AppState.Assemblies, 2, true);
        d.Settle();

        Assert.All(search.Rows, r => Assert.Equal(greeter.Assembly, r.Node.Assembly));
        Assert.Same(selected, search.SelectedRow!.Node);
    }

    [Fact]
    public void A_closed_search_panel_does_not_search()
    {
        var d = AppStateDriver.Opened();
        var search = d.Ui.Search;
        search.Query = "Greet";

        Searches.Update(search, d.AppState.Assemblies, 0, false);
        Searches.Update(search, d.AppState.Assemblies, 5, false);

        Assert.False(search.IsBusy);
        Assert.Empty(search.Rows);
    }

    [Fact]
    public void The_search_box_asked_to_take_focus_does_not_once_its_panel_closed_before_it_was_drawn()
    {
        var d = AppStateDriver.Opened();
        ScreenLayout.OpenSearch(d.Ui);

        OnPanelClosed(d.Ui, DockPanels.Search);

        Assert.False(Searches.TryTakeFocus(d.Ui.Search));
    }

    [Fact]
    public void A_search_that_fails_is_reported_unless_another_replaced_it()
    {
        var posted = PostedActions.OfThisTest();
        var reported = new List<string>();
        var replaced = new HeldSearch();
        var current = new HeldSearch();
        var streams = new Queue<HeldSearch>([replaced, current]);
        var search = new Search
        {
            Work = new BackgroundWork(),
            ReportError = problem => reported.Add(ProblemTexts.Describe(Strings.En, problem).Title),
            Stream = (_, _, _, _) => streams.Dequeue().StreamAsync(),
        };
        var assemblies = new AssemblyList();
        search.Query = "Greet";
        Searches.Update(search, assemblies, 0, true);
        Searches.Update(search, assemblies, 1, true);

        search.Query = "Farewell";
        Searches.Update(search, assemblies, 2, true);
        Searches.Update(search, assemblies, 3, true);
        replaced.Fail();
        posted.Run();
        var afterReplaced = reported.Count;
        current.Fail();
        posted.Run();

        Assert.Empty(streams);
        Assert.Equal(0, afterReplaced);
        Assert.Equal(["Search failed"], reported);
    }

    /// <summary>A search's stream that yields nothing until it is told to fail, whatever cancels it.</summary>
    private sealed class HeldSearch
    {
        private readonly TaskCompletionSource<IReadOnlyList<SearchResult>> _next = new();

        public async IAsyncEnumerable<IReadOnlyList<SearchResult>> StreamAsync()
        {
            yield return await _next.Task;
        }

        public void Fail()
        {
            _next.SetException(new InvalidOperationException("The search broke."));
        }
    }

    [Fact]
    public void Search_selection_moves_by_keys_within_the_results()
    {
        var d = AppStateDriver.Opened();
        var search = d.Ui.Search;
        search.Query = "e";
        Searches.Update(search, d.AppState.Assemblies, 0, true);
        Searches.Update(search, d.AppState.Assemblies, 1, true);
        d.Settle();
        Assert.True(search.Rows.Count > 3);

        Searches.Move(search, 1);
        Assert.Same(search.Rows[0], search.SelectedRow);
        Searches.Move(search, 2);
        Assert.Same(search.Rows[2], search.SelectedRow);
        Searches.Move(search, -10);
        Assert.Same(search.Rows[0], search.SelectedRow);
        Searches.Move(search, search.Rows.Count);
        Assert.Same(search.Rows[search.Rows.Count - 1], search.SelectedRow);
    }

    [Fact]
    public void Results_streamed_in_many_batches_end_up_in_sort_order()
    {
        var d = AppStateDriver.Opened();
        d.Start((a, ct) => a.AssemblyOpening.OpenAssemblyAsync(a, typeof(object).Assembly.Location, ct));
        d.RunUntil(() => d.AppState.Assemblies.Assemblies.Count == 2);
        var search = d.Ui.Search;
        Searches.SortBy(search, SearchSort.Name);
        search.Query = "e";

        Searches.Update(search, d.AppState.Assemblies, 0, true);
        Searches.Update(search, d.AppState.Assemblies, 1, true);
        d.Settle();

        var sorted = search.Rows
            .OrderBy(r => r.Text, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Location, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Node.Assembly.NameAndVersion, StringComparer.OrdinalIgnoreCase);
        Assert.True(search.Rows.Count > 1000);
        Assert.Equal(sorted, search.Rows);
    }

    [Fact]
    public void A_tab_whose_place_is_gone_after_a_reload_says_so_and_reports_it()
    {
        var d = new AppStateDriver();
        using var temp = new TempDir();
        var copy = temp.Copy(AppStateDriver.FixturePath, "fixture.dll");
        d.Start((a, ct) => a.AssemblyOpening.OpenAssemblyAsync(a, copy, ct));
        d.RunUntil(() => d.AppState.Tabs.Active?.Node is not null);
        d.Settle();
        var greeter = d.AppState.Tabs.Active!.Node!.Child(AppStateDriver.FixturesNamespace, "Greeter");
        d.Show(greeter);
        File.Delete(copy);

        d.Start((a, ct) => a.AssemblyReloading.RefreshAssembliesAsync(a, Trees.ListExpandedPaths(d.Ui.Tree), ct));
        d.Settle();

        var tab = Assert.Single(d.AppState.Tabs.Open);
        Assert.Null(tab.Node);
        Assert.Equal("Greeter", tab.Title);
        Assert.Equal(new TabContent.Gone(tab.Location!, "Greeter"), tab.Content);
        Assert.Contains("gone", TabPlaceholders.Write(d.Ui.Strings, tab.Content));
        Assert.IsType<FailedAssembly>(Assert.Single(d.AppState.Assemblies.Listed));
        Assert.Contains(d.AppState.Errors.Entries, e => e.Words().Title.Contains("Greeter"));
    }
}

public sealed class NamespacePageTests
{
    [Fact]
    public void A_type_named_on_its_namespaces_page_opens_when_clicked()
    {
        var app = AppDriver.Opened();
        var d = app.Driver;
        d.Show(d.Node(AppStateDriver.FixturesNamespace));
        var tab = d.AppState.Tabs.Active!;
        var (line, column) = tab.Document.GetPosition("Greeter");
        var span = tab.Document.SpanAt(line, column);
        app.Run(3);

        var view = app.Frames.Canvas.GlyphRuns.First(r => r.Text == "Greeter");
        app.Frames.Click(view.Origin.X + 4f, app.Frames.Height - view.Origin.Y + 4f);
        d.Settle();

        Assert.False(span.IsDefinition);
        Assert.Equal("Greeter", tab.Node!.Text);
        Assert.Equal(NodeIcon.Class, tab.Node.Icon);
    }
}
