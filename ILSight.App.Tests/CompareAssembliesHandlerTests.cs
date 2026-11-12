using ILSight.Core;
using ILSight.Core.Comparison;
using ILSight.AssemblyArea;

namespace ILSight.App.Tests;

public sealed class CompareAssembliesHandlerTests
{
    /// <summary>A comparison of the fixture assembly with itself, started among <paramref name="d"/>'s open
    /// assemblies.</summary>
    private static ComparisonRun StartComparing(AppStateDriver d, AssemblyComparison comparison)
    {
        var run = Comparisons.ChangeComparedScope(d.AppState.Assemblies, comparison, ApiScope.PublicApi);
        Assert.NotNull(run);
        return run;
    }

    private static AssemblyComparison NewComparison(AppStateDriver d)
    {
        var path = d.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == AppStateDriver.FixturePath).FilePath;
        return new AssemblyComparison { LeftPath = path, RightPath = path };
    }

    private static ApiDiff Diff(ComparisonRun run)
    {
        return ApiEntries.Compare(run.Left, run.Right, run.Scope, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_Compared_ShowsWhatChanged()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var comparison = NewComparison(d);
        var run = StartComparing(d, comparison);
        var ports = new FakeCompareAssembliesHandlerPorts();
        var diff = Diff(run);

        var comparing = new CompareAssembliesHandler(ports).HandleAsync(run, CancellationToken.None);
        Assert.True(comparison.IsComparing);
        Assert.Single(ports.Answers).SetResult(new Outcome<ApiDiff>.Ok(diff));
        await comparing;

        Assert.Same(diff, Assert.IsType<ComparisonResult.Compared>(comparison.Result).Diff);
        Assert.False(comparison.IsComparing);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_Failed_ShowsAndReportsTheFailure()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var comparison = NewComparison(d);
        var run = StartComparing(d, comparison);
        var ports = new FakeCompareAssembliesHandlerPorts();
        var error = new BadImageFormatException("Unreadable.");

        var comparing = new CompareAssembliesHandler(ports).HandleAsync(run, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(new Outcome<ApiDiff>.Failed(error));
        await comparing;

        var failed = Assert.IsType<ComparisonResult.Failed>(comparison.Result);
        Assert.IsType<ComparisonFailure.Failed>(failed.Failure);
        Assert.Same(error, Assert.IsType<AppProblem.ComparisonFailed>(Assert.Single(ports.Reported)).Error);
    }

    [Fact]
    public async Task HandleAsync_NewerComparisonStartedMeanwhile_DropsTheOlderAndShowsTheNewer()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var comparison = NewComparison(d);
        var ports = new FakeCompareAssembliesHandlerPorts();
        var handler = new CompareAssembliesHandler(ports);
        var older = StartComparing(d, comparison);
        var olderComparing = handler.HandleAsync(older, CancellationToken.None);
        var newer = StartComparing(d, comparison);
        var newerComparing = handler.HandleAsync(newer, CancellationToken.None);
        var newerDiff = Diff(newer);

        ports.Answers[0].SetResult(new Outcome<ApiDiff>.Ok(Diff(older)));
        await olderComparing;
        Assert.IsType<ComparisonResult.NotYet>(comparison.Result);
        ports.Answers[1].SetResult(new Outcome<ApiDiff>.Ok(newerDiff));
        await newerComparing;

        Assert.True(older.Work.Token.IsCancellationRequested);
        Assert.Same(newerDiff, Assert.IsType<ComparisonResult.Compared>(comparison.Result).Diff);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public void Starting_a_comparison_of_an_assembly_no_longer_open_shows_so_and_starts_nothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var comparison = NewComparison(d);
        d.AppState.ListSession.CloseAssemblies([d.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == AppStateDriver.FixturePath)]);

        var run = Comparisons.SwapComparedAssemblies(d.AppState.Assemblies, comparison);

        Assert.Null(run);
        Assert.IsType<ComparisonFailure.AssemblyClosed>(Assert.IsType<ComparisonResult.Failed>(comparison.Result).Failure);
        Assert.False(comparison.IsComparing);
    }
}
