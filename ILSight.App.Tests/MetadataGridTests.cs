using ZGF.Desktop;
using ZGF.Geometry;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.Core.Metadata;
using ZGF.KeyboardModule;
using static ILSight.App.Tests.BuiltInGlyphs;
using ILSight.MetadataGridArea;
using ILSight.CommandArea;
using ILSight.MetadataArea;
using ILSight.SettingArea;
using static ILSight.App.Tests.TestPaths;

namespace ILSight.App.Tests;

public sealed class MetadataGridTests
{
    private static async Task<BrowseNode> TableAsync(AppDriver app, string name)
    {
        return await (await app.NodeAsync("Metadata", "Tables")).TableAsync(name);
    }

    private static async Task<(AppDriver App, DocumentTab Tab, MetadataGrid Grid)> ShowTableAsync(string name, int frames = 3)
    {
        var app = AppDriver.Opened(width: 2400f);
        app.Show(await TableAsync(app, name), frames);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var grid = tab.Grid;
        Assert.NotNull(grid);
        return (app, tab, grid);
    }

    private static MetadataGridState GridOf(AppDriver app, DocumentTab tab)
    {
        var grid = tab.Grid;
        Assert.NotNull(grid);
        return DocumentPanels.GetGridState(DocumentPanels.GetView(app.Ui.Documents, tab), grid);
    }

    private static async Task<(AppDriver App, DocumentTab Tab, MetadataGrid Grid, int Row)> ShowGreeterRowAsync()
    {
        var (app, tab, grid) = await ShowTableAsync("TypeDef", frames: 0);
        var row = grid.RowNamed("Greeter");
        MetadataGridWidget.RevealRow(GridOf(app, tab), row);
        app.Run(3);
        return (app, tab, grid, row);
    }

    /// <summary>Where <paramref name="text"/> was drawn on the same line as <paramref name="besides"/>.</summary>
    private static UiRect TextBeside(AppDriver app, string text, string besides)
    {
        var line = app.TextRect(besides);
        return app.Frames.TextRects().First(x => x.Text == text && Math.Abs(x.Rect.Y - line.Y) < 1f).Rect;
    }

    /// <summary>Edits the cell at <paramref name="y"/> and drags across its text.</summary>
    private static void Drag(AppDriver app, float fromX, float toX, float y)
    {
        app.Frames.Wait(1).Click(fromX, y).Key(KeyboardKey.F2).Frame();
        app.Frames.Wait(1).Drag(new PointF(fromX, y), new PointF(toX, y)).Release().Frame();
    }

    [Fact]
    public async Task A_table_opens_as_a_grid_of_ILSpys_columns_that_switching_language_leaves_alone()
    {
        var (app, tab, grid) = await ShowTableAsync("TypeDef", frames: 0);

        app.AppState.Decompiler.UseLanguage(DecompilationLanguage.IL);
        app.Settle(2);

        Assert.Same(grid, tab.Grid);
        Assert.Equal(["RID", "Token", "Offset", "Attributes", "Name", "Namespace", "BaseType", "FieldList", "MethodList"], grid.Columns.Select(c => c.Name));
        app.TextRect("RID");
        app.TextRect("<Module>");
    }

    [Fact]
    public async Task Columns_wider_than_the_view_scroll_sideways_with_their_headers()
    {
        var app = AppDriver.Opened(width: 700f);
        app.Show(await TableAsync(app, "TypeDef"), 2);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var grid = tab.Grid;
        Assert.NotNull(grid);
        var methodList = MetadataGrids.GetText(grid, 0, grid.Column("MethodList"));

        GridOf(app, tab).Scroll.X = 10000f;
        app.Run(3);

        var header = app.TextRect("MethodList");
        var cell = app.TextRect(methodList);
        Assert.Equal(header.X, cell.X, 0);
        Assert.InRange(header.X + header.Width, 0f, 700f);
    }

    [Fact]
    public async Task A_text_node_of_the_metadata_decompiles_to_comments()
    {
        var d = AppStateDriver.Opened();

        d.Show(await d.NodeAsync("Metadata", "Tables"));

        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Assert.Null(tab.Grid);
        Assert.StartsWith("// Tables", Documents.GetText(tab.Document));
    }

    [Fact]
    public async Task Going_back_to_a_grid_after_switching_language_returns_to_the_row_it_left()
    {
        var (app, tab, grid, row) = await ShowGreeterRowAsync();
        var baseType = MetadataGrids.GetText(grid, row, grid.Column("BaseType"));
        app.Frames.Click(TextBeside(app, "Greeter", "Greeter"));
        app.Frames.Click(TextBeside(app, baseType, "Greeter"), modifiers: KeyModifiers.Control);
        app.Settle(3);

        app.AppState.Decompiler.UseLanguage(DecompilationLanguage.IL);
        app.Settle(2);
        app.GoBack();
        app.Settle(3);

        Assert.Same(await TableAsync(app, "TypeDef"), tab.Node);
        Assert.Equal(row, GridOf(app, tab).Selected);
    }

    [Fact]
    public async Task Ctrl_clicking_a_token_shows_its_row_and_going_back_returns_to_the_row_it_left()
    {
        var (app, tab, grid, row) = await ShowGreeterRowAsync();
        var baseType = MetadataGrids.GetText(grid, row, grid.Column("BaseType"));
        app.Frames.Click(TextBeside(app, "Greeter", "Greeter"));

        app.Frames.Click(TextBeside(app, baseType, "Greeter"), modifiers: KeyModifiers.Control);
        app.Settle(3);

        var typeRefs = tab.Grid;
        Assert.NotNull(typeRefs);
        Assert.Same(await TableAsync(app, "TypeRef"), tab.Node);
        Assert.Equal("Object", MetadataGrids.GetText(typeRefs, GridOf(app, tab).Selected, typeRefs.Column("Name")));
        app.TextRect("Object");

        app.GoBack();
        app.Settle(3);

        Assert.Same(await TableAsync(app, "TypeDef"), tab.Node);
        Assert.Equal(row, GridOf(app, tab).Selected);
    }

    [Fact]
    public async Task The_tree_menu_shows_a_type_in_metadata_and_going_back_returns_to_its_code()
    {
        var app = AppDriver.Opened(width: 1800f);
        var greeter = await app.NodeAsync(AppStateDriver.FixturesNamespace, "Greeter");
        app.Show(greeter, 3);
        app.Ui.Tree.RevealNode(greeter);
        app.Run(2);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);

        app.Frames.ClickOn(Selector.OfRole(SemanticRole.TreeItem, "Greeter"), PointerButton.Right).Frame();
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Show in metadata")).Frame();
        app.Settle(3);

        var typeDefs = tab.Grid;
        Assert.NotNull(typeDefs);
        Assert.Same(await TableAsync(app, "TypeDef"), tab.Node);
        Assert.Equal("Greeter", MetadataGrids.GetText(typeDefs, GridOf(app, tab).Selected, typeDefs.Column("Name")));

        app.GoBack();
        app.Settle(3);

        Assert.Same(greeter, tab.Node);
    }

    [Fact]
    public async Task The_frame_show_in_metadata_is_picked_in_says_the_app_is_resolving()
    {
        var app = AppDriver.Opened(width: 1800f);
        var greeter = await app.NodeAsync(AppStateDriver.FixturesNamespace, "Greeter");
        app.Show(greeter, 3);
        app.Ui.Tree.RevealNode(greeter);
        app.Run(2);

        app.Frames.ClickOn(Selector.OfRole(SemanticRole.TreeItem, "Greeter"), PointerButton.Right).Frame();
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Show in metadata"));

        Assert.True(app.Frames.Drew(app.Ui.Strings.StatusResolving));
        app.Settle(3);
        Assert.Same(await TableAsync(app, "TypeDef"), app.AppState.Navigation.ActiveTab?.Node);
    }

    [Fact]
    public async Task Clicking_the_link_icon_of_a_hovered_token_shows_its_row()
    {
        var (app, tab, grid, row) = await ShowGreeterRowAsync();
        var token = TextBeside(app, MetadataGrids.GetText(grid, row, grid.Column("BaseType")), "Greeter");

        app.Frames.MovePointer(token.X + 2f, token.Y + token.Height / 2).Frame(2);
        app.Frames.Click(TextBeside(app, Text(Icons.LinkToken), "Greeter"));
        app.Settle(3);

        Assert.Same(await TableAsync(app, "TypeRef"), tab.Node);
    }

    [Fact]
    public async Task A_row_stays_highlighted_while_its_link_icon_is_hovered()
    {
        var (app, _, grid) = await ShowTableAsync("TypeDef");
        var baseType = grid.Column("BaseType");
        var row = Enumerable.Range(0, grid.RowCount).First(r => MetadataGrids.FindLink(grid, r, baseType) is not null);
        var name = MetadataGrids.GetText(grid, row, grid.Column("Name"));
        var token = TextBeside(app, MetadataGrids.GetText(grid, row, baseType), name);
        app.Frames.MovePointer(token.X + 2f, token.Y + token.Height / 2).Frame(2);
        var icon = TextBeside(app, Text(Icons.LinkToken), name);

        app.Frames.MovePointer(icon).Frame(2);

        Assert.True(app.Frames.Drew(Text(Icons.LinkToken)));
        Assert.Contains(app.Frames.Canvas.Rects, r => r.Inputs.Style.BackgroundColor == app.Theme.RowHover
                                                      && Math.Abs(app.Frames.Height - r.Inputs.Position.Top + r.Inputs.Position.Height / 2 - (icon.Y + icon.Height / 2)) < 4f);
    }

    [Fact]
    public async Task Double_clicking_a_cell_selects_its_text_instead_of_decompiling_its_row()
    {
        var (app, tab, _, _) = await ShowGreeterRowAsync();

        app.Frames.DoubleClick(TextBeside(app, "Greeter", "Greeter"));
        app.Settle();

        Assert.Same(await TableAsync(app, "TypeDef"), tab.Node);
        Assert.Contains(app.Frames.Canvas.Rects, r => r.Inputs.Style.BackgroundColor == app.Theme.TextSelection);
    }

    [Fact]
    public async Task Enter_on_the_selected_row_decompiles_it_and_Ctrl_C_copies_the_current_cell()
    {
        var (app, tab, _, _) = await ShowGreeterRowAsync();
        app.Frames.Click(TextBeside(app, "Greeter", "Greeter"));

        app.Frames.Key(Shortcuts.Copy).Frame();
        var copied = app.Frames.Clipboard.Text;
        app.Frames.Key(KeyboardKey.Enter).Frame();
        app.Settle();

        Assert.Equal("Greeter", copied);
        Assert.Same(await app.FixtureAsync("Greeter"), tab.Node);
    }

    [Fact]
    public async Task Clicking_a_header_sorts_by_its_column_and_again_the_other_way()
    {
        var (app, tab, grid, _) = await ShowGreeterRowAsync();
        var name = grid.Column("Name");

        app.Frames.ClickOn(Selector.WithText("Name"));
        app.Settle();
        var ascendingOrder = GridOf(app, tab).Order;
        Assert.NotNull(ascendingOrder);
        var ascending = ascendingOrder.Select(r => MetadataGrids.GetText(grid, r, name)).ToList();
        app.Frames.ClickOn(Selector.WithText("Name"));
        app.Settle();
        var descendingOrder = GridOf(app, tab).Order;
        Assert.NotNull(descendingOrder);
        var descending = descendingOrder.Select(r => MetadataGrids.GetText(grid, r, name)).ToList();

        Assert.Equal(ascending.Order(StringComparer.CurrentCulture), ascending);
        Assert.Equal(descending.OrderDescending(StringComparer.CurrentCulture), descending);
    }

    [Fact]
    public async Task Sorting_a_large_table_shows_it_sorting_until_the_sorted_rows_land()
    {
        var app = AppDriver.Opened(width: 2400f);
        var path = typeof(object).Assembly.Location;
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [PathOf(path)], ct));
        app.Driver.RunUntil(() => app.AppState.Assemblies.IsListed(PathOf(path)));
        app.Settle();
        var coreLib = app.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == path);
        app.Show(await (await coreLib.ReadRootAsync().GetChildAsync("Metadata", "Tables")).TableAsync("Param"), 3);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var grid = tab.Grid;
        Assert.NotNull(grid);
        var name = grid.Column("Name");

        app.Frames.ClickOn(Selector.WithText("Name"));
        var sorting = app.Frames.Canvas.Texts.Any(t => t.Inputs.Text == "Sorting…");
        var shownWhileSorting = GridOf(app, tab).Order;
        app.Settle(1);

        var order = GridOf(app, tab).Order;
        Assert.True(sorting);
        Assert.Null(shownWhileSorting);
        Assert.NotNull(order);
        var names = order.Select(r => MetadataGrids.GetText(grid, r, name)).ToList();
        Assert.Equal(names.Order(StringComparer.CurrentCulture), names);
        Assert.DoesNotContain(app.Frames.Canvas.Texts, t => t.Inputs.Text == "Sorting…");
    }

    [Fact]
    public async Task Only_the_newest_sort_asked_for_lands()
    {
        var (app, _, grid) = await ShowTableAsync("TypeDef");
        var state = MetadataGridWidget.CreateState(grid);
        var views = new MetadataGridViews();
        var name = grid.Column("Name");
        MetadataGridWidget.UpdateView(state, app.Ui.MetadataFilters, views, isClosed: false);
        MetadataGridWidget.SortBy(state, name);
        MetadataGridWidget.UpdateView(state, app.Ui.MetadataFilters, views, isClosed: false);
        var older = Assert.Single(views.TakeAsked());
        MetadataGridWidget.SortBy(state, name);
        MetadataGridWidget.UpdateView(state, app.Ui.MetadataFilters, views, isClosed: false);
        var newer = Assert.Single(views.TakeAsked());
        var handler = new ArrangeMetadataGridHandler(new FakeArrangeMetadataGridHandlerPorts());

        await handler.HandleAsync(newer, CancellationToken.None);
        var landed = state.Order;
        await handler.HandleAsync(older, CancellationToken.None);

        Assert.Equal(MetadataGrids.OrderRows(grid, name, descending: true), landed);
        Assert.Same(landed, state.Order);
        Assert.False(state.IsViewUnderWay);
    }

    [Fact]
    public async Task A_closed_assemblys_row_menu_shows_its_links_disabled_saying_why()
    {
        var (app, tab, grid, row) = await ShowGreeterRowAsync();
        var greeter = TextBeside(app, MetadataGrids.GetText(grid, row, grid.Column("Name")), "Greeter");
        var fixtures = app.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == AppStateDriver.FixturePath);
        var closed = app.Ui.Strings.DocumentClosed(fixtures.FilePath.FileName);
        using (app.Driver.Turn())
        {
            Commands.CloseAssemblies(app.AppState, [fixtures]);
        }

        app.Settle(2);

        app.Frames.Click(greeter, PointerButton.Right).Frame(2);
        var disabled = new[] { "Go to definition", "Open in new tab" }
            .Select(label => app.Frames.Find(Selector.OfRole(SemanticRole.MenuItem, label)).Has(SemanticStates.Disabled))
            .ToList();
        var why = app.Frames.Drew(closed);
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Go to definition")).Frame(2);
        app.Settle(2);

        Assert.Equal([true, true], disabled);
        Assert.True(why);
        Assert.Same(tab, app.AppState.Navigation.ActiveTab);
        Assert.Same(grid, tab.Grid);
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public async Task Hovering_a_flags_cell_breaks_its_bits_down()
    {
        var (app, _, grid, row) = await ShowGreeterRowAsync();
        var attributes = TextBeside(app, MetadataGrids.GetText(grid, row, grid.Column("Attributes")), "Greeter");

        app.Frames.MovePointer(attributes.X + 2f, attributes.Y + attributes.Height / 2).Frame(90);

        app.TextRect("Visibility");
        app.TextRect("Public (0001)");
        app.TextRect("BeforeFieldInit (100000)");
    }

    /// <summary>The funnel drawn in the header of <paramref name="column"/>.</summary>
    private static UiRect FunnelOf(AppDriver app, string column)
    {
        var header = app.TextRect(column);
        return app.Frames.TextRects()
            .Where(t => t.Text == Text(Icons.MetadataFilter))
            .Select(t => t.Rect)
            .Where(r => r.X > header.X && Math.Abs(r.Y + r.Height / 2 - (header.Y + header.Height / 2)) < 4f)
            .MinBy(r => r.X);
    }

    [Fact]
    public async Task Typing_in_a_columns_filter_keeps_the_rows_that_match_and_Escape_clears_it()
    {
        var (app, tab, grid) = await ShowTableAsync("TypeDef");
        var name = grid.Column("Name");

        app.Frames.Click(FunnelOf(app, "Name"));
        app.Frames.Type("greet").Frame(2);
        app.Settle();
        var filtered = GridOf(app, tab).View.Select(r => MetadataGrids.GetText(grid, r, name)).ToList();
        app.Frames.Key(KeyboardKey.Escape).Frame(2);
        app.Settle();

        Assert.Contains("Greeter", filtered);
        Assert.All(filtered, n => Assert.Contains("greet", n, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(grid.RowCount, GridOf(app, tab).View.Length);
    }

    [Fact]
    public async Task A_flags_filter_picks_values_in_its_popup_and_is_kept_for_that_column_of_every_table_of_the_kind()
    {
        var (app, tab, grid) = await ShowTableAsync("TypeDef");
        var attributes = grid.Column("Attributes");

        app.Frames.Click(FunnelOf(app, "Attributes"));
        app.Frames.ClickOn(Selector.WithText("Public (0001)")).Frame(2);

        var visibility = System.Reflection.TypeAttributes.VisibilityMask;
        Assert.NotEmpty(GridOf(app, tab).View);
        Assert.All(GridOf(app, tab).View, r =>
            Assert.Equal(1u, Convert.ToUInt32(MetadataGrids.GetText(grid, r, attributes), 16) & (uint)visibility));
        app.TextRect("Visibility = Public");

        using var temp = new TempDir();
        var settings = temp.Settings();
        settings.FlagsFilters = MetadataGridWidget.ListSaved(app.Ui.MetadataFilters);
        Assert.IsType<SettingsWriteResult.Written>(TempSettings.Save(settings));
        var saved = temp.Settings().FlagsFilters;
        var restored = MetadataGridWidget.GetFilter(MetadataGridWidget.CreateFilters(saved), grid, attributes);
        Assert.Equal("Visibility = Public", FilterStatePresenter.Describe(restored));
    }

    [Fact]
    public async Task Hovering_a_headers_flags_value_breaks_its_bits_down()
    {
        var app = AppDriver.Opened(width: 1800f, height: 1200f);
        app.Show(await app.NodeAsync("Metadata", "COFF Header"), 3);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var grid = tab.Grid;
        Assert.NotNull(grid);
        var row = Enumerable.Range(0, grid.RowCount).Single(r => MetadataGrids.GetText(grid, r, grid.Column("Member")) == "Characteristics");
        var value = TextBeside(app, MetadataGrids.GetText(grid, row, grid.Column("Value")), "Characteristics");

        app.Frames.MovePointer(value.X + 2f, value.Y + value.Height / 2).Frame(90);

        app.TextRect("File is executable (0002)");
        app.TextRect("DLL (2000)");
    }

    [Fact]
    public async Task Dragging_across_a_cell_selects_part_of_its_text_for_Ctrl_C()
    {
        var (app, tab, _, row) = await ShowGreeterRowAsync();
        var name = TextBeside(app, "Greeter", "Greeter");
        var advance = name.Width / "Greeter".Length;

        Drag(app, name.X + 1f, name.X + advance * 4, name.Y + name.Height / 2);
        app.Frames.Key(Shortcuts.Copy).Frame();

        Assert.Equal("Gree", app.Frames.Clipboard.Text);
        Assert.Equal(row, GridOf(app, tab).Selected);
    }

    [Fact]
    public async Task The_selected_cells_outline_is_drawn_over_the_text_selected_in_it()
    {
        var (app, _, _, _) = await ShowGreeterRowAsync();
        var name = TextBeside(app, "Greeter", "Greeter");

        Drag(app, name.X + 1f, name.X + name.Width - 1f, name.Y + name.Height / 2);
        app.Run();

        var rects = app.Frames.Canvas.Rects;
        var highlight = rects.Last(r => r.Inputs.Style.BackgroundColor == app.Theme.TextSelection).Sequence;
        var outline = rects.Last(r => r.Inputs.Style.BorderColor.Top.Value == app.Theme.FocusRing).Sequence;
        Assert.True(outline > highlight);
    }

    [Fact]
    public async Task Clicking_a_token_selects_its_cell_for_Ctrl_C_instead_of_following_it()
    {
        var (app, tab, grid, row) = await ShowGreeterRowAsync();
        var baseType = MetadataGrids.GetText(grid, row, grid.Column("BaseType"));

        app.Frames.Click(TextBeside(app, baseType, "Greeter"));
        app.Settle();
        app.Frames.Key(Shortcuts.Copy).Frame();

        Assert.Same(await TableAsync(app, "TypeDef"), tab.Node);
        Assert.Equal(baseType, app.Frames.Clipboard.Text);
    }

    [Fact]
    public async Task Moving_to_another_cell_drops_the_text_selected_in_the_last()
    {
        var (app, _, _, _) = await ShowGreeterRowAsync();
        var name = TextBeside(app, "Greeter", "Greeter");
        Drag(app, name.X + 1f, name.X + name.Width / 2, name.Y + name.Height / 2);

        app.Frames.Key(KeyboardKey.LeftArrow).Frame();
        app.Frames.Key(KeyboardKey.RightArrow).Frame();
        app.Frames.Key(Shortcuts.Copy).Frame();

        Assert.Equal("Greeter", app.Frames.Clipboard.Text);
    }

    [Fact]
    public async Task Selecting_debug_information_shows_its_decoded_value_under_it()
    {
        var app = AppDriver.Opened(width: 1800f, height: 900f);
        var table = await (await app.NodeAsync("Debug Metadata (From portable PDB)", "Tables")).TableAsync("CustomDebugInformation");
        app.Show(table);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var grid = tab.Grid;
        Assert.NotNull(grid);
        var kind = grid.Column("KindString");
        var options = Enumerable.Range(0, grid.RowCount).Single(r => MetadataGrids.GetText(grid, r, kind).StartsWith("Compilation Options"));

        MetadataGridWidget.RevealRow(GridOf(app, tab), options);
        app.Run(4);

        Assert.Equal(options, GridOf(app, tab).Selected);
        Assert.True(app.TextRect("language").Y > TextBeside(app, MetadataGrids.GetText(grid, options, kind), MetadataGrids.GetText(grid, options, kind)).Y);
    }

    /// <summary>Where each of <paramref name="rows"/> stands, by its token cell.</summary>
    private static float[] FindTops(AppDriver app, MetadataGrid grid, params int[] rows)
    {
        var token = grid.Column("Token");
        return rows.Select(row => app.TextRect(MetadataGrids.GetText(grid, row, token)).Y).ToArray();
    }

    [Fact]
    public async Task An_open_cell_makes_its_row_as_tall_as_its_longest_and_leaves_the_others_alone()
    {
        var (app, tab, grid) = await ShowTableAsync("TypeDef");
        var state = GridOf(app, tab);
        var line = state.RowHeight;

        state.Open[(3, grid.Column("Name"))] = 2;
        state.Open[(1, grid.Column("Name"))] = 3;
        state.Open[(1, grid.Column("Namespace"))] = 5;
        app.Run(3);

        var tops = FindTops(app, grid, 0, 1, 2, 3, 4);
        Assert.Equal(line, tops[1] - tops[0], 0.01f);
        Assert.Equal(5 * line, tops[2] - tops[1], 0.01f);
        Assert.Equal(line, tops[3] - tops[2], 0.01f);
        Assert.Equal(2 * line, tops[4] - tops[3], 0.01f);
    }

    [Fact]
    public async Task Tall_rows_are_listed_in_view_order_as_a_virtual_scroll_needs_them()
    {
        var (app, tab, grid) = await ShowTableAsync("TypeDef");
        var state = GridOf(app, tab);
        var name = grid.Column("Name");
        state.Open[(3, name)] = 2;
        state.Open[(1, name)] = 3;

        MetadataGridWidget.MeasureTallRows(state, 10f, 0f);

        Assert.Equal([new TallRow(1, 30f), new TallRow(3, 20f)], state.TallRows);
        Assert.Equal(new RowHeights { Cells = 30f, Details = 0f }, state.TallRowsByIndex[1]);
    }

    [Fact]
    public async Task A_row_the_filter_hides_is_no_tall_row()
    {
        var (app, tab, grid) = await ShowTableAsync("TypeDef");
        var state = GridOf(app, tab);
        var name = grid.Column("Name");
        state.Filters[name] = new MetadataColumnFilter.ByText { Text = MetadataGrids.GetText(grid, 1, name) };
        app.Run();
        app.Settle(1);
        state.Open[(2, name)] = 4;

        MetadataGridWidget.MeasureTallRows(state, 10f, 0f);

        Assert.Empty(state.TallRows);
        Assert.Empty(state.TallRowsByIndex);
    }

    [Fact]
    public async Task An_open_cell_in_a_row_the_filter_hides_makes_no_row_taller()
    {
        var (app, tab, grid) = await ShowTableAsync("TypeDef");
        var state = GridOf(app, tab);
        var name = grid.Column("Name");
        var line = state.RowHeight;

        state.Filters[name] = new MetadataColumnFilter.ByText { Text = $"/^({MetadataGrids.GetText(grid, 1, name)}|{MetadataGrids.GetText(grid, 3, name)})$/" };
        state.Open[(2, name)] = 4;
        app.Run();
        app.Settle(3);

        var tops = FindTops(app, grid, 1, 3);
        Assert.Equal(line, tops[1] - tops[0], 0.01f);
    }

    [Fact]
    public async Task The_selected_rows_details_go_under_its_tallest_cell()
    {
        var app = AppDriver.Opened(width: 1800f, height: 900f);
        app.Show(await (await app.NodeAsync("Debug Metadata (From portable PDB)", "Tables")).TableAsync("CustomDebugInformation"));
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var grid = tab.Grid;
        Assert.NotNull(grid);
        var state = GridOf(app, tab);
        var kind = grid.Column("KindString");
        var selected = Enumerable.Range(0, grid.RowCount - 1).First(r => MetadataGrids.GetText(grid, r, kind).StartsWith("Compilation Options"));
        MetadataGridWidget.RevealRow(state, selected);
        app.Run(4);
        var closed = FindTops(app, grid, selected, selected + 1);

        state.Open[(selected, kind)] = 3;
        app.Run(3);

        var open = FindTops(app, grid, selected, selected + 1);
        Assert.True(closed[1] - closed[0] > state.RowHeight);
        Assert.Equal(2 * state.RowHeight, open[1] - open[0] - (closed[1] - closed[0]), 0.01f);
    }
}
