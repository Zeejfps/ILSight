using System.Diagnostics;

namespace ILSight.App.Tests;

internal static class Poll
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>Takes <paramref name="step"/> until <paramref name="done"/>, at least once.</summary>
    public static void Until(Func<bool> done, Action step)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            step();
            if (done())
            {
                return;
            }

            if (clock.Elapsed > Timeout)
            {
                throw new TimeoutException("It did not get there in time.");
            }

            Thread.Sleep(1);
        }
    }
}
