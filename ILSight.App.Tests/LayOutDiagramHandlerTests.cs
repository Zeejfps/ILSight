using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.DiagramArea;

namespace ILSight.App.Tests;

public sealed class LayOutDiagramHandlerTests
{
    private static DiagramMeasures MeasuresOf(params CodeReference.Entity[] types)
    {
        var cards = types
            .Select(static t => new MeasuredCard { Type = t, Width = 120f, Height = 80f })
            .ToList();
        return new DiagramMeasures { Cards = cards, Links = [] };
    }

    [Fact]
    public async Task HandleAsync_NewerLayoutStarted_DropsTheOlderAndHoldsTheNewer()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var vehicle = (await d.FixtureAsync("Vehicle")).Definition;
        var car = (await d.FixtureAsync("Car")).Definition;
        var diagram = new TypeDiagram();
        var ports = new FakeLayOutDiagramHandlerPorts();
        var handler = new LayOutDiagramHandler(ports);
        var older = diagram.StartLayout();
        Assert.NotNull(older);
        var olderMeasures = MeasuresOf(vehicle, car);
        var olderLaying = d.BeginAsync(() => handler.HandleAsync(diagram, older, olderMeasures, CancellationToken.None));
        var newer = diagram.StartLayout();
        Assert.NotNull(newer);
        var newerMeasures = MeasuresOf(vehicle);
        var newerLaying = d.BeginAsync(() => handler.HandleAsync(diagram, newer, newerMeasures, CancellationToken.None));

        ports.Answers[0].SetResult(new Outcome<TypeLayout>.Ok(DiagramLayouts.Arrange(olderMeasures)));
        await d.EndAsync(olderLaying);
        Assert.Null(diagram.Layout);
        var newerLayout = DiagramLayouts.Arrange(newerMeasures);
        ports.Answers[1].SetResult(new Outcome<TypeLayout>.Ok(newerLayout));
        await d.EndAsync(newerLaying);

        Assert.True(older.Token.IsCancellationRequested);
        Assert.Same(newerLayout, diagram.Layout);
        Assert.False(diagram.IsBusy);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_Failed_ReportsIt()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var diagram = new TypeDiagram();
        var ports = new FakeLayOutDiagramHandlerPorts();
        var layout = diagram.StartLayout();
        Assert.NotNull(layout);
        var error = new InvalidOperationException("No room.");

        var measures = MeasuresOf((await d.FixtureAsync("Vehicle")).Definition);
        var laying = d.BeginAsync(() => new LayOutDiagramHandler(ports).HandleAsync(diagram, layout, measures, CancellationToken.None));
        Assert.Single(ports.Answers).SetResult(new Outcome<TypeLayout>.Failed(error));
        await d.EndAsync(laying);

        Assert.Same(error, Assert.IsType<AppProblem.DiagramNotLaidOut>(Assert.Single(ports.Reported)).Error);
        Assert.Null(diagram.Layout);
        Assert.False(diagram.IsBusy);
    }

    [Fact]
    public void StartLayout_DiagramClosed_StartsNothing()
    {
        var diagram = new TypeDiagram();
        diagram.Close();

        Assert.Null(diagram.StartLayout());
    }
}
