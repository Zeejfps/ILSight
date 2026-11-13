using ILSight.Core;
using ILSight.DiagramArea;

namespace ILSight.App.Tests;

/// <summary>Work whose start left a state pending that ends with an unexpected exception: reported once, and the state
/// settled, rather than pending for good.</summary>
public sealed class SettlingWorkflowsTests
{
    private static readonly string LeftPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiDiff", "Older", "ApiDiffSample.dll");
    private static readonly string RightPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiDiff", "Newer", "ApiDiffSample.dll");

    private static AppProblem.Unexpected ReportedOnce(AppState appState)
    {
        return Assert.IsType<AppProblem.Unexpected>(Assert.Single(appState.Errors.Entries).Problem);
    }

    [Fact]
    public void A_search_run_that_throws_is_reported_once_and_the_search_stops_searching()
    {
        var ports = new FakeRunSearchHandlerPorts { Found = [], Throws = new InvalidOperationException("broken") };
        var app = new AppDriver(hooks: new AdapterHooks { Searching = _ => ports });
        app.Driver.OpenFixtures();
        ScreenLayout.OpenSearch(app.Ui);
        app.Run();

        app.Frames.Type("Greeter").Frame();
        app.RunOut();

        Assert.False(app.Ui.Search.Results.IsBusy);
        Assert.Equal("broken", ReportedOnce(app.AppState).Error.Message);
    }

    [Fact]
    public void A_comparison_run_that_throws_is_reported_once_and_the_comparison_shows_failed()
    {
        var ports = new FakeCompareAssembliesHandlerPorts();
        var d = AppStateDriver.OpenedWith(new AdapterHooks { Comparing = _ => ports });
        d.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [TestPaths.PathOf(LeftPath), TestPaths.PathOf(RightPath)], ct));
        d.RunUntil(() => d.AppState.Assemblies.Assemblies.Count == 3);
        d.Settle();
        var left = d.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == LeftPath);
        var right = d.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == RightPath);
        d.StartComparing(Comparisons.CompareAssemblies(d.AppState.Navigation, d.AppState.Assemblies, left, right, Names.Comparison(left)));
        d.RunUntil(() => ports.Answers.Count == 1);
        var comparison = d.AppState.Navigation.ActiveTab?.Comparison;
        Assert.NotNull(comparison);
        Assert.True(comparison.IsComparing);

        ports.Answers[0].SetException(new InvalidOperationException("broken"));
        d.Settle();

        Assert.False(comparison.IsComparing);
        Assert.IsType<ComparisonFailure.Failed>(Assert.IsType<ComparisonResult.Failed>(comparison.Result).Failure);
        Assert.Equal("broken", ReportedOnce(d.AppState).Error.Message);
    }

    [Fact]
    public async Task A_diagram_read_that_throws_is_reported_once_and_the_diagram_stops_reading()
    {
        var ports = new FakeDiagramGrowingPorts();
        var d = AppStateDriver.OpenedWith(new AdapterHooks { Growing = _ => ports });
        var greeter = (await d.FixtureAsync("Greeter")).Definition;
        Assert.NotNull(greeter);
        d.InTurn(() => DiagramView.ShowInNewDiagramLater(d.Ui, greeter, "Greeter"));
        d.RunUntil(() => ports.Requests.Count == 1);
        var diagram = d.AppState.Navigation.ActiveTab?.Diagram;
        Assert.NotNull(diagram);
        Assert.True(diagram.IsBusy);

        ports.Requests[0].Answer.SetException(new InvalidOperationException("broken"));
        d.Settle();

        Assert.False(diagram.IsBusy);
        Assert.Equal("broken", ReportedOnce(d.AppState).Error.Message);
    }
}
