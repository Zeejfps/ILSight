using ILSight.Core;

namespace ILSight.Tests;

public sealed class AsyncGuardedTests
{
    [Fact]
    public async Task A_change_made_holding_it_stays()
    {
        var guarded = new AsyncGuarded<int>(1);

        using (var held = await guarded.EnterAsync())
        {
            held.Value = 2;
        }

        using var again = await guarded.EnterAsync();
        Assert.Equal(2, again.Value);
    }

    [Fact]
    public async Task Another_caller_waits_while_it_is_held_and_gets_it_after()
    {
        var guarded = new AsyncGuarded<int>(0);

        var held = await guarded.EnterAsync();
        var waiting = guarded.EnterAsync();
        Assert.False(waiting.IsCompleted);

        held.Dispose();
        using var next = await waiting;
        Assert.Equal(0, next.Value);
    }

    [Fact]
    public async Task It_can_be_held_across_an_await_on_another_thread()
    {
        var guarded = new AsyncGuarded<int>(0);

        using var held = await guarded.EnterAsync();
        await Task.Yield();
        await Task.Delay(1);
        held.Value = 1;

        Assert.Equal(1, held.Value);
    }

    [Fact]
    public async Task A_wait_cancelled_holds_nothing_and_takes_nothing_from_the_one_holding_it()
    {
        var guarded = new AsyncGuarded<int>(0);
        using var cancellation = new CancellationTokenSource();
        var held = await guarded.EnterAsync();

        var waiting = guarded.EnterAsync(cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.False(guarded.EnterAsync(new CancellationToken(canceled: true)).IsCompletedSuccessfully);
        held.Dispose();
        using var next = await guarded.EnterAsync();
        Assert.Equal(0, next.Value);
    }

    [Fact]
    public async Task Letting_go_twice_lets_go_once_and_reaches_nothing_after()
    {
        var guarded = new AsyncGuarded<int>(0);

        var held = await guarded.EnterAsync();
        held.Dispose();
        held.Dispose();

        Assert.Throws<InvalidOperationException>(() => held.Value);
        using var next = await guarded.EnterAsync();
        Assert.False(guarded.EnterAsync().IsCompleted);
    }

    [Fact]
    public async Task Callers_changing_it_at_once_across_awaits_lose_no_change()
    {
        const int callers = 16;
        const int rounds = 200;
        var guarded = new AsyncGuarded<int>(0);

        await Task.WhenAll(Enumerable.Range(0, callers).Select(_ => Task.Run(async () =>
        {
            for (var round = 0; round < rounds; round++)
            {
                using var held = await guarded.EnterAsync();
                var seen = held.Value;
                await Task.Yield();
                held.Value = seen + 1;
            }
        })));

        using var total = await guarded.EnterAsync();
        Assert.Equal(callers * rounds, total.Value);
    }
}
