using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class NavigateHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReferenceFound_ShowsItAndRecordsItInTheHistory()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var farewell = d.Fixture("Farewell");
        var reference = farewell.Definition;
        var resolvingWhileLooking = false;
        var lookup = new FakeNavigateHandlerPorts { Found = new Outcome<BrowseNode?>.Ok(farewell), WhileLooking = () => resolvingWhileLooking = appState.IsResolving };

        var load = await new NavigateHandler(lookup).HandleAsync(appState, reference, appState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);

        Assert.Same(reference, Assert.Single(lookup.LookedUp));
        Assert.Same(farewell, appState.Navigation.ActiveTab?.Node);
        var active = appState.Navigation.ActiveTab;
        Assert.NotNull(active);
        Assert.Same(active, load?.Tab);
        Assert.True(appState.Navigation.IsCurrentPlace(appState.Navigation.ActiveTab?.Location));
        Assert.True(resolvingWhileLooking);
        Assert.False(appState.IsResolving);
        Assert.Empty(lookup.Reported);
    }

    [Fact]
    public async Task HandleAsync_ReferenceLeadsNowhereOrLookupFailed_ReportsItUnresolved()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var shown = d.AppState.Navigation.ActiveTab?.Node;
        Assert.NotNull(shown);
        var reference = d.Fixture("Farewell").Definition;
        var error = new IOException("unreadable");
        var missing = new FakeNavigateHandlerPorts { Found = new Outcome<BrowseNode?>.Ok(null) };
        var failed = new FakeNavigateHandlerPorts { Found = new Outcome<BrowseNode?>.Failed(error) };

        var missingLoad = await new NavigateHandler(missing).HandleAsync(d.AppState, reference, d.AppState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);
        var failedLoad = await new NavigateHandler(failed).HandleAsync(d.AppState, reference, d.AppState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);

        var notThere = Assert.IsType<AppProblem.Unresolved>(Assert.Single(missing.Reported));
        Assert.Same(reference, notThere.Reference);
        Assert.Null(notThere.Error);
        Assert.Same(error, Assert.IsType<AppProblem.Unresolved>(Assert.Single(failed.Reported)).Error);
        Assert.Same(shown, d.AppState.Navigation.ActiveTab?.Node);
        Assert.False(d.AppState.IsResolving);
        Assert.Null(missingLoad);
        Assert.Null(failedLoad);
    }

    [Fact]
    public async Task HandleAsync_SomethingElseAskedForMeanwhile_NeitherShowsNorReports()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var shown = appState.Navigation.ActiveTab?.Node;
        Assert.NotNull(shown);
        var farewell = d.Fixture("Farewell");
        var found = new FakeNavigateHandlerPorts { Found = new Outcome<BrowseNode?>.Ok(farewell), WhileLooking = () => appState.Navigation.BeginRequest() };
        var missing = new FakeNavigateHandlerPorts { Found = new Outcome<BrowseNode?>.Ok(null), WhileLooking = () => appState.Navigation.BeginRequest() };

        var foundLoad = await new NavigateHandler(found).HandleAsync(appState, farewell.Definition, appState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);
        var missingLoad = await new NavigateHandler(missing).HandleAsync(appState, farewell.Definition, appState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);

        Assert.Same(shown, appState.Navigation.ActiveTab?.Node);
        Assert.Null(foundLoad);
        Assert.Empty(found.Reported);
        Assert.Empty(missing.Reported);
        Assert.Null(missingLoad);
    }
}
