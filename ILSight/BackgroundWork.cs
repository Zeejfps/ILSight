namespace ILSight;

/// <summary>Work running on the thread pool. Awaited on the UI thread, it always continues on a
/// later turn of the UI thread, even when the work is already done, with its <see cref="Outcome{T}"/>.
/// Awaited where there is no UI thread, it continues on the thread pool as <c>await</c> does.</summary>
public readonly struct BackgroundWork<T>(Task<T> task)
{
    public BackgroundWorkAwaiter<T> GetAwaiter()
    {
        return new BackgroundWorkAwaiter<T>(task);
    }
}
