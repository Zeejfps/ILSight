using System.Text.Json;
using ZGF.Desktop;
using ZGF.Geometry;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.Core.Metadata;
using ZGF.KeyboardModule;
using static ILSight.Navigation;
using static ILSight.Preferences;
using static ILSight.App.Tests.BuiltInGlyphs;

namespace ILSight.App.Tests;

public sealed class MetadataGridTests
{
    private static BrowseNode Table(AppDriver app, string name) => app.Node("Metadata", "Tables").Table(name);

    private static (AppDriver App, DocumentTab Tab, MetadataGrid Grid) ShowTable(string name, int frames = 3)
    {
        var app = AppDriver.Opened(width: 1800f);
        app.Show(Table(app, name), frames);
        var tab = app.AppState.Tabs.Active!;
        return (app, tab, tab.Grid!);
    }

    private static MetadataGridState GridOf(AppDriver app, DocumentTab tab) => DocumentPanels.GetGridState(app.Ui.Documents.ViewOf(tab), tab.Grid!);

    private static (AppDriver App, DocumentTab Tab, MetadataGrid Grid, int Row) ShowGreeterRow()
    {
        var (app, tab, grid) = ShowTable("TypeDef", frames: 0);
        var row = grid.RowNamed("Greeter");
        GridOf(app, tab).Reveal(row);
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
    public void A_table_opens_as_a_grid_of_ILSpys_columns_that_switching_language_leaves_alone()
    {
        var (app, tab, grid) = ShowTable("TypeDef", frames: 0);

        SetLanguage(app.AppState, DecompilationLanguage.IL);
        app.Settle(2);

        Assert.Same(grid, tab.Grid);
        Assert.Equal(["RID", "Token", "Offset", "Attributes", "Name", "Namespace", "BaseType", "FieldList", "MethodList"], grid.Columns.Select(c => c.Name));
        app.TextRect("RID");
        app.TextRect("<Module>");
    }

    [Fact]
    public void Columns_wider_than_the_view_scroll_sideways_with_their_headers()
    {
        var app = AppDriver.Opened(width: 700f);
        app.Show(Table(app, "TypeDef"), 2);
        var tab = app.AppState.Tabs.Active!;
        var grid = tab.Grid!;
        var methodList = grid.Text(0, grid.Column("MethodList"));

        GridOf(app, tab).Scroll.X = 10000f;
        app.Run(3);

        var header = app.TextRect("MethodList");
        var cell = app.TextRect(methodList);
        Assert.Equal(header.X, cell.X, 0);
        Assert.InRange(header.X + header.Width, 0f, 700f);
    }

    [Fact]
    public void A_text_node_of_the_metadata_decompiles_to_comments()
    {
        var d = AppStateDriver.Opened();

        d.Show(d.Node("Metadata", "Tables"));

        Assert.Null(d.AppState.Tabs.Active!.Grid);
        Assert.StartsWith("// Tables", Documents.GetText(d.AppState.Tabs.Active.Document));
    }

    [Fact]
    public void Going_back_to_a_grid_after_switching_language_returns_to_the_row_it_left()
    {
        var (app, tab, grid, row) = ShowGreeterRow();
        var baseType = grid.Text(row, grid.Column("BaseType"));
        app.Frames.Click(TextBeside(app, "Greeter", "Greeter"));
        app.Frames.Click(TextBeside(app, baseType, "Greeter"), modifiers: KeyModifiers.Control);
        app.Settle(3);

        SetLanguage(app.AppState, DecompilationLanguage.IL);
        app.Settle(2);
        GoBack(app.AppState);
        app.Settle(3);

        Assert.Same(Table(app, "TypeDef"), tab.Node);
        Assert.Equal(row, GridOf(app, tab).Selected);
    }

    [Fact]
    public void Ctrl_clicking_a_token_shows_its_row_and_going_back_returns_to_the_row_it_left()
    {
        var (app, tab, grid, row) = ShowGreeterRow();
        var baseType = grid.Text(row, grid.Column("BaseType"));
        app.Frames.Click(TextBeside(app, "Greeter", "Greeter"));

        app.Frames.Click(TextBeside(app, baseType, "Greeter"), modifiers: KeyModifiers.Control);
        app.Settle(3);

        var typeRefs = tab.Grid!;
        Assert.Same(Table(app, "TypeRef"), tab.Node);
        Assert.Equal("Object", typeRefs.Text(GridOf(app, tab).Selected, typeRefs.Column("Name")));
        app.TextRect("Object");

        GoBack(app.AppState);
        app.Settle(3);

        Assert.Same(Table(app, "TypeDef"), tab.Node);
        Assert.Equal(row, GridOf(app, tab).Selected);
    }

    [Fact]
    public void The_tree_menu_shows_a_type_in_metadata_and_going_back_returns_to_its_code()
    {
        var app = AppDriver.Opened(width: 1800f);
        var greeter = app.Node(AppStateDriver.FixturesNamespace, "Greeter");
        app.Show(greeter, 3);
        app.Ui.Tree.Reveal(greeter);
        app.Run(2);
        var tab = app.AppState.Tabs.Active!;
        var row = app.TextRect("Greeter");

        app.Frames.Click(row.X + 2f, row.Y + row.Height / 2, PointerButton.Right).Frame();
        app.ClickMenuItem("Show in metadata").Frame();
        app.Settle(3);

        var typeDefs = tab.Grid!;
        Assert.Same(Table(app, "TypeDef"), tab.Node);
        Assert.Equal("Greeter", typeDefs.Text(GridOf(app, tab).Selected, typeDefs.Column("Name")));

        GoBack(app.AppState);
        app.Settle(3);

        Assert.Same(greeter, tab.Node);
    }

    [Fact]
    public void Clicking_the_link_icon_of_a_hovered_token_shows_its_row()
    {
        var (app, tab, grid, row) = ShowGreeterRow();
        var token = TextBeside(app, grid.Text(row, grid.Column("BaseType")), "Greeter");

        app.Frames.MovePointer(token.X + 2f, token.Y + token.Height / 2).Frame(2);
        app.Frames.Click(TextBeside(app, Text(Icons.LinkToken), "Greeter"));
        app.Settle(3);

        Assert.Same(Table(app, "TypeRef"), tab.Node);
    }

    [Fact]
    public void A_row_stays_highlighted_while_its_link_icon_is_hovered()
    {
        var (app, _, grid) = ShowTable("TypeDef");
        var baseType = grid.Column("BaseType");
        var row = Enumerable.Range(0, grid.RowCount).First(r => grid.Link(r, baseType) != null);
        var name = grid.Text(row, grid.Column("Name"));
        var token = TextBeside(app, grid.Text(row, baseType), name);
        app.Frames.MovePointer(token.X + 2f, token.Y + token.Height / 2).Frame(2);
        var icon = TextBeside(app, Text(Icons.LinkToken), name);

        app.Frames.MovePointer(icon).Frame(2);

        Assert.True(app.Frames.Drew(Text(Icons.LinkToken)));
        Assert.Contains(app.Frames.Canvas.Rects, r => r.Inputs.Style.BackgroundColor == app.Theme.RowHover
                                                      && Math.Abs(app.Frames.Height - r.Inputs.Position.Top + r.Inputs.Position.Height / 2 - (icon.Y + icon.Height / 2)) < 4f);
    }

    [Fact]
    public void Double_clicking_a_cell_selects_its_text_instead_of_decompiling_its_row()
    {
        var (app, tab, _, _) = ShowGreeterRow();

        app.Frames.DoubleClick(TextBeside(app, "Greeter", "Greeter"));
        app.Settle();

        Assert.Same(Table(app, "TypeDef"), tab.Node);
        Assert.Contains(app.Frames.Canvas.Rects, r => r.Inputs.Style.BackgroundColor == app.Theme.TextSelection);
    }

    [Fact]
    public void Enter_on_the_selected_row_decompiles_it_and_Ctrl_C_copies_the_current_cell()
    {
        var (app, tab, _, _) = ShowGreeterRow();
        app.Frames.Click(TextBeside(app, "Greeter", "Greeter"));

        app.Frames.Key(Shortcuts.Copy).Frame();
        var copied = app.Frames.Clipboard.Text;
        app.Frames.Key(KeyboardKey.Enter).Frame();
        app.Settle();

        Assert.Equal("Greeter", copied);
        Assert.Same(app.Fixture("Greeter"), tab.Node);
    }

    [Fact]
    public void Clicking_a_header_sorts_by_its_column_and_again_the_other_way()
    {
        var (app, tab, grid, _) = ShowGreeterRow();
        var name = grid.Column("Name");

        app.Click("Name");
        var ascending = GridOf(app, tab).Order!.Select(r => grid.Text(r, name)).ToList();
        app.Click("Name");
        var descending = GridOf(app, tab).Order!.Select(r => grid.Text(r, name)).ToList();

        Assert.Equal(ascending.Order(StringComparer.CurrentCulture), ascending);
        Assert.Equal(descending.OrderDescending(StringComparer.CurrentCulture), descending);
    }

    [Fact]
    public void Hovering_a_flags_cell_breaks_its_bits_down()
    {
        var (app, _, grid, row) = ShowGreeterRow();
        var attributes = TextBeside(app, grid.Text(row, grid.Column("Attributes")), "Greeter");

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
    public void Typing_in_a_columns_filter_keeps_the_rows_that_match_and_Escape_clears_it()
    {
        var (app, tab, grid) = ShowTable("TypeDef");
        var name = grid.Column("Name");

        app.Frames.Click(FunnelOf(app, "Name"));
        app.Frames.Type("greet").Frame(2);
        var filtered = GridOf(app, tab).View.Select(r => grid.Text(r, name)).ToList();
        app.Frames.Key(KeyboardKey.Escape).Frame(2);

        Assert.Contains("Greeter", filtered);
        Assert.All(filtered, n => Assert.Contains("greet", n, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(grid.RowCount, GridOf(app, tab).View.Length);
    }

    [Fact]
    public void A_flags_filter_picks_values_in_its_popup_and_is_kept_for_that_column_of_every_table_of_the_kind()
    {
        var (app, tab, grid) = ShowTable("TypeDef");
        var attributes = grid.Column("Attributes");

        app.Frames.Click(FunnelOf(app, "Attributes"));
        app.ClickMenuItem("Public (0001)").Frame(2);

        var visibility = System.Reflection.TypeAttributes.VisibilityMask;
        Assert.NotEmpty(GridOf(app, tab).View);
        Assert.All(GridOf(app, tab).View, r =>
            Assert.Equal(1u, Convert.ToUInt32(grid.Text(r, attributes), 16) & (uint)visibility));
        app.TextRect("Visibility = Public");

        using var temp = new TempDir();
        var settings = temp.Settings();
        settings.FlagsFilters = MetadataGridWidget.ListSaved(app.Ui.MetadataFilters);
        SettingsJson.SaveSettings(settings);
        var saved = temp.Settings().FlagsFilters;
        var restored = MetadataGridWidget.FindFilter(MetadataGridWidget.CreateFilters(saved), grid, attributes);
        Assert.Equal("Visibility = Public", FilterStatePresenter.Describe(restored));
    }

    [Fact]
    public void Hovering_a_headers_flags_value_breaks_its_bits_down()
    {
        var app = AppDriver.Opened(width: 1800f, height: 1200f);
        app.Show(app.Node("Metadata", "COFF Header"), 3);
        var grid = app.AppState.Tabs.Active!.Grid!;
        var row = Enumerable.Range(0, grid.RowCount).Single(r => grid.Text(r, grid.Column("Member")) == "Characteristics");
        var value = TextBeside(app, grid.Text(row, grid.Column("Value")), "Characteristics");

        app.Frames.MovePointer(value.X + 2f, value.Y + value.Height / 2).Frame(90);

        app.TextRect("File is executable (0002)");
        app.TextRect("DLL (2000)");
    }

    [Fact]
    public void Dragging_across_a_cell_selects_part_of_its_text_for_Ctrl_C()
    {
        var (app, tab, _, row) = ShowGreeterRow();
        var name = TextBeside(app, "Greeter", "Greeter");
        var advance = name.Width / "Greeter".Length;

        Drag(app, name.X + 1f, name.X + advance * 4, name.Y + name.Height / 2);
        app.Frames.Key(Shortcuts.Copy).Frame();

        Assert.Equal("Gree", app.Frames.Clipboard.Text);
        Assert.Equal(row, GridOf(app, tab).Selected);
    }

    [Fact]
    public void The_selected_cells_outline_is_drawn_over_the_text_selected_in_it()
    {
        var (app, _, _, _) = ShowGreeterRow();
        var name = TextBeside(app, "Greeter", "Greeter");

        Drag(app, name.X + 1f, name.X + name.Width - 1f, name.Y + name.Height / 2);
        app.Run();

        var rects = app.Frames.Canvas.Rects;
        var highlight = rects.Last(r => r.Inputs.Style.BackgroundColor == app.Theme.TextSelection).Sequence;
        var outline = rects.Last(r => r.Inputs.Style.BorderColor.Top.Value == app.Theme.FocusRing).Sequence;
        Assert.True(outline > highlight);
    }

    [Fact]
    public void Clicking_a_token_selects_its_cell_for_Ctrl_C_instead_of_following_it()
    {
        var (app, tab, grid, row) = ShowGreeterRow();
        var baseType = grid.Text(row, grid.Column("BaseType"));

        app.Frames.Click(TextBeside(app, baseType, "Greeter"));
        app.Settle();
        app.Frames.Key(Shortcuts.Copy).Frame();

        Assert.Same(Table(app, "TypeDef"), tab.Node);
        Assert.Equal(baseType, app.Frames.Clipboard.Text);
    }

    [Fact]
    public void Moving_to_another_cell_drops_the_text_selected_in_the_last()
    {
        var (app, _, _, _) = ShowGreeterRow();
        var name = TextBeside(app, "Greeter", "Greeter");
        Drag(app, name.X + 1f, name.X + name.Width / 2, name.Y + name.Height / 2);

        app.Frames.Key(KeyboardKey.LeftArrow).Frame();
        app.Frames.Key(KeyboardKey.RightArrow).Frame();
        app.Frames.Key(Shortcuts.Copy).Frame();

        Assert.Equal("Greeter", app.Frames.Clipboard.Text);
    }

    [Fact]
    public void Selecting_debug_information_shows_its_decoded_value_under_it()
    {
        var app = AppDriver.Opened(width: 1800f, height: 900f);
        var table = app.Node("Debug Metadata (From portable PDB)", "Tables").Table("CustomDebugInformation");
        app.Show(table);
        var tab = app.AppState.Tabs.Active!;
        var grid = tab.Grid!;
        var kind = grid.Column("KindString");
        var options = Enumerable.Range(0, grid.RowCount).Single(r => grid.Text(r, kind).StartsWith("Compilation Options"));

        GridOf(app, tab).Reveal(options);
        app.Run(4);

        Assert.Equal(options, GridOf(app, tab).Selected);
        Assert.True(app.TextRect("language").Y > TextBeside(app, grid.Text(options, kind), grid.Text(options, kind)).Y);
    }

    /// <summary>Where each of <paramref name="rows"/> stands, by its token cell.</summary>
    private static float[] FindTops(AppDriver app, MetadataGrid grid, params int[] rows)
    {
        var token = grid.Column("Token");
        return rows.Select(row => app.TextRect(grid.Text(row, token)).Y).ToArray();
    }

    [Fact]
    public void An_open_cell_makes_its_row_as_tall_as_its_longest_and_leaves_the_others_alone()
    {
        var (app, tab, grid) = ShowTable("TypeDef");
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
    public void Tall_rows_are_listed_in_view_order_as_a_virtual_scroll_needs_them()
    {
        var (app, tab, grid) = ShowTable("TypeDef");
        var state = GridOf(app, tab);
        var name = grid.Column("Name");
        state.Open[(3, name)] = 2;
        state.Open[(1, name)] = 3;

        MetadataGridWidget.MeasureTallRows(state, 10f, 0f);

        Assert.Equal([new TallRow(1, 30f), new TallRow(3, 20f)], state.TallRows);
        Assert.Equal(new RowHeights(30f, 0f), state.TallRowsByIndex[1]);
    }

    [Fact]
    public void A_row_the_filter_hides_is_no_tall_row()
    {
        var (app, tab, grid) = ShowTable("TypeDef");
        var state = GridOf(app, tab);
        var name = grid.Column("Name");
        state.Filters[name] = new MetadataColumnFilter { Text = grid.Text(1, name) };
        state.UpdateView(app.Ui.MetadataFilters);
        state.Open[(2, name)] = 4;

        MetadataGridWidget.MeasureTallRows(state, 10f, 0f);

        Assert.Empty(state.TallRows);
        Assert.Empty(state.TallRowsByIndex);
    }

    [Fact]
    public void An_open_cell_in_a_row_the_filter_hides_makes_no_row_taller()
    {
        var (app, tab, grid) = ShowTable("TypeDef");
        var state = GridOf(app, tab);
        var name = grid.Column("Name");
        var line = state.RowHeight;

        state.Filters[name] = new MetadataColumnFilter { Text = $"/^({grid.Text(1, name)}|{grid.Text(3, name)})$/" };
        state.Open[(2, name)] = 4;
        app.Run(3);

        var tops = FindTops(app, grid, 1, 3);
        Assert.Equal(line, tops[1] - tops[0], 0.01f);
    }

    [Fact]
    public void The_selected_rows_details_go_under_its_tallest_cell()
    {
        var app = AppDriver.Opened(width: 1800f, height: 900f);
        app.Show(app.Node("Debug Metadata (From portable PDB)", "Tables").Table("CustomDebugInformation"));
        var tab = app.AppState.Tabs.Active!;
        var grid = tab.Grid!;
        var state = GridOf(app, tab);
        var kind = grid.Column("KindString");
        var selected = Enumerable.Range(0, grid.RowCount - 1).First(r => grid.Text(r, kind).StartsWith("Compilation Options"));
        state.Reveal(selected);
        app.Run(4);
        var closed = FindTops(app, grid, selected, selected + 1);

        state.Open[(selected, kind)] = 3;
        app.Run(3);

        var open = FindTops(app, grid, selected, selected + 1);
        Assert.True(closed[1] - closed[0] > state.RowHeight);
        Assert.Equal(2 * state.RowHeight, open[1] - open[0] - (closed[1] - closed[0]), 0.01f);
    }
}
