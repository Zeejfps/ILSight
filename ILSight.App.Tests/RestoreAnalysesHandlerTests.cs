using ILSight.Core;
using ILSight.Core.Analysis;

namespace ILSight.App.Tests;

public sealed class RestoreAnalysesHandlerTests
{
    /// <summary>Two roots restored from saved analyses, of Greeter and Farewell, waiting to be found.</summary>
    private static AnalysesRestore WaitForGreeterAndFarewell(AppStateDriver d)
    {
        var analyzer = d.AppState.Analyzer;
        analyzer.AddRoot(SymbolOf(d, "Greeter"));
        analyzer.AddRoot(SymbolOf(d, "Farewell"));
        var saved = analyzer.ListSavedAnalyses();
        analyzer.ClearRoots();
        analyzer.AddPending(saved);
        var restoring = Analysis.BeginRestoring(analyzer, d.AppState.Assemblies);
        Assert.NotNull(restoring);
        return restoring;
    }

    private static AnalysisSymbol SymbolOf(AppStateDriver d, string type)
    {
        var symbol = d.Fixture(type).Analysis;
        Assert.NotNull(symbol);
        return symbol;
    }

    [Fact]
    public async Task HandleAsync_SymbolsFound_AnalyzesEachAgainAndRemovesThoseGone()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var restore = WaitForGreeterAndFarewell(d);
        var greeter = SymbolOf(d, "Greeter");
        var ports = new FakeRestoreAnalysesHandlerPorts { Found = new Outcome<List<AnalysisSymbol?>>.Ok([greeter, null]) };

        await new RestoreAnalysesHandler(ports).HandleAsync(d.AppState.Analyzer, d.AppState.Assemblies, d.AppState.ViewCommands, restore, CancellationToken.None);

        Assert.Equal(greeter, Assert.Single(d.AppState.Analyzer.Found()).Symbol);
        Assert.Single(d.AppState.Analyzer.Roots);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_LookupFailed_ReportsItAndRemovesTheRoots()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var restore = WaitForGreeterAndFarewell(d);
        var error = new IOException("unreadable");
        var ports = new FakeRestoreAnalysesHandlerPorts { Found = new Outcome<List<AnalysisSymbol?>>.Failed(error) };

        await new RestoreAnalysesHandler(ports).HandleAsync(d.AppState.Analyzer, d.AppState.Assemblies, d.AppState.ViewCommands, restore, CancellationToken.None);

        Assert.Same(error, Assert.IsType<AppProblem.AnalysesNotRestored>(Assert.Single(ports.Reported)).Error);
        Assert.Empty(d.AppState.Analyzer.Roots);
    }

    [Fact]
    public async Task HandleAsync_ListClearedMeanwhile_LeavesTheRootsAlone()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var restore = WaitForGreeterAndFarewell(d);
        var ports = new FakeRestoreAnalysesHandlerPorts
        {
            Found = new Outcome<List<AnalysisSymbol?>>.Failed(new IOException("unreadable")),
            WhileLooking = () => d.AppState.Assemblies.CloseAll(),
        };

        await new RestoreAnalysesHandler(ports).HandleAsync(d.AppState.Analyzer, d.AppState.Assemblies, d.AppState.ViewCommands, restore, CancellationToken.None);

        Assert.Empty(ports.Reported);
        Assert.Equal(2, d.AppState.Analyzer.Roots.Count);
    }
}
