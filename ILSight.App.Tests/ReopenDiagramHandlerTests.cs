using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.DiagramArea;

namespace ILSight.App.Tests;

public sealed class ReopenDiagramHandlerTests
{
    /// <summary>A tab of <paramref name="d"/>'s, reopened to wait for a diagram of the fixture Vehicle titled
    /// "Vehicle diagram", and the read of its cards that reopening it started.</summary>
    private static async Task<SavedDiagramRead> ReopenAsync(AppStateDriver d)
    {
        var vehicle = (await TypeCards.ReadAsync(d.AppState.Assemblies, (await d.FixtureAsync("Vehicle")).Definition, CancellationToken.None)).FoundOrNull;
        Assert.NotNull(vehicle);
        var saved = new DiagramDocumentRecord
        {
            Panel = "document-1",
            Title = "Vehicle diagram",
            Types = [vehicle.Anchor],
            HomeAssemblies = null,
            View = null,
        };
        var tab = d.AppState.Navigation.OpenReopenedTab();
        d.AppState.Navigation.WaitForDiagram(tab, saved.Title);
        var reopened = SavedDocuments.WaitFor(d.AppState.Reopened, tab, saved);
        var read = Diagrams.ReopenDiagram(d.AppState.Navigation, d.AppState.Reopened, reopened, saved);
        Assert.NotNull(read);
        return read;
    }

    [Fact]
    public async Task HandleAsync_Read_AddsTheCardsAndStopsTheTabWaiting()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var read = await ReopenAsync(d);
        var ports = new FakeReopenDiagramHandlerPorts();
        var vehicle = (await TypeCards.ReadAsync(d.AppState.Assemblies, (await d.FixtureAsync("Vehicle")).Definition, CancellationToken.None)).FoundOrNull;
        Assert.NotNull(vehicle);
        Assert.True(read.Diagram.IsBusy);

        var reading = d.BeginAsync(() => new ReopenDiagramHandler(ports).HandleAsync(d.AppState.Reopened, read, CancellationToken.None));
        Assert.Single(ports.Answers).SetResult(new Outcome<List<Lookup<TypeCard>>>.Ok([new Lookup<TypeCard>.Found { Value = vehicle }]));
        await d.EndAsync(reading);

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
        var read = await ReopenAsync(d);
        var ports = new FakeReopenDiagramHandlerPorts();

        var reading = d.BeginAsync(() => new ReopenDiagramHandler(ports).HandleAsync(d.AppState.Reopened, read, CancellationToken.None));
        d.AppState.Navigation.RestoreDiagram(read.Reopened.Tab, new TypeDiagram(), "Other diagram");
        Assert.Single(ports.Answers).SetResult(new Outcome<List<Lookup<TypeCard>>>.Failed(new InvalidOperationException("Gone.")));
        await d.EndAsync(reading);

        Assert.Empty(read.Diagram.Cards);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_ReadFailed_ReportsTheDiagramNotReopened()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var read = await ReopenAsync(d);
        var ports = new FakeReopenDiagramHandlerPorts();
        var error = new InvalidOperationException("Unreadable.");

        var reading = d.BeginAsync(() => new ReopenDiagramHandler(ports).HandleAsync(d.AppState.Reopened, read, CancellationToken.None));
        Assert.Single(ports.Answers).SetResult(new Outcome<List<Lookup<TypeCard>>>.Failed(error));
        await d.EndAsync(reading);

        var problem = Assert.IsType<AppProblem.DiagramNotReopened>(Assert.Single(ports.Reported));
        Assert.Equal("Vehicle diagram", problem.Diagram);
        Assert.Same(error, problem.Error);
        Assert.Empty(read.Diagram.Cards);
    }
}
