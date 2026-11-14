using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class OpenAndShowIdHandlerTests
{
    [Fact]
    public async Task HandleAsync_IdFound_ShowsTheNode()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var greeter = await d.FixtureAsync("Greeter");
        var opening = new FakeOpenAndShowIdHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(greeter)) };

        var load = await d.RunAsync(() => new OpenAndShowIdHandler(opening).HandleAsync(d.AppState.Navigation, d.AppState.Decompiler, [], "T:Greeter", CancellationToken.None));

        Assert.Same(greeter, d.AppState.Navigation.ActiveTab?.Node);
        Assert.Same(greeter, load?.Node);
        Assert.Empty(opening.Reported);
    }

    [Fact]
    public async Task HandleAsync_LookupThrows_LetsItThroughToTheEdge()
    {
        var d = AppStateDriver.WithoutScreen();
        var bug = new InvalidOperationException("A bug in the core's own code on what it read.");
        var opening = new FakeOpenAndShowIdHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(null)), WhileOpening = () => throw bug };

        var handling = d.BeginAsync(() => new OpenAndShowIdHandler(opening).HandleAsync(d.AppState.Navigation, d.AppState.Decompiler, [], "T:Greeter", CancellationToken.None));

        Assert.Same(bug, await Assert.ThrowsAsync<InvalidOperationException>(async () => Assert.Null(await d.EndAsync(handling))));
        Assert.Empty(opening.Reported);
    }

    [Fact]
    public async Task HandleAsync_IdNotFound_ReportsTheId()
    {
        var d = AppStateDriver.WithoutScreen();
        var opening = new FakeOpenAndShowIdHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(null)) };

        var load = await d.RunAsync(() => new OpenAndShowIdHandler(opening).HandleAsync(d.AppState.Navigation, d.AppState.Decompiler, [], "T:Nowhere.Missing", CancellationToken.None));

        var refused = Assert.IsType<Refusal.IdNotFound>(Assert.Single(opening.Refused));
        Assert.Equal("T:Nowhere.Missing", refused.Id);
        Assert.Empty(opening.Reported);
        Assert.Null(load);
    }

    [Fact]
    public async Task HandleAsync_SomethingElseAskedForMeanwhile_NeitherShowsNorReports()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var shown = d.AppState.Navigation.ActiveTab?.Node;
        Assert.NotNull(shown);
        var appState = d.AppState;
        var found = new FakeOpenAndShowIdHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(await d.FixtureAsync("Greeter"))), WhileOpening = () => appState.Navigation.BeginRequest() };
        var missing = new FakeOpenAndShowIdHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(null)), WhileOpening = () => appState.Navigation.BeginRequest() };

        var foundLoad = await d.RunAsync(() => new OpenAndShowIdHandler(found).HandleAsync(appState.Navigation, appState.Decompiler, [], "T:Greeter", CancellationToken.None));
        var missingLoad = await d.RunAsync(() => new OpenAndShowIdHandler(missing).HandleAsync(appState.Navigation, appState.Decompiler, [], "T:Nowhere.Missing", CancellationToken.None));

        Assert.Same(shown, appState.Navigation.ActiveTab?.Node);
        Assert.Empty(found.Reported);
        Assert.Empty(missing.Reported);
        Assert.Null(foundLoad);
        Assert.Null(missingLoad);
    }

    [Fact]
    public async Task HandleAsync_OpeningFailed_ReportsItACancellationNobodyAskedForToo()
    {
        var d = AppStateDriver.WithoutScreen();
        var failed = new FakeOpenAndShowIdHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Failed(new IOException("unreadable")) };
        // The library's timeout: the request's own cancellation is let through, never answered as a failure.
        var timedOut = new FakeOpenAndShowIdHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Failed(new TaskCanceledException()) };

        var failedLoad = await d.RunAsync(() => new OpenAndShowIdHandler(failed).HandleAsync(d.AppState.Navigation, d.AppState.Decompiler, [], "T:Greeter", CancellationToken.None));
        var timedOutLoad = await d.RunAsync(() => new OpenAndShowIdHandler(timedOut).HandleAsync(d.AppState.Navigation, d.AppState.Decompiler, [], "T:Greeter", CancellationToken.None));

        var reported = Assert.IsType<AppProblem.OpenFailed>(Assert.Single(failed.Reported));
        Assert.Equal("unreadable", reported.Error.Message);
        Assert.IsType<TaskCanceledException>(Assert.IsType<AppProblem.OpenFailed>(Assert.Single(timedOut.Reported)).Error);
        Assert.Null(failedLoad);
        Assert.Null(timedOutLoad);
    }
}
