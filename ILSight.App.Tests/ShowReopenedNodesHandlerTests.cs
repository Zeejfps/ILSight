using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class ShowReopenedNodesHandlerTests
{
    /// <summary>Looks up whatever places it is asked for when the test says, lets the cancellation of the token it
    /// was asked with through, as the adapter does; keeps what it was asked.</summary>
    private sealed class FakeLookup : ShowReopenedNodesHandler.IPorts
    {
        public List<IReadOnlyList<NodePath?>> Asked { get; } = [];

        public List<TaskCompletionSource<Outcome<List<BrowseNode?>>>> Answers { get; } = [];

        public Task<Outcome<List<BrowseNode?>>> LookUpListedPlacesAsync(IReadOnlyList<NodePath?> places, CancellationToken ct)
        {
            var answer = new TaskCompletionSource<Outcome<List<BrowseNode?>>>();
            ct.Register(() => answer.TrySetCanceled(ct));
            Asked.Add(places);
            Answers.Add(answer);
            return answer.Task;
        }
    }

    /// <summary>A tab of <paramref name="d"/>'s reopened to wait for the fixture Greeter, as the saved documents
    /// keep it.</summary>
    private static WaitingNode WaitForGreeter(AppStateDriver d)
    {
        var place = BrowseNodes.GetPath(d.Fixture("Greeter"));
        var record = new NodeDocumentRecord("document-9", "Greeter", place, DecompilationLanguage.CSharp, null);
        var tab = Tabs.AddNew(d.AppState.Tabs);
        Tabs.WaitForNode(tab, place, record.Title, record.Language);
        var reopened = SavedDocuments.WaitFor(d.AppState.Reopened, tab, record);
        return new WaitingNode { Reopened = reopened, Record = record, Place = place };
    }

    [Fact]
    public async Task HandleAsync_NodeFound_PointsTheTabAtItAndReturnsTheLoad()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var waiting = WaitForGreeter(d);
        var greeter = d.Fixture("Greeter");
        var ports = new FakeLookup();

        var showing = new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState, [waiting], CancellationToken.None);
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
        var ports = new FakeLookup();

        var showing = new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState, [waiting], CancellationToken.None);
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
        var ports = new FakeLookup();

        var showing = new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState, [waiting], CancellationToken.None);
        Tabs.Clear(waiting.Reopened.Tab);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<BrowseNode?>>.Ok([d.Fixture("Greeter")]));
        var loads = await showing;

        Assert.Empty(loads);
        Assert.IsType<TabContent.Empty>(waiting.Reopened.Tab.Content);
    }

    [Fact]
    public async Task HandleAsync_NoneWaiting_LooksUpNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var ports = new FakeLookup();

        var loads = await new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState, [], CancellationToken.None);

        Assert.Empty(loads);
        Assert.Empty(ports.Asked);
        Assert.False(d.AppState.IsFindingDocuments);
    }
}
