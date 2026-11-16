using ILSight.Core;
using ILSight.CommandArea;

namespace ILSight.App.Tests;

public sealed class AnalyzeReferenceHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReferenceFound_TurnsTheRootIntoItsAnalysisAndShowsIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var analyzer = d.AppState.ListSession.Analyzer;
        var greeter = await d.FixtureAsync("Greeter");
        var pending = Analysis.BeginAnalyzing(analyzer, greeter.Definition);
        var ports = new FakeAnalyzeReferenceHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(greeter)) };
        d.TakeViewCommands();

        await d.RunAsync(() => new AnalyzeReferenceHandler(ports).HandleAsync(analyzer, d.AppState.ViewCommands, pending, CancellationToken.None));

        var root = Assert.Single(analyzer.Found());
        Assert.Equal("Greeter", root.Symbol.Label);
        var viewCommands = d.TakeViewCommands();
        Assert.Equal([new ViewCommand.ShowAnalysis(root)], viewCommands);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_NotFoundRemovesTheRootButFailedShowsItFailedInPlaceAndReportsIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var analyzer = d.AppState.ListSession.Analyzer;
        var reference = (await d.FixtureAsync("Greeter")).Definition;
        var error = new IOException("unreadable");
        var missing = new FakeAnalyzeReferenceHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(null)) };
        var failed = new FakeAnalyzeReferenceHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Failed(error) };

        await d.RunAsync(() =>
        {
            var pendingAnalysis = Analysis.BeginAnalyzing(analyzer, reference);
            return new AnalyzeReferenceHandler(missing).HandleAsync(analyzer, d.AppState.ViewCommands, pendingAnalysis, CancellationToken.None);
        });
        await d.RunAsync(() =>
        {
            var pendingAnalysis = Analysis.BeginAnalyzing(analyzer, reference);
            return new AnalyzeReferenceHandler(failed).HandleAsync(analyzer, d.AppState.ViewCommands, pendingAnalysis, CancellationToken.None);
        });

        var unresolved = Assert.IsType<AnalyzerUnresolvedNode>(Assert.Single(analyzer.Roots));
        Assert.Equal((reference.Name, "unreadable"), (unresolved.Name, unresolved.Error.Message));
        Assert.Empty(analyzer.ListSavedAnalyses());
        Assert.Equal([new Refusal.Unresolved { Reference = reference }], missing.Refused);
        Assert.Empty(missing.Reported);
        Assert.Same(error, Assert.IsType<AppProblem.Unresolved>(Assert.Single(failed.Reported)).Error);
        Assert.Empty(failed.Refused);
    }

    [Fact]
    public async Task FailUnresolved_TheLookupsWorkflowFailed_ShowsTheRootFailedUntilRemoved()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var analyzer = d.AppState.ListSession.Analyzer;
        var pending = Analysis.BeginAnalyzing(analyzer, (await d.FixtureAsync("Greeter")).Definition);

        var broken = new InvalidOperationException("broken");
        analyzer.FailUnresolved(pending.Root, "Greeter", broken);
        var failed = Assert.IsType<AnalyzerUnresolvedNode>(Assert.Single(analyzer.Roots));
        analyzer.RemoveRoot(failed);

        Assert.Same(broken, failed.Error);
        Assert.Empty(analyzer.Roots);
    }

    [Fact]
    public async Task HandleAsync_RootRemovedMeanwhile_NeitherAnalyzesNorReports()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var analyzer = d.AppState.ListSession.Analyzer;
        var greeter = await d.FixtureAsync("Greeter");
        var pending = Analysis.BeginAnalyzing(analyzer, greeter.Definition);
        var ports = new FakeAnalyzeReferenceHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Failed(new IOException("unreadable")), WhileLooking = () => analyzer.RemoveRoot(pending.Root) };

        await d.RunAsync(() => new AnalyzeReferenceHandler(ports).HandleAsync(analyzer, d.AppState.ViewCommands, pending, CancellationToken.None));

        Assert.Empty(analyzer.Roots);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_AssemblyClosedWhileLooking_LeavesNoAnalysisAndSavesNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var analyzer = d.AppState.ListSession.Analyzer;
        var greeter = await d.FixtureAsync("Greeter");
        var assembly = d.AppState.ListSession.Assemblies.Assemblies.Single(a => a.FilePath.Text == AppStateDriver.FixturePath);
        var pending = Analysis.BeginAnalyzing(analyzer, greeter.Definition);
        var ports = new FakeAnalyzeReferenceHandlerPorts
        {
            Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(greeter)),
            WhileLooking = () => Commands.CloseAssemblies(d.AppState, [assembly]),
        };
        d.TakeViewCommands();

        await d.RunAsync(() => new AnalyzeReferenceHandler(ports).HandleAsync(analyzer, d.AppState.ViewCommands, pending, CancellationToken.None));

        Assert.Empty(analyzer.Roots);
        Assert.Empty(analyzer.ListSavedAnalyses());
        var viewCommands = d.TakeViewCommands();
        Assert.Empty(viewCommands);
        Assert.Empty(ports.Reported);
    }
}
