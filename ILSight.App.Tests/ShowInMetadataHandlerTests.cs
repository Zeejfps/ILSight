using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.App.Tests;

public sealed class ShowInMetadataHandlerTests
{
    [Fact]
    public async Task HandleAsync_RowFound_ShowsTheTableItIsIn()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var reference = d.Fixture("Greeter").Definition;
        var row = Assert.NotNull(NodeLookup.FindMetadataRow(d.AppState.Assemblies, d.AppState.Session, reference));
        var lookup = new FakeShowInMetadataHandlerPorts { Found = new Outcome<MetadataTarget?>.Ok(row) };

        var load = await new ShowInMetadataHandler(lookup).HandleAsync(d.AppState, reference, inNewTab: false, CancellationToken.None);

        Assert.Same(row.Node, d.AppState.Tabs.Active?.Node);
        Assert.Same(row.Node, load?.Node);
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
        var missing = new FakeShowInMetadataHandlerPorts { Found = new Outcome<MetadataTarget?>.Ok(null) };
        var failed = new FakeShowInMetadataHandlerPorts { Found = new Outcome<MetadataTarget?>.Failed(error) };

        var missingLoad = await new ShowInMetadataHandler(missing).HandleAsync(d.AppState, reference, inNewTab: false, CancellationToken.None);
        var failedLoad = await new ShowInMetadataHandler(failed).HandleAsync(d.AppState, reference, inNewTab: false, CancellationToken.None);

        Assert.Null(Assert.IsType<AppProblem.Unresolved>(Assert.Single(missing.Reported)).Error);
        Assert.Same(error, Assert.IsType<AppProblem.Unresolved>(Assert.Single(failed.Reported)).Error);
        Assert.Same(shown, d.AppState.Tabs.Active?.Node);
        Assert.False(d.AppState.IsResolving);
        Assert.Null(missingLoad);
        Assert.Null(failedLoad);
    }

    [Fact]
    public async Task HandleAsync_SomethingElseAskedForMeanwhile_NeitherShowsNorReports()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var appState = d.AppState;
        var shown = appState.Tabs.Active?.Node;
        var reference = d.Fixture("Greeter").Definition;
        var row = NodeLookup.FindMetadataRow(appState.Assemblies, appState.Session, reference);
        var found = new FakeShowInMetadataHandlerPorts { Found = new Outcome<MetadataTarget?>.Ok(row), WhileLooking = () => appState.Intent.Begin() };
        var missing = new FakeShowInMetadataHandlerPorts { Found = new Outcome<MetadataTarget?>.Ok(null), WhileLooking = () => appState.Intent.Begin() };

        var foundLoad = await new ShowInMetadataHandler(found).HandleAsync(appState, reference, inNewTab: false, CancellationToken.None);
        var missingLoad = await new ShowInMetadataHandler(missing).HandleAsync(appState, reference, inNewTab: false, CancellationToken.None);

        Assert.Same(shown, appState.Tabs.Active?.Node);
        Assert.Empty(found.Reported);
        Assert.Empty(missing.Reported);
        Assert.Null(foundLoad);
        Assert.Null(missingLoad);
    }
}
