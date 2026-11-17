using ILSight.CommandArea;
using ZGF.Desktop;
using ZGF.KeyboardModule;
using ILSight.Core;
using ILSight.CodeViewArea;
using ILSight.BookmarkArea;
using ILSight.SettingArea;

namespace ILSight.App.Tests;

public sealed class BookmarkTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    private static async Task<(AppStateDriver Driver, DocumentTab Tab, int Line)> OutlineAtAsync(string text, AppStateDriver? driver = null)
    {
        var d = driver ?? AppStateDriver.Started();
        d.OpenFixtures();
        var outline = await d.FixtureAsync("Outline");
        d.Show(outline);
        var tab = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        return (d, tab, tab.Document.GetPosition(text).Line);
    }

    [Fact]
    public async Task Toggling_a_statement_line_bookmarks_it_by_its_statement_and_toggling_again_removes_it()
    {
        var (d, tab, line) = await OutlineAtAsync("return First();", AppStateDriver.WithoutScreen());
        var bookmarks = d.AppState.Bookmarks;

        var added = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        var bookmark = Assert.Single(bookmarks.Items);
        Assert.NotNull(tab.Node);
        var marked = bookmarks.ListMarkedLines(tab.Document, tab.Node);
        var removed = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);

        Assert.Equal(new BookmarkToggle.Added(bookmark), added);
        Assert.Equal(new BookmarkToggle.Removed { Bookmark = bookmark, Row = 0 }, removed);
        Assert.Equal("Bookmark 1", bookmark.Name);
        Assert.Equal(CodeAnchorKind.Statement, bookmark.Anchor?.Kind);
        Assert.Equal("Outline:" + (line + 1), bookmark.Location);
        Assert.True(marked[line]);
        Assert.Empty(bookmarks.Items);
    }

    [Fact]
    public async Task A_bookmark_records_what_ILSpy_records_from_the_values_the_node_and_the_document_kept()
    {
        var (d, tab, line) = await OutlineAtAsync("return First();", AppStateDriver.WithoutScreen());
        var node = tab.Node;
        Assert.NotNull(node);
        var module = node.Assembly;

        var anchor = Documents.FindAnchorAt(tab.Document, line);

        var toggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(toggle);
        var info = Assert.Single(d.AppState.Bookmarks.Items).ILSpy;
        Assert.NotNull(anchor);
        Assert.Equal(module.FullName, info.AssemblyFullName);
        Assert.Equal(module.FilePath.FileName, info.ModuleName);
        Assert.Equal((uint)anchor.Value.Token, info.Token);
        Assert.Equal(AppStateDriver.FixturesNamespace + ".Outline.Third", info.MemberName);
        Assert.Equal(AppStateDriver.FixturesNamespace + ".Outline", info.LocationNodeName);
        Assert.Equal([module.FilePath.Text, AppStateDriver.FixturesNamespace, AppStateDriver.FixturesNamespace + ".Outline"], info.TreePath);
    }

    [Fact]
    public async Task Stepping_goes_to_the_next_enabled_bookmark_and_asks_the_view_to_reveal_its_line()
    {
        var (d, tab, line) = await OutlineAtAsync("return First();", AppStateDriver.WithoutScreen());
        var first = tab.Document.GetPosition("foreach").Line;
        var firstToggle = Bookmarks.ToggleBookmark(d.AppState, tab, first, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(firstToggle);
        var secondToggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(secondToggle);
        var bookmarks = d.AppState.Bookmarks;
        var panel = new BookmarksPanel();
        panel.Selection.Select(bookmarks.Items[0], bookmarks.Items);
        d.TakeViewCommands();

        d.InTurn(() => BookmarksPanelView.Step(panel, d.AppState, 1));
        d.Settle();
        var commands = d.TakeViewCommands();

        Assert.Same(bookmarks.Items[1], panel.Selection.Item);
        Assert.Contains(new ViewCommand.ShowTab { Tab = tab, Beside = null }, commands);
        Assert.Contains(new ViewCommand.RevealLine { Tab = tab, Line = line }, commands);
        Assert.NotNull(tab.Node);
        Assert.Contains(new ViewCommand.RevealNode(tab.Node), commands);
    }

    [Fact]
    public async Task A_bookmark_follows_its_statement_into_IL_and_into_the_methods_own_document()
    {
        var (d, tab, line) = await OutlineAtAsync("return First();");
        var toggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(toggle);
        var bookmarks = d.AppState.Bookmarks;

        d.AppState.Decompiler.UseLanguage(DecompilationLanguage.IL);
        d.Settle();
        Assert.NotNull(tab.Node);
        var il = tab.Document.Lines[Assert.Single(bookmarks.ListMarkedLines(tab.Document, tab.Node)).Key].Text;
        d.AppState.Decompiler.UseLanguage(DecompilationLanguage.CSharp);
        var outline = await d.FixtureAsync("Outline");
        var outlineChildren = await outline.ListChildrenAsync();
        d.Show(outlineChildren.Single(c => c.Text.StartsWith("Third()")));
        Assert.NotNull(tab.Node);
        var own = tab.Document.Lines[Assert.Single(bookmarks.ListMarkedLines(tab.Document, tab.Node)).Key].Text;

        Assert.StartsWith("IL_", il.TrimStart());
        Assert.Contains("return First();", own);
    }

    [Fact]
    public async Task A_line_with_nothing_to_anchor_to_is_marked_only_where_it_was_made()
    {
        var (d, tab, line) = await OutlineAtAsync("return First();");
        var brace = line - 1;
        Assert.Equal("{", tab.Document.Lines[brace].Text.Trim());
        var toggle = Bookmarks.ToggleBookmark(d.AppState, tab, brace, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(toggle);
        var bookmark = Assert.Single(d.AppState.Bookmarks.Items);

        d.AppState.Decompiler.UseLanguage(DecompilationLanguage.IL);
        d.Settle();

        Assert.Null(bookmark.Anchor);
        Assert.NotNull(tab.Node);
        Assert.Empty(d.AppState.Bookmarks.ListMarkedLines(tab.Document, tab.Node));
    }

    [Fact]
    public async Task Going_to_a_bookmark_shows_its_place_and_reveals_its_line()
    {
        var app = new AppDriver(height: 360f);
        var (d, tab, line) = await OutlineAtAsync("return First();", app.Driver);
        var toggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(toggle);
        var greeter = await app.FixtureAsync("Greeter");
        app.Show(greeter, 3);

        var bookmark = d.AppState.Bookmarks.Items[0];
        d.InTurn(() => Commands.StartGoingToBookmark(d.AppState, bookmark));
        app.Settle(4);

        Assert.Equal("Outline", tab.Node?.Text);
        Assert.Equal(line, app.Ui.Documents.GetView(tab).Code.Reveal.Flash?.Line);
        Assert.Equal("Outline", Assert.IsType<HistoryEntry.Node>(d.AppState.ListSession.Navigation.HistoryEntries[d.AppState.ListSession.Navigation.HistoryIndex]).Place.Label);
    }

    [Fact]
    public async Task Next_and_previous_skip_disabled_bookmarks_and_wrap()
    {
        var (d, tab, line) = await OutlineAtAsync("return First();");
        var first = tab.Document.GetPosition("foreach").Line;
        var firstToggle = Bookmarks.ToggleBookmark(d.AppState, tab, first, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(firstToggle);
        var secondToggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(secondToggle);
        var bookmarks = d.AppState.Bookmarks;
        bookmarks.SetEnabled(bookmarks.Items[1], false);

        var next = bookmarks.FindNext(null, 1);
        var again = bookmarks.FindNext(next, 1);
        var previous = bookmarks.FindNext(again, -1);

        Assert.Same(bookmarks.Items[0], next);
        Assert.Same(bookmarks.Items[0], again);
        Assert.Same(bookmarks.Items[0], previous);
        Assert.Equal("Bookmark 2", bookmarks.Items[1].Name);
    }

    [Fact]
    public async Task A_bookmark_marking_a_line_already_marked_is_not_added()
    {
        var (d, tab, line) = await OutlineAtAsync("return First();");
        var toggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(toggle);
        var bookmarks = d.AppState.Bookmarks;
        var twin = bookmarks.Items[0];

        var added = bookmarks.TryAdd(twin);

        Assert.False(added);
        Assert.Single(bookmarks.Items);
    }

    [Fact]
    public async Task Saved_bookmarks_marking_one_line_load_as_one_and_say_how_many_were_left_out()
    {
        var (d, tab, line) = await OutlineAtAsync("return First();", AppStateDriver.Started(_temp.Settings()));
        var toggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(toggle);
        var write = d.AppState.SettingsSaver.Save(d.AppState);
        Assert.IsType<SettingsWriteResult.Written>(write);
        var settings = _temp.Settings();
        settings.Bookmarks.Add(settings.Bookmarks[0] with { Name = "Twin" });

        var next = AppStateDriver.Started(settings);

        Assert.Single(next.AppState.Bookmarks.Items);
        Refusal leftOut = UnionAsserts.GetCase<Refusal.BookmarksLeftOut>(next.AppState.Refusals.Shown);
        Assert.Equal("1 bookmark marked the same line as another and was left out", leftOut.Words(Strings.For(Locale.En)).What);
        Assert.Empty(next.AppState.Errors.Entries);
    }

    [Fact]
    public async Task Bookmarks_are_saved_with_the_settings_and_found_again_on_the_next_run()
    {
        var (d, tab, line) = await OutlineAtAsync("return First();", AppStateDriver.Started(_temp.Settings()));
        var toggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(toggle);
        var bookmarks = d.AppState.Bookmarks;
        bookmarks.Rename(bookmarks.Items[0], "Calls First");
        d.Pump();

        var (next, nextTab, _) = await OutlineAtAsync("return First();", AppStateDriver.Started(_temp.Settings()));
        var saved = Assert.Single(next.AppState.Bookmarks.Items);

        Assert.Equal("Calls First", saved.Name);
        Assert.Equal(tab.Location, saved.Place);
        Assert.Equal(bookmarks.Items[0].Anchor, saved.Anchor);
        Assert.NotNull(nextTab.Node);
        Assert.True(next.AppState.Bookmarks.ListMarkedLines(nextTab.Document, nextTab.Node)[line]);
    }

    [Fact]
    public async Task Clicking_the_margin_asks_to_toggle_that_line_and_a_bookmarked_line_is_marked()
    {
        var view = CodeViewDriver.Showing(await CodeViewDriver.OutlineAsync(), 1200f, frames: 2);
        var line = CodeFolding.GetLineAt(view.State.Selection.Folds, new CodeRow(3));

        view.Bookmarks = new Dictionary<int, bool> { [CodeFolding.GetLineAt(view.State.Selection.Folds, new CodeRow(5))] = true };
        var clicked = view.Click(view.MarkOf(new CodeRow(3)));

        Assert.Equal(line, Assert.IsType<CodeViewIntent.ToggleBookmark>(clicked.Intent).Line);
        Assert.Equal(2, view.Frames.Canvas.Texts.Count(t => t.Inputs.Text == BuiltInGlyphs.Text(BuiltInGlyphs.Icons.CodeBookmark)));
        Assert.False(view.State.Selection.HasSelection);
    }

    [Fact]
    public async Task Without_bookmarks_the_margin_toggles_nothing()
    {
        var view = CodeViewDriver.Showing(await CodeViewDriver.OutlineAsync(), 1200f);

        var clicked = view.Click(view.MarkOf(new CodeRow(3)));

        Assert.IsNotType<CodeViewIntent.ToggleBookmark>(clicked.Intent);
        Assert.DoesNotContain(view.Frames.Canvas.Texts, t => t.Inputs.Text == BuiltInGlyphs.Text(BuiltInGlyphs.Icons.CodeBookmark));
    }

    [Fact]
    public async Task The_bookmarks_panel_lists_bookmarks_and_Ctrl_clicking_a_location_goes_to_one()
    {
        var (app, d, tab, line) = await ShowBookmarkAsync(frames: 0);
        var greeter = await app.FixtureAsync("Greeter");
        app.Show(greeter, 4);

        Assert.True(app.Frames.Drew("Bookmark 1"));
        Click(app, "Outline:" + (line + 1));
        d.Settle();
        Assert.Equal("Greeter", tab.Node?.Text);
        Click(app, "Outline:" + (line + 1), KeyModifiers.Control);
        app.Settle(4);

        Assert.Equal("Outline", tab.Node?.Text);
        Assert.Equal(line, app.Ui.Documents.GetView(tab).Code.Reveal.Flash?.Line);
    }

    private static void Click(AppDriver app, string text, KeyModifiers modifiers = KeyModifiers.None)
    {
        app.Frames.Wait(1);
        app.Frames.Click(app.TextRect(text), modifiers: modifiers);
        app.Frames.Frame();
    }

    private static void DoubleClick(AppDriver app, string text)
    {
        app.Frames.Wait(1);
        app.Frames.DoubleClick(app.TextRect(text));
        app.Frames.Frame();
    }

    private static async Task<(AppDriver App, AppStateDriver Driver, DocumentTab Tab, int Line)> ShowBookmarkAsync(int frames = 3)
    {
        var app = new AppDriver(height: 500f);
        var (d, tab, line) = await OutlineAtAsync("return First();", app.Driver);
        var toggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(toggle);
        d.Ui.Panels.Open(DockView.Bookmarks);
        app.Run(frames);
        return (app, d, tab, line);
    }

    [Fact]
    public async Task Hovering_a_location_shows_where_it_points_how_to_follow_it_and_a_link_icon_that_goes_there()
    {
        var (app, d, tab, _) = await ShowBookmarkAsync();
        var greeter = await app.FixtureAsync("Greeter");
        app.Show(greeter, 2);
        var location = app.TextRect(d.AppState.Bookmarks.Items[0].Location);

        app.Frames.MovePointer(location.X + 2f, location.Y + location.Height / 2);
        app.Frames.Frame(90);
        Assert.True(app.Frames.Drew(Strings.En.BookmarksGoToHint));

        app.Frames.Click(app.TextRect(BuiltInGlyphs.Icons.LinkBookmark));
        d.Settle();

        Assert.Equal("Outline", tab.Node?.Text);
    }

    [Fact]
    public async Task A_bookmark_outside_the_active_list_says_going_to_it_adds_its_assembly_and_going_there_adds_it()
    {
        var (app, d, _, _) = await ShowBookmarkAsync();
        var bookmark = d.AppState.Bookmarks.Items[0];
        d.AppState.Lists.Add("Other");
        d.SwitchList("Other");
        d.Settle();
        app.Run(2);
        var location = app.TextRect(bookmark.Location);

        app.Frames.MovePointer(location.X + 2f, location.Y + location.Height / 2);
        app.Frames.Frame(90);
        Assert.Contains(app.Frames.TextRects(), t => t.Text.StartsWith(bookmark.Module + " "));

        d.InTurn(() => Commands.StartGoingToBookmark(d.AppState, bookmark));
        app.Settle(4);

        Assert.Equal([AppStateDriver.FixturePath], d.AppState.ListSession.Assemblies.ListFilePaths().Select(static p => p.Text));
        Assert.Equal("Outline", d.AppState.ListSession.Navigation.ActiveTab?.Node?.Text);
    }

    [Fact]
    public async Task The_selected_bookmark_stays_selected_when_one_before_it_is_removed()
    {
        var app = new AppDriver(height: 500f);
        var (d, tab, line) = await OutlineAtAsync("return First();", app.Driver);
        var firstToggle = Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition("foreach").Line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(firstToggle);
        var secondToggle = Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition("needle").Line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(secondToggle);
        var thirdToggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(thirdToggle);
        d.Ui.Panels.Open(DockView.Bookmarks);
        app.Run(3);
        var bookmarks = d.AppState.Bookmarks;
        var panel = app.Ui.BookmarksPanel;
        var second = bookmarks.Items[1];
        d.InTurn(() => BookmarksPanelView.Step(panel, d.AppState, 1));
        d.InTurn(() => BookmarksPanelView.Step(panel, d.AppState, 1));
        app.Run(2);

        BookmarksPanelView.Remove(panel, bookmarks, bookmarks.Items[0]);
        app.Run(2);

        Assert.Same(second, panel.Selection.Item);
    }

    [Fact]
    public async Task Removing_the_selected_bookmark_selects_the_one_that_takes_its_row()
    {
        var app = new AppDriver(height: 500f);
        var (d, tab, line) = await OutlineAtAsync("return First();", app.Driver);
        var firstToggle = Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition("foreach").Line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(firstToggle);
        var secondToggle = Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition("needle").Line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(secondToggle);
        var thirdToggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(thirdToggle);
        d.Ui.Panels.Open(DockView.Bookmarks);
        app.Run(3);
        var bookmarks = d.AppState.Bookmarks;
        var panel = app.Ui.BookmarksPanel;
        var third = bookmarks.Items[2];
        d.InTurn(() => BookmarksPanelView.Step(panel, d.AppState, 1));
        d.InTurn(() => BookmarksPanelView.Step(panel, d.AppState, 1));
        app.Run(2);

        BookmarksPanelView.Remove(panel, bookmarks, bookmarks.Items[1]);
        app.Run(2);

        Assert.Same(third, panel.Selection.Item);
    }

    [Fact]
    public async Task Clicking_a_name_only_selects_its_cell()
    {
        var (app, d, _, _) = await ShowBookmarkAsync();

        Click(app, "Bookmark 1");
        app.Frames.Frame(2);

        Assert.Null(d.Ui.BookmarksPanel.Renamer.Bookmark);
        Assert.True(d.Ui.BookmarksPanel.Cells.IsAt(0, BookmarksPanelView.NameColumn));
    }

    [Fact]
    public async Task Double_clicking_a_name_selects_it_to_type_over_and_Enter_keeps_the_name()
    {
        var (app, d, _, _) = await ShowBookmarkAsync();

        DoubleClick(app, "Bookmark 1");
        app.Frames.Type("Calls First");
        app.Frames.Frame(2);
        app.Frames.Key(KeyboardKey.Enter);
        app.Frames.Frame(2);

        Assert.Equal("Calls First", d.AppState.Bookmarks.Items[0].Name);
        Assert.Null(d.Ui.BookmarksPanel.Renamer.Bookmark);
        Assert.True(app.Frames.Drew("Calls First"));
    }

    [Fact]
    public async Task Escape_leaves_a_renamed_name_as_it_was()
    {
        var (app, d, _, _) = await ShowBookmarkAsync();

        DoubleClick(app, "Bookmark 1");
        app.Frames.Type("Gone");
        app.Frames.Frame(2);
        app.Frames.Key(KeyboardKey.Escape);
        app.Frames.Frame(2);

        Assert.Equal("Bookmark 1", d.AppState.Bookmarks.Items[0].Name);
        Assert.Null(d.Ui.BookmarksPanel.Renamer.Bookmark);
    }

    [Fact]
    public async Task Clicking_a_location_selects_its_cell_for_Ctrl_C()
    {
        var (app, d, _, _) = await ShowBookmarkAsync();
        var location = d.AppState.Bookmarks.Items[0].Location;

        Click(app, location);
        app.Frames.Key(Shortcuts.Copy);
        app.Frames.Frame();

        Assert.Equal(location, app.Frames.Clipboard.Text);
        Assert.Null(d.Ui.BookmarksPanel.Renamer.Bookmark);
    }

    [Fact]
    public async Task F2_renames_the_selected_bookmark_in_place_and_Enter_keeps_the_name()
    {
        var (app, d, _, _) = await ShowBookmarkAsync();
        Click(app, d.AppState.Bookmarks.Items[0].Location);

        app.Frames.Key(KeyboardKey.F2);
        app.Frames.Frame(2);
        app.Frames.Type("Calls First");
        app.Frames.Frame(2);
        app.Frames.Key(KeyboardKey.Enter);
        app.Frames.Frame(2);

        Assert.Equal("Calls First", d.AppState.Bookmarks.Items[0].Name);
        Assert.Null(d.Ui.BookmarksPanel.Renamer.Bookmark);
        Assert.True(app.Frames.Drew("Calls First"));
    }

    [Fact]
    public async Task The_bookmarks_panel_is_a_table_whose_header_lines_up_with_its_rows()
    {
        var (app, _, _, line) = await ShowBookmarkAsync(frames: 4);

        Assert.Equal(app.TextRect("Name").X, app.TextRect("Bookmark 1").X);
        Assert.Equal(app.TextRect("Location").X, app.TextRect("Outline:" + (line + 1)).X);
        var module = app.Frames.Canvas.Texts.Single(t => t.Inputs.Text.EndsWith(".dll", StringComparison.Ordinal)).Inputs.Position.Left;
        Assert.Equal(app.TextRect("Module").X, module);
    }

    [Fact]
    public void The_bookmark_column_widths_are_saved_with_the_settings()
    {
        var d = AppStateDriver.Started(_temp.Settings());
        d.Ui.BookmarksPanel.Columns.Restore([1f, 1f, 4f, 1f]);
        d.NextFrame();
        var write = d.AppState.SettingsSaver.Save(d.AppState);
        Assert.IsType<SettingsWriteResult.Written>(write);

        var restored = AppStateDriver.Started(_temp.Settings()).Ui.BookmarksPanel.Columns.Weights;

        Assert.Equal([1f, 1f, 4f, 1f], restored);
    }

    private static void ImportBookmarks(AppStateDriver d, string path)
    {
        d.Start((a, ct) => a.BookmarkImporter.ImportAsync(a.Bookmarks, a.Imports, path, ct));
    }

    private static void FinishBookmarkImport(AppStateDriver d, PendingImport import, BookmarkImportMode? mode)
    {
        d.AppState.BookmarkImporter.FinishImport(d.AppState.Bookmarks, d.AppState.Imports, import, mode);
    }

    private static void ExportBookmarks(AppStateDriver d, string path)
    {
        d.AppState.ExportBookmarksHandler.Handle(d.AppState.Bookmarks, path);
    }

    private async Task<string> ExportedAsync(params string[] lines)
    {
        return await ExportedAsAsync("exported.json", lines);
    }

    private async Task<string> ExportedAsAsync(string name, params string[] lines)
    {
        Directory.CreateDirectory(_temp.Path);
        var path = _temp.PathOf(name);
        var (d, tab, _) = await OutlineAtAsync("return First();");
        foreach (var text in lines)
        {
            var toggle = Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition(text).Line, Names.Bookmark);
            UnionAsserts.GetCase<BookmarkToggle.Added>(toggle);
        }

        ExportBookmarks(d, path);
        Assert.Empty(d.AppState.Errors.Entries);
        return path;
    }

    [Fact]
    public async Task Exported_bookmarks_import_at_once_into_an_app_state_with_none()
    {
        var path = await ExportedAsync("return First();", "foreach");
        var (d, tab, line) = await OutlineAtAsync("return First();");

        ImportBookmarks(d, path);
        d.Settle();

        Assert.Null(d.AppState.Imports.Asking);
        Assert.Equal(["Bookmark 1", "Bookmark 2"], d.AppState.Bookmarks.Items.Select(b => b.Name));
        Assert.NotNull(tab.Node);
        Assert.True(d.AppState.Bookmarks.ListMarkedLines(tab.Document, tab.Node)[line]);
    }

    [Fact]
    public async Task Importing_beside_bookmarks_asks_then_merges_only_new_lines_or_replaces()
    {
        var path = await ExportedAsync("return First();", "foreach");
        var (d, tab, line) = await OutlineAtAsync("return First();");
        var bookmarks = d.AppState.Bookmarks;
        var toggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(toggle);
        bookmarks.Rename(bookmarks.Items[0], "Mine");

        ImportBookmarks(d, path);
        d.Settle();
        var asked = d.AppState.Imports.Asking;
        Assert.NotNull(asked);
        FinishBookmarkImport(d, asked, BookmarkImportMode.Merge);
        var merged = bookmarks.Items.Select(b => b.Name).ToList();
        ImportBookmarks(d, path);
        d.Settle();
        var again = d.AppState.Imports.Asking;
        Assert.NotNull(again);
        FinishBookmarkImport(d, again, BookmarkImportMode.Replace);

        Assert.Equal(["Mine", "Bookmark 2"], merged);
        Assert.Equal(["Bookmark 1", "Bookmark 2"], bookmarks.Items.Select(b => b.Name));
        Assert.Null(d.AppState.Imports.Asking);
    }

    [Fact]
    public async Task Overlapping_imports_each_wait_with_their_own_bookmarks_and_are_asked_in_turn()
    {
        var first = await ExportedAsAsync("first.json", "foreach");
        var second = await ExportedAsAsync("second.json", "needle");
        var (d, tab, line) = await OutlineAtAsync("return First();");
        var toggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(toggle);

        ImportBookmarks(d, first);
        ImportBookmarks(d, second);
        d.Settle();
        var asked = d.AppState.Imports.Asking;
        Assert.NotNull(asked);
        FinishBookmarkImport(d, asked, BookmarkImportMode.Merge);
        var next = d.AppState.Imports.Asking;
        Assert.NotNull(next);
        FinishBookmarkImport(d, next, BookmarkImportMode.Merge);

        Assert.NotSame(asked, next);
        Assert.Equal(3, d.AppState.Bookmarks.Items.Count);
        Assert.Null(d.AppState.Imports.Asking);
    }

    [Fact]
    public async Task A_file_that_is_not_a_bookmarks_file_is_refused_and_changes_nothing()
    {
        var path = _temp.Write("other.json", "{ \"assemblies\": [] }");
        var (d, tab, line) = await OutlineAtAsync("return First();");
        var toggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(toggle);

        ImportBookmarks(d, path);
        d.Settle();

        Assert.Single(d.AppState.Bookmarks.Items);
        Assert.Null(d.AppState.Imports.Asking);
        Refusal refused = UnionAsserts.GetCase<Refusal.NotBookmarksFile>(d.AppState.Refusals.Shown);
        Assert.Equal(("Cannot import bookmarks from other.json", "Not a bookmarks file."), refused.Words(Strings.For(Locale.En)));
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void Read_JsonWithoutABookmarksList_IsNotABookmarksFile()
    {
        var path = _temp.Write("other.json", "{ \"assemblies\": [] }");

        UnionAsserts.GetCase<BookmarksRead.NotBookmarksFile>(BookmarkFile.Read(path));
    }

    [Fact]
    public void Read_FileThatIsNotJson_FailsNamingTheFile()
    {
        var path = _temp.Write("broken.json", "not json");

        var failed = UnionAsserts.GetCase<BookmarksRead.Failed>(BookmarkFile.Read(path));

        Assert.StartsWith(path + Environment.NewLine, failed.Reason);
    }

    [Fact]
    public void A_file_that_cannot_be_read_is_reported_with_why_and_changes_nothing()
    {
        Directory.CreateDirectory(_temp.Path);
        var path = _temp.PathOf("missing.json");
        var d = AppStateDriver.Started();

        ImportBookmarks(d, path);
        d.Settle();

        Assert.Empty(d.AppState.Bookmarks.Items);
        var words = Assert.Single(d.AppState.Errors.Entries).Words();
        Assert.Equal("Cannot import bookmarks from missing.json", words.Title);
        Assert.StartsWith(path + Environment.NewLine, words.Details);
    }

    [Fact]
    public async Task Import_in_the_panel_picks_a_file_and_the_dialog_merges_it()
    {
        var path = await ExportedAsync("foreach");
        var (app, d, _, _) = await ShowBookmarkAsync();
        app.Frames.FilePicker.Answer = path;

        app.Frames.ClickOn(Selector.AtPath("dock/bookmarks/import"));
        app.Frames.Frame();
        app.Settle(2);
        var asked = app.Frames.Drew("Import bookmarks");
        app.Frames.ClickOn(Selector.WithText("Merge"));
        app.Frames.Frame(2);

        Assert.True(asked);
        Assert.Equal(2, d.AppState.Bookmarks.Items.Count);
        Assert.False(app.Frames.Drew("Merge"));
    }

    private const string Fixtures = AppStateDriver.FixturesNamespace;

    private static int TokenOf(string method)
    {
        return (typeof(Fixtures.Outline).GetMethod(method) ?? throw new MissingMethodException("Outline", method)).MetadataToken;
    }

    private string ILSpyFile(string records)
    {
        return _temp.Write("ILSpy.Bookmarks.json", $$"""{ "Version": 1, "Bookmarks": [ {{records}} ] }""");
    }

    // ILSpy records the full name of the member token names as well as the token.
    private static string BuildRecord(string name, string kind, int token, int ilOffset, int lineNumber, string treePath, string? file = null,
        string memberName = Fixtures + ".Outline.Third")
    {
        return System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["Name"] = name,
            ["Enabled"] = true,
            ["FileName"] = file ?? AppStateDriver.FixturePath,
            ["AssemblyFullName"] = "ILSight.App.Tests",
            ["ModuleName"] = "ILSight.App.Tests.dll",
            ["Token"] = token,
            ["Kind"] = kind,
            ["ILOffset"] = ilOffset,
            ["LineNumber"] = lineNumber,
            ["MemberName"] = memberName,
            ["LocationNodeName"] = Fixtures + ".Outline",
            ["ViewState"] = new Dictionary<string, object?>
            {
                ["Version"] = 1,
                ["CaretOffset"] = 0,
                ["VerticalOffset"] = 0.0,
                ["HorizontalOffset"] = 0.0,
                ["FoldingChecksum"] = null,
                ["ExpandedFoldings"] = null,
                ["RenderedLayoutSettings"] = null,
                ["SelectedTreeNodePath"] = new[] { file ?? AppStateDriver.FixturePath, Fixtures, treePath },
            },
        });
    }

    [Fact]
    public async Task Exports_are_written_in_ILSpys_bookmark_format()
    {
        Directory.CreateDirectory(_temp.Path);
        var path = _temp.PathOf("exported.json");
        var (d, tab, line) = await OutlineAtAsync("return First();");
        var toggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(toggle);

        ExportBookmarks(d, path);
        using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var root = json.RootElement;
        var entry = root.GetProperty("Bookmarks")[0];

        Assert.Equal(1, root.GetProperty("Version").GetInt32());
        Assert.Equal(
            ["Name", "Enabled", "FileName", "AssemblyFullName", "ModuleName", "Token", "Kind", "ILOffset", "LineNumber", "MemberName", "LocationNodeName",
                "ViewState", "Language", "Path"],
            entry.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Body", entry.GetProperty("Kind").GetString());
        Assert.Equal((uint)TokenOf("Third"), entry.GetProperty("Token").GetUInt32());
        Assert.Equal(line + 1, entry.GetProperty("LineNumber").GetInt32());
        Assert.Equal(AppStateDriver.FixturePath, entry.GetProperty("FileName").GetString());
        Assert.Equal("ILSight.App.Tests.dll", entry.GetProperty("ModuleName").GetString());
        Assert.Equal(Fixtures + ".Outline.Third", entry.GetProperty("MemberName").GetString());
        Assert.Equal(Fixtures + ".Outline", entry.GetProperty("LocationNodeName").GetString());
        Assert.Equal([AppStateDriver.FixturePath, Fixtures, Fixtures + ".Outline"],
            entry.GetProperty("ViewState").GetProperty("SelectedTreeNodePath").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task A_file_ILSpy_wrote_imports_with_each_bookmark_placed_by_ILSpys_tree_path()
    {
        var path = ILSpyFile(BuildRecord("Bookmark0", "Body", TokenOf("Third"), 0, 99, Fixtures + ".Outline") + "," +
                             BuildRecord("Bookmark1", "Line", TokenOf("Third"), 0, 3, Fixtures + ".Outline"));
        var (d, tab, line) = await OutlineAtAsync("return First();");

        ImportBookmarks(d, path);
        d.Settle();
        var bookmarks = d.AppState.Bookmarks.Items;
        Assert.NotNull(tab.Node);
        var marked = d.AppState.Bookmarks.ListMarkedLines(tab.Document, tab.Node);

        Assert.Equal(["Bookmark0", "Bookmark1"], bookmarks.Select(b => b.Name));
        Assert.All(bookmarks, b => Assert.Equal(tab.Location, b.Place));
        Assert.True(marked[line]);
        Assert.True(marked[2]);
        Assert.Equal("Bookmark0", Assert.Single(d.AppState.Bookmarks.ListRecords(), r => r.Kind == BookmarkKind.Body).Name);
    }

    [Fact]
    public void Bookmarks_from_a_file_marking_one_line_replace_as_one_and_say_how_many_were_left_out()
    {
        var twins = BuildRecord("First", "Body", TokenOf("Third"), 0, 1, Fixtures + ".Outline") + "," +
                    BuildRecord("Second", "Body", TokenOf("Third"), 0, 1, Fixtures + ".Outline") + "," +
                    BuildRecord("Third", "Body", TokenOf("Third"), 0, 1, Fixtures + ".Outline");
        var path = ILSpyFile(twins);
        var d = AppStateDriver.Started();

        ImportBookmarks(d, path);
        d.Settle();

        Assert.Equal("First", Assert.Single(d.AppState.Bookmarks.Items).Name);
        Refusal leftOut = UnionAsserts.GetCase<Refusal.BookmarksLeftOut>(d.AppState.Refusals.Shown);
        Assert.Equal("2 bookmarks marked the same line as others and were left out", leftOut.Words(Strings.For(Locale.En)).What);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public async Task Merging_skips_bookmarks_marking_a_line_already_marked_without_a_word()
    {
        var path = await ExportedAsync("return First();");
        var (d, tab, line) = await OutlineAtAsync("return First();");
        var toggle = Bookmarks.ToggleBookmark(d.AppState, tab, line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(toggle);

        ImportBookmarks(d, path);
        d.Settle();
        var asked = d.AppState.Imports.Asking;
        Assert.NotNull(asked);
        FinishBookmarkImport(d, asked, BookmarkImportMode.Merge);

        Assert.Single(d.AppState.Bookmarks.Items);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void A_bookmark_in_a_file_that_is_not_one_at_all_is_left_out_alone_and_reported()
    {
        var path = ILSpyFile("""{ "Name": "Broken", "Token": "not a number" }, """ +
                             BuildRecord("Kept", "Body", TokenOf("Third"), 0, 1, Fixtures + ".Outline"));
        var d = AppStateDriver.Started();

        ImportBookmarks(d, path);
        d.Settle();

        Assert.Equal("Kept", Assert.Single(d.AppState.Bookmarks.Items).Name);
        var words = Assert.Single(d.AppState.Errors.Entries).Words();
        Assert.Equal("Some bookmarks in ILSpy.Bookmarks.json could not be read and were left out", words.Title);
        Assert.StartsWith("#1: ", words.Details);
    }

    [Fact]
    public void Read_EntryThatIsNotABookmark_LeavesItOutByItsPosition()
    {
        var path = ILSpyFile(BuildRecord("Kept", "Body", TokenOf("Third"), 0, 1, Fixtures + ".Outline") + "," +
                             """{ "Name": "Broken", "Token": "not a number" }""");

        var read = UnionAsserts.GetCase<BookmarksRead.Read>(BookmarkFile.Read(path));

        Assert.Equal("Kept", Assert.Single(read.Bookmarks).Name);
        Assert.Equal(2, UnionAsserts.GetCase<BookmarkEntryProblem.NotABookmark>(Assert.Single(read.Problems)).Entry);
    }

    [Fact]
    public void A_bookmark_whose_saved_place_cannot_be_read_is_placed_as_ILSpy_places_it_and_reported_by_its_position()
    {
        var unreadablePlace = """{ "Path": { "Version": 99, "Steps": [] }, """ + BuildRecord("Placed", "Body", TokenOf("Third"), 0, 1, Fixtures + ".Outline")[1..];
        var path = ILSpyFile(unreadablePlace);
        var d = AppStateDriver.Started();

        ImportBookmarks(d, path);
        d.Settle();

        Assert.Equal("Placed", Assert.Single(d.AppState.Bookmarks.Items).Name);
        var words = Assert.Single(d.AppState.Errors.Entries).Words();
        Assert.Equal("Some bookmarks in ILSpy.Bookmarks.json could not be read and were left out", words.Title);
        Assert.Equal("#1: the place it was made in cannot be read.", words.Details);
    }

    [Fact]
    public void A_bookmark_whose_tree_path_is_stale_is_placed_by_its_token_and_one_with_no_file_is_said_left_out()
    {
        var gone = BuildRecord("Gone", "Token", TokenOf("Third"), 0, 1, Fixtures + ".Outline", _temp.PathOf("gone.dll"));
        var path = ILSpyFile(BuildRecord("Moved", "Token", TokenOf("Third"), 0, 1, Fixtures + ".Renamed") + "," + gone);
        var d = AppStateDriver.Started();

        ImportBookmarks(d, path);
        d.Settle();

        var moved = Assert.Single(d.AppState.Bookmarks.Items);
        Assert.Equal("Moved", moved.Name);
        Assert.StartsWith("Third()", moved.Place.Label);
        Refusal lost = UnionAsserts.GetCase<Refusal.ImportedBookmarksLost>(d.AppState.Refusals.Shown);
        Assert.Equal("Some bookmarks from ILSpy.Bookmarks.json were not found and were left out", lost.Words(Strings.For(Locale.En)).What);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void A_bookmark_whose_token_now_names_another_member_is_said_left_out_rather_than_placed_there()
    {
        var path = ILSpyFile(BuildRecord("Rebuilt", "Token", TokenOf("Third"), 0, 1, Fixtures + ".Renamed", memberName: Fixtures + ".Outline.Removed"));
        var d = AppStateDriver.Started();

        ImportBookmarks(d, path);
        d.Settle();

        Assert.Empty(d.AppState.Bookmarks.Items);
        Refusal lost = UnionAsserts.GetCase<Refusal.ImportedBookmarksLost>(d.AppState.Refusals.Shown);
        Assert.Equal("Some bookmarks from ILSpy.Bookmarks.json were not found and were left out", lost.Words(Strings.For(Locale.En)).What);
        Assert.Empty(d.AppState.Errors.Entries);
    }
}
