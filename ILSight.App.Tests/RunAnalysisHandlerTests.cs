using ILSight.Core.Analysis;

namespace ILSight.App.Tests;

public sealed class RunAnalysisHandlerTests
{
    private static async Task<AnalysisSymbol> SymbolOfAsync(AppStateDriver d, string type)
    {
        var symbol = (await d.FixtureAsync(type)).Analysis;
        Assert.NotNull(symbol);
        return symbol;
    }

    /// <summary>The first analysis of Greeter, started.</summary>
    private static async Task<AnalysisRun> StartGreeterAnalysisAsync(AppStateDriver d)
    {
        var symbol = await SymbolOfAsync(d, "Greeter");
        var kind = new AnalyzerKindNode { Subject = symbol, Kind = symbol.Kinds[0] };
        var run = kind.StartRun();
        Assert.NotNull(run);
        return run;
    }

    private static Outcome<IReadOnlyList<AnalysisFind>> Batch(params AnalysisSymbol[] symbols)
    {
        return new Outcome<IReadOnlyList<AnalysisFind>>.Ok(symbols.Select(static s => (AnalysisFind)new AnalysisFind.Found { Symbol = s }).ToList());
    }

    [Fact]
    public async Task HandleAsync_BatchesFound_AddsEachAndEndsDone()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var analyzer = d.AppState.Analyzer;
        var run = await StartGreeterAnalysisAsync(d);
        var farewell = await SymbolOfAsync(d, "Farewell");
        var outline = await SymbolOfAsync(d, "Outline");
        var ports = new FakeRunAnalysisHandlerPorts { Found = [Batch(farewell), Batch(outline)] };
        var findings = analyzer.Findings;

        await d.RunAsync(() => new RunAnalysisHandler(ports).HandleAsync(analyzer, run, CancellationToken.None));

        var found = run.Kind.Found.OfType<AnalyzerSymbolNode>().Select(static n => n.Symbol).ToList();
        Assert.Equal([farewell, outline], found);
        Assert.True(run.Kind.IsDone);
        Assert.True(analyzer.Findings > findings);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_Failed_EndsWithARowTellingWhyAndReportsIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var run = await StartGreeterAnalysisAsync(d);
        var error = new InvalidOperationException("unreadable");
        var ports = new FakeRunAnalysisHandlerPorts
        {
            Found = [Batch(await SymbolOfAsync(d, "Farewell")), new Outcome<IReadOnlyList<AnalysisFind>>.Failed(error)],
        };

        await d.RunAsync(() => new RunAnalysisHandler(ports).HandleAsync(d.AppState.Analyzer, run, CancellationToken.None));

        Assert.IsType<AnalyzerSymbolNode>(run.Kind.Found[0]);
        Assert.Equal("unreadable", Assert.IsType<AnalyzerMessageNode>(run.Kind.Found[1]).Error);
        Assert.True(run.Kind.IsDone);
        Assert.Same(error, Assert.IsType<AppProblem.AnalysisFailed>(Assert.Single(ports.Reported)).Error);
    }

    [Fact]
    public async Task HandleAsync_AnalysisCancelledMeanwhile_AddsNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var run = await StartGreeterAnalysisAsync(d);
        var ports = new FakeRunAnalysisHandlerPorts { Found = [Batch(await SymbolOfAsync(d, "Farewell"))] };
        Analysis.CancelNode(run.Kind);

        await d.RunAsync(() => new RunAnalysisHandler(ports).HandleAsync(d.AppState.Analyzer, run, CancellationToken.None));

        Assert.Empty(run.Kind.Found);
        Assert.IsType<AnalyzerRun.Stopped>(run.Kind.Run);
    }

    [Fact]
    public async Task StartRun_StartedBefore_StartsNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var run = await StartGreeterAnalysisAsync(d);

        var again = run.Kind.StartRun();

        Assert.Null(again);
        Assert.Same(run.Work, run.Kind.Work);
    }
}
