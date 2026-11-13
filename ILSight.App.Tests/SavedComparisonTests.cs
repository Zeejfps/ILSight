using ZGF.Desktop;
using ILSight.Core;
using ILSight.Core.Comparison;
using ILSight.AssemblyArea;
using static ILSight.AssemblyListArea.AssemblyLists;
using static ILSight.Comparisons;
using static ILSight.App.Tests.TestPaths;
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

    /// <summary>An app over the temporary settings that opened both versions of the sample library and
    /// compared them, the older on the left.</summary>
    private AppDriver Compared()
    {
        var app = new AppDriver(settings: _temp.Settings());
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [PathOf(LeftPath), PathOf(RightPath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Assemblies.Count == 2, Patience);
        app.Settle(2);
        var assemblies = app.AppState.Assemblies.Assemblies;
        app.StartComparing(CompareAssemblies(app.AppState.Navigation, app.AppState.Assemblies, assemblies[0], assemblies[1],
            Names.Comparison(assemblies[0])));
        app.Frames.WaitUntil(() => app.AppState.Navigation.ActiveTab?.Comparison?.Result is ComparisonResult.Compared, Patience);
        app.Settle(2);
        return app;
    }

    private AppDriver Restart(AppDriver app)
    {
        Assert.True(app.App.TryClose(new WindowBounds(10, 20, 1000, 700, false)));
        var next = new AppDriver(settings: _temp.Settings());
        next.Start(AppStateDriver.OpenAtStartupAsync);
        next.Frames.WaitUntil(() => FindComparison(next)?.Result is ComparisonResult.Compared, Patience);
        next.Settle(2);
        return next;
    }

    private static AssemblyComparison? FindComparison(AppDriver app)
    {
        return app.AppState.Navigation
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
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var comparison = tab.Comparison;
        Assert.NotNull(comparison);
        app.StartComparing(SwapComparedAssemblies(app.AppState.Assemblies, comparison));
        app.StartComparing(ChangeComparedScope(app.AppState.Assemblies, comparison, ApiScope.AllDeclared));
        Comparisons.ShowOnlyDifferences(DocumentPanels.GetView(app.Ui.Documents, tab).Comparison, false);
        app.Settle(2);
        var panel = DocumentPanels.GetPanel(app.Ui.Documents, tab);

        var next = Restart(app);

        var restored = next.AppState.Navigation.OpenTabs.Single(t => t.Comparison is not null);
        var reopened = restored.Comparison;
        Assert.NotNull(reopened);
        Assert.Equal("ApiDiffSample API diff", restored.Title);
        Assert.Equal(panel, DocumentPanels.GetPanel(next.Ui.Documents, restored));
        Assert.Equal(RightPath, reopened.LeftPath.Text);
        Assert.Equal(LeftPath, reopened.RightPath.Text);
        var diff = Assert.IsType<ComparisonResult.Compared>(reopened.Result).Diff;
        Assert.Equal(ApiScope.AllDeclared, diff.Scope);
        Assert.Equal("ApiDiffSample (2.0.0.0)", diff.LeftName);
        Assert.False(DocumentPanels.GetView(next.Ui.Documents, restored).Comparison.OnlyDifferences);
    }

    [Fact]
    public void Swapping_the_sides_or_showing_every_entry_is_saved_without_anything_else_changing()
    {
        var app = Compared();
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Assert.Equal(LeftPath, ReadSavedComparison().LeftPath.Text);

        var comparison = tab.Comparison;
        Assert.NotNull(comparison);
        app.StartComparing(SwapComparedAssemblies(app.AppState.Assemblies, comparison));
        app.Settle(2);
        Assert.Equal(RightPath, ReadSavedComparison().LeftPath.Text);

        Comparisons.ShowOnlyDifferences(DocumentPanels.GetView(app.Ui.Documents, tab).Comparison, false);
        app.Settle(2);
        var savedView = ReadSavedComparison().View;
        Assert.NotNull(savedView);
        Assert.False(savedView.OnlyDifferences);
    }

    [Fact]
    public void A_comparison_of_an_assembly_no_longer_open_comes_back_saying_so()
    {
        var app = Compared();
        Commands.CloseAssemblies(app.AppState, [app.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == RightPath)]);
        app.Settle(2);
        Assert.True(app.App.TryClose(new WindowBounds(10, 20, 1000, 700, false)));

        var next = new AppDriver(settings: _temp.Settings());
        next.Start(AppStateDriver.OpenAtStartupAsync);
        next.Frames.WaitUntil(() => FindComparison(next)?.Result is ComparisonResult.Failed, Patience);

        var failed = Assert.IsType<ComparisonResult.Failed>(FindComparison(next)?.Result);
        Assert.Equal(new ComparisonFailure.AssemblyClosed("ApiDiffSample.dll"), failed.Failure);
    }
}
