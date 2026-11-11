namespace ILSight.App.Tests;

/// <summary>Of a series of works, only the newest counts: starting one cancels the one before, a work overtaken
/// finishes to no effect, and closing cancels what is in flight and whatever is started later.</summary>
public sealed class LatestWorkTests
{
    [Fact]
    public void Start_NothingRunning_Runs()
    {
        var work = new LatestWork();

        var ticket = work.Start();

        Assert.True(work.IsRunning);
        Assert.False(ticket.Token.IsCancellationRequested);
    }

    [Fact]
    public void Start_WhileOlderRuns_CancelsTheOlder()
    {
        var work = new LatestWork();
        var older = work.Start();

        var newer = work.Start();

        Assert.True(older.Token.IsCancellationRequested);
        Assert.False(newer.Token.IsCancellationRequested);
        Assert.True(work.IsRunning);
    }

    [Fact]
    public void TryFinish_Newest_EndsTheWork()
    {
        var work = new LatestWork();
        var ticket = work.Start();

        Assert.True(work.TryFinish(ticket));
        Assert.False(work.IsRunning);
    }

    [Fact]
    public void TryFinish_StaleTicket_DoesNothing()
    {
        var work = new LatestWork();
        var older = work.Start();
        var newer = work.Start();

        Assert.False(work.TryFinish(older));
        Assert.True(work.IsRunning);
        Assert.False(newer.Token.IsCancellationRequested);
        Assert.True(work.TryFinish(newer));
    }

    [Fact]
    public void Cancel_WhileRunning_CancelsAndDropsTheResult()
    {
        var work = new LatestWork();
        var ticket = work.Start();

        work.Cancel();

        Assert.True(ticket.Token.IsCancellationRequested);
        Assert.False(work.IsRunning);
        Assert.False(work.IsClosed);
        Assert.False(work.TryFinish(ticket));
    }

    [Fact]
    public void Close_WhileRunning_CancelsAndDropsTheResult()
    {
        var work = new LatestWork();
        var ticket = work.Start();

        work.Close();

        Assert.True(work.IsClosed);
        Assert.True(work.UntilClosed.IsCancellationRequested);
        Assert.True(ticket.Token.IsCancellationRequested);
        Assert.False(work.IsRunning);
        Assert.False(work.TryFinish(ticket));
    }

    [Fact]
    public void Start_AfterClose_IsRefused()
    {
        var work = new LatestWork();
        work.Close();

        var ticket = work.Start();

        Assert.True(ticket.Token.IsCancellationRequested);
        Assert.False(work.IsRunning);
        Assert.False(work.TryFinish(ticket));
    }
}
