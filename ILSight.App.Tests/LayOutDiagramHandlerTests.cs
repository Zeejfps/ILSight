using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight.App.Tests;

public sealed class LayOutDiagramHandlerTests
{
    /// <summary>Lays out whatever it is asked to when the test says, lets the cancellation of the token it was asked
    /// with through, as the adapter does; keeps what it was asked and the problems reported.</summary>
    private sealed class FakeLayouts : LayOutDiagramHandler.IPorts
    {
        public List<TaskCompletionSource<Outcome<TypeLayout>>> Answers { get; } = [];

        public List<AppProblem> Reported { get; } = [];

        public void ReportError(AppProblem problem)
        {
            Reported.Add(problem);
        }

        public Task<Outcome<TypeLayout>> ArrangeAsync(DiagramMeasures measures, CancellationToken ct)
        {
            var answer = new TaskCompletionSource<Outcome<TypeLayout>>();
            ct.Register(() => answer.TrySetCanceled(ct));
            Answers.Add(answer);
            return answer.Task;
        }
    }

    private static DiagramMeasures MeasuresOf(params CodeReference.Entity[] types)
    {
        var cards = types
            .Select(static t => new MeasuredCard(t, 120f, 80f))
            .ToList();
        return new DiagramMeasures(cards, []);
    }

    [Fact]
    public async Task HandleAsync_NewerLayoutStarted_DropsTheOlderAndHoldsTheNewer()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var vehicle = d.Fixture("Vehicle").Definition;
        var car = d.Fixture("Car").Definition;
        var diagram = new TypeDiagram();
        var ports = new FakeLayouts();
        var handler = new LayOutDiagramHandler(ports);
        var older = Diagrams.StartLayout(diagram);
        Assert.NotNull(older);
        var olderMeasures = MeasuresOf(vehicle, car);
        var olderLaying = handler.HandleAsync(diagram, older, olderMeasures, CancellationToken.None);
        var newer = Diagrams.StartLayout(diagram);
        Assert.NotNull(newer);
        var newerMeasures = MeasuresOf(vehicle);
        var newerLaying = handler.HandleAsync(diagram, newer, newerMeasures, CancellationToken.None);

        ports.Answers[0].SetResult(new Outcome<TypeLayout>.Ok(DiagramLayouts.Arrange(olderMeasures)));
        await olderLaying;
        Assert.Null(diagram.Layout);
        var newerLayout = DiagramLayouts.Arrange(newerMeasures);
        ports.Answers[1].SetResult(new Outcome<TypeLayout>.Ok(newerLayout));
        await newerLaying;

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
        var ports = new FakeLayouts();
        var layout = Diagrams.StartLayout(diagram);
        Assert.NotNull(layout);
        var error = new InvalidOperationException("No room.");

        var laying = new LayOutDiagramHandler(ports).HandleAsync(diagram, layout, MeasuresOf(d.Fixture("Vehicle").Definition), CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(new Outcome<TypeLayout>.Failed(error));
        await laying;

        Assert.Same(error, Assert.IsType<AppProblem.DiagramNotLaidOut>(Assert.Single(ports.Reported)).Error);
        Assert.Null(diagram.Layout);
        Assert.False(diagram.IsBusy);
    }

    [Fact]
    public void StartLayout_DiagramClosed_StartsNothing()
    {
        var diagram = new TypeDiagram();
        diagram.Work.Close();

        Assert.Null(Diagrams.StartLayout(diagram));
    }
}
