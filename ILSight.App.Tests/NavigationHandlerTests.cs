using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.App.Tests;

public sealed class NavigateHandlerTests
{
    /// <summary>Finds <see cref="Found"/> for any reference, doing <see cref="WhileLooking"/> meanwhile; keeps the
    /// references looked up and the problems reported.</summary>
    private sealed class FakeLookup : NavigateHandler.IPorts
    {
        public required Outcome<BrowseNode?> Found { get; init; }

        public Action WhileLooking { get; init; } = static () => { };

        public List<CodeReference.Navigable> LookedUp { get; } = [];

        public List<AppProblem> Reported { get; } = [];

        public void ReportError(AppProblem problem)
        {
            Reported.Add(problem);
        }

        public Task<Outcome<BrowseNode?>> LookUpReferenceAsync(CodeReference.Navigable reference, CancellationToken ct)
        {
            LookedUp.Add(reference);
            WhileLooking();
            return Task.FromResult(Found);
        }
    }

    [Fact]
    public async Task HandleAsync_ReferenceFound_ShowsItAndRecordsItInTheHistory()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var farewell = d.Fixture("Farewell");
        var reference = farewell.Definition;
        var resolvingWhileLooking = false;
        var lookup = new FakeLookup { Found = new Outcome<BrowseNode?>.Ok(farewell), WhileLooking = () => resolvingWhileLooking = appState.IsResolving };

        await new NavigateHandler(lookup).HandleAsync(appState, reference, inNewTab: false, CancellationToken.None);

        Assert.Same(reference, Assert.Single(lookup.LookedUp));
        Assert.Same(farewell, appState.Tabs.Active?.Node);
        Assert.True(appState.History.IsCurrentPlace(appState.Tabs.Active?.Location));
        Assert.True(resolvingWhileLooking);
        Assert.False(appState.IsResolving);
        Assert.Empty(lookup.Reported);
    }

    [Fact]
    public async Task HandleAsync_ReferenceLeadsNowhereOrLookupFailed_ReportsItUnresolved()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var shown = d.AppState.Tabs.Active?.Node;
        var reference = d.Fixture("Farewell").Definition;
        var error = new IOException("unreadable");
        var missing = new FakeLookup { Found = new Outcome<BrowseNode?>.Ok(null) };
        var failed = new FakeLookup { Found = new Outcome<BrowseNode?>.Failed(error) };

        await new NavigateHandler(missing).HandleAsync(d.AppState, reference, inNewTab: false, CancellationToken.None);
        await new NavigateHandler(failed).HandleAsync(d.AppState, reference, inNewTab: false, CancellationToken.None);

        var notThere = Assert.IsType<AppProblem.Unresolved>(Assert.Single(missing.Reported));
        Assert.Same(reference, notThere.Reference);
        Assert.Null(notThere.Error);
        Assert.Same(error, Assert.IsType<AppProblem.Unresolved>(Assert.Single(failed.Reported)).Error);
        Assert.Same(shown, d.AppState.Tabs.Active?.Node);
        Assert.False(d.AppState.IsResolving);
    }

    [Fact]
    public async Task HandleAsync_SomethingElseAskedForMeanwhile_NeitherShowsNorReports()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var shown = appState.Tabs.Active?.Node;
        var farewell = d.Fixture("Farewell");
        var found = new FakeLookup { Found = new Outcome<BrowseNode?>.Ok(farewell), WhileLooking = () => appState.Intent.Begin() };
        var missing = new FakeLookup { Found = new Outcome<BrowseNode?>.Ok(null), WhileLooking = () => appState.Intent.Begin() };

        await new NavigateHandler(found).HandleAsync(appState, farewell.Definition, inNewTab: false, CancellationToken.None);
        await new NavigateHandler(missing).HandleAsync(appState, farewell.Definition, inNewTab: false, CancellationToken.None);

        Assert.Same(shown, appState.Tabs.Active?.Node);
        Assert.Empty(found.Reported);
        Assert.Empty(missing.Reported);
    }
}

public sealed class ShowInMetadataHandlerTests
{
    /// <summary>Finds <see cref="Found"/> for any reference, doing <see cref="WhileLooking"/> meanwhile; keeps the
    /// problems reported.</summary>
    private sealed class FakeLookup : ShowInMetadataHandler.IPorts
    {
        public required Outcome<MetadataTarget?> Found { get; init; }

        public Action WhileLooking { get; init; } = static () => { };

        public List<AppProblem> Reported { get; } = [];

        public void ReportError(AppProblem problem)
        {
            Reported.Add(problem);
        }

        public Task<Outcome<MetadataTarget?>> LookUpMetadataRowAsync(CodeReference.Navigable reference, CancellationToken ct)
        {
            WhileLooking();
            return Task.FromResult(Found);
        }
    }

    [Fact]
    public async Task HandleAsync_RowFound_ShowsTheTableItIsIn()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var reference = d.Fixture("Greeter").Definition;
        var row = Assert.NotNull(NodeLookup.FindMetadataRow(d.AppState.Assemblies, d.AppState.Session, reference));
        var lookup = new FakeLookup { Found = new Outcome<MetadataTarget?>.Ok(row) };

        await new ShowInMetadataHandler(lookup).HandleAsync(d.AppState, reference, inNewTab: false, CancellationToken.None);

        Assert.Same(row.Node, d.AppState.Tabs.Active?.Node);
        Assert.False(d.AppState.IsResolving);
        Assert.Empty(lookup.Reported);
    }

    [Fact]
    public async Task HandleAsync_NoRowOrLookupFailed_ReportsItUnresolved()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var shown = d.AppState.Tabs.Active?.Node;
        var reference = d.Fixture("Greeter").Definition;
        var error = new IOException("unreadable");
        var missing = new FakeLookup { Found = new Outcome<MetadataTarget?>.Ok(null) };
        var failed = new FakeLookup { Found = new Outcome<MetadataTarget?>.Failed(error) };

        await new ShowInMetadataHandler(missing).HandleAsync(d.AppState, reference, inNewTab: false, CancellationToken.None);
        await new ShowInMetadataHandler(failed).HandleAsync(d.AppState, reference, inNewTab: false, CancellationToken.None);

        Assert.Null(Assert.IsType<AppProblem.Unresolved>(Assert.Single(missing.Reported)).Error);
        Assert.Same(error, Assert.IsType<AppProblem.Unresolved>(Assert.Single(failed.Reported)).Error);
        Assert.Same(shown, d.AppState.Tabs.Active?.Node);
        Assert.False(d.AppState.IsResolving);
    }

    [Fact]
    public async Task HandleAsync_SomethingElseAskedForMeanwhile_NeitherShowsNorReports()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var shown = appState.Tabs.Active?.Node;
        var reference = d.Fixture("Greeter").Definition;
        var row = NodeLookup.FindMetadataRow(appState.Assemblies, appState.Session, reference);
        var found = new FakeLookup { Found = new Outcome<MetadataTarget?>.Ok(row), WhileLooking = () => appState.Intent.Begin() };
        var missing = new FakeLookup { Found = new Outcome<MetadataTarget?>.Ok(null), WhileLooking = () => appState.Intent.Begin() };

        await new ShowInMetadataHandler(found).HandleAsync(appState, reference, inNewTab: false, CancellationToken.None);
        await new ShowInMetadataHandler(missing).HandleAsync(appState, reference, inNewTab: false, CancellationToken.None);

        Assert.Same(shown, appState.Tabs.Active?.Node);
        Assert.Empty(found.Reported);
        Assert.Empty(missing.Reported);
    }
}

public sealed class GoToHistoryEntryHandlerTests
{
    /// <summary>Finds <see cref="Found"/> for any place, doing <see cref="WhileLooking"/> meanwhile; keeps the
    /// places looked up and the problems reported.</summary>
    private sealed class FakeLookup : GoToHistoryEntryHandler.IPorts
    {
        public required Outcome<BrowseNode?> Found { get; init; }

        public Action WhileLooking { get; init; } = static () => { };

        public List<NodePath> LookedUp { get; } = [];

        public List<AppProblem> Reported { get; } = [];

        public void ReportError(AppProblem problem)
        {
            Reported.Add(problem);
        }

        public Task<Outcome<BrowseNode?>> LookUpPlaceAsync(NodePath place, CancellationToken ct)
        {
            LookedUp.Add(place);
            WhileLooking();
            return Task.FromResult(Found);
        }
    }

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
        return Assert.IsType<HistoryEntry.Node>(appState.History.Entries[index]).Place;
    }

    [Fact]
    public async Task HandleAsync_PlaceFound_ShowsItAndMovesToTheEntry()
    {
        var d = ShowGreeterThenFarewell();
        var greeter = d.Fixture("Greeter");
        var back = d.AppState.History.CurrentIndex - 1;
        var lookup = new FakeLookup { Found = new Outcome<BrowseNode?>.Ok(greeter) };

        await new GoToHistoryEntryHandler(lookup).HandleAsync(d.AppState, back, CancellationToken.None);

        Assert.Equal(PlaceOf(d.AppState, back), Assert.Single(lookup.LookedUp));
        Assert.Same(greeter, d.AppState.Tabs.Active?.Node);
        Assert.Equal(back, d.AppState.History.CurrentIndex);
        Assert.False(d.AppState.IsResolving);
        Assert.Empty(lookup.Reported);
    }

    [Fact]
    public async Task HandleAsync_PlaceGone_ReportsThePlaceGone()
    {
        var d = ShowGreeterThenFarewell();
        var farewell = d.Fixture("Farewell");
        var back = d.AppState.History.CurrentIndex - 1;
        var lookup = new FakeLookup { Found = new Outcome<BrowseNode?>.Ok(null) };

        await new GoToHistoryEntryHandler(lookup).HandleAsync(d.AppState, back, CancellationToken.None);

        var gone = Assert.IsType<AppProblem.HistoryPlaceGone>(Assert.Single(lookup.Reported));
        Assert.Equal(PlaceOf(d.AppState, back), gone.Place);
        Assert.Null(gone.Error);
        Assert.Same(farewell, d.AppState.Tabs.Active?.Node);
        Assert.False(d.AppState.IsResolving);
    }

    [Fact]
    public async Task HandleAsync_LookupFailed_ReportsThePlaceGoneWithTheError()
    {
        var d = ShowGreeterThenFarewell();
        var back = d.AppState.History.CurrentIndex - 1;
        var error = new IOException("unreadable");
        var lookup = new FakeLookup { Found = new Outcome<BrowseNode?>.Failed(error) };

        await new GoToHistoryEntryHandler(lookup).HandleAsync(d.AppState, back, CancellationToken.None);

        Assert.Same(error, Assert.IsType<AppProblem.HistoryPlaceGone>(Assert.Single(lookup.Reported)).Error);
        Assert.False(d.AppState.IsResolving);
    }

    [Fact]
    public async Task HandleAsync_SomethingElseAskedForMeanwhile_NeitherShowsNorReports()
    {
        var found = ShowGreeterThenFarewell();
        var missing = ShowGreeterThenFarewell();
        var foundLookup = new FakeLookup { Found = new Outcome<BrowseNode?>.Ok(found.Fixture("Greeter")), WhileLooking = () => found.AppState.Intent.Begin() };
        var missingLookup = new FakeLookup { Found = new Outcome<BrowseNode?>.Ok(null), WhileLooking = () => missing.AppState.Intent.Begin() };

        await new GoToHistoryEntryHandler(foundLookup).HandleAsync(found.AppState, found.AppState.History.CurrentIndex - 1, CancellationToken.None);
        await new GoToHistoryEntryHandler(missingLookup).HandleAsync(missing.AppState, missing.AppState.History.CurrentIndex - 1, CancellationToken.None);

        Assert.Same(found.Fixture("Farewell"), found.AppState.Tabs.Active?.Node);
        Assert.Empty(foundLookup.Reported);
        Assert.Empty(missingLookup.Reported);
    }
}
