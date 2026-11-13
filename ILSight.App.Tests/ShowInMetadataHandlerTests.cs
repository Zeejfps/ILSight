using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.MetadataArea;

namespace ILSight.App.Tests;

public sealed class ShowInMetadataHandlerTests
{
    [Fact]
    public async Task HandleAsync_RowFound_ShowsTheTableItIsIn()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var reference = (await d.FixtureAsync("Greeter")).Definition;
        var row = Assert.IsType<Lookup<MetadataTarget>.Found>(await NodeLookup.FindMetadataRowAsync(d.AppState.Assemblies, d.AppState.Session, reference, CancellationToken.None)).Value;
        var lookup = new FakeShowInMetadataHandlerPorts { Found = new Outcome<Lookup<MetadataTarget>>.Ok(Lookup.FromNullable<MetadataTarget>(row)) };

        var load = await new ShowInMetadataHandler(lookup).HandleAsync(d.AppState, reference, d.AppState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);

        Assert.Same(row.Node, d.AppState.Navigation.ActiveTab?.Node);
        Assert.Same(row.Node, load?.Node);
        Assert.False(d.AppState.IsResolving);
        Assert.Empty(lookup.Reported);
    }

    [Fact]
    public async Task HandleAsync_NoRowOrLookupFailed_ReportsItUnresolved()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var shown = d.AppState.Navigation.ActiveTab?.Node;
        Assert.NotNull(shown);
        var reference = (await d.FixtureAsync("Greeter")).Definition;
        var error = new IOException("unreadable");
        var missing = new FakeShowInMetadataHandlerPorts { Found = new Outcome<Lookup<MetadataTarget>>.Ok(Lookup.FromNullable<MetadataTarget>(null)) };
        var failed = new FakeShowInMetadataHandlerPorts { Found = new Outcome<Lookup<MetadataTarget>>.Failed(error) };

        var missingLoad = await new ShowInMetadataHandler(missing).HandleAsync(d.AppState, reference, d.AppState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);
        var failedLoad = await new ShowInMetadataHandler(failed).HandleAsync(d.AppState, reference, d.AppState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);

        Assert.Null(Assert.IsType<AppProblem.Unresolved>(Assert.Single(missing.Reported)).Error);
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
        var reference = (await d.FixtureAsync("Greeter")).Definition;
        var row = (await NodeLookup.FindMetadataRowAsync(appState.Assemblies, appState.Session, reference, CancellationToken.None)).FoundValueOrNull;
        var found = new FakeShowInMetadataHandlerPorts { Found = new Outcome<Lookup<MetadataTarget>>.Ok(Lookup.FromNullable<MetadataTarget>(row)), WhileLooking = () => appState.Navigation.BeginRequest() };
        var missing = new FakeShowInMetadataHandlerPorts { Found = new Outcome<Lookup<MetadataTarget>>.Ok(Lookup.FromNullable<MetadataTarget>(null)), WhileLooking = () => appState.Navigation.BeginRequest() };

        var foundLoad = await new ShowInMetadataHandler(found).HandleAsync(appState, reference, appState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);
        var missingLoad = await new ShowInMetadataHandler(missing).HandleAsync(appState, reference, appState.Navigation.BeginResolving(inNewTab: false), CancellationToken.None);

        Assert.Same(shown, appState.Navigation.ActiveTab?.Node);
        Assert.Empty(found.Reported);
        Assert.Empty(missing.Reported);
        Assert.Null(foundLoad);
        Assert.Null(missingLoad);
    }
}
