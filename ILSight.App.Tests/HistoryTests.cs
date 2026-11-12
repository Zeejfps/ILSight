using ILSight.Core;
using ILSight.CodeViewArea;
using ILSight.ILSightAppArea;
using static ILSight.App.Tests.BuiltInGlyphs;

namespace ILSight.App.Tests;

public sealed class HistoryTests
{
    private static AppDriver Opened()
    {
        var app = AppDriver.Opened(height: 360f);
        app.Run(3);
        return app;
    }

    private static void Show(AppDriver app, string type)
    {
        app.Show(app.Fixture(type), 4);
    }

    private static CodeViewPosition ScrollTo(AppDriver app, int topLine, TextPosition anchor, TextPosition caret)
    {
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        CodeViewWidget.RestorePosition(CodeOf(app, tab), new CodeViewPosition(topLine, 0f, anchor, caret));
        app.Run(4);
        var scrolled = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(scrolled);
        return CodeOf(app, scrolled).Position;
    }

    private static CodeViewState CodeOf(AppDriver app, DocumentTab tab)
    {
        return DocumentPanels.GetView(app.Ui.Documents, tab).Code;
    }

    [Fact]
    public void Going_back_and_forward_returns_each_place_to_where_it_was_left()
    {
        var app = Opened();
        Show(app, "Outline");
        var outline = ScrollTo(app, 20, new TextPosition(21, 4), new TextPosition(22, 8));
        Show(app, "Greeter");
        var greeter = ScrollTo(app, 3, new TextPosition(4, 0), new TextPosition(4, 6));

        app.GoBack();
        app.Settle(4);
        var backTab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(backTab);
        var back = backTab.Node?.Text;
        var backPosition = CodeOf(app, backTab).Position;
        app.GoForward();
        app.Settle(4);
        var forward = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(forward);

        Assert.Equal(20, outline.TopLine);
        Assert.Equal("Outline", back);
        Assert.Equal(outline, backPosition);
        Assert.Equal("Greeter", forward.Node?.Text);
        Assert.Equal(greeter, CodeOf(app, forward).Position);
    }

    [Fact]
    public void The_frame_back_is_pressed_in_says_the_app_is_resolving_the_place_its_tab_left()
    {
        var app = Opened();
        Show(app, "Outline");
        Show(app, "Greeter");

        app.Frames.Key(Shortcuts.Back).Frame();

        Assert.True(app.Frames.Drew(app.Ui.Strings.StatusResolving));
        app.Settle(4);
        Assert.Equal("Outline", app.AppState.Navigation.ActiveTab?.Node?.Text);
    }

    [Fact]
    public void The_frame_back_is_pressed_in_shows_the_tab_that_still_shows_the_entry()
    {
        var app = Opened();
        Show(app, "Outline");
        var outline = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(outline);
        app.Show(app.Fixture("Greeter"), 4, inNewTab: true);

        app.Frames.Key(Shortcuts.Back).Frame();

        Assert.True(app.Frames.Drew(BrowseNodes.GetFullName(app.Fixture("Outline"))));
        Assert.Same(outline, app.AppState.Navigation.ActiveTab);
    }

    [Fact]
    public void Going_back_from_a_followed_reference_returns_to_where_it_was_followed_from()
    {
        var app = Opened();
        Show(app, "Outline");
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var (line, column) = tab.Document.GetPosition("return First();");
        var reference = Assert.IsAssignableFrom<CodeReference.Navigable>(tab.Document.SpanAt(line, column + "return ".Length).Reference);
        var before = ScrollTo(app, 25, new TextPosition(line, column), new TextPosition(line, column + 6));

        app.Start((a, ct) => a.NavigateHandler.HandleAsync(a, reference, a.Navigation.BeginResolving(inNewTab: false), ct));
        app.Settle(4);
        Assert.NotNull(tab.Node);
        var followed = tab.Node.Text;
        app.GoBack();
        app.Settle(4);

        Assert.StartsWith("First()", followed);
        Assert.Equal("Outline", tab.Node?.Text);
        Assert.Equal(before, CodeOf(app, tab).Position);
    }

    [Fact]
    public void Where_the_view_stood_when_its_tab_moved_on_in_the_frame_is_where_going_back_returns_to()
    {
        var app = Opened();
        Show(app, "Outline");
        ScrollTo(app, 20, new TextPosition(21, 4), new TextPosition(22, 8));
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var (line, column) = tab.Document.GetPosition("return First();");
        var scrolled = new CodeViewPosition(25, 0f, new TextPosition(line, column), new TextPosition(line, column + 6));
        CodeViewWidget.RestorePosition(CodeOf(app, tab), scrolled);

        app.Driver.Show(app.Fixture("Greeter"));
        AppFrames.KeepScreenChanges(app.Ui, app.AppState);
        app.Settle(4);
        app.GoBack();
        app.Settle(4);

        Assert.Equal("Outline", tab.Node?.Text);
        Assert.Equal(scrolled, CodeOf(app, tab).Position);
    }

    [Fact]
    public void A_position_left_in_another_language_is_not_applied()
    {
        var app = Opened();
        Show(app, "Outline");
        ScrollTo(app, 20, new TextPosition(21, 4), new TextPosition(22, 8));
        Show(app, "Greeter");

        app.AppState.Language = DecompilationLanguage.IL;
        app.Settle(4);
        app.GoBack();
        app.Settle(4);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);

        Assert.Equal("Outline", tab.Node?.Text);
        Assert.Equal(DecompilationLanguage.IL, tab.Language);
        Assert.False(CodeOf(app, tab).HasSelection);
        Assert.Equal(0, CodeOf(app, tab).Position.TopLine);
    }

    [Fact]
    public void Going_to_an_entry_jumps_over_the_ones_between_and_keeps_them_all()
    {
        var app = Opened();
        var navigation = app.AppState.Navigation;
        Show(app, "Outline");
        var first = navigation.HistoryIndex;
        Show(app, "Greeter");
        Show(app, "Outline");
        var count = navigation.HistoryEntries.Count;

        app.GoToEntry(first);
        app.Settle(4);
        var back = app.AppState.Navigation.ActiveTab?.Node?.Text;

        Assert.Equal("Outline", back);
        Assert.Equal(first, navigation.HistoryIndex);
        Assert.Equal(count, navigation.HistoryEntries.Count);
        Assert.True(navigation.CanGoForward);
    }

    [Fact]
    public void The_back_dropdown_lists_earlier_places_nearest_first_and_goes_to_the_one_picked()
    {
        var app = Opened();
        Show(app, "Outline");
        var first = app.AppState.Navigation.HistoryIndex;
        Show(app, "Greeter");
        Show(app, "Outline");

        ClickBackDropdown(app);
        var greeter = app.Frames.Canvas.Texts.Last(t => t.Inputs.Text == "Greeter").Inputs.Position;
        var outline = app.Frames.Canvas.Texts.Last(t => t.Inputs.Text == "Outline").Inputs.Position;
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Outline"));
        app.Settle(4);

        Assert.True(greeter.Top > outline.Top);
        Assert.Equal("Outline", app.AppState.Navigation.ActiveTab?.Node?.Text);
        Assert.Equal(first, app.AppState.Navigation.HistoryIndex);
    }

    [Fact]
    public void Going_somewhere_while_the_back_dropdown_is_open_closes_it()
    {
        var app = Opened();
        Show(app, "Outline");
        Show(app, "Greeter");
        ClickBackDropdown(app);
        var row = app.Frames.Canvas.Texts.Last(t => t.Inputs.Text == "Outline").Inputs.Position;

        app.GoBack();
        app.Settle(4);

        Assert.DoesNotContain(app.Frames.Canvas.Texts, t => t.Inputs.Position.Top == row.Top && t.Inputs.Position.Left == row.Left);
        Assert.Equal("Outline", app.AppState.Navigation.ActiveTab?.Node?.Text);
    }

    [Fact]
    public void The_back_dropdown_shows_each_place_with_its_tree_icon()
    {
        var app = Opened();
        var outline = app.Fixture("Outline");
        app.Show(outline, 4);
        Show(app, "Greeter");

        ClickBackDropdown(app);
        var row = app.Frames.Canvas.Texts.Last(t => t.Inputs.Text == "Outline").Inputs.Position;
        var glyph = Text(NodeIcons.Glyph(NodeIcons.Of(outline.Subject), Icons));

        Assert.Contains(app.Frames.Canvas.Texts, t => t.Inputs.Text == glyph && t.Inputs.Position.Left < row.Left
            && MathF.Abs(t.Inputs.Position.Top - t.Inputs.Position.Height / 2 - (row.Top - row.Height / 2)) < 2f);
    }

    private static void ClickBackDropdown(AppDriver app)
    {
        var icon = app.TextRect(Icons.ToolbarBack);
        var x = icon.X + icon.Width / 2 + app.Theme.IconButtonSize / 2 + app.Theme.DropdownButtonWidth / 2;
        app.Frames.Click(x, icon.Y + icon.Height / 2).Frame();
    }
}
