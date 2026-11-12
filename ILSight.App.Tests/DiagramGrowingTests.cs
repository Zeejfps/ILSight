using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.DiagramArea;

namespace ILSight.App.Tests;

public sealed class DiagramGrowingTests
{
    private static CodeReference.Entity TypeOf(AppStateDriver d, string type)
    {
        return d.Fixture(type).Definition;
    }

    private static TypeCard CardOf(AppStateDriver d, string type)
    {
        var card = TestWaits.Wait(() => TypeCards.ReadAsync(d.AppState.Assemblies, TypeOf(d, type), CancellationToken.None)).FoundOrNull;
        Assert.NotNull(card);
        return card;
    }

    private static List<string> CardNames(TypeDiagram diagram)
    {
        return diagram.Cards
            .Select(static c => c.Name)
            .Order()
            .ToList();
    }

    [Fact]
    public void ShowInNewDiagram_LookupStillRunning_IsANewIntentThatDropsItsAnswer()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var navigation = d.AppState.Navigation;
        var lookup = navigation.BeginResolving(inNewTab: false);

        var read = Diagrams.ShowInNewDiagram(d.AppState, TypeOf(d, "Vehicle"), "Vehicle diagram");

        Assert.Same(read.Tab, navigation.ActiveTab);
        Assert.True(navigation.IsStale(lookup.Intent));
        Assert.False(navigation.IsResolving);
    }

    [Fact]
    public void ShowInDiagram_LookupStillRunning_IsANewIntentThatDropsItsAnswer()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var navigation = d.AppState.Navigation;
        var first = Diagrams.ShowInNewDiagram(d.AppState, TypeOf(d, "Vehicle"), "Vehicle diagram");
        var lookup = navigation.BeginResolving(inNewTab: false);

        var read = Diagrams.ShowInDiagram(d.AppState, TypeOf(d, "Greeter"), "Greeter diagram");

        Assert.Same(first.Tab, read.Tab);
        Assert.Same(read.Tab, navigation.ActiveTab);
        Assert.True(navigation.IsStale(lookup.Intent));
        Assert.False(navigation.IsResolving);
    }

    [Fact]
    public async Task AddWithNeighboursAsync_Read_TakesTheTypeHomeAddsTheCardsAndFocusesTheTypes()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var vehicle = TypeOf(d, "Vehicle");
        var read = Diagrams.ShowInNewDiagram(d.AppState, vehicle, "Vehicle diagram");
        var viewCommands = new Queue<ViewCommand>();
        var ports = new FakeDiagramGrowingPorts();
        Assert.True(read.Diagram.IsBusy);

        var adding = new DiagramGrowing(ports).AddWithNeighboursAsync(viewCommands, read, CancellationToken.None);
        var request = Assert.Single(ports.Requests);
        request.Answer.SetResult(new Outcome<List<TypeCard>>.Ok([CardOf(d, "Vehicle"), CardOf(d, "Car")]));
        await adding;

        Assert.Equal(nameof(FakeDiagramGrowingPorts.ReadWithNeighboursAsync), request.Read);
        Assert.Equal([vehicle], request.Types);
        Assert.Equal(["Car", "Vehicle"], CardNames(read.Diagram));
        Assert.True(Diagrams.IsHome(read.Diagram, read.Diagram.Cards[0].AssemblyPath));
        Assert.False(read.Diagram.IsBusy);
        Assert.Equal([new ViewCommand.FocusCard { Tab = read.Tab, Type = vehicle }], viewCommands);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task AddWithNeighboursAsync_NothingRead_ReportsTheTypeUnreadable()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var member = d.Fixture("Greeter").Child("Twice() : int").Definition;
        var read = Diagrams.ShowInNewDiagram(d.AppState, member, "Twice diagram");
        var viewCommands = new Queue<ViewCommand>();
        var ports = new FakeDiagramGrowingPorts();

        var adding = new DiagramGrowing(ports).AddWithNeighboursAsync(viewCommands, read, CancellationToken.None);
        Assert.Single(ports.Requests).Answer.SetResult(new Outcome<List<TypeCard>>.Ok([]));
        await adding;

        var problem = Assert.IsType<AppProblem.TypeNotDiagrammed>(Assert.Single(ports.Reported));
        Assert.Equal(member, problem.Type);
        Assert.Null(problem.Error);
        Assert.Empty(read.Diagram.Cards);
        Assert.Empty(viewCommands);
    }

    [Fact]
    public async Task AddWithNeighboursAsync_DiagramClosedMeanwhile_DropsWhatArrives()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var read = Diagrams.ShowInNewDiagram(d.AppState, TypeOf(d, "Vehicle"), "Vehicle diagram");
        var viewCommands = new Queue<ViewCommand>();
        var ports = new FakeDiagramGrowingPorts();

        var adding = new DiagramGrowing(ports).AddWithNeighboursAsync(viewCommands, read, CancellationToken.None);
        d.AppState.Navigation.CloseTab(read.Tab);
        Assert.Single(ports.Requests).Answer.SetResult(new Outcome<List<TypeCard>>.Failed(new InvalidOperationException("Gone.")));
        await adding;

        Assert.True(read.Diagram.IsClosed);
        Assert.Empty(read.Diagram.Cards);
        Assert.Empty(viewCommands);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task ExpandBasesAsync_BasesMissing_ReadsThemAndAddsThem()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var diagram = new TypeDiagram();
        var car = CardOf(d, "Car");
        Diagrams.AddCards(diagram, [car]);
        var ports = new FakeDiagramGrowingPorts();

        var adding = new DiagramGrowing(ports).ExpandBasesAsync(diagram, car, CancellationToken.None);
        var request = Assert.Single(ports.Requests);
        Assert.True(diagram.IsBusy);
        request.Answer.SetResult(new Outcome<List<TypeCard>>.Ok([CardOf(d, "Vehicle")]));
        await adding;

        Assert.Equal(nameof(FakeDiagramGrowingPorts.ReadCardsAsync), request.Read);
        Assert.Equal([TypeOf(d, "Vehicle")], request.Types);
        Assert.Equal(["Car", "Vehicle"], CardNames(diagram));
        Assert.False(diagram.IsBusy);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task ExpandBasesAsync_NoneMissing_ReadsNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var diagram = new TypeDiagram();
        var car = CardOf(d, "Car");
        Diagrams.AddCards(diagram, [car, CardOf(d, "Vehicle")]);
        var ports = new FakeDiagramGrowingPorts();

        await new DiagramGrowing(ports).ExpandBasesAsync(diagram, car, CancellationToken.None);

        Assert.Empty(ports.Requests);
        Assert.False(diagram.IsBusy);
    }

    [Fact]
    public async Task ExpandDerivedAsync_ReadFailed_ReportsTheType()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var diagram = new TypeDiagram();
        var vehicle = CardOf(d, "Vehicle");
        Diagrams.AddCards(diagram, [vehicle]);
        var ports = new FakeDiagramGrowingPorts();
        var error = new InvalidOperationException("Unreadable.");

        var adding = new DiagramGrowing(ports).ExpandDerivedAsync(diagram, vehicle, CancellationToken.None);
        var request = Assert.Single(ports.Requests);
        request.Answer.SetResult(new Outcome<List<TypeCard>>.Failed(error));
        await adding;

        Assert.Equal(nameof(FakeDiagramGrowingPorts.ReadDerivedAsync), request.Read);
        var problem = Assert.IsType<AppProblem.TypeNotDiagrammed>(Assert.Single(ports.Reported));
        Assert.Equal(vehicle.Type, problem.Type);
        Assert.Same(error, problem.Error);
        Assert.Equal(["Vehicle"], CardNames(diagram));
        Assert.False(diagram.IsBusy);
    }
}
