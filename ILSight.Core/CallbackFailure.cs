namespace ILSight.Core;

/// <summary>Whether the app's code a listed library calls back during one read threw (CD4): the
/// <see cref="DocumentBuilder"/> the decompiler writes into, the token writers decorating the library's, the
/// disassembler's C# above each method, the decompiler's member-selection transforms and the resolver the library asks.
/// That code is the app's, its failure a bug wherever it runs: each such member does its work through <c>Run</c>, and
/// the catch around the library's read adds <c>&amp;&amp; !failure.Failed</c> to its filter, so the app's bug goes on
/// to the edge instead of reading as the library's failure on the user's data. One read's builders share one (a
/// method's C# is written into a builder of its own), so the catch that holds them all sees it.
/// <para>Not a mark set in a finally: the runtime asks a catch's filter in its first pass over the stack, before any
/// finally on the way runs, so such a mark isn't there yet when the read's filter asks. <c>Run</c> counts the work as
/// begun before doing it and as ended only when it returns, with no catch and no finally: while what it threw is on
/// its way out, and after, the count stays up, and <see cref="Failed"/> says so. One read's code runs on one thread at
/// a time, so no other call is in progress when its filter asks. Once up, it stays up for the rest of the read.</para>
/// <para>Only the app's own work is counted (CF9): what a callback reads of the library's objects (a type's definition,
/// a member's token, a reference row's name) and what it hands back to the library's code (a <c>base</c> writer, the
/// library's own resolver) is the read's, done before <c>Run</c> or between two, so a lazy decode that throws there is
/// the data's failure, whatever the library is believed to hold (CJ9). A reference compared by identity, or an object
/// tested for its type, reads nothing of it.</para></summary>
internal sealed class CallbackFailure
{
    private int _unreturned;

    /// <summary>Whether some of the app's code the library called back began and didn't return: it threw.</summary>
    public bool Failed => Volatile.Read(ref _unreturned) > 0;

    /// <summary>Does <paramref name="work"/> on <paramref name="state"/>, counted as the app's work the library called
    /// back until it returns. A static lambda and its state allocate nothing per call.</summary>
    public void Run<TState>(TState state, Action<TState> work)
    {
        Interlocked.Increment(ref _unreturned);
        work(state);
        Interlocked.Decrement(ref _unreturned);
    }

    /// <summary>What <paramref name="work"/> makes of <paramref name="state"/>, counted as the app's work the library
    /// called back until it returns.</summary>
    public TResult Run<TState, TResult>(TState state, Func<TState, TResult> work)
    {
        Interlocked.Increment(ref _unreturned);
        var result = work(state);
        Interlocked.Decrement(ref _unreturned);
        return result;
    }
}
