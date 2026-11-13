namespace ILSight.App.Tests;

/// <summary>The frame edge's drain of the commands the draw queued.</summary>
public sealed class QueuedCommandsTests
{
    [Fact]
    public void RunAppCommands_OneThrows_ReportsItAndRunsTheRest()
    {
        var d = AppStateDriver.Opened();
        var ranAfter = false;
        d.Ui.AppCommands.Enqueue(static _ => throw new InvalidOperationException("broken"));
        d.Ui.AppCommands.Enqueue(_ => ranAfter = true);

        d.InTurn(() => QueuedCommands.RunAppCommands(d.Ui, d.AppState));

        Assert.True(ranAfter);
        Assert.Empty(d.Ui.AppCommands);
        var problem = Assert.IsType<AppProblem.Unexpected>(Assert.Single(d.AppState.Errors.Entries).Problem);
        Assert.Equal("broken", problem.Error.Message);
    }
}
