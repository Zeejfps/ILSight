using ILSight.Core;
using ILSight.Core.AnalysisArea;
using ILSight.CommandArea;

namespace ILSight.App.Tests;

public sealed class RestoreAnalysesHandlerTests
{
    private static async Task<AnalysesRestore> WaitForGreeterAndFarewellAsync(AppStateDriver d)
    {
        var analyzer = d.AppState.ListSession.Analyzer;
        var greeterSymbol = await SymbolOfAsync(d, "Greeter");
        var greeterAdd = analyzer.AddRoot(greeterSymbol);
        var greeter = UnionAsserts.GetCase<AnalyzerAdd.Analyzing>(greeterAdd).Root;
        var farewellSymbol = await SymbolOfAsync(d, "Farewell");
        var farewellAdd = analyzer.AddRoot(farewellSymbol);
        var farewell = UnionAsserts.GetCase<AnalyzerAdd.Analyzing>(farewellAdd).Root;
        Assert.Equal<AnalyzerNode>([greeter, farewell], analyzer.Roots);
        var saved = analyzer.ListSavedAnalyses();
        analyzer.ClearRoots();
        analyzer.AddPending(saved);
        var restoring = Analysis.BeginRestoring(analyzer, d.AppState.ListSession.Assemblies);
        Assert.NotNull(restoring);
        return restoring;
    }

    private static async Task<AnalysisSymbol> SymbolOfAsync(AppStateDriver d, string type)
    {
        var fixture = await d.FixtureAsync(type);
        var symbol = fixture.Analysis;
        Assert.NotNull(symbol);
        return symbol;
    }

    [Fact]
    public async Task HandleAsync_SymbolsFound_AnalyzesEachAgainAndKeepsThoseGoneAsSavedFailed()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var restore = await WaitForGreeterAndFarewellAsync(d);
        var saved = d.AppState.ListSession.Analyzer.ListSavedAnalyses();
        var greeter = await SymbolOfAsync(d, "Greeter");
        var ports = new FakeRestoreAnalysesHandlerPorts { Found = [new Outcome<Lookup<AnalysisSymbol>>.Ok(new Lookup<AnalysisSymbol>.Found { Value = greeter }), new Outcome<Lookup<AnalysisSymbol>>.Ok(new Lookup<AnalysisSymbol>.NotFound())] };

        await d.RunAsync(() => new RestoreAnalysesHandler(ports).HandleAsync(d.AppState.ListSession.Analyzer, d.AppState.ListSession.Assemblies, d.AppState.ViewCommands, restore, CancellationToken.None));

        Assert.Equal(greeter, Assert.Single(d.AppState.ListSession.Analyzer.Found()).Symbol);
        var gone = Assert.IsType<AnalyzerFailedNode>(d.AppState.ListSession.Analyzer.Roots[1]);
        Assert.IsType<RestoreFailure.NotFound>(gone.Failure);
        Assert.Equal(saved, d.AppState.ListSession.Analyzer.ListSavedAnalyses());
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_OneLookupFailed_ReportsItOnceKeepsThatRootSavedFailedWithWhyAndAnalyzesTheOther()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var restore = await WaitForGreeterAndFarewellAsync(d);
        var saved = d.AppState.ListSession.Analyzer.ListSavedAnalyses();
        var farewell = await SymbolOfAsync(d, "Farewell");
        var error = new IOException("unreadable");
        var ports = new FakeRestoreAnalysesHandlerPorts { Found = [new Outcome<Lookup<AnalysisSymbol>>.Failed(error), new Outcome<Lookup<AnalysisSymbol>>.Ok(new Lookup<AnalysisSymbol>.Found { Value = farewell })] };

        await d.RunAsync(() => new RestoreAnalysesHandler(ports).HandleAsync(d.AppState.ListSession.Analyzer, d.AppState.ListSession.Assemblies, d.AppState.ViewCommands, restore, CancellationToken.None));

        Assert.Same(error, UnionAsserts.GetCase<AppProblem.AnalysesNotRestored>(Assert.Single(ports.Reported)).Error);
        Assert.Same(error, Assert.IsType<RestoreFailure.Failed>(Assert.IsType<AnalyzerFailedNode>(d.AppState.ListSession.Analyzer.Roots[0]).Failure).Error);
        Assert.Equal(farewell, Assert.Single(d.AppState.ListSession.Analyzer.Found()).Symbol);
        Assert.Equal(saved, d.AppState.ListSession.Analyzer.ListSavedAnalyses());
    }

    [Fact]
    public async Task HandleAsync_ListClearedMeanwhile_LeavesTheRootsAlone()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var restore = await WaitForGreeterAndFarewellAsync(d);
        var ports = new FakeRestoreAnalysesHandlerPorts
        {
            Found = [new Outcome<Lookup<AnalysisSymbol>>.Failed(new IOException("unreadable")), new Outcome<Lookup<AnalysisSymbol>>.Failed(new IOException("unreadable"))],
            WhileLooking = () =>
            {
                var removed = d.AppState.ListSession.Assemblies.CloseAll();
                LoadedAssemblies.Release(removed);
            },
        };

        await d.RunAsync(() => new RestoreAnalysesHandler(ports).HandleAsync(d.AppState.ListSession.Analyzer, d.AppState.ListSession.Assemblies, d.AppState.ViewCommands, restore, CancellationToken.None));

        Assert.Empty(ports.Reported);
        Assert.Equal(2, d.AppState.ListSession.Analyzer.Roots.Count);
    }

    [Fact]
    public async Task HandleAsync_AssemblyClosedWhileLooking_RestoresNoAnalysisOfIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var restore = await WaitForGreeterAndFarewellAsync(d);
        var greeter = await SymbolOfAsync(d, "Greeter");
        var farewell = await SymbolOfAsync(d, "Farewell");
        var assembly = d.AppState.ListSession.Assemblies.Assemblies.Single(a => a.FilePath.Text == AppStateDriver.FixturePath);
        var ports = new FakeRestoreAnalysesHandlerPorts
        {
            Found = [new Outcome<Lookup<AnalysisSymbol>>.Ok(new Lookup<AnalysisSymbol>.Found { Value = greeter }), new Outcome<Lookup<AnalysisSymbol>>.Ok(new Lookup<AnalysisSymbol>.Found { Value = farewell })],
            WhileLooking = () => Commands.CloseAssemblies(d.AppState, [assembly]),
        };

        await d.RunAsync(() => new RestoreAnalysesHandler(ports).HandleAsync(d.AppState.ListSession.Analyzer, d.AppState.ListSession.Assemblies, d.AppState.ViewCommands, restore, CancellationToken.None));

        Assert.Empty(d.AppState.ListSession.Analyzer.Roots);
        Assert.Empty(d.AppState.ListSession.Analyzer.ListSavedAnalyses());
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_FilesStillOnTheirWay_AnalyzesEachAsItsLookupAnswers()
    {
        var opened = AppStateDriver.OpenedWithoutScreen();
        var greeter = await SymbolOfAsync(opened, "Greeter");
        var d = AppStateDriver.WithoutScreen();
        var analyzer = d.AppState.ListSession.Analyzer;
        d.AppState.Lists.RememberActivePaths([AssemblyPath.Of(AppStateDriver.FixturePath)]);
        var reopening = d.AppState.ListSession.BeginReopening(d.AppState.Lists);
        analyzer.AddPending([greeter.Definition ?? throw new InvalidOperationException("Greeter has no definition.")]);
        var restore = Analysis.BeginRestoring(analyzer, d.AppState.ListSession.Assemblies);
        Assert.NotNull(restore);
        var ports = new FakeRestoreAnalysesHandlerPorts { Found = [new Outcome<Lookup<AnalysisSymbol>>.Ok(new Lookup<AnalysisSymbol>.Found { Value = greeter })] };

        await d.RunAsync(() => new RestoreAnalysesHandler(ports).HandleAsync(analyzer, d.AppState.ListSession.Assemblies, d.AppState.ViewCommands, restore, CancellationToken.None));

        Assert.Empty(d.AppState.ListSession.Assemblies.Listed);
        Assert.Equal(greeter, Assert.Single(analyzer.Found()).Symbol);
        Assert.Equal([AssemblyPath.Of(AppStateDriver.FixturePath)], reopening.Paths);
    }
}
