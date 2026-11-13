using ILSight.Core;
using ILSight.CodeViewArea;
using ILSight.ILSightAppArea;
using ILSight.CommandArea;

namespace ILSight.App.Tests;

public sealed class HistoryTests
{
    private static readonly string ApiDiffSamplePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiDiff", "Older", "ApiDiffSample.dll");

    private static AppDriver Opened()
    {
        var app = AppDriver.Opened(height: 360f);
        app.Run(3);
        return app;
    }

    private static async Task ShowAsync(AppDriver app, string type)
    {
        app.Show(await app.FixtureAsync(type), 4);
    }

    private static CodeViewPosition ScrollTo(AppDriver app, int topLine, TextPosition anchor, TextPosition caret)
    {
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        CodeViewWidget.RestorePosition(CodeOf(app, tab), new CodeViewPosition
        {
            TopLine = topLine,
            ScrollX = 0f,
            Anchor = anchor,
            Caret = caret,
        });
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
    public async Task Going_back_and_forward_returns_each_place_to_where_it_was_left()
    {
        var app = Opened();
        await ShowAsync(app, "Outline");
        var outline = ScrollTo(app, 20, new TextPosition { Line = 21, Column = 4 }, new TextPosition { Line = 22, Column = 8 });
        await ShowAsync(app, "Greeter");
        var greeter = ScrollTo(app, 3, new TextPosition { Line = 4, Column = 0 }, new TextPosition { Line = 4, Column = 6 });

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
    public async Task The_frame_back_is_pressed_in_says_the_app_is_resolving_the_place_its_tab_left()
    {
        var app = Opened();
        await ShowAsync(app, "Outline");
        await ShowAsync(app, "Greeter");

        app.Frames.Key(Shortcuts.Back).Frame();

        Assert.True(app.Frames.Drew(app.Ui.Strings.StatusResolving));
        app.Settle(4);
        Assert.Equal("Outline", app.AppState.Navigation.ActiveTab?.Node?.Text);
    }

    [Fact]
    public async Task The_frame_back_is_pressed_in_shows_the_tab_that_still_shows_the_entry()
    {
        var app = Opened();
        await ShowAsync(app, "Outline");
        var outline = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(outline);
        app.Show(await app.FixtureAsync("Greeter"), 4, inNewTab: true);

        app.Frames.Key(Shortcuts.Back).Frame();

        Assert.True(app.Frames.Drew((await app.FixtureAsync("Outline")).FullName));
        Assert.Same(outline, app.AppState.Navigation.ActiveTab);
    }

    [Fact]
    public async Task Going_back_from_a_followed_reference_returns_to_where_it_was_followed_from()
    {
        var app = Opened();
        await ShowAsync(app, "Outline");
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var (line, column) = tab.Document.GetPosition("return First();");
        var reference = Assert.IsAssignableFrom<CodeReference.Navigable>(tab.Document.SpanAt(line, column + "return ".Length).Reference);
        var before = ScrollTo(app, 25,
            new TextPosition { Line = line, Column = column }, new TextPosition { Line = line, Column = column + 6 });

        app.Start((a, ct) => a.NavigateHandler.HandleAsync(a.Navigation, a.Decompiler, reference, a.Navigation.BeginResolving(inNewTab: false), ct));
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
    public async Task Where_the_view_stood_when_its_tab_moved_on_in_the_frame_is_where_going_back_returns_to()
    {
        var app = Opened();
        await ShowAsync(app, "Outline");
        ScrollTo(app, 20, new TextPosition { Line = 21, Column = 4 }, new TextPosition { Line = 22, Column = 8 });
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var (line, column) = tab.Document.GetPosition("return First();");
        var scrolled = new CodeViewPosition
        {
            TopLine = 25,
            ScrollX = 0f,
            Anchor = new TextPosition
            {
                Line = line,
                Column = column,
            },
            Caret = new TextPosition
            {
                Line = line,
                Column = column + 6,
            },
        };
        CodeViewWidget.RestorePosition(CodeOf(app, tab), scrolled);

        app.Driver.Show(await app.FixtureAsync("Greeter"));
        app.Driver.InTurn(() => AppFrames.KeepScreenChanges(app.Ui, app.AppState));
        app.Settle(4);
        app.GoBack();
        app.Settle(4);

        Assert.Equal("Outline", tab.Node?.Text);
        Assert.Equal(scrolled, CodeOf(app, tab).Position);
    }

    [Fact]
    public async Task A_position_left_in_another_language_is_not_applied()
    {
        var app = Opened();
        await ShowAsync(app, "Outline");
        ScrollTo(app, 20, new TextPosition { Line = 21, Column = 4 }, new TextPosition { Line = 22, Column = 8 });
        await ShowAsync(app, "Greeter");

        app.AppState.Decompiler.UseLanguage(DecompilationLanguage.IL);
        app.Settle(4);
        app.GoBack();
        app.Settle(4);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);

        Assert.Equal("Outline", tab.Node?.Text);
        Assert.Equal(DecompilationLanguage.IL, tab.Load.Language);
        Assert.False(CodeOf(app, tab).HasSelection);
        Assert.Equal(0, CodeOf(app, tab).Position.TopLine);
    }

    [Fact]
    public async Task Going_to_an_entry_jumps_over_the_ones_between_and_keeps_them_all()
    {
        var app = Opened();
        var navigation = app.AppState.Navigation;
        await ShowAsync(app, "Outline");
        var first = navigation.HistoryIndex;
        await ShowAsync(app, "Greeter");
        await ShowAsync(app, "Outline");
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
    public async Task The_back_dropdown_lists_earlier_places_nearest_first_and_goes_to_the_one_picked()
    {
        var app = Opened();
        await ShowAsync(app, "Outline");
        var first = app.AppState.Navigation.HistoryIndex;
        await ShowAsync(app, "Greeter");
        await ShowAsync(app, "Outline");

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
    public async Task Going_somewhere_while_the_back_dropdown_is_open_closes_it()
    {
        var app = Opened();
        await ShowAsync(app, "Outline");
        await ShowAsync(app, "Greeter");
        ClickBackDropdown(app);
        var row = app.Frames.Canvas.Texts.Last(t => t.Inputs.Text == "Outline").Inputs.Position;

        app.GoBack();
        app.Settle(4);

        Assert.DoesNotContain(app.Frames.Canvas.Texts, t => t.Inputs.Position.Top == row.Top && t.Inputs.Position.Left == row.Left);
        Assert.Equal("Outline", app.AppState.Navigation.ActiveTab?.Node?.Text);
    }

    [Fact]
    public async Task The_back_dropdown_shows_each_place_with_its_tree_icon()
    {
        var app = Opened();
        var outline = await app.FixtureAsync("Outline");
        app.Show(outline, 4);
        await ShowAsync(app, "Greeter");

        ClickBackDropdown(app);
        var row = app.Frames.Canvas.Texts.Last(t => t.Inputs.Text == "Outline").Inputs.Position;
        var glyph = BuiltInGlyphs.Text(NodeIcons.Glyph(NodeIcons.Of(outline.Subject), BuiltInGlyphs.Icons));

        Assert.Contains(app.Frames.Canvas.Texts, t => t.Inputs.Text == glyph && t.Inputs.Position.Left < row.Left
            && MathF.Abs(t.Inputs.Position.Top - t.Inputs.Position.Height / 2 - (row.Top - row.Height / 2)) < 2f);
    }

    private static async Task<AppDriver> ShowAccountThenCloseItsAssemblyAsync()
    {
        var app = Opened();
        await ShowAsync(app, "Greeter");
        app.Show(await OpenApiDiffSample(app).ReadRootAsync().GetChildAsync("ApiDiffSample", "Account"), 4);
        await ShowAsync(app, "Farewell");
        app.Driver.InTurn(() => Commands.CloseAssemblies(app.AppState, [ApiDiffSampleOf(app)]));
        app.Run(4);
        return app;
    }

    private static LoadedModule OpenApiDiffSample(AppDriver app)
    {
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [TestPaths.PathOf(ApiDiffSamplePath)], ct));
        app.Driver.RunUntil(() => app.AppState.Assemblies.IsListed(TestPaths.PathOf(ApiDiffSamplePath)));
        app.Settle(4);
        return ApiDiffSampleOf(app);
    }

    private static LoadedModule ApiDiffSampleOf(AppDriver app)
    {
        return app.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == ApiDiffSamplePath);
    }

    [Fact]
    public async Task Going_back_passes_over_a_place_whose_assembly_was_closed_and_opens_nothing()
    {
        var app = await ShowAccountThenCloseItsAssemblyAsync();

        app.GoBack();
        app.Settle(4);

        Assert.Equal("Greeter", app.AppState.Navigation.ActiveTab?.Node?.Text);
        Assert.False(app.AppState.Assemblies.IsListed(TestPaths.PathOf(ApiDiffSamplePath)));
    }

    [Fact]
    public async Task The_back_dropdown_marks_a_place_whose_assembly_was_closed_and_offers_it_no_click()
    {
        var app = await ShowAccountThenCloseItsAssemblyAsync();
        var current = app.AppState.Navigation.HistoryIndex;

        ClickBackDropdown(app);
        var account = app.Frames.Find(Selector.OfRole(SemanticRole.MenuItem, "Account (ApiDiffSample closed)"));
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Account (ApiDiffSample closed)"));
        app.Settle(4);

        Assert.True(account.Has(ZGF.Gui.Immediate.SemanticStates.Disabled));
        Assert.Equal("Farewell", app.AppState.Navigation.ActiveTab?.Node?.Text);
        Assert.Equal(current, app.AppState.Navigation.HistoryIndex);
        Assert.False(app.AppState.Assemblies.IsListed(TestPaths.PathOf(ApiDiffSamplePath)));
    }

    [Fact]
    public async Task Going_back_reaches_a_passed_over_place_again_once_its_assembly_is_open_again()
    {
        var app = await ShowAccountThenCloseItsAssemblyAsync();

        OpenApiDiffSample(app);
        app.GoBack();
        app.Settle(4);
        var fromOpened = app.AppState.Navigation.ActiveTab?.Node?.Text;
        app.GoBack();
        app.Settle(4);

        Assert.Equal("Farewell", fromOpened);
        Assert.Equal("Account", app.AppState.Navigation.ActiveTab?.Node?.Text);
    }

    [Fact]
    public async Task Picking_an_entry_from_the_dropdown_goes_to_that_entry_only()
    {
        var app = await ShowAccountThenCloseItsAssemblyAsync();
        var account = app.AppState.Navigation.HistoryIndex - 1;

        app.GoToEntry(account);
        app.Settle(4);

        Assert.Equal("Farewell", app.AppState.Navigation.ActiveTab?.Node?.Text);
        Assert.Equal(account + 1, app.AppState.Navigation.HistoryIndex);
    }

    private static void ClickBackDropdown(AppDriver app)
    {
        var icon = app.TextRect(BuiltInGlyphs.Icons.ToolbarBack);
        var x = icon.X + icon.Width / 2 + app.Theme.IconButtonSize / 2 + app.Theme.DropdownButtonWidth / 2;
        app.Frames.Click(x, icon.Y + icon.Height / 2).Frame();
    }
}
