using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class AnalyzeReferenceHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReferenceFound_TurnsTheRootIntoItsAnalysisAndShowsIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var analyzer = d.AppState.Analyzer;
        var greeter = d.Fixture("Greeter");
        var pending = Analysis.BeginAnalyzing(analyzer, greeter.Definition);
        var ports = new FakeAnalyzeReferenceHandlerPorts { Found = new Outcome<BrowseNode?>.Ok(greeter) };
        d.TakeViewCommands();

        await new AnalyzeReferenceHandler(ports).HandleAsync(analyzer, d.AppState.ViewCommands, pending, CancellationToken.None);

        var root = Assert.Single(analyzer.Found());
        Assert.Equal("Greeter", root.Symbol.Label);
        Assert.Equal([new ViewCommand.ShowAnalysis(root)], d.TakeViewCommands());
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_NotFoundOrFailed_RemovesTheRootAndReportsIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var analyzer = d.AppState.Analyzer;
        var reference = d.Fixture("Greeter").Definition;
        var error = new IOException("unreadable");
        var missing = new FakeAnalyzeReferenceHandlerPorts { Found = new Outcome<BrowseNode?>.Ok(null) };
        var failed = new FakeAnalyzeReferenceHandlerPorts { Found = new Outcome<BrowseNode?>.Failed(error) };

        await new AnalyzeReferenceHandler(missing).HandleAsync(analyzer, d.AppState.ViewCommands, Analysis.BeginAnalyzing(analyzer, reference), CancellationToken.None);
        await new AnalyzeReferenceHandler(failed).HandleAsync(analyzer, d.AppState.ViewCommands, Analysis.BeginAnalyzing(analyzer, reference), CancellationToken.None);

        Assert.Empty(analyzer.Roots);
        Assert.Null(Assert.IsType<AppProblem.Unresolved>(Assert.Single(missing.Reported)).Error);
        Assert.Same(error, Assert.IsType<AppProblem.Unresolved>(Assert.Single(failed.Reported)).Error);
    }

    [Fact]
    public async Task HandleAsync_RootRemovedMeanwhile_NeitherAnalyzesNorReports()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var analyzer = d.AppState.Analyzer;
        var greeter = d.Fixture("Greeter");
        var pending = Analysis.BeginAnalyzing(analyzer, greeter.Definition);
        var ports = new FakeAnalyzeReferenceHandlerPorts { Found = new Outcome<BrowseNode?>.Failed(new IOException("unreadable")), WhileLooking = () => analyzer.RemoveRoot(pending.Root) };

        await new AnalyzeReferenceHandler(ports).HandleAsync(analyzer, d.AppState.ViewCommands, pending, CancellationToken.None);

        Assert.Empty(analyzer.Roots);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_AssemblyClosedWhileLooking_LeavesNoAnalysisAndSavesNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var analyzer = d.AppState.Analyzer;
        var greeter = d.Fixture("Greeter");
        var assembly = d.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == AppStateDriver.FixturePath);
        var pending = Analysis.BeginAnalyzing(analyzer, greeter.Definition);
        var ports = new FakeAnalyzeReferenceHandlerPorts
        {
            Found = new Outcome<BrowseNode?>.Ok(greeter),
            WhileLooking = () => d.AppState.ListSession.CloseAssemblies([assembly]),
        };
        d.TakeViewCommands();

        await new AnalyzeReferenceHandler(ports).HandleAsync(analyzer, d.AppState.ViewCommands, pending, CancellationToken.None);

        Assert.Empty(analyzer.Roots);
        Assert.Empty(analyzer.ListSavedAnalyses());
        Assert.Empty(d.TakeViewCommands());
        Assert.Empty(ports.Reported);
    }
}
