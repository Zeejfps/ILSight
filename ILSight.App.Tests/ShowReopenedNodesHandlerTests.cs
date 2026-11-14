using ILSight.Core;
using ILSight.DocumentViewArea;

namespace ILSight.App.Tests;

public sealed class ShowReopenedNodesHandlerTests
{
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
        var tab = d.AppState.ListSession.Navigation.OpenReopenedTab();
        d.AppState.ListSession.Navigation.WaitForNode(tab, place, record.Title, record.Language);
        var reopened = SavedDocuments.WaitFor(d.AppState.ListSession.Reopened, tab, record);
        return new WaitingNode { Reopened = reopened, Record = record, Place = place };
    }

    [Fact]
    public async Task HandleAsync_NodeFound_PointsTheTabAtItAndReturnsTheLoad()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var waiting = await WaitForGreeterAsync(d);
        var greeter = await d.FixtureAsync("Greeter");
        var ports = new FakeShowReopenedNodesHandlerPorts();

        var showing = d.BeginAsync(() => new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState.ListSession.Reopened, d.AppState.ListSession.Navigation, d.AppState.Decompiler, d.AppState.ViewCommands, new ReopenedNodesLookup { Nodes = [waiting] }, CancellationToken.None));
        Assert.IsType<TabContent.Reloading>(waiting.Reopened.Tab.Content);
        Assert.Single(ports.Answers).SetResult([new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.Found { Value = greeter })]);
        var loads = await d.EndAsync(showing);

        Assert.Equal([waiting.Place], Assert.Single(ports.Asked));
        var load = Assert.Single(loads);
        Assert.Same(waiting.Reopened.Tab, load.Tab);
        Assert.Same(greeter, load.Node);
        Assert.Empty(d.AppState.ListSession.Reopened.Tabs);
    }

    [Fact]
    public async Task HandleAsync_OneLookupFailed_ShowsThatTabFailedSayingWhyReportedOnceAndTheOtherFound()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var waiting = await WaitForGreeterAsync(d);
        var other = await WaitForGreeterAsync(d);
        var greeter = await d.FixtureAsync("Greeter");
        var ports = new FakeShowReopenedNodesHandlerPorts();
        var error = new InvalidOperationException("Unreadable.");

        var showing = d.BeginAsync(() => new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState.ListSession.Reopened, d.AppState.ListSession.Navigation, d.AppState.Decompiler, d.AppState.ViewCommands, new ReopenedNodesLookup { Nodes = [waiting, other] }, CancellationToken.None));
        Assert.Single(ports.Answers).SetResult([new Outcome<Lookup<BrowseNode>>.Failed(error), new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.Found { Value = greeter })]);
        var loads = await d.EndAsync(showing);

        var failed = Assert.IsType<TabContent.Unreadable>(waiting.Reopened.Tab.Content);
        Assert.Equal("Unreadable.", failed.Why);
        Assert.Equal(waiting.Record.Location, failed.Location);
        Assert.Same(error, Assert.IsType<AppProblem.DecompilationFailed>(Assert.Single(ports.Reported)).Error);
        Assert.Same(other.Reopened.Tab, Assert.Single(loads).Tab);
        Assert.Empty(d.AppState.ListSession.Reopened.Tabs);
    }

    [Fact]
    public async Task HandleAsync_PlaceInAModuleThatCouldNotBeRead_ShowsTheTabSayingWhichAndWhy()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var waiting = await WaitForGreeterAsync(d);
        var module = d.AppState.ListSession.Assemblies.Assemblies.OfType<LoadedModule>().Single(a => a.FilePath.Text == AppStateDriver.FixturePath);
        var unreadable = new UnreadableModule { Module = module, Why = new BadImageFormatException("Bad namespace.") };
        var ports = new FakeShowReopenedNodesHandlerPorts();

        var showing = d.BeginAsync(() => new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState.ListSession.Reopened, d.AppState.ListSession.Navigation, d.AppState.Decompiler, d.AppState.ViewCommands, new ReopenedNodesLookup { Nodes = [waiting] }, CancellationToken.None));
        Assert.Single(ports.Answers).SetResult([new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.Unreadable { Module = unreadable })]);
        var loads = await d.EndAsync(showing);

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

        var showing = d.BeginAsync(() => new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState.ListSession.Reopened, d.AppState.ListSession.Navigation, d.AppState.Decompiler, d.AppState.ViewCommands, new ReopenedNodesLookup { Nodes = [waiting] }, CancellationToken.None));
        d.AppState.ListSession.Navigation.RestoreDiagram(waiting.Reopened.Tab, new TypeDiagram(), "Other diagram");
        Assert.Single(ports.Answers).SetResult([new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.Found { Value = await d.FixtureAsync("Greeter") })]);
        var loads = await d.EndAsync(showing);

        Assert.Empty(loads);
        Assert.IsType<TabContent.Diagram>(waiting.Reopened.Tab.Content);
    }

    [Fact]
    public async Task HandleAsync_NoneWaiting_LooksUpNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var ports = new FakeShowReopenedNodesHandlerPorts();

        var loads = await d.RunAsync(() => new ShowReopenedNodesHandler(ports).HandleAsync(d.AppState.ListSession.Reopened, d.AppState.ListSession.Navigation, d.AppState.Decompiler, d.AppState.ViewCommands, new ReopenedNodesLookup { Nodes = [] }, CancellationToken.None));

        Assert.Empty(loads);
        Assert.Empty(ports.Asked);
    }
}
