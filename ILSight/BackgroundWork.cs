namespace ILSight;

/// <summary>Work running on the thread pool. Awaited on the UI thread, it always continues on a
/// later turn of the UI thread, even when the work is already done; awaited where there is no UI thread, it continues
/// on the thread pool. Either way it gives what the work gave, or throws what the work ended with (its cancellation,
/// carrying the token, or its exception), as <c>await</c> does: the adapter awaiting it translates the failures it
/// expects.</summary>
public readonly struct BackgroundWork<T>(Task<T> task)
{
    public BackgroundWorkAwaiter<T> GetAwaiter()
    {
        return new BackgroundWorkAwaiter<T>(task);
    }
}
