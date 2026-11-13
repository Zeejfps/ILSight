using ILSight.Core;
using ILSight.Core.Comparison;
using ILSight.AssemblyArea;
using ILSight.CommandArea;

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

    private static async Task<ApiDiff> DiffAsync(ComparisonRun run)
    {
        var compared = await ApiEntries.CompareAsync(run.Left, run.Right, run.Scope, CancellationToken.None);
        return Assert.IsType<TypeSystemWork<ApiDiff>.Done>(compared).Value;
    }

    [Fact]
    public async Task HandleAsync_Compared_ShowsWhatChanged()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var comparison = NewComparison(d);
        var run = StartComparing(d, comparison);
        var ports = new FakeCompareAssembliesHandlerPorts();
        var diff = await DiffAsync(run);

        var comparing = d.BeginAsync(() => new CompareAssembliesHandler(ports).HandleAsync(run, CancellationToken.None));
        Assert.True(comparison.IsComparing);
        Assert.Single(ports.Answers).SetResult(new Outcome<ModuleRead<ApiDiff>>.Ok(new ModuleRead<ApiDiff>.Read { Value = diff }));
        await d.EndAsync(comparing);

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

        var comparing = d.BeginAsync(() => new CompareAssembliesHandler(ports).HandleAsync(run, CancellationToken.None));
        Assert.Single(ports.Answers).SetResult(new Outcome<ModuleRead<ApiDiff>>.Failed(error));
        await d.EndAsync(comparing);

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
        var olderComparing = d.BeginAsync(() => handler.HandleAsync(older, CancellationToken.None));
        var newer = StartComparing(d, comparison);
        var newerComparing = d.BeginAsync(() => handler.HandleAsync(newer, CancellationToken.None));
        var newerDiff = await DiffAsync(newer);

        ports.Answers[0].SetResult(new Outcome<ModuleRead<ApiDiff>>.Ok(new ModuleRead<ApiDiff>.Read { Value = await DiffAsync(older) }));
        await d.EndAsync(olderComparing);
        Assert.IsType<ComparisonResult.NotYet>(comparison.Result);
        ports.Answers[1].SetResult(new Outcome<ModuleRead<ApiDiff>>.Ok(new ModuleRead<ApiDiff>.Read { Value = newerDiff }));
        await d.EndAsync(newerComparing);

        Assert.True(older.Work.Token.IsCancellationRequested);
        Assert.Same(newerDiff, Assert.IsType<ComparisonResult.Compared>(comparison.Result).Diff);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public void Starting_a_comparison_of_an_assembly_no_longer_open_shows_so_and_starts_nothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var comparison = NewComparison(d);
        d.InTurn(() => Commands.CloseAssemblies(d.AppState, [d.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == AppStateDriver.FixturePath)]));

        var run = Comparisons.SwapComparedAssemblies(d.AppState.Assemblies, comparison);

        Assert.Null(run);
        Assert.IsType<ComparisonFailure.AssemblyClosed>(Assert.IsType<ComparisonResult.Failed>(comparison.Result).Failure);
        Assert.False(comparison.IsComparing);
    }
}
