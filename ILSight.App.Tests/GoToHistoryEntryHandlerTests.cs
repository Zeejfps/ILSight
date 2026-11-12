using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class GoToHistoryEntryHandlerTests
{
    /// <summary>A driver that showed Greeter, then Farewell in its place, so that going back to Greeter looks it up.</summary>
    private static AppStateDriver ShowGreeterThenFarewell()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(d.Fixture("Greeter"));
        d.Show(d.Fixture("Farewell"));
        return d;
    }

    private static NodePath PlaceOf(AppState appState, int index)
    {
        return Assert.IsType<HistoryEntry.Node>(appState.Navigation.HistoryEntries[index]).Place;
    }

    /// <summary>Goes to the history entry at <paramref name="index"/>, as the toolbar does in the frame it is picked in,
    /// and takes the lookup of its place that this began, since the tab no longer shows it.</summary>
    private static HistoryMove.LookUp GoTo(AppState appState, int index)
    {
        var move = appState.Navigation.GoToEntry(index, appState.Assemblies, appState.Language, appState.DecompilerOptions);
        return Assert.IsType<HistoryMove.LookUp>(move);
    }

    [Fact]
    public async Task HandleAsync_PlaceFound_ShowsItAndMovesToTheEntry()
    {
        var d = ShowGreeterThenFarewell();
        var greeter = d.Fixture("Greeter");
        var back = d.AppState.Navigation.HistoryIndex - 1;
        var lookup = new FakeGoToHistoryEntryHandlerPorts { Found = new Outcome<BrowseNode?>.Ok(greeter) };

        var load = await new GoToHistoryEntryHandler(lookup).HandleAsync(d.AppState, GoTo(d.AppState, back), CancellationToken.None);

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
        var d = ShowGreeterThenFarewell();
        var farewell = d.Fixture("Farewell");
        var back = d.AppState.Navigation.HistoryIndex - 1;
        var lookup = new FakeGoToHistoryEntryHandlerPorts { Found = new Outcome<BrowseNode?>.Ok(null) };

        var load = await new GoToHistoryEntryHandler(lookup).HandleAsync(d.AppState, GoTo(d.AppState, back), CancellationToken.None);

        var gone = Assert.IsType<AppProblem.HistoryPlaceGone>(Assert.Single(lookup.Reported));
        Assert.Equal(PlaceOf(d.AppState, back), gone.Place);
        Assert.Null(gone.Error);
        Assert.Same(farewell, d.AppState.Navigation.ActiveTab?.Node);
        Assert.False(d.AppState.IsResolving);
        Assert.Null(load);
    }

    [Fact]
    public async Task HandleAsync_LookupFailed_ReportsThePlaceGoneWithTheError()
    {
        var d = ShowGreeterThenFarewell();
        var back = d.AppState.Navigation.HistoryIndex - 1;
        var error = new IOException("unreadable");
        var lookup = new FakeGoToHistoryEntryHandlerPorts { Found = new Outcome<BrowseNode?>.Failed(error) };

        var load = await new GoToHistoryEntryHandler(lookup).HandleAsync(d.AppState, GoTo(d.AppState, back), CancellationToken.None);

        Assert.Same(error, Assert.IsType<AppProblem.HistoryPlaceGone>(Assert.Single(lookup.Reported)).Error);
        Assert.False(d.AppState.IsResolving);
        Assert.Null(load);
    }

    [Fact]
    public async Task HandleAsync_SomethingElseAskedForMeanwhile_NeitherShowsNorReports()
    {
        var found = ShowGreeterThenFarewell();
        var missing = ShowGreeterThenFarewell();
        var foundLookup = new FakeGoToHistoryEntryHandlerPorts { Found = new Outcome<BrowseNode?>.Ok(found.Fixture("Greeter")), WhileLooking = () => found.AppState.Navigation.BeginRequest() };
        var missingLookup = new FakeGoToHistoryEntryHandlerPorts { Found = new Outcome<BrowseNode?>.Ok(null), WhileLooking = () => missing.AppState.Navigation.BeginRequest() };

        var foundLoad = await new GoToHistoryEntryHandler(foundLookup).HandleAsync(found.AppState, GoTo(found.AppState, found.AppState.Navigation.HistoryIndex - 1), CancellationToken.None);
        var missingLoad = await new GoToHistoryEntryHandler(missingLookup).HandleAsync(missing.AppState, GoTo(missing.AppState, missing.AppState.Navigation.HistoryIndex - 1), CancellationToken.None);

        Assert.Same(found.Fixture("Farewell"), found.AppState.Navigation.ActiveTab?.Node);
        Assert.Empty(foundLookup.Reported);
        Assert.Empty(missingLookup.Reported);
        Assert.Null(foundLoad);
        Assert.Null(missingLoad);
    }
}
