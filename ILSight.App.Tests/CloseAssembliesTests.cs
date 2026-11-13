using ILSight.Core;
using ILSight.DiagramArea;
using ZGF.KeyboardModule;
using ILSight.CommandArea;
using ILSight.AssemblyArea;

namespace ILSight.App.Tests;

public sealed class CloseAssembliesTests
{
    private static readonly string KeyboardPath = typeof(KeyboardKey).Assembly.Location;
    private static readonly string CorePath = typeof(AssemblyList).Assembly.Location;

    private static AppStateDriver OpenedThree()
    {
        var d = AppStateDriver.Opened();
        d.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(KeyboardPath)], ct));
        d.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(CorePath)], ct));
        d.RunUntil(() => d.AppState.Assemblies.Listed.Count == 3);
        d.Settle();
        return d;
    }

    private static LoadedAssembly Listed(AppStateDriver d, string path)
    {
        return d.AppState.Assemblies.Listed.Single(a => a.FilePath.Text == path);
    }

    private static List<string> ListedPaths(AppStateDriver d)
    {
        return d.AppState.Assemblies.Listed.Select(static a => a.FilePath.Text).ToList();
    }

    [Fact]
    public void The_dialog_lists_every_item_in_the_list_order_with_the_picked_one_ticked()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;

        closer.Open(d.AppState.Assemblies, Listed(d, KeyboardPath));

        Assert.True(closer.IsOpen);
        Assert.Equal(ListedPaths(d), closer.Rows.Order.Select(static c => c.Assembly.FilePath.Text));
        Assert.Equal([false, true, false], closer.Rows.Order.Select(static c => c.Included));
    }

    [Fact]
    public void With_nothing_picked_nothing_starts_ticked()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;
        var core = Listed(d, CorePath);

        closer.Open(d.AppState.Assemblies, null);
        Assert.All(closer.Rows.Order, static c => Assert.False(c.Included));

        closer.Open(d.AppState.Assemblies, core);
        Assert.Equal([core], closer.Rows.ListToClose());
    }

    [Fact]
    public void Ticking_all_shown_ticks_only_what_the_filter_shows_and_close_takes_every_ticked_one()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;
        closer.Open(d.AppState.Assemblies, Listed(d, AppStateDriver.FixturePath));

        closer.Filter = "ILSight.Core";
        var shown = AssemblyClosing.ListShown(closer);
        AssemblyClosing.IncludeShown(shown, true);

        Assert.Equal([CorePath], shown.Select(static c => c.Assembly.FilePath.Text));
        Assert.Equal(true, AssemblyClosing.CalculateAllShownIncluded(shown));
        Assert.Equal([AppStateDriver.FixturePath, CorePath], closer.Rows.ListToClose().Select(static a => a.FilePath.Text));
    }

    [Fact]
    public void Selected_only_shows_the_ticked_ones_and_clearing_the_filter_shows_all_again()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;
        closer.Open(d.AppState.Assemblies, Listed(d, KeyboardPath));

        closer.SelectedOnly = true;
        Assert.Equal([KeyboardPath], AssemblyClosing.ListShown(closer).Select(static c => c.Assembly.FilePath.Text));

        closer.ClearFilter();
        Assert.Equal(3, AssemblyClosing.ListShown(closer).Count);
    }

    [Fact]
    public void A_shift_click_gives_the_range_from_the_anchor_the_clicked_tick()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;
        closer.Open(d.AppState.Assemblies, null);
        var shown = AssemblyClosing.ListShown(closer);

        shown[0].Included = true;
        closer.Rows.TickClicked(shown, shown[0], extend: false);
        shown[2].Included = true;
        closer.Rows.TickClicked(shown, shown[2], extend: true);

        Assert.Equal([true, true, true], closer.Rows.Order.Select(static c => c.Included));
        Assert.Equal(3, closer.Rows.CountIncluded());
    }

    [Fact]
    public void Closing_takes_the_ticked_items_off_the_list_and_leaves_the_rest()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;
        closer.Open(d.AppState.Assemblies, Listed(d, KeyboardPath));
        closer.Rows.Order[2].Included = true;

        d.InTurn(() => Commands.CloseAssemblies(d.AppState, closer.Rows.ListToClose()));

        Assert.Equal([AppStateDriver.FixturePath], ListedPaths(d));
    }

    [Fact]
    public async Task Closing_an_analyzed_assembly_from_the_dialog_drops_its_analysis_and_keeps_the_others()
    {
        var app = AppDriver.Opened();
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(KeyboardPath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 2, AppStateDriver.Patience);
        app.Run(2);
        var keyboard = Assert.IsType<LoadedModule>(app.AppState.Assemblies.Listed.Single(static a => a.FilePath.Text == KeyboardPath));
        app.Analyze(await app.FixtureAsync("Greeter"));
        app.Analyze(await keyboard.ReadRootAsync().GetChildAsync("ZGF.KeyboardModule", "KeyboardKey"));
        app.Settle(2);
        Assert.Equal(2, app.AppState.Analyzer.Roots.Count);

        var fixtures = app.AppState.Assemblies.Listed.Single(static a => a.FilePath.Text == AppStateDriver.FixturePath);
        app.Ui.Closer.Open(app.AppState.Assemblies, fixtures);
        app.Run(2);
        app.Frames.ClickOn(Selector.AtPath("close-ticked")).Frame(2);
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 1, AppStateDriver.Patience);
        app.Run(2);

        var left = Assert.IsType<AnalyzerSymbolNode>(Assert.Single(app.AppState.Analyzer.Roots));
        Assert.Equal("KeyboardKey", left.Symbol.Label);
    }

    [Fact]
    public void A_list_changed_while_the_dialog_is_up_is_shown_with_the_ticks_kept()
    {
        var d = AppStateDriver.Opened();
        d.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(KeyboardPath)], ct));
        d.RunUntil(() => d.AppState.Assemblies.Listed.Count == 2);
        var closer = d.Ui.Closer;
        closer.Open(d.AppState.Assemblies, Listed(d, KeyboardPath));

        d.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(CorePath)], ct));
        d.RunUntil(() => d.AppState.Assemblies.Listed.Count == 3);
        closer.Rows.Update(d.AppState.Assemblies);

        Assert.Equal(ListedPaths(d), closer.Rows.Order.Select(static c => c.Assembly.FilePath.Text));
        Assert.Equal([KeyboardPath], closer.Rows.ListToClose().Select(static a => a.FilePath.Text));
    }

    [Fact]
    public void The_trash_button_opens_the_dialog_and_its_close_button_closes_the_ticked_ones()
    {
        var app = AppDriver.Opened();
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(KeyboardPath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 2, AppStateDriver.Patience);
        app.Run(2);

        app.Frames.ClickOn(Selector.AtPath("dock/assemblies/close-assembly")).Frame(2);
        var closer = app.Ui.Closer;
        Assert.True(closer.IsOpen);
        closer.Rows.Order[1].Included = true;
        app.Run(2);
        app.Frames.ClickOn(Selector.AtPath("close-ticked")).Frame(2);
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 1, AppStateDriver.Patience);

        Assert.False(closer.IsOpen);
        Assert.Equal(AppStateDriver.FixturePath, Assert.Single(app.AppState.Assemblies.Listed).FilePath.Text);
    }

    private static readonly string KeyboardFile = Path.GetFileName(KeyboardPath);

    private static async Task<AppDriver> ShowingGreeterAndKeyboardKeyAsync()
    {
        var app = AppDriver.Opened();
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(KeyboardPath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 2, AppStateDriver.Patience);
        app.Run(2);
        app.Show(await app.FixtureAsync("Greeter"), 2);
        app.Show(await KeyboardKeyOfAsync(app), 2, inNewTab: true);
        return app;
    }

    private static LoadedModule KeyboardOf(AppDriver app)
    {
        return Assert.IsType<LoadedModule>(app.AppState.Assemblies.Listed.Single(static a => a.FilePath.Text == KeyboardPath));
    }

    private static async Task<BrowseNode> KeyboardKeyOfAsync(AppDriver app)
    {
        return await KeyboardOf(app).ReadRootAsync().GetChildAsync("ZGF.KeyboardModule", "KeyboardKey");
    }

    private static DocumentTab TabShowing(AppDriver app, string title)
    {
        return app.AppState.Navigation.OpenTabs.Single(t => t.Title == title);
    }

    private static void CloseKeyboardFromTheDialog(AppDriver app)
    {
        app.Ui.Closer.Open(app.AppState.Assemblies, KeyboardOf(app));
        app.Run(2);
        app.Frames.ClickOn(Selector.AtPath("close-ticked")).Frame(2);
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 1, AppStateDriver.Patience);
        app.Run(2);
    }

    [Fact]
    public async Task Closing_an_assembly_leaves_its_tab_with_its_last_code_and_says_it_was_closed()
    {
        var app = await ShowingGreeterAndKeyboardKeyAsync();
        var tab = TabShowing(app, "KeyboardKey");
        var shown = Assert.IsType<TabContent.Code>(tab.Content);
        Assert.Null(tab.Load.ClosedAssembly);

        CloseKeyboardFromTheDialog(app);

        Assert.Same(tab, app.AppState.Navigation.ActiveTab);
        Assert.Same(shown.Document, Assert.IsType<TabContent.Code>(tab.Content).Document);
        Assert.Equal(KeyboardFile, tab.Load.ClosedAssembly);
        Assert.True(app.Frames.Drew($"{KeyboardFile} was closed."));
        Assert.Null(TabShowing(app, "Greeter").Load.ClosedAssembly);
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public async Task Switching_the_language_after_a_close_decompiles_the_tabs_of_listed_assemblies_but_not_the_closed_ones()
    {
        var app = await ShowingGreeterAndKeyboardKeyAsync();
        CloseKeyboardFromTheDialog(app);
        var closed = TabShowing(app, "KeyboardKey");
        var code = closed.Document;

        app.AppState.Decompiler.UseLanguage(DecompilationLanguage.IL);
        app.Settle(2);
        var inFront = closed.Load.Language;
        app.Frames.ClickOn(Targets.GetTab("Greeter")).Frame(2);
        app.Settle(2);

        Assert.Equal(DecompilationLanguage.CSharp, inFront);
        Assert.Equal(DecompilationLanguage.CSharp, closed.Load.Language);
        Assert.Same(code, closed.Document);
        Assert.False(closed.Load.IsBusy);
        Assert.Equal(DecompilationLanguage.IL, TabShowing(app, "Greeter").Load.Language);
    }

    [Fact]
    public async Task Changing_a_decompiler_option_after_a_close_decompiles_the_tabs_of_listed_assemblies_but_not_the_closed_ones()
    {
        var app = await ShowingGreeterAndKeyboardKeyAsync();
        CloseKeyboardFromTheDialog(app);
        var closed = TabShowing(app, "KeyboardKey");
        var code = closed.Document;
        var shownWith = closed.Load.Options;
        app.Frames.ClickOn(Targets.GetTab("Greeter")).Frame(2);
        app.Settle(2);
        var stringConcat = DecompilerToggles.FindToggle("StringConcat");
        Assert.NotNull(stringConcat);

        Preferences.SetDecompilerToggle(app.AppState, stringConcat, false);
        app.Settle(2);
        var greeter = TabShowing(app, "Greeter");
        var greeterText = Documents.GetText(greeter.Document);
        app.Frames.ClickOn(Targets.GetTab("KeyboardKey")).Frame(2);
        app.Settle(2);

        Assert.Contains("string.Concat", greeterText);
        Assert.Equal(app.AppState.Decompiler.Setup.Options, greeter.Load.Options);
        Assert.NotEqual(shownWith, app.AppState.Decompiler.Setup.Options);
        Assert.Same(closed, app.AppState.Navigation.ActiveTab);
        Assert.Equal(shownWith, closed.Load.Options);
        Assert.Same(code, closed.Document);
        Assert.False(closed.Load.IsBusy);
    }

    [Fact]
    public async Task A_decompile_still_on_its_way_when_its_assembly_closes_is_dropped_and_the_tab_says_it_was_closed()
    {
        var app = await ShowingGreeterAndKeyboardKeyAsync();
        var keyboard = KeyboardOf(app);
        var keyboardKey = await KeyboardKeyOfAsync(app);
        app.Driver.StartShowing(keyboardKey, inNewTab: true);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Assert.IsType<TabContent.Loading>(tab.Content);

        app.Driver.InTurn(() => Commands.CloseAssemblies(app.AppState, [keyboard]));
        app.Settle(2);

        Assert.Same(tab, app.AppState.Navigation.ActiveTab);
        Assert.Equal(new TabContent.Stopped(keyboardKey), tab.Content);
        Assert.Equal(KeyboardFile, tab.Load.ClosedAssembly);
        Assert.False(tab.Load.IsBusy);
        Assert.True(app.Frames.Drew($"{KeyboardFile} was closed."));
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public async Task Reading_the_list_again_leaves_the_tab_of_a_closed_assembly_as_it_was()
    {
        var app = await ShowingGreeterAndKeyboardKeyAsync();
        CloseKeyboardFromTheDialog(app);
        var closed = TabShowing(app, "KeyboardKey");
        var shown = closed.Content;

        app.Start((a, ct) => a.AssemblyReloading.RefreshAssembliesAsync(a.Lists, a.ListSession, a.ViewCommands, a.Decompiler, [], ct));
        app.Settle(4);

        Assert.Same(shown, closed.Content);
        Assert.Equal(KeyboardFile, closed.Load.ClosedAssembly);
        Assert.Empty(app.AppState.Errors.Entries);
    }

    private static async Task<(AppDriver App, DocumentTab Tab)> ClosedKeyboardKeyWithCaretOnItAsync()
    {
        var app = await ShowingGreeterAndKeyboardKeyAsync();
        CloseKeyboardFromTheDialog(app);
        var tab = TabShowing(app, "KeyboardKey");
        var code = DocumentPanels.GetView(app.Ui.Documents, tab).Code;
        var bar = app.TextRect($"{KeyboardFile} was closed.");
        app.Frames.Click(bar.X + 300f, bar.Y + 150f).Frame();
        var (line, column) = tab.Document.GetPosition("enum KeyboardKey");
        var name = new TextPosition { Line = line, Column = column + "enum ".Length + 1 };
        code.Selection.Select(name, name);
        return (app, tab);
    }

    private static void PressAnalyzeSixTimes(AppDriver app)
    {
        for (var pressed = 0; pressed < 6; pressed++)
        {
            app.Frames.Key(Shortcuts.Analyze).Frame(2);
        }

        app.Settle(2);
    }

    private static int CountDrawn(AppDriver app, string text)
    {
        return app.Frames.Canvas.Texts.Count(t => t.Inputs.Text == text);
    }

    [Fact]
    public async Task Six_actions_that_would_read_a_closed_assembly_are_refused_on_the_refusal_line_and_report_no_error()
    {
        var (app, _) = await ClosedKeyboardKeyWithCaretOnItAsync();

        PressAnalyzeSixTimes(app);

        Assert.Empty(app.AppState.Errors.Entries);
        for (var count = 0; count <= 6; count++)
        {
            Assert.False(app.Frames.Drew(app.Ui.Strings.StatusErrors(count)));
        }

        Assert.Equal(new Refusal.AssemblyClosed { Assembly = KeyboardFile }, app.AppState.Refusals.Shown);
        Assert.Equal(2, CountDrawn(app, $"{KeyboardFile} was closed."));
    }

    [Fact]
    public async Task A_failure_reported_before_actions_a_closed_assembly_refuses_stays_alone_in_the_error_panel()
    {
        var (app, _) = await ClosedKeyboardKeyWithCaretOnItAsync();
        var failure = new AppProblem.Unexpected(new InvalidOperationException("Failed before the refusals."));
        app.Driver.InTurn(() => Errors.Report(app.AppState, failure));

        PressAnalyzeSixTimes(app);

        Assert.Same(failure, Assert.Single(app.AppState.Errors.Entries).Problem);
        Assert.True(app.Frames.Drew(app.Ui.Strings.StatusErrors(1)));
        Assert.Equal(new Refusal.AssemblyClosed { Assembly = KeyboardFile }, app.AppState.Refusals.Shown);
    }

    [Fact]
    public async Task Analyze_in_a_closed_assemblys_code_looks_nothing_up_and_says_why_where_pressed()
    {
        var (app, tab) = await ClosedKeyboardKeyWithCaretOnItAsync();

        app.Frames.Key(Shortcuts.Analyze).Frame(2);
        app.Settle(2);

        Assert.Empty(app.AppState.Analyzer.Roots);
        Assert.Single(app.AppState.Assemblies.Listed);
        Assert.Equal(KeyboardFile, tab.Load.ClosedAssembly);
        Assert.Equal(new Refusal.AssemblyClosed { Assembly = KeyboardFile }, app.AppState.Refusals.Shown);
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public async Task Opening_a_closed_assembly_again_revives_its_tab_which_reads_again_without_the_closed_bar()
    {
        var app = await ShowingGreeterAndKeyboardKeyAsync();
        CloseKeyboardFromTheDialog(app);
        var closed = TabShowing(app, "KeyboardKey");
        var before = closed.Node;
        Assert.Equal(KeyboardFile, closed.Load.ClosedAssembly);

        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(KeyboardPath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 2, AppStateDriver.Patience);
        app.Settle(4);
        app.Frames.ClickOn(Targets.GetTab("KeyboardKey")).Frame(2);
        app.Settle(2);

        Assert.Same(closed, TabShowing(app, "KeyboardKey"));
        Assert.Null(closed.Load.ClosedAssembly);
        Assert.NotNull(closed.Node);
        Assert.NotSame(before, closed.Node);
        Assert.Same(KeyboardOf(app), closed.Node.Assembly);
        Assert.IsType<TabContent.Code>(closed.Content);
        Assert.False(app.Frames.Drew($"{KeyboardFile} was closed."));
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public async Task A_tab_of_a_closed_assembly_is_left_out_of_the_saved_session_and_gone_after_a_restart()
    {
        using var temp = new TempDir();
        var app = new AppDriver(settings: temp.Settings());
        app.Driver.OpenFixtures();
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(KeyboardPath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 2, AppStateDriver.Patience);
        app.Run(2);
        app.Show(await app.FixtureAsync("Greeter"), 2);
        app.Show(await KeyboardKeyOfAsync(app), 2, inNewTab: true);
        app.Driver.Pump();
        var savedOpen = (temp.Settings().AssemblyLists.Single().Documents?.Tabs ?? []).Select(static t => t.Title).ToList();

        CloseKeyboardFromTheDialog(app);
        app.Driver.Pump();
        var saved = temp.Settings().AssemblyLists.Single().Documents?.Tabs ?? [];
        var restarted = new AppStateDriver(temp.Settings());
        restarted.Settle();

        Assert.Equal(["Greeter", "KeyboardKey"], savedOpen);
        Assert.Equal("KeyboardKey", TabShowing(app, "KeyboardKey").Title);
        Assert.Equal(["Greeter"], saved.Select(static t => t.Title));
        Assert.DoesNotContain(restarted.AppState.Navigation.OpenTabs, static t => t.Title == "KeyboardKey");
        Assert.Contains(restarted.AppState.Navigation.OpenTabs, static t => t.Title == "Greeter");
    }

    private static AppDriver OpenedWithKeyboard(TempDir temp)
    {
        var app = new AppDriver(settings: temp.Settings());
        app.Driver.OpenFixtures();
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(KeyboardPath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 2, AppStateDriver.Patience);
        app.Run(2);
        return app;
    }

    private static List<string?> SavedTitles(TempDir temp)
    {
        return (temp.Settings().AssemblyLists.Single().Documents?.Tabs ?? []).Select(static t => t.Title).ToList();
    }

    private static void OpenKeyboardAgain(AppDriver app)
    {
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(KeyboardPath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 2, AppStateDriver.Patience);
        app.Settle(4);
    }

    [Fact]
    public async Task A_diagram_of_a_closed_assembly_says_so_is_left_out_of_the_saved_session_and_revives_when_it_is_opened_again()
    {
        using var temp = new TempDir();
        var app = OpenedWithKeyboard(temp);
        DiagramView.ShowInNewDiagramLater(app.Ui, (await KeyboardKeyOfAsync(app)).Definition, "Keys");
        app.Frames.WaitUntil(() => app.AppState.Navigation.ActiveTab?.Diagram?.Cards.Count > 0, AppStateDriver.Patience);
        app.Settle(2);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var before = tab.Diagram;
        app.Driver.Pump();
        var savedOpen = SavedTitles(temp);

        CloseKeyboardFromTheDialog(app);
        app.Driver.Pump();
        var closed = tab.ClosedAssembly;
        var shownClosed = tab.Diagram;
        var drewBar = app.Frames.Drew($"{KeyboardFile} was closed.");
        var savedClosed = SavedTitles(temp);
        OpenKeyboardAgain(app);
        app.Frames.WaitUntil(() => tab.Diagram != before && tab.Diagram?.Cards.Count > 0 && tab.Diagram?.IsBusy == false, AppStateDriver.Patience);
        app.Run(2);

        Assert.Contains("Keys", savedOpen);
        Assert.Equal(KeyboardFile, closed);
        Assert.Same(before, shownClosed);
        Assert.True(drewBar);
        Assert.DoesNotContain("Keys", savedClosed);
        Assert.Null(tab.ClosedAssembly);
        var revived = tab.Diagram;
        Assert.NotNull(revived);
        Assert.Contains(revived.Cards, static c => c.Name == "KeyboardKey");
        Assert.False(app.Frames.Drew($"{KeyboardFile} was closed."));
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public void A_comparison_of_a_closed_assembly_says_so_is_left_out_of_the_saved_session_and_revives_when_it_is_opened_again()
    {
        using var temp = new TempDir();
        var app = OpenedWithKeyboard(temp);
        var fixture = Assert.IsType<LoadedModule>(app.AppState.Assemblies.Listed.Single(static a => a.FilePath.Text != KeyboardPath));
        app.StartComparing(Comparisons.CompareAssemblies(app.AppState.Navigation, app.AppState.Assemblies, fixture, KeyboardOf(app), "Compared"));
        app.Frames.WaitUntil(() => app.AppState.Navigation.ActiveTab?.Comparison?.Result is ComparisonResult.Compared, AppStateDriver.Patience);
        app.Settle(2);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var before = tab.Comparison;
        app.Driver.Pump();
        var savedOpen = SavedTitles(temp);

        CloseKeyboardFromTheDialog(app);
        app.Driver.Pump();
        var closed = tab.ClosedAssembly;
        var shownClosed = tab.Comparison?.Result;
        var drewBar = app.Frames.Drew($"{KeyboardFile} was closed.");
        var savedClosed = SavedTitles(temp);
        OpenKeyboardAgain(app);
        app.Frames.WaitUntil(() => tab.Comparison?.Result is ComparisonResult.Compared && tab.Comparison != before, AppStateDriver.Patience);
        app.Run(2);

        Assert.Contains("Compared", savedOpen);
        Assert.Equal(KeyboardFile, closed);
        Assert.IsType<ComparisonResult.Compared>(shownClosed);
        Assert.True(drewBar);
        Assert.DoesNotContain("Compared", savedClosed);
        Assert.Null(tab.ClosedAssembly);
        Assert.False(app.Frames.Drew($"{KeyboardFile} was closed."));
        Assert.Empty(app.AppState.Errors.Entries);
    }
}
