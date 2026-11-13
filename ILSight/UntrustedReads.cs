namespace ILSight;

/// <summary>An adapter's read of the user's data through the decompiler (ICSharpCode, which
/// <c>rulelab.untrusted_libraries</c> lists: it reads what the user brought and fails on it in any way, its own bugs
/// included), translated where the adapter awaits it, as R009 has it: everything the read throws, but the cancellation
/// of the read's token, is the failure of the unit the user asked for, carrying why. A cancellation nobody asked for
/// (newer opens stopping this one, a clear) is such a failure too. The cancellation of the read's token goes on to the
/// workflow's edge, even when the read ended before it saw it.</summary>
public static class UntrustedReads
{
    /// <summary>What <paramref name="read"/> gave, or what it failed with; <paramref name="ct"/> is the token the read
    /// was given, whose cancellation is let through.</summary>
    public static async Task<Outcome<T>> CatchAsync<T>(BackgroundWork<T> read, CancellationToken ct)
    {
        try
        {
            var value = await read;
            ct.ThrowIfCancellationRequested();
            return new Outcome<T>.Ok(value);
        }
#pragma warning disable RL0902 // The read awaited is the decompiler's, reached through ILSight.Core's reading code, which the check can't follow from another project: R009's catch around an untrusted library's read.
        catch (Exception e) when (!ct.IsCancellationRequested)
#pragma warning restore RL0902
        {
            return new Outcome<T>.Failed(e);
        }
    }
}
