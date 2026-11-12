using ILSight.Core.Analysis;

namespace ILSight.App.Tests;

public sealed class RunAnalysisHandlerTests
{
    private static AnalysisSymbol SymbolOf(AppStateDriver d, string type)
    {
        var symbol = d.Fixture(type).Analysis;
        Assert.NotNull(symbol);
        return symbol;
    }

    /// <summary>The first analysis of Greeter, started.</summary>
    private static AnalysisRun StartGreeterAnalysis(AppStateDriver d)
    {
        var symbol = SymbolOf(d, "Greeter");
        var kind = new AnalyzerKindNode { Subject = symbol, Kind = symbol.Kinds[0] };
        var run = Analysis.StartRun(kind);
        Assert.NotNull(run);
        return run;
    }

    private static Outcome<IReadOnlyList<AnalysisSymbol>> Batch(params AnalysisSymbol[] symbols)
    {
        return new Outcome<IReadOnlyList<AnalysisSymbol>>.Ok(symbols);
    }

    [Fact]
    public async Task HandleAsync_BatchesFound_AddsEachAndEndsDone()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var analyzer = d.AppState.Analyzer;
        var run = StartGreeterAnalysis(d);
        var farewell = SymbolOf(d, "Farewell");
        var outline = SymbolOf(d, "Outline");
        var ports = new FakeRunAnalysisHandlerPorts { Found = [Batch(farewell), Batch(outline)] };
        var findings = analyzer.Findings;

        await new RunAnalysisHandler(ports).HandleAsync(analyzer, run, CancellationToken.None);

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
        var run = StartGreeterAnalysis(d);
        var error = new InvalidOperationException("unreadable");
        var ports = new FakeRunAnalysisHandlerPorts
        {
            Found = [Batch(SymbolOf(d, "Farewell")), new Outcome<IReadOnlyList<AnalysisSymbol>>.Failed(error)],
        };

        await new RunAnalysisHandler(ports).HandleAsync(d.AppState.Analyzer, run, CancellationToken.None);

        Assert.IsType<AnalyzerSymbolNode>(run.Kind.Found[0]);
        Assert.Equal("unreadable", Assert.IsType<AnalyzerMessageNode>(run.Kind.Found[1]).Error);
        Assert.True(run.Kind.IsDone);
        Assert.Same(error, Assert.IsType<AppProblem.AnalysisFailed>(Assert.Single(ports.Reported)).Error);
    }

    [Fact]
    public async Task HandleAsync_AnalysisCancelledMeanwhile_AddsNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var run = StartGreeterAnalysis(d);
        var ports = new FakeRunAnalysisHandlerPorts { Found = [Batch(SymbolOf(d, "Farewell"))] };
        Analysis.CancelNode(run.Kind);

        await new RunAnalysisHandler(ports).HandleAsync(d.AppState.Analyzer, run, CancellationToken.None);

        Assert.Empty(run.Kind.Found);
        Assert.IsType<AnalyzerRun.Stopped>(run.Kind.Run);
    }

    [Fact]
    public void StartRun_StartedBefore_StartsNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var run = StartGreeterAnalysis(d);

        var again = Analysis.StartRun(run.Kind);

        Assert.Null(again);
        Assert.Same(run.Work, run.Kind.Work);
    }
}
