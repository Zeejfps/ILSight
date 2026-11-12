using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class OpenAndShowIdHandlerTests
{
    [Fact]
    public async Task HandleAsync_IdFound_ShowsTheNode()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var greeter = d.Fixture("Greeter");
        var opening = new FakeOpenAndShowIdHandlerPorts { Found = new Outcome<BrowseNode?>.Ok(greeter) };

        var load = await new OpenAndShowIdHandler(opening).HandleAsync(d.AppState, [], "T:Greeter", CancellationToken.None);

        Assert.Same(greeter, d.AppState.Navigation.ActiveTab?.Node);
        Assert.Same(greeter, load?.Node);
        Assert.Empty(opening.Reported);
    }

    [Fact]
    public async Task HandleAsync_IdNotFound_ReportsTheId()
    {
        var d = AppStateDriver.WithoutScreen();
        var opening = new FakeOpenAndShowIdHandlerPorts { Found = new Outcome<BrowseNode?>.Ok(null) };

        var load = await new OpenAndShowIdHandler(opening).HandleAsync(d.AppState, [], "T:Nowhere.Missing", CancellationToken.None);

        var reported = Assert.IsType<AppProblem.IdNotFound>(Assert.Single(opening.Reported));
        Assert.Equal("T:Nowhere.Missing", reported.Id);
        Assert.Null(load);
    }

    [Fact]
    public async Task HandleAsync_SomethingElseAskedForMeanwhile_NeitherShowsNorReports()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var shown = d.AppState.Navigation.ActiveTab?.Node;
        Assert.NotNull(shown);
        var appState = d.AppState;
        var found = new FakeOpenAndShowIdHandlerPorts { Found = new Outcome<BrowseNode?>.Ok(d.Fixture("Greeter")), WhileOpening = () => appState.Navigation.BeginRequest() };
        var missing = new FakeOpenAndShowIdHandlerPorts { Found = new Outcome<BrowseNode?>.Ok(null), WhileOpening = () => appState.Navigation.BeginRequest() };

        var foundLoad = await new OpenAndShowIdHandler(found).HandleAsync(appState, [], "T:Greeter", CancellationToken.None);
        var missingLoad = await new OpenAndShowIdHandler(missing).HandleAsync(appState, [], "T:Nowhere.Missing", CancellationToken.None);

        Assert.Same(shown, appState.Navigation.ActiveTab?.Node);
        Assert.Empty(found.Reported);
        Assert.Empty(missing.Reported);
        Assert.Null(foundLoad);
        Assert.Null(missingLoad);
    }

    [Fact]
    public async Task HandleAsync_OpeningFailed_ReportsItUnlessItWasCancelled()
    {
        var d = AppStateDriver.WithoutScreen();
        var failed = new FakeOpenAndShowIdHandlerPorts { Found = new Outcome<BrowseNode?>.Failed(new IOException("unreadable")) };
        var cancelled = new FakeOpenAndShowIdHandlerPorts { Found = new Outcome<BrowseNode?>.Failed(new OperationCanceledException()) };

        var failedLoad = await new OpenAndShowIdHandler(failed).HandleAsync(d.AppState, [], "T:Greeter", CancellationToken.None);
        var cancelledLoad = await new OpenAndShowIdHandler(cancelled).HandleAsync(d.AppState, [], "T:Greeter", CancellationToken.None);

        var reported = Assert.IsType<AppProblem.OpenFailed>(Assert.Single(failed.Reported));
        Assert.Equal("unreadable", reported.Error.Message);
        Assert.Empty(cancelled.Reported);
        Assert.Null(failedLoad);
        Assert.Null(cancelledLoad);
    }
}
