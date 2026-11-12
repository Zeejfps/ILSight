using ZGF.Gui.Immediate;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.Core.Comparison;
using ILSight.AssemblyHandling;
using static ILSight.Comparisons;
using static ILSight.Docking;
using static ILSight.App.Tests.TestPaths;

namespace ILSight.App.Tests;

public sealed class ComparisonTests
{
    private const string LeftName = "ApiDiffSample (1.0.0.0)";
    private const string RightName = "ApiDiffSample (2.0.0.0)";
    private const string Compared = LeftName + "  →  " + RightName;

    private static readonly string LeftPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiDiff", "Older", "ApiDiffSample.dll");
    private static readonly string RightPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiDiff", "Newer", "ApiDiffSample.dll");

    /// <summary>A driver that opened both versions of the sample library, the left first.</summary>
    private static AppStateDriver OpenBoth()
    {
        var d = new AppStateDriver();
        d.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [PathOf(LeftPath), PathOf(RightPath)], ct));
        d.RunUntil(() => d.AppState.Assemblies.Assemblies.Count == 2);
        d.Settle();
        return d;
    }

    private static LoadedModule Opened(AppStateDriver d, string path)
    {
        return d.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == path);
    }

    /// <summary>Compares <paramref name="assembly"/> with <paramref name="other"/> and waits for the comparison.</summary>
    private static AssemblyComparison Compare(AppStateDriver d, string assembly, string other)
    {
        d.StartComparing(CompareAssemblies(d.AppState.Navigation, d.AppState.Assemblies, Opened(d, assembly), Opened(d, other),
            Names.Comparison(Opened(d, assembly))));
        d.Settle();
        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var comparison = tab.Comparison;
        Assert.NotNull(comparison);
        return comparison;
    }

    /// <summary>What the newest comparison of <paramref name="comparison"/> found; fails the test when it found nothing.</summary>
    private static ApiDiff DiffOf(AssemblyComparison comparison)
    {
        return Assert.IsType<ComparisonResult.Compared>(comparison.Result).Diff;
    }

    private static bool HasCompared(AssemblyComparison? comparison)
    {
        return comparison?.Result is ComparisonResult.Compared;
    }

    private static ApiEntry Entry(ApiDiff diff, string namespaceName, string type)
    {
        var ns = diff.Namespaces.Single(n => n.Text == namespaceName);
        return ns.Children.Single(c => c.Text == type);
    }

    [Fact]
    public void Comparing_two_assemblies_shows_what_changed_in_a_tab_kept_open()
    {
        var d = OpenBoth();

        var comparison = Compare(d, LeftPath, RightPath);

        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Assert.Equal("ApiDiffSample API diff", tab.Title);
        Assert.NotSame(tab, d.AppState.Navigation.PreviewTab);
        var diff = DiffOf(comparison);
        Assert.Equal(LeftName, diff.LeftName);
        Assert.Equal(RightName, diff.RightName);
        Assert.Equal(4, diff.Added);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void Comparing_is_a_new_intent_that_drops_what_was_still_being_looked_up()
    {
        var d = OpenBoth();
        var navigation = d.AppState.Navigation;
        var lookup = navigation.BeginResolving(inNewTab: false);

        d.StartComparing(CompareAssemblies(navigation, d.AppState.Assemblies, Opened(d, LeftPath), Opened(d, RightPath), Names.Comparison(Opened(d, LeftPath))));
        d.Settle();

        Assert.True(navigation.IsStale(lookup.Intent));
        Assert.False(navigation.IsResolving);
    }

    [Fact]
    public void The_assemblies_keep_the_sides_they_were_picked_for_whatever_their_versions()
    {
        var d = OpenBoth();

        var comparison = Compare(d, RightPath, LeftPath);

        Assert.Equal(RightPath, comparison.LeftPath.Text);
        Assert.Equal(RightName, DiffOf(comparison).LeftName);
    }

    [Fact]
    public void Swapping_compares_the_other_way_round()
    {
        var d = OpenBoth();
        var comparison = Compare(d, LeftPath, RightPath);
        var added = DiffOf(comparison).Added;
        var removed = DiffOf(comparison).Removed;

        d.StartComparing(SwapComparedAssemblies(d.AppState.Assemblies, comparison));
        d.Settle();

        var swapped = DiffOf(comparison);
        Assert.Equal(RightName, swapped.LeftName);
        Assert.Equal(removed, swapped.Added);
        Assert.Equal(added, swapped.Removed);
    }

    [Fact]
    public void Taking_in_all_that_is_declared_compares_again_with_what_is_not_public()
    {
        var d = OpenBoth();
        var comparison = Compare(d, LeftPath, RightPath);
        Assert.Equal(ApiChange.Removed, Entry(DiffOf(comparison), "ApiDiffSample", "Helper").Change);

        d.StartComparing(ChangeComparedScope(d.AppState.Assemblies, comparison, ApiScope.AllDeclared));
        d.Settle();

        Assert.Equal(ApiScope.AllDeclared, DiffOf(comparison).Scope);
        Assert.Equal(ApiChange.Changed, Entry(DiffOf(comparison), "ApiDiffSample", "Helper").Change);
    }

    [Fact]
    public void Going_to_an_entry_shows_its_code_in_another_tab_leaving_the_comparison_open()
    {
        var d = OpenBoth();
        d.TurnOffPreviewTabs();
        var comparison = Compare(d, LeftPath, RightPath);
        var comparisonTab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(comparisonTab);
        var account = Entry(DiffOf(comparison), "ApiDiffSample", "Account");

        var sides = Assert.IsType<ApiSides.Both>(Assert.IsType<ApiEntry.Entity>(account).Sides);
        d.Start((a, ct) => a.NavigateHandler.HandleAsync(a, sides.Right.Reference, a.Navigation.BeginResolving(inNewTab: false), ct));
        d.Settle();

        var shown = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(shown);
        Assert.NotSame(comparisonTab, shown);
        Assert.NotNull(shown.Node);
        Assert.Equal("Account", shown.Node.Text);
        Assert.Equal(RightPath, shown.Node.Assembly.FilePath.Text);
        Assert.Same(comparison, comparisonTab.Comparison);
    }

    [Fact]
    public void Comparing_again_once_an_assembly_is_closed_says_it_is_no_longer_open()
    {
        var d = OpenBoth();
        var comparison = Compare(d, LeftPath, RightPath);

        AssemblyListing.CloseAssembly(d.AppState.Assemblies, Opened(d, RightPath));
        d.StartComparing(SwapComparedAssemblies(d.AppState.Assemblies, comparison));
        d.Settle();

        var failed = Assert.IsType<ComparisonResult.Failed>(comparison.Result);
        Assert.Equal(new ComparisonFailure.AssemblyClosed("ApiDiffSample.dll"), failed.Failure);
    }

    [Fact]
    public void Closing_the_tab_drops_the_comparison_in_flight()
    {
        var d = OpenBoth();
        d.StartComparing(CompareAssemblies(d.AppState.Navigation, d.AppState.Assemblies, Opened(d, LeftPath), Opened(d, RightPath),
            Names.Comparison(Opened(d, LeftPath))));
        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var comparison = tab.Comparison;
        Assert.NotNull(comparison);

        d.AppState.Navigation.CloseTab(tab);
        d.Settle();

        Assert.True(comparison.Work.IsClosed);
        Assert.IsType<ComparisonResult.NotYet>(comparison.Result);
        Assert.False(d.AppState.IsComparing);
    }

    [Fact]
    public void Compare_with_in_the_tree_menu_opens_a_dialog_on_the_assembly()
    {
        var app = OpenBothOnScreen();

        PickCompareWith(app);

        var picker = app.Ui.ComparisonPicker;
        Assert.True(picker.Choice.IsOpen);
        Assert.Equal(LeftPath, picker.Choice.Compared?.FilePath.Text);
        Assert.True(app.Frames.Drew("Compare " + LeftName + " with"));
        Assert.True(app.Frames.Drew(RightName));
    }

    [Fact]
    public void Escape_dismisses_the_dialog_which_goes_out_still_showing_what_it_compared()
    {
        var app = OpenBothOnScreen();
        PickCompareWith(app);
        var title = "Compare " + LeftName + " with";

        app.Frames.Key(KeyboardKey.Escape).Frame();
        app.Run();

        Assert.False(app.Ui.ComparisonPicker.Choice.IsOpen);
        Assert.True(app.Frames.Drew(title));

        app.Run(60);

        Assert.False(app.Frames.Drew(title));
    }

    [Fact]
    public void Cancel_closes_the_dialog_at_once()
    {
        var app = OpenBothOnScreen();
        PickCompareWith(app);

        app.Frames.ClickOn(Selector.AtPath("compare-picker-cancel")).Frame(2);

        Assert.False(app.Ui.ComparisonPicker.Choice.IsOpen);
        Assert.Null(app.Ui.ComparisonPicker.Choice.Compared);
        Assert.False(app.Frames.Drew("Compare " + LeftName + " with"));
    }

    [Fact]
    public void Compare_in_the_dialog_compares_with_the_selected_assembly_in_a_new_tab()
    {
        var app = OpenBothOnScreen();
        PickCompareWith(app);

        app.Frames.ClickOn(Selector.AtPath("compare-picker-compare")).Frame();
        app.Frames.WaitUntil(() => HasCompared(app.AppState.Navigation.ActiveTab?.Comparison), Patience);
        app.Settle(2);

        Assert.False(app.Ui.ComparisonPicker.Choice.IsOpen);
        Assert.True(app.Frames.Drew(Compared));
        Assert.True(app.Frames.Drew("4 added"));
        Assert.True(app.Frames.Drew("Account"));
        Assert.False(app.Frames.Drew("IStore"));
    }

    [Fact]
    public void The_swap_button_in_the_bar_over_the_document_swaps_the_sides()
    {
        var app = ComparedOnScreen();
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var comparison = tab.Comparison;
        Assert.NotNull(comparison);
        Assert.True(app.TextRect(app.Ui.Icons.ComparisonSwap).Y < app.TextRect(Compared).Y);

        app.Frames.ClickOn(Selector.AtPath("dock/document-2/swap")).Frame();
        app.Frames.WaitUntil(() => DiffOf(comparison).LeftName == RightName, Patience);
        app.Settle(2);

        Assert.Equal(LeftPath, comparison.RightPath.Text);
        Assert.True(app.Frames.Drew(RightName + "  →  " + LeftName));
        Assert.True(app.Frames.Drew("4 removed"));
    }

    [Fact]
    public void The_tools_sit_in_the_bar_over_the_document_or_in_the_tab_while_that_bar_is_hidden()
    {
        var app = ComparedOnScreen();
        var bar = app.TextRect("Only differences");
        var summary = app.TextRect(Compared);
        Assert.True(bar.Y < summary.Y);

        app.Ui.Display = app.Ui.Display with { ShowOmnibar = false };
        app.Run(2);

        Assert.True(app.Frames.Drew("Only differences"));
    }

    /// <summary>An app that compared the two versions of the sample library, the older on the left.</summary>
    private static AppDriver ComparedOnScreen()
    {
        var app = OpenBothOnScreen();
        app.StartComparing(CompareAssemblies(app.AppState.Navigation, app.AppState.Assemblies, Opened(app.Driver, LeftPath), Opened(app.Driver, RightPath),
            Names.Comparison(Opened(app.Driver, LeftPath))));
        app.Frames.WaitUntil(() => HasCompared(app.AppState.Navigation.ActiveTab?.Comparison), Patience);
        app.Settle(2);
        return app;
    }

    private static AppDriver OpenBothOnScreen()
    {
        var app = new AppDriver(width: 1400f);
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [PathOf(LeftPath), PathOf(RightPath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Assemblies.Count == 2, Patience);
        app.Settle(2);
        return app;
    }

    /// <summary>Right-clicks the left assembly's row and picks Compare with… from its menu.</summary>
    private static void PickCompareWith(AppDriver app)
    {
        app.Frames.ClickOn(GetTreeRow(LeftName), PointerButton.Right).Frame();
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Compare with…")).Frame();
        app.Run(2);
    }

    private static AssemblyComparison NewComparison()
    {
        return new AssemblyComparison { LeftPath = PathOf(LeftPath), RightPath = PathOf(RightPath) };
    }

    [Fact]
    public void SwapSides_AnyComparison_PutsTheRightOnTheLeftAndCountsAChange()
    {
        var comparison = NewComparison();

        comparison.SwapSides();

        Assert.Equal(RightPath, comparison.LeftPath.Text);
        Assert.Equal(LeftPath, comparison.RightPath.Text);
        Assert.Equal(1, comparison.Version);
    }

    [Fact]
    public void TakeInScope_AnyScope_TakesItInAndCountsAChange()
    {
        var comparison = NewComparison();

        comparison.TakeInScope(ApiScope.AllDeclared);

        Assert.Equal(ApiScope.AllDeclared, comparison.Scope);
        Assert.Equal(1, comparison.Version);
    }

    [Fact]
    public void ShowFailure_WhileComparing_DropsTheComparisonInFlightAndSaysWhy()
    {
        var comparison = NewComparison();
        var ticket = comparison.Work.Start();
        var failure = new ComparisonFailure.AssemblyClosed("ApiDiffSample.dll");

        comparison.ShowFailure(failure);

        Assert.False(comparison.IsComparing);
        Assert.True(ticket.Token.IsCancellationRequested);
        Assert.Equal(new ComparisonResult.Failed { Failure = failure }, comparison.Result);
        Assert.False(comparison.Work.TryFinish(ticket));
    }

    [Fact]
    public void Showing_every_entry_draws_those_that_did_not_change_too()
    {
        var app = ComparedOnScreen();

        app.Frames.ClickOn(Selector.WithText("Only differences")).Frame();

        Assert.True(app.Frames.Drew("IStore"));
    }
}
