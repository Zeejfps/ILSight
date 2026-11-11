using ZGF.Desktop;
using ILSight.Core.Comparison;
using static ILSight.Assemblies;
using static ILSight.AssemblyLists;
using static ILSight.Comparisons;
using static ILSight.App.Tests.TestPaths;

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
        OpenAssemblies(app.AppState, [PathOf(LeftPath), PathOf(RightPath)]);
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Assemblies.Count == 2, Patience);
        app.Settle(2);
        var assemblies = app.AppState.Assemblies.Assemblies;
        CompareAssemblies(app.AppState, assemblies[0], assemblies[1], Names.Comparison(assemblies[0]));
        app.Frames.WaitUntil(() => app.AppState.Tabs.Active?.Comparison?.Diff is not null, Patience);
        app.Settle(2);
        return app;
    }

    private AppDriver Restart(AppDriver app)
    {
        app.App.SaveOnExit(new WindowBounds(10, 20, 1000, 700, false));
        var next = new AppDriver(settings: _temp.Settings());
        RestoreActiveAssemblyList(next.AppState);
        next.Frames.WaitUntil(() => FindComparison(next)?.Diff is not null, Patience);
        next.Settle(2);
        return next;
    }

    private static AssemblyComparison? FindComparison(AppDriver app)
    {
        return app.AppState.Tabs
            .Open.Select(static t => t.Comparison)
            .FirstOrDefault(static c => c is not null);
    }

    private ComparisonDocumentRecord ReadSavedComparison()
    {
        var documents = _temp.Settings().AssemblyLists.Single().Documents!;
        return documents.Tabs
            .OfType<ComparisonDocumentRecord>()
            .Single();
    }

    [Fact]
    public void After_a_restart_a_comparison_comes_back_in_its_panel_with_its_sides_and_filters_and_compares_again()
    {
        var app = Compared();
        var tab = app.AppState.Tabs.Active!;
        var comparison = tab.Comparison!;
        SwapComparedAssemblies(app.AppState, comparison);
        ChangeComparedScope(app.AppState, comparison, ApiScope.AllDeclared);
        Comparisons.ShowOnlyDifferences(DocumentPanels.GetView(app.Ui.Documents, tab).Comparison, false);
        app.Settle(2);
        var panel = DocumentPanels.GetPanel(app.Ui.Documents, tab);

        var next = Restart(app);

        var restored = next.AppState.Tabs.Open.Single(t => t.Comparison is not null);
        var reopened = restored.Comparison!;
        Assert.Equal("ApiDiffSample API diff", restored.Title);
        Assert.Equal(panel, DocumentPanels.GetPanel(next.Ui.Documents, restored));
        Assert.Equal(RightPath, reopened.LeftPath.Text);
        Assert.Equal(LeftPath, reopened.RightPath.Text);
        Assert.Equal(ApiScope.AllDeclared, reopened.Diff!.Scope);
        Assert.Equal("ApiDiffSample (2.0.0.0)", reopened.Diff.LeftName);
        Assert.False(DocumentPanels.GetView(next.Ui.Documents, restored).Comparison.OnlyDifferences);
    }

    [Fact]
    public void Swapping_the_sides_or_showing_every_entry_is_saved_without_anything_else_changing()
    {
        var app = Compared();
        var tab = app.AppState.Tabs.Active!;
        Assert.Equal(LeftPath, ReadSavedComparison().LeftPath.Text);

        SwapComparedAssemblies(app.AppState, tab.Comparison!);
        app.Settle(2);
        Assert.Equal(RightPath, ReadSavedComparison().LeftPath.Text);

        Comparisons.ShowOnlyDifferences(DocumentPanels.GetView(app.Ui.Documents, tab).Comparison, false);
        app.Settle(2);
        Assert.False(ReadSavedComparison().View!.OnlyDifferences);
    }

    [Fact]
    public void A_comparison_of_an_assembly_no_longer_open_comes_back_saying_so()
    {
        var app = Compared();
        CloseAssembly(app.AppState, app.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == RightPath));
        app.Settle(2);
        app.App.SaveOnExit(new WindowBounds(10, 20, 1000, 700, false));

        var next = new AppDriver(settings: _temp.Settings());
        RestoreActiveAssemblyList(next.AppState);
        next.Frames.WaitUntil(() => FindComparison(next)?.Failure != null, Patience);

        Assert.Equal(new ComparisonFailure.AssemblyClosed("ApiDiffSample.dll"), FindComparison(next)!.Failure);
    }
}
