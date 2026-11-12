using ILSight.Core.Diagrams;
using ILSight.Diagramming;

namespace ILSight.App.Tests;

public sealed class ReopenDiagramHandlerTests
{
    /// <summary>A tab of <paramref name="d"/>'s, reopened to wait for a diagram of the fixture Vehicle titled
    /// "Vehicle diagram", and the read of its cards that reopening it started.</summary>
    private static SavedDiagramRead Reopen(AppStateDriver d)
    {
        var vehicle = TypeCards.Read(d.AppState.Assemblies, d.Fixture("Vehicle").Definition);
        Assert.NotNull(vehicle);
        var saved = new DiagramDocumentRecord("document-1", "Vehicle diagram", [vehicle.Anchor], null, null);
        var tab = d.AppState.Navigation.OpenReopenedTab();
        Tabs.WaitForDiagram(tab, saved.Title);
        var reopened = SavedDocuments.WaitFor(d.AppState.Reopened, tab, saved);
        var read = Diagrams.ReopenDiagram(d.AppState.Navigation, d.AppState.Reopened, reopened, saved);
        Assert.NotNull(read);
        return read;
    }

    [Fact]
    public async Task HandleAsync_Read_AddsTheCardsAndStopsTheTabWaiting()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var read = Reopen(d);
        var ports = new FakeReopenDiagramHandlerPorts();
        var vehicle = TypeCards.Read(d.AppState.Assemblies, d.Fixture("Vehicle").Definition);
        Assert.NotNull(vehicle);
        Assert.True(read.Diagram.IsBusy);

        var reading = new ReopenDiagramHandler(ports).HandleAsync(d.AppState.Reopened, read, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<TypeCard>>.Ok([vehicle]));
        await reading;

        Assert.Equal([vehicle.Anchor], Assert.Single(ports.Asked));
        Assert.Equal(["Vehicle"], read.Diagram.Cards.Select(static c => c.Name));
        Assert.False(read.Diagram.IsBusy);
        Assert.Empty(d.AppState.Reopened.Tabs);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_TabPointedElsewhereMeanwhile_DropsWhatArrives()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var read = Reopen(d);
        var ports = new FakeReopenDiagramHandlerPorts();

        var reading = new ReopenDiagramHandler(ports).HandleAsync(d.AppState.Reopened, read, CancellationToken.None);
        Tabs.Clear(read.Reopened.Tab);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<TypeCard>>.Failed(new InvalidOperationException("Gone.")));
        await reading;

        Assert.Empty(read.Diagram.Cards);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_ReadFailed_ReportsTheDiagramNotReopened()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var read = Reopen(d);
        var ports = new FakeReopenDiagramHandlerPorts();
        var error = new InvalidOperationException("Unreadable.");

        var reading = new ReopenDiagramHandler(ports).HandleAsync(d.AppState.Reopened, read, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<TypeCard>>.Failed(error));
        await reading;

        var problem = Assert.IsType<AppProblem.DiagramNotReopened>(Assert.Single(ports.Reported));
        Assert.Equal("Vehicle diagram", problem.Diagram);
        Assert.Same(error, problem.Error);
        Assert.Empty(read.Diagram.Cards);
    }
}
