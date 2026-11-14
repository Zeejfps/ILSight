namespace ILSight.Tests;

/// <summary>The hang guard of a test's wait for work elsewhere: a wait by condition is never left without a bound, so
/// a bug that never sends what the test waits for fails that test by name and lets the rest of the run go on.</summary>
internal static class Waits
{
    /// <summary>The most real time a test waits for work elsewhere to reach what it waits for, sized for the full run
    /// on a busy machine: past it, it never came.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    /// <summary>Waits for <paramref name="work"/> to end, failing with a <see cref="TimeoutException"/> past
    /// <see cref="Patience"/>.</summary>
    public static Task WithinPatienceAsync(this Task work)
    {
        return work.WaitAsync(Patience);
    }

    /// <summary>As <see cref="WithinPatienceAsync(Task)"/>, answering what the work came to.</summary>
    public static Task<T> WithinPatienceAsync<T>(this Task<T> work)
    {
        return work.WaitAsync(Patience);
    }
}
