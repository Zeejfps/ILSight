using ILSight.Core;

namespace ILSight.Tests;

public sealed class GuardedTests
{
    [Fact]
    public void A_change_made_holding_it_stays()
    {
        var guarded = new Guarded<int>(1);

        using (var held = guarded.Enter())
            held.Value = 2;

        using var again = guarded.Enter();
        Assert.Equal(2, again.Value);
    }

    [Fact]
    public void A_scope_made_by_default_reaches_nothing_and_lets_go_of_nothing()
    {
        Assert.Throws<InvalidOperationException>(static () =>
        {
            var held = default(Guarded<int>.Scope);
            return held.Value;
        });

        var nothing = default(Guarded<int>.Scope);
        nothing.Dispose();
    }

    [Fact]
    public void Letting_go_twice_lets_go_once()
    {
        var guarded = new Guarded<int>(0);
        using var outer = guarded.Enter();

        var inner = guarded.Enter();
        inner.Dispose();
        inner.Dispose();

        Assert.False(IsFreeFromAnotherThread(guarded));
    }

    [Fact]
    public void A_scope_let_go_of_reaches_nothing()
    {
        var guarded = new Guarded<int>(0);

        Assert.Throws<InvalidOperationException>(() =>
        {
            var held = guarded.Enter();
            held.Dispose();
            return held.Value;
        });
    }

    [Fact]
    public void The_thread_holding_it_can_enter_again()
    {
        var guarded = new Guarded<int>(1);

        using var outer = guarded.Enter();
        using var inner = guarded.Enter();

        Assert.Equal(1, inner.Value);
    }

    [Fact]
    public void Another_thread_waits_while_it_is_held_and_gets_it_after()
    {
        var guarded = new Guarded<int>(0);

        using (guarded.Enter())
            Assert.False(IsFreeFromAnotherThread(guarded));

        Assert.True(IsFreeFromAnotherThread(guarded));
    }

    [Fact]
    public void An_exception_inside_lets_it_go()
    {
        var guarded = new Guarded<int>(0);

        void Fail()
        {
            using var held = guarded.Enter();
            throw new ArithmeticException();
        }

        Assert.Throws<ArithmeticException>(Fail);
        Assert.True(IsFreeFromAnotherThread(guarded));
    }

    [Fact]
    public void Threads_changing_it_at_once_lose_no_change()
    {
        const int threads = 8;
        const int rounds = 20_000;
        var guarded = new Guarded<int>(0);

        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
        {
            for (var round = 0; round < rounds; round++)
            {
                using var held = guarded.Enter();
                held.Value++;
            }
        });

        using var total = guarded.Enter();
        Assert.Equal(threads * rounds, total.Value);
    }

    /// <summary>Whether a thread other than this one gets in within a moment.</summary>
    private static bool IsFreeFromAnotherThread(Guarded<int> guarded)
    {
        var entered = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            using var held = guarded.Enter();
            entered.Set();
        }) { IsBackground = true };
        thread.Start();
        return entered.Wait(TimeSpan.FromMilliseconds(200));
    }
}
