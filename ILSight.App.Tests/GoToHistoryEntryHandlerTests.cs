using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class GoToHistoryEntryHandlerTests
{
    // Farewell shows in Greeter's place, so that going back to Greeter looks it up.
    private static async Task<AppStateDriver> ShowGreeterThenFarewellAsync()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(await d.FixtureAsync("Greeter"));
        d.Show(await d.FixtureAsync("Farewell"));
        return d;
    }

    private static NodePath PlaceOf(AppState appState, int index)
    {
        return Assert.IsType<HistoryEntry.Node>(appState.Navigation.HistoryEntries[index]).Place;
    }

    private static HistoryMove.LookUp GoTo(AppState appState, int index)
    {
        var move = appState.Navigation.GoToEntry(index, appState.Assemblies, appState.FinishedExports, appState.Decompiler.Setup);
        // The tab no longer shows the entry's place, so going to it begins a lookup.
        return Assert.IsType<HistoryMove.LookUp>(move);
    }

    [Fact]
    public async Task HandleAsync_PlaceFound_ShowsItAndMovesToTheEntry()
    {
        var d = await ShowGreeterThenFarewellAsync();
        var greeter = await d.FixtureAsync("Greeter");
        var back = d.AppState.Navigation.HistoryIndex - 1;
        var lookup = new FakeGoToHistoryEntryHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(greeter)) };

        var load = await d.RunAsync(() => new GoToHistoryEntryHandler(lookup).HandleAsync(d.AppState, GoTo(d.AppState, back), CancellationToken.None));

        Assert.Equal(PlaceOf(d.AppState, back), Assert.Single(lookup.LookedUp));
        Assert.Same(greeter, d.AppState.Navigation.ActiveTab?.Node);
        Assert.Same(greeter, load?.Node);
        Assert.Equal(back, d.AppState.Navigation.HistoryIndex);
        Assert.False(d.AppState.IsResolving);
        Assert.Empty(lookup.Reported);
    }

    [Fact]
    public async Task HandleAsync_PlaceGone_ReportsThePlaceGone()
    {
        var d = await ShowGreeterThenFarewellAsync();
        var farewell = await d.FixtureAsync("Farewell");
        var back = d.AppState.Navigation.HistoryIndex - 1;
        var lookup = new FakeGoToHistoryEntryHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(null)) };

        var load = await d.RunAsync(() => new GoToHistoryEntryHandler(lookup).HandleAsync(d.AppState, GoTo(d.AppState, back), CancellationToken.None));

        var gone = Assert.IsType<Refusal.HistoryPlaceGone>(Assert.Single(lookup.Refused));
        Assert.Equal(PlaceOf(d.AppState, back), gone.Place);
        Assert.Empty(lookup.Reported);
        Assert.Same(farewell, d.AppState.Navigation.ActiveTab?.Node);
        Assert.False(d.AppState.IsResolving);
        Assert.Null(load);
    }

    [Fact]
    public async Task HandleAsync_LookupFailed_ReportsThePlaceGoneWithTheError()
    {
        var d = await ShowGreeterThenFarewellAsync();
        var back = d.AppState.Navigation.HistoryIndex - 1;
        var error = new IOException("unreadable");
        var lookup = new FakeGoToHistoryEntryHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Failed(error) };

        var load = await d.RunAsync(() => new GoToHistoryEntryHandler(lookup).HandleAsync(d.AppState, GoTo(d.AppState, back), CancellationToken.None));

        Assert.Same(error, Assert.IsType<AppProblem.HistoryPlaceGone>(Assert.Single(lookup.Reported)).Error);
        Assert.False(d.AppState.IsResolving);
        Assert.Null(load);
    }

    [Fact]
    public async Task HandleAsync_SomethingElseAskedForMeanwhile_NeitherShowsNorReports()
    {
        var found = await ShowGreeterThenFarewellAsync();
        var missing = await ShowGreeterThenFarewellAsync();
        var foundLookup = new FakeGoToHistoryEntryHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(await found.FixtureAsync("Greeter"))), WhileLooking = () => found.AppState.Navigation.BeginRequest() };
        var missingLookup = new FakeGoToHistoryEntryHandlerPorts { Found = new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(null)), WhileLooking = () => missing.AppState.Navigation.BeginRequest() };

        var foundLoad = await new GoToHistoryEntryHandler(foundLookup).HandleAsync(found.AppState, GoTo(found.AppState, found.AppState.Navigation.HistoryIndex - 1), CancellationToken.None);
        var missingLoad = await new GoToHistoryEntryHandler(missingLookup).HandleAsync(missing.AppState, GoTo(missing.AppState, missing.AppState.Navigation.HistoryIndex - 1), CancellationToken.None);

        Assert.Same(await found.FixtureAsync("Farewell"), found.AppState.Navigation.ActiveTab?.Node);
        Assert.Empty(foundLookup.Reported);
        Assert.Empty(missingLookup.Reported);
        Assert.Null(foundLoad);
        Assert.Null(missingLoad);
    }
}
