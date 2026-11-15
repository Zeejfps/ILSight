using ZGF.Desktop;
using ILSight.Core;
using ILSight.Core.Comparison;
using ILSight.AssemblyArea;
using ILSight.CommandArea;

namespace ILSight.App.Tests;

public sealed class SavedComparisonTests : IDisposable
{
    private const string ComparisonPanel = "document-3";

    private static readonly string LeftPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiDiff", "Older", "ApiDiffSample.dll");
    private static readonly string RightPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiDiff", "Newer", "ApiDiffSample.dll");

    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    private AppDriver Compared()
    {
        var app = new AppDriver(settings: _temp.Settings());
        app.Open([TestPaths.PathOf(LeftPath), TestPaths.PathOf(RightPath)]);
        app.Frames.WaitUntil(() => app.AppState.ListSession.Assemblies.Assemblies.Count == 2, Waits.Patience);
        app.Settle(2);
        var assemblies = app.AppState.ListSession.Assemblies.Assemblies;
        app.StartComparing(Comparisons.CompareAssemblies(app.AppState.ListSession.Navigation, app.AppState.ListSession.Assemblies, assemblies[0], assemblies[1],
            Names.Comparison(assemblies[0])));
        app.Frames.WaitUntil(() => app.AppState.ListSession.Navigation.ActiveTab?.Comparison?.Result is ComparisonResult.Compared, Waits.Patience);
        app.Settle(2);
        return app;
    }

    private AppDriver Restart(AppDriver app)
    {
        Assert.True(app.App.TryClose(new WindowBounds(10, 20, 1000, 700, false)));
        var next = new AppDriver(settings: _temp.Settings());
        next.Frames.WaitUntil(() => FindComparison(next)?.Result is ComparisonResult.Compared, Waits.Patience);
        next.Settle(2);
        return next;
    }

    private static AssemblyComparison? FindComparison(AppDriver app)
    {
        return app.AppState.ListSession.Navigation
            .OpenTabs.Select(static t => t.Comparison)
            .FirstOrDefault(static c => c is not null);
    }

    private ComparisonDocumentRecord ReadSavedComparison()
    {
        var documents = _temp.Settings().AssemblyLists.Single().Documents;
        Assert.NotNull(documents);
        return documents.Tabs
            .OfType<ComparisonDocumentRecord>()
            .Single();
    }

    [Fact]
    public void After_a_restart_a_comparison_comes_back_in_its_panel_with_its_sides_and_filters_and_compares_again()
    {
        var app = Compared();
        var tab = app.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var comparison = tab.Comparison;
        Assert.NotNull(comparison);
        app.StartComparing(Comparisons.SwapComparedAssemblies(app.AppState.ListSession.Assemblies, comparison));
        app.StartComparing(Comparisons.ChangeComparedScope(app.AppState.ListSession.Assemblies, comparison, ApiScope.AllDeclared));
        app.Ui.Documents.GetView(tab).Comparison.ShowOnlyDifferences(false);
        app.Settle(2);
        var panel = app.Ui.Documents.GetPanel(tab);

        var next = Restart(app);

        var restored = next.AppState.ListSession.Navigation.OpenTabs.Single(t => t.Comparison is not null);
        var reopened = restored.Comparison;
        Assert.NotNull(reopened);
        Assert.Equal("ApiDiffSample API diff", restored.TitleIn(Strings.En));
        Assert.Equal(panel, next.Ui.Documents.GetPanel(restored));
        Assert.Equal(RightPath, reopened.LeftPath.Text);
        Assert.Equal(LeftPath, reopened.RightPath.Text);
        var diff = Assert.IsType<ComparisonResult.Compared>(reopened.Result).Diff;
        Assert.Equal(ApiScope.AllDeclared, diff.Scope);
        Assert.Equal("ApiDiffSample (2.0.0.0)", diff.LeftName);
        Assert.False(next.Ui.Documents.GetView(restored).Comparison.OnlyDifferences);
    }

    [Fact]
    public void Swapping_the_sides_or_showing_every_entry_is_saved_without_anything_else_changing()
    {
        var app = Compared();
        var tab = app.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Assert.Equal(LeftPath, ReadSavedComparison().LeftPath.Text);

        var comparison = tab.Comparison;
        Assert.NotNull(comparison);
        app.StartComparing(Comparisons.SwapComparedAssemblies(app.AppState.ListSession.Assemblies, comparison));
        app.Settle(2);
        Assert.Equal(RightPath, ReadSavedComparison().LeftPath.Text);

        app.Ui.Documents.GetView(tab).Comparison.ShowOnlyDifferences(false);
        app.Settle(2);
        var savedView = ReadSavedComparison().View;
        Assert.NotNull(savedView);
        Assert.False(savedView.OnlyDifferences);
    }

    [Fact]
    public void A_comparison_of_an_assembly_closed_before_the_app_closes_is_not_reopened()
    {
        var app = Compared();
        app.Driver.InTurn(() => Commands.CloseAssemblies(app.AppState, [app.AppState.ListSession.Assemblies.Assemblies.Single(a => a.FilePath.Text == RightPath)]));
        app.Settle(2);
        Assert.True(app.App.TryClose(new WindowBounds(10, 20, 1000, 700, false)));

        var next = new AppDriver(settings: _temp.Settings());
        next.Frames.WaitUntil(() => next.AppState.ListSession.Assemblies.Assemblies.Count == 1, Waits.Patience);
        next.Settle(2);

        Assert.Null(FindComparison(next));
        Assert.Null(_temp.Settings().AssemblyLists.Single().Documents);
    }
}
