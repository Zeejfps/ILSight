using ILSight.Core;
using ILSight.Core.DiagramArea;
using ILSight.DiagramArea;

namespace ILSight.App.Tests;

public sealed class DiagramGrowerTests
{
    private static async Task<CodeReference.Entity> TypeOfAsync(AppStateDriver d, string type)
    {
        var fixture = await d.FixtureAsync(type);
        return fixture.Definition;
    }

    private static async Task<TypeCard> CardOfAsync(AppStateDriver d, string type)
    {
        var entity = await TypeOfAsync(d, type);
        var card = (await TypeCards.ReadAsync(d.AppState.ListSession.Assemblies, entity, CancellationToken.None)).GetValue().FoundOrNull;
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
    public async Task ShowInNewDiagram_LookupStillRunning_IsANewIntentThatDropsItsAnswer()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var navigation = d.AppState.ListSession.Navigation;
        var lookup = navigation.BeginResolving(inNewTab: false);

        var vehicleType = await TypeOfAsync(d, "Vehicle");
        var read = Diagrams.ShowInNewDiagram(d.AppState.ListSession.Navigation, vehicleType, new TabTitle.Diagram("Vehicle"));

        Assert.Same(read.Tab, navigation.ActiveTab);
        Assert.True(navigation.IsStale(lookup.Intent));
        Assert.False(navigation.IsResolving);
    }

    [Fact]
    public async Task ShowInDiagram_LookupStillRunning_IsANewIntentThatDropsItsAnswer()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var navigation = d.AppState.ListSession.Navigation;
        var vehicleType = await TypeOfAsync(d, "Vehicle");
        var first = Diagrams.ShowInNewDiagram(d.AppState.ListSession.Navigation, vehicleType, new TabTitle.Diagram("Vehicle"));
        var lookup = navigation.BeginResolving(inNewTab: false);

        var greeterType = await TypeOfAsync(d, "Greeter");
        var read = Diagrams.ShowInDiagram(d.AppState.ListSession.Navigation, greeterType, new TabTitle.Diagram("Greeter"));

        Assert.Same(first.Tab, read.Tab);
        Assert.Same(read.Tab, navigation.ActiveTab);
        Assert.True(navigation.IsStale(lookup.Intent));
        Assert.False(navigation.IsResolving);
    }

    [Fact]
    public async Task AddWithNeighboursAsync_Read_TakesTheTypeHomeAddsTheCardsAndFocusesTheTypes()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var vehicle = await TypeOfAsync(d, "Vehicle");
        var read = Diagrams.ShowInNewDiagram(d.AppState.ListSession.Navigation, vehicle, new TabTitle.Diagram("Vehicle"));
        var viewCommands = new Queue<ViewCommand>();
        var ports = new FakeDiagramGrowerPorts();
        Assert.True(read.Diagram.IsBusy);

        var adding = d.BeginAsync(() => new DiagramGrower(ports).AddWithNeighboursAsync(viewCommands, read, CancellationToken.None));
        var request = Assert.Single(ports.Requests);
        var vehicleCard = await CardOfAsync(d, "Vehicle");
        var carCard = await CardOfAsync(d, "Car");
        request.Answer.SetResult(new Outcome<List<TypeCard>>.Ok([vehicleCard, carCard]));
        await d.EndAsync(adding);

        Assert.Equal(nameof(FakeDiagramGrowerPorts.ReadWithNeighboursAsync), request.Read);
        Assert.Equal([vehicle], request.Types);
        Assert.Equal(["Car", "Vehicle"], CardNames(read.Diagram));
        Assert.True(read.Diagram.IsHome(read.Diagram.Cards[0].AssemblyPath));
        Assert.False(read.Diagram.IsBusy);
        Assert.Equal([new ViewCommand.FocusCard { Tab = read.Tab, Type = vehicle }], viewCommands);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task AddWithNeighboursAsync_NothingRead_ReportsTheTypeUnreadable()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var greeter = await d.FixtureAsync("Greeter");
        var twice = await greeter.GetChildAsync("Twice() : int");
        var member = twice.Definition;
        var read = Diagrams.ShowInNewDiagram(d.AppState.ListSession.Navigation, member, new TabTitle.Diagram("Twice"));
        var viewCommands = new Queue<ViewCommand>();
        var ports = new FakeDiagramGrowerPorts();

        var adding = d.BeginAsync(() => new DiagramGrower(ports).AddWithNeighboursAsync(viewCommands, read, CancellationToken.None));
        Assert.Single(ports.Requests).Answer.SetResult(new Outcome<List<TypeCard>>.Ok([]));
        await d.EndAsync(adding);

        var refused = Assert.IsType<Refusal.TypeNotDiagrammed>(Assert.Single(ports.Refused));
        Assert.Equal(member, refused.Type);
        Assert.Empty(ports.Reported);
        Assert.Empty(read.Diagram.Cards);
        Assert.Empty(viewCommands);
    }

    [Fact]
    public async Task AddWithNeighboursAsync_DiagramClosedMeanwhile_DropsWhatArrives()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var vehicleType = await TypeOfAsync(d, "Vehicle");
        var read = Diagrams.ShowInNewDiagram(d.AppState.ListSession.Navigation, vehicleType, new TabTitle.Diagram("Vehicle"));
        var viewCommands = new Queue<ViewCommand>();
        var ports = new FakeDiagramGrowerPorts();

        var adding = d.BeginAsync(() => new DiagramGrower(ports).AddWithNeighboursAsync(viewCommands, read, CancellationToken.None));
        d.AppState.ListSession.Navigation.CloseTab(read.Tab);
        Assert.Single(ports.Requests).Answer.SetResult(new Outcome<List<TypeCard>>.Failed(new InvalidOperationException("Gone.")));
        await d.EndAsync(adding);

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
        var car = await CardOfAsync(d, "Car");
        diagram.AddCards([car]);
        var ports = new FakeDiagramGrowerPorts();

        var adding = d.BeginAsync(() =>
        {
            var expansion = ExpansionRead.Start(diagram, car);
            return new DiagramGrower(ports).ExpandBasesAsync(expansion, CancellationToken.None);
        });
        var request = Assert.Single(ports.Requests);
        Assert.True(diagram.IsBusy);
        var vehicleCard = await CardOfAsync(d, "Vehicle");
        request.Answer.SetResult(new Outcome<List<TypeCard>>.Ok([vehicleCard]));
        await d.EndAsync(adding);

        Assert.Equal(nameof(FakeDiagramGrowerPorts.ReadCardsAsync), request.Read);
        var vehicleType = await TypeOfAsync(d, "Vehicle");
        Assert.Equal([vehicleType], request.Types);
        Assert.Equal(["Car", "Vehicle"], CardNames(diagram));
        Assert.False(diagram.IsBusy);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task ExpandBasesAsync_NoneMissing_ReadsNothing()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var diagram = new TypeDiagram();
        var car = await CardOfAsync(d, "Car");
        var vehicleCard = await CardOfAsync(d, "Vehicle");
        diagram.AddCards([car, vehicleCard]);
        var ports = new FakeDiagramGrowerPorts();

        await d.RunAsync(() =>
        {
            var expansion = ExpansionRead.Start(diagram, car);
            return new DiagramGrower(ports).ExpandBasesAsync(expansion, CancellationToken.None);
        });

        Assert.Empty(ports.Requests);
        Assert.False(diagram.IsBusy);
    }

    [Fact]
    public async Task ExpandDerivedAsync_ReadFailed_ReportsTheType()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var diagram = new TypeDiagram();
        var vehicle = await CardOfAsync(d, "Vehicle");
        diagram.AddCards([vehicle]);
        var ports = new FakeDiagramGrowerPorts();
        var error = new InvalidOperationException("Unreadable.");

        var adding = d.BeginAsync(() =>
        {
            var expansion = ExpansionRead.Start(diagram, vehicle);
            return new DiagramGrower(ports).ExpandDerivedAsync(expansion, CancellationToken.None);
        });
        var request = Assert.Single(ports.Requests);
        request.Answer.SetResult(new Outcome<List<TypeCard>>.Failed(error));
        await d.EndAsync(adding);

        Assert.Equal(nameof(FakeDiagramGrowerPorts.ReadDerivedAsync), request.Read);
        var problem = Assert.IsType<AppProblem.TypeNotDiagrammed>(Assert.Single(ports.Reported));
        Assert.Equal(vehicle.Type, problem.Type);
        Assert.Same(error, problem.Error);
        Assert.Equal(["Vehicle"], CardNames(diagram));
        Assert.False(diagram.IsBusy);
        Assert.Same(error, diagram.ReadFailure);
    }

    [Fact]
    public async Task ExpandDerivedAsync_SearchReachedAModuleItCouldNotRead_AddsTheCardsFoundAndNamesIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var diagram = new TypeDiagram();
        var vehicle = await CardOfAsync(d, "Vehicle");
        diagram.AddCards([vehicle]);
        var module = d.AppState.ListSession.Assemblies.Assemblies.OfType<LoadedModule>().Single(a => a.FilePath.Text == AppStateDriver.FixturePath);
        var why = new BadImageFormatException("Bad namespace.");
        var ports = new FakeDiagramGrowerPorts();
        ports.SearchedUnreadable.Add(new UnreadableModule { Module = module, Why = why });

        var adding = d.BeginAsync(() =>
        {
            var expansion = ExpansionRead.Start(diagram, vehicle);
            return new DiagramGrower(ports).ExpandDerivedAsync(expansion, CancellationToken.None);
        });
        var request = Assert.Single(ports.Requests);
        var bikeCard = await CardOfAsync(d, "Bike");
        var carCard = await CardOfAsync(d, "Car");
        request.Answer.SetResult(new Outcome<List<TypeCard>>.Ok([bikeCard, carCard]));
        await d.EndAsync(adding);

        Assert.Equal(nameof(FakeDiagramGrowerPorts.ReadDerivedAsync), request.Read);
        Assert.Equal(["Bike", "Car", "Vehicle"], CardNames(diagram));
        var problem = Assert.IsType<AppProblem.ModuleUnreadable>(Assert.Single(ports.Reported));
        Assert.Equal(module.FilePath.FileName, problem.Module);
        Assert.Same(why, problem.Why);
        Assert.False(diagram.IsBusy);
    }

    [Fact]
    public void A_failed_read_shows_on_the_diagram_until_a_read_is_started_again()
    {
        var diagram = new TypeDiagram();
        var read = diagram.StartReading();
        var error = new InvalidOperationException("Unreadable.");
        diagram.FailReading(read, error);
        var failed = (diagram.IsBusy, diagram.ReadFailure);

        var again = diagram.StartReading();

        Assert.Equal((false, error), failed);
        Assert.Null(diagram.ReadFailure);
        Assert.True(diagram.IsBusy);
        diagram.FinishReading(again);
    }
}
