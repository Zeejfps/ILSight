using ILSight.Core;
using ILSight.Core.Analysis;
using ILSight.CommandArea;

namespace ILSight.App.Tests;

public sealed class RestoreAnalysesHandlerTests
{
    /// <summary>Two roots restored from saved analyses, of Greeter and Farewell, waiting to be found.</summary>
    private static async Task<AnalysesRestore> WaitForGreeterAndFarewellAsync(AppStateDriver d)
    {
        var analyzer = d.AppState.Analyzer;
        var greeter = Assert.IsType<AnalyzerAdd.Analyzing>(analyzer.AddRoot(await SymbolOfAsync(d, "Greeter"))).Root;
        var farewell = Assert.IsType<AnalyzerAdd.Analyzing>(analyzer.AddRoot(await SymbolOfAsync(d, "Farewell"))).Root;
        Assert.Equal<AnalyzerNode>([greeter, farewell], analyzer.Roots);
        var saved = analyzer.ListSavedAnalyses();
        analyzer.ClearRoots();
        analyzer.AddPending(saved);
        var restoring = Analysis.BeginRestoring(analyzer, d.AppState.Assemblies);
        Assert.NotNull(restoring);
        return restoring;
    }

    private static async Task<AnalysisSymbol> SymbolOfAsync(AppStateDriver d, string type)
    {
        var symbol = (await d.FixtureAsync(type)).Analysis;
        Assert.NotNull(symbol);
        return symbol;
    }

    [Fact]
    public async Task HandleAsync_SymbolsFound_AnalyzesEachAgainAndKeepsThoseGoneAsSavedFailed()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var restore = await WaitForGreeterAndFarewellAsync(d);
        var saved = d.AppState.Analyzer.ListSavedAnalyses();
        var greeter = await SymbolOfAsync(d, "Greeter");
        var ports = new FakeRestoreAnalysesHandlerPorts { Found = new Outcome<List<Lookup<AnalysisSymbol>>>.Ok([new Lookup<AnalysisSymbol>.Found { Value = greeter }, new Lookup<AnalysisSymbol>.NotFound()]) };

        await new RestoreAnalysesHandler(ports).HandleAsync(d.AppState.Analyzer, d.AppState.Assemblies, d.AppState.ViewCommands, restore, CancellationToken.None);

        Assert.Equal(greeter, Assert.Single(d.AppState.Analyzer.Found()).Symbol);
        var gone = Assert.IsType<AnalyzerFailedNode>(d.AppState.Analyzer.Roots[1]);
        Assert.IsType<RestoreFailure.NotFound>(gone.Failure);
        Assert.Equal(saved, d.AppState.Analyzer.ListSavedAnalyses());
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_LookupFailed_ReportsItAndKeepsTheRootsAsSavedFailedWithWhy()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var restore = await WaitForGreeterAndFarewellAsync(d);
        var saved = d.AppState.Analyzer.ListSavedAnalyses();
        var error = new IOException("unreadable");
        var ports = new FakeRestoreAnalysesHandlerPorts { Found = new Outcome<List<Lookup<AnalysisSymbol>>>.Failed(error) };

        await new RestoreAnalysesHandler(ports).HandleAsync(d.AppState.Analyzer, d.AppState.Assemblies, d.AppState.ViewCommands, restore, CancellationToken.None);

        Assert.Same(error, Assert.IsType<AppProblem.AnalysesNotRestored>(Assert.Single(ports.Reported)).Error);
        Assert.All(d.AppState.Analyzer.Roots, static r => Assert.Equal("unreadable", Assert.IsType<RestoreFailure.Failed>(Assert.IsType<AnalyzerFailedNode>(r).Failure).Why));
        Assert.Equal(saved, d.AppState.Analyzer.ListSavedAnalyses());
    }

    [Fact]
    public async Task HandleAsync_ListClearedMeanwhile_LeavesTheRootsAlone()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var restore = await WaitForGreeterAndFarewellAsync(d);
        var ports = new FakeRestoreAnalysesHandlerPorts
        {
            Found = new Outcome<List<Lookup<AnalysisSymbol>>>.Failed(new IOException("unreadable")),
            WhileLooking = () => LoadedAssemblies.Release(d.AppState.Assemblies.CloseAll()),
        };

        await new RestoreAnalysesHandler(ports).HandleAsync(d.AppState.Analyzer, d.AppState.Assemblies, d.AppState.ViewCommands, restore, CancellationToken.None);

        Assert.Empty(ports.Reported);
        Assert.Equal(2, d.AppState.Analyzer.Roots.Count);
    }

    [Fact]
    public async Task HandleAsync_AssemblyClosedWhileLooking_RestoresNoAnalysisOfIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var restore = await WaitForGreeterAndFarewellAsync(d);
        var greeter = await SymbolOfAsync(d, "Greeter");
        var farewell = await SymbolOfAsync(d, "Farewell");
        var assembly = d.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == AppStateDriver.FixturePath);
        var ports = new FakeRestoreAnalysesHandlerPorts
        {
            Found = new Outcome<List<Lookup<AnalysisSymbol>>>.Ok([new Lookup<AnalysisSymbol>.Found { Value = greeter }, new Lookup<AnalysisSymbol>.Found { Value = farewell }]),
            WhileLooking = () => Commands.CloseAssemblies(d.AppState, [assembly]),
        };

        await new RestoreAnalysesHandler(ports).HandleAsync(d.AppState.Analyzer, d.AppState.Assemblies, d.AppState.ViewCommands, restore, CancellationToken.None);

        Assert.Empty(d.AppState.Analyzer.Roots);
        Assert.Empty(d.AppState.Analyzer.ListSavedAnalyses());
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_FilesStillOnTheirWay_AnalyzesEachAsItsLookupAnswers()
    {
        var opened = AppStateDriver.OpenedWithoutScreen();
        var greeter = await SymbolOfAsync(opened, "Greeter");
        var d = AppStateDriver.WithoutScreen();
        var analyzer = d.AppState.Analyzer;
        d.AppState.Lists.RememberActivePaths([AssemblyPath.Of(AppStateDriver.FixturePath)]);
        var reopening = d.AppState.ListSession.BeginReopening(d.AppState.Lists);
        analyzer.AddPending([greeter.Definition ?? throw new InvalidOperationException("Greeter has no definition.")]);
        var restore = Analysis.BeginRestoring(analyzer, d.AppState.Assemblies);
        Assert.NotNull(restore);
        var ports = new FakeRestoreAnalysesHandlerPorts { Found = new Outcome<List<Lookup<AnalysisSymbol>>>.Ok([new Lookup<AnalysisSymbol>.Found { Value = greeter }]) };

        await new RestoreAnalysesHandler(ports).HandleAsync(analyzer, d.AppState.Assemblies, d.AppState.ViewCommands, restore, CancellationToken.None);

        Assert.Empty(d.AppState.Assemblies.Listed);
        Assert.Equal(greeter, Assert.Single(analyzer.Found()).Symbol);
        Assert.Equal([AssemblyPath.Of(AppStateDriver.FixturePath)], reopening.Paths);
    }
}
