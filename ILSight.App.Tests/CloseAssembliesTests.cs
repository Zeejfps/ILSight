using ILSight.Core;
using ZGF.KeyboardModule;
using static ILSight.AssemblyArea.AssemblyClosing;
using static ILSight.Preferences;
using ILSight.CommandArea;

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

        OpenCloser(closer, d.AppState.Assemblies, Listed(d, KeyboardPath));

        Assert.True(closer.IsOpen);
        Assert.Equal(ListedPaths(d), closer.Order.Select(static c => c.Assembly.FilePath.Text));
        Assert.Equal([false, true, false], closer.Order.Select(static c => c.Included));
    }

    [Fact]
    public void With_nothing_picked_nothing_starts_ticked()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;
        var core = Listed(d, CorePath);

        OpenCloser(closer, d.AppState.Assemblies, null);
        Assert.All(closer.Order, static c => Assert.False(c.Included));

        OpenCloser(closer, d.AppState.Assemblies, core);
        Assert.Equal([core], ListToClose(closer));
    }

    [Fact]
    public void Ticking_all_shown_ticks_only_what_the_filter_shows_and_close_takes_every_ticked_one()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;
        OpenCloser(closer, d.AppState.Assemblies, Listed(d, AppStateDriver.FixturePath));

        closer.Filter = "ILSight.Core";
        var shown = ListShown(closer);
        IncludeShown(shown, true);

        Assert.Equal([CorePath], shown.Select(static c => c.Assembly.FilePath.Text));
        Assert.Equal(true, CalculateAllShownIncluded(shown));
        Assert.Equal([AppStateDriver.FixturePath, CorePath], ListToClose(closer).Select(static a => a.FilePath.Text));
    }

    [Fact]
    public void Selected_only_shows_the_ticked_ones_and_clearing_the_filter_shows_all_again()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;
        OpenCloser(closer, d.AppState.Assemblies, Listed(d, KeyboardPath));

        closer.SelectedOnly = true;
        Assert.Equal([KeyboardPath], ListShown(closer).Select(static c => c.Assembly.FilePath.Text));

        ClearFilter(closer);
        Assert.Equal(3, ListShown(closer).Count);
    }

    [Fact]
    public void A_shift_click_gives_the_range_from_the_anchor_the_clicked_tick()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;
        OpenCloser(closer, d.AppState.Assemblies, null);
        var shown = ListShown(closer);

        shown[0].Included = true;
        TickClicked(closer, shown, shown[0], extend: false);
        shown[2].Included = true;
        TickClicked(closer, shown, shown[2], extend: true);

        Assert.Equal([true, true, true], closer.Order.Select(static c => c.Included));
        Assert.Equal(3, CountIncluded(closer));
    }

    [Fact]
    public void Closing_takes_the_ticked_items_off_the_list_and_leaves_the_rest()
    {
        var d = OpenedThree();
        var closer = d.Ui.Closer;
        OpenCloser(closer, d.AppState.Assemblies, Listed(d, KeyboardPath));
        closer.Order[2].Included = true;

        Commands.CloseAssemblies(d.AppState, ListToClose(closer));

        Assert.Equal([AppStateDriver.FixturePath], ListedPaths(d));
    }

    [Fact]
    public void Closing_an_analyzed_assembly_from_the_dialog_drops_its_analysis_and_keeps_the_others()
    {
        var app = AppDriver.Opened();
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(KeyboardPath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 2, Patience);
        app.Run(2);
        var keyboard = Assert.IsType<LoadedModule>(app.AppState.Assemblies.Listed.Single(static a => a.FilePath.Text == KeyboardPath));
        app.Analyze(app.Fixture("Greeter"));
        app.Analyze(keyboard.Root.Child("ZGF.KeyboardModule", "KeyboardKey"));
        app.Settle(2);
        Assert.Equal(2, app.AppState.Analyzer.Roots.Count);

        var fixtures = app.AppState.Assemblies.Listed.Single(static a => a.FilePath.Text == AppStateDriver.FixturePath);
        OpenCloser(app.Ui.Closer, app.AppState.Assemblies, fixtures);
        app.Run(2);
        app.Frames.ClickOn(Selector.AtPath("close-ticked")).Frame(2);
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 1, Patience);
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
        OpenCloser(closer, d.AppState.Assemblies, Listed(d, KeyboardPath));

        d.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(CorePath)], ct));
        d.RunUntil(() => d.AppState.Assemblies.Listed.Count == 3);
        Update(closer, d.AppState.Assemblies);

        Assert.Equal(ListedPaths(d), closer.Order.Select(static c => c.Assembly.FilePath.Text));
        Assert.Equal([KeyboardPath], ListToClose(closer).Select(static a => a.FilePath.Text));
    }

    [Fact]
    public void The_trash_button_opens_the_dialog_and_its_close_button_closes_the_ticked_ones()
    {
        var app = AppDriver.Opened();
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(KeyboardPath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 2, Patience);
        app.Run(2);

        app.Frames.ClickOn(Selector.AtPath("dock/assemblies/close-assembly")).Frame(2);
        var closer = app.Ui.Closer;
        Assert.True(closer.IsOpen);
        closer.Order[1].Included = true;
        app.Run(2);
        app.Frames.ClickOn(Selector.AtPath("close-ticked")).Frame(2);
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 1, Patience);

        Assert.False(closer.IsOpen);
        Assert.Equal(AppStateDriver.FixturePath, Assert.Single(app.AppState.Assemblies.Listed).FilePath.Text);
    }

    private static readonly string KeyboardFile = Path.GetFileName(KeyboardPath);

    /// <summary>The app with the fixture and ZGF.KeyboardModule open, Greeter shown, then KeyboardKey in a tab of its
    /// own, active.</summary>
    private static AppDriver ShowingGreeterAndKeyboardKey()
    {
        var app = AppDriver.Opened();
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(KeyboardPath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 2, Patience);
        app.Run(2);
        app.Show(app.Fixture("Greeter"), 2);
        app.Show(KeyboardKeyOf(app), 2, inNewTab: true);
        return app;
    }

    private static LoadedModule KeyboardOf(AppDriver app)
    {
        return Assert.IsType<LoadedModule>(app.AppState.Assemblies.Listed.Single(static a => a.FilePath.Text == KeyboardPath));
    }

    private static BrowseNode KeyboardKeyOf(AppDriver app)
    {
        return KeyboardOf(app).Root.Child("ZGF.KeyboardModule", "KeyboardKey");
    }

    private static DocumentTab TabShowing(AppDriver app, string title)
    {
        return app.AppState.Navigation.OpenTabs.Single(t => t.Title == title);
    }

    /// <summary>Closes ZGF.KeyboardModule from the close dialog, as the user does.</summary>
    private static void CloseKeyboardFromTheDialog(AppDriver app)
    {
        OpenCloser(app.Ui.Closer, app.AppState.Assemblies, KeyboardOf(app));
        app.Run(2);
        app.Frames.ClickOn(Selector.AtPath("close-ticked")).Frame(2);
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Listed.Count == 1, Patience);
        app.Run(2);
    }

    [Fact]
    public void Closing_an_assembly_leaves_its_tab_with_its_last_code_and_says_it_was_closed()
    {
        var app = ShowingGreeterAndKeyboardKey();
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
    public void Switching_the_language_after_a_close_decompiles_the_tabs_of_listed_assemblies_but_not_the_closed_ones()
    {
        var app = ShowingGreeterAndKeyboardKey();
        CloseKeyboardFromTheDialog(app);
        var closed = TabShowing(app, "KeyboardKey");
        var code = closed.Document;

        app.AppState.Decompiler.UseLanguage(DecompilationLanguage.IL);
        app.Settle(2);
        var inFront = closed.Load.Language;
        app.Frames.ClickOn(GetTab("Greeter")).Frame(2);
        app.Settle(2);

        Assert.Equal(DecompilationLanguage.CSharp, inFront);
        Assert.Equal(DecompilationLanguage.CSharp, closed.Load.Language);
        Assert.Same(code, closed.Document);
        Assert.False(closed.Load.IsBusy);
        Assert.Equal(DecompilationLanguage.IL, TabShowing(app, "Greeter").Load.Language);
    }

    [Fact]
    public void Changing_a_decompiler_option_after_a_close_decompiles_the_tabs_of_listed_assemblies_but_not_the_closed_ones()
    {
        var app = ShowingGreeterAndKeyboardKey();
        CloseKeyboardFromTheDialog(app);
        var closed = TabShowing(app, "KeyboardKey");
        var code = closed.Document;
        var shownWith = closed.Load.Options;
        app.Frames.ClickOn(GetTab("Greeter")).Frame(2);
        app.Settle(2);
        var stringConcat = DecompilerToggles.FindToggle("StringConcat");
        Assert.NotNull(stringConcat);

        SetDecompilerToggle(app.AppState, stringConcat, false);
        app.Settle(2);
        var greeter = TabShowing(app, "Greeter");
        var greeterText = Documents.GetText(greeter.Document);
        app.Frames.ClickOn(GetTab("KeyboardKey")).Frame(2);
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
    public void A_decompile_still_on_its_way_when_its_assembly_closes_is_dropped_and_the_tab_says_it_was_closed()
    {
        var app = ShowingGreeterAndKeyboardKey();
        var keyboard = KeyboardOf(app);
        var keyboardKey = KeyboardKeyOf(app);
        app.Driver.StartShowing(keyboardKey, inNewTab: true);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Assert.IsType<TabContent.Loading>(tab.Content);

        Commands.CloseAssemblies(app.AppState, [keyboard]);
        app.Settle(2);

        Assert.Same(tab, app.AppState.Navigation.ActiveTab);
        Assert.Equal(new TabContent.Stopped(keyboardKey), tab.Content);
        Assert.Equal(KeyboardFile, tab.Load.ClosedAssembly);
        Assert.False(tab.Load.IsBusy);
        Assert.True(app.Frames.Drew($"{KeyboardFile} was closed."));
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public void Reading_the_list_again_leaves_the_tab_of_a_closed_assembly_as_it_was()
    {
        var app = ShowingGreeterAndKeyboardKey();
        CloseKeyboardFromTheDialog(app);
        var closed = TabShowing(app, "KeyboardKey");
        var shown = closed.Content;

        app.Start((a, ct) => a.AssemblyReloading.RefreshAssembliesAsync(a.Lists, a.ListSession, a.ViewCommands, a.Decompiler, [], ct));
        app.Settle(4);

        Assert.Same(shown, closed.Content);
        Assert.Equal(KeyboardFile, closed.Load.ClosedAssembly);
        Assert.Empty(app.AppState.Errors.Entries);
    }
}
