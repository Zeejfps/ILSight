using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class ShowReopenedNodesHandlerTests
{
    /// <summary>A tab of <paramref name="d"/>'s reopened to wait for the fixture Greeter, as the saved documents
    /// keep it.</summary>
    private static WaitingNode WaitForGreeter(AppStateDriver d)
    {
        var place = d.Fixture("Greeter").Path;
        var record = new NodeDocumentRecord
        {
            Panel = "document-9",
            Title = "Greeter",
            Location = place,
            Language = DecompilationLanguage.CSharp,
            View = null,
        };
        var tab = d.AppState.Navigation.OpenReopenedTab();
        d.AppState.Navigation.WaitForNode(tab, place, record.Title, record.Language);
        var reopened = SavedDocuments.WaitFor(d.AppState.Reopened, tab, record);
        return new WaitingNode { Reopened = reopened, Record = record, Place = place };
    }

    [Fact]
    public async Task HandleAsync_NodeFound_PointsTheTabAtItAndReturnsTheLoad()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var waiting = WaitForGreeter(d);
        var greeter = d.Fixture("Greeter");
        var ports = new FakeShowReopenedNodesHandlerPorts();

        var showing = new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState, new ReopenedNodesLookup { Nodes = [waiting] }, CancellationToken.None);
        Assert.True(d.AppState.IsFindingDocuments);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<BrowseNode?>>.Ok([greeter]));
        var loads = await showing;

        Assert.Equal([waiting.Place], Assert.Single(ports.Asked));
        var load = Assert.Single(loads);
        Assert.Same(waiting.Reopened.Tab, load.Tab);
        Assert.Same(greeter, load.Node);
        Assert.False(d.AppState.IsFindingDocuments);
        Assert.Empty(d.AppState.Reopened.Tabs);
    }

    [Fact]
    public async Task HandleAsync_LookupFailed_ShowsTheTabsPlaceGone()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var waiting = WaitForGreeter(d);
        var ports = new FakeShowReopenedNodesHandlerPorts();

        var showing = new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState, new ReopenedNodesLookup { Nodes = [waiting] }, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<BrowseNode?>>.Failed(new InvalidOperationException("Unreadable.")));
        var loads = await showing;

        Assert.Empty(loads);
        Assert.IsType<TabContent.Gone>(waiting.Reopened.Tab.Content);
        Assert.False(d.AppState.IsFindingDocuments);
    }

    [Fact]
    public async Task HandleAsync_TabPointedElsewhereMeanwhile_LeavesItAlone()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var waiting = WaitForGreeter(d);
        var ports = new FakeShowReopenedNodesHandlerPorts();

        var showing = new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState, new ReopenedNodesLookup { Nodes = [waiting] }, CancellationToken.None);
        d.AppState.Navigation.RestoreDiagram(waiting.Reopened.Tab, new TypeDiagram(), "Other diagram");
        Assert.Single(ports.Answers).SetResult(new Outcome<List<BrowseNode?>>.Ok([d.Fixture("Greeter")]));
        var loads = await showing;

        Assert.Empty(loads);
        Assert.IsType<TabContent.Diagram>(waiting.Reopened.Tab.Content);
    }

    [Fact]
    public async Task HandleAsync_NoneWaiting_LooksUpNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var ports = new FakeShowReopenedNodesHandlerPorts();

        var loads = await new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState, new ReopenedNodesLookup { Nodes = [] }, CancellationToken.None);

        Assert.Empty(loads);
        Assert.Empty(ports.Asked);
        Assert.False(d.AppState.IsFindingDocuments);
    }
}
