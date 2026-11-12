using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class NavigateHandlerTests
{
    /// <summary>Finds <see cref="Found"/> for any reference, doing <see cref="WhileLooking"/> meanwhile; keeps the
    /// references looked up, the loads it was asked to bring in, and the problems reported.</summary>
    private sealed class FakeLookup : NavigateHandler.IPorts
    {
        public required Outcome<BrowseNode?> Found { get; init; }

        public Action WhileLooking { get; init; } = static () => { };

        public List<CodeReference.Navigable> LookedUp { get; } = [];

        public List<DecompileTicket> Decompiled { get; } = [];

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

        public Task DecompileIntoTabAsync(Queue<ViewCommand> viewCommands, DecompileTicket load, CancellationToken ct)
        {
            Decompiled.Add(load);
            return Task.CompletedTask;
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
        Assert.Same(appState.Tabs.Active, Assert.Single(lookup.Decompiled).Tab);
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
        Assert.Empty(found.Decompiled);
        Assert.Empty(found.Reported);
        Assert.Empty(missing.Reported);
    }
}
