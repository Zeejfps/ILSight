using ILSight.Core;
using ILSight.DocumentViewArea;

namespace ILSight.App.Tests;

public sealed class ShowReopenedNodesHandlerTests
{
    /// <summary>A tab of <paramref name="d"/>'s reopened to wait for the fixture Greeter, as the saved documents
    /// keep it.</summary>
    private static async Task<WaitingNode> WaitForGreeterAsync(AppStateDriver d)
    {
        var place = (await d.FixtureAsync("Greeter")).Path;
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
        var waiting = await WaitForGreeterAsync(d);
        var greeter = await d.FixtureAsync("Greeter");
        var ports = new FakeShowReopenedNodesHandlerPorts();

        var showing = new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState, new ReopenedNodesLookup { Nodes = [waiting] }, CancellationToken.None);
        Assert.IsType<TabContent.Reloading>(waiting.Reopened.Tab.Content);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<Lookup<BrowseNode>>>.Ok([new Lookup<BrowseNode>.Found { Value = greeter }]));
        var loads = await showing;

        Assert.Equal([waiting.Place], Assert.Single(ports.Asked));
        var load = Assert.Single(loads);
        Assert.Same(waiting.Reopened.Tab, load.Tab);
        Assert.Same(greeter, load.Node);
        Assert.Empty(d.AppState.Reopened.Tabs);
    }

    [Fact]
    public async Task HandleAsync_LookupFailed_ShowsTheTabsPlaceGone()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var waiting = await WaitForGreeterAsync(d);
        var ports = new FakeShowReopenedNodesHandlerPorts();

        var showing = new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState, new ReopenedNodesLookup { Nodes = [waiting] }, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<Lookup<BrowseNode>>>.Failed(new InvalidOperationException("Unreadable.")));
        var loads = await showing;

        Assert.Empty(loads);
        Assert.IsType<TabContent.Gone>(waiting.Reopened.Tab.Content);
    }

    [Fact]
    public async Task HandleAsync_PlaceInAModuleThatCouldNotBeRead_ShowsTheTabSayingWhichAndWhy()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var waiting = await WaitForGreeterAsync(d);
        var module = d.AppState.Assemblies.Assemblies.OfType<LoadedModule>().Single(a => a.FilePath.Text == AppStateDriver.FixturePath);
        var unreadable = new UnreadableModule { Module = module, Why = new BadImageFormatException("Bad namespace.") };
        var ports = new FakeShowReopenedNodesHandlerPorts();

        var showing = new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState, new ReopenedNodesLookup { Nodes = [waiting] }, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<Lookup<BrowseNode>>>.Ok([new Lookup<BrowseNode>.Unreadable { Module = unreadable }]));
        var loads = await showing;

        Assert.Empty(loads);
        var content = Assert.IsType<TabContent.Unreadable>(waiting.Reopened.Tab.Content);
        Assert.Equal(waiting.Place, content.Location);
        Assert.Equal(module.FilePath.FileName, content.Module);
        Assert.Equal("Bad namespace.", content.Why);
        Assert.Equal($"{module.FilePath.FileName} couldn't be read (Bad namespace.).", TabPlaceholders.Write(Strings.For(Locale.En), content));
        Assert.Equal(waiting.Place, waiting.Reopened.Tab.Location);
    }

    [Fact]
    public async Task HandleAsync_TabPointedElsewhereMeanwhile_LeavesItAlone()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var waiting = await WaitForGreeterAsync(d);
        var ports = new FakeShowReopenedNodesHandlerPorts();

        var showing = new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState, new ReopenedNodesLookup { Nodes = [waiting] }, CancellationToken.None);
        d.AppState.Navigation.RestoreDiagram(waiting.Reopened.Tab, new TypeDiagram(), "Other diagram");
        Assert.Single(ports.Answers).SetResult(new Outcome<List<Lookup<BrowseNode>>>.Ok([new Lookup<BrowseNode>.Found { Value = await d.FixtureAsync("Greeter") }]));
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
    }
}
