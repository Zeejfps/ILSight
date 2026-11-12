namespace ILSight;

/// <summary>Work running on the thread pool that gives nothing back. Awaited, it continues as
/// <see cref="BackgroundWork{T}"/> does, with its <see cref="Outcome"/>.</summary>
public readonly struct BackgroundWait(Task task)
{
    public BackgroundWaitAwaiter GetAwaiter()
    {
        return new BackgroundWaitAwaiter(task);
    }
}
