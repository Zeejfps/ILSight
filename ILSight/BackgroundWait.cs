namespace ILSight;

/// <summary>Work running on the thread pool that gives nothing back. Awaited, it continues as
/// <see cref="BackgroundWork{T}"/> does, and throws what the work ended with as it does.</summary>
public readonly struct BackgroundWait(Task task)
{
    public BackgroundWaitAwaiter GetAwaiter()
    {
        return new BackgroundWaitAwaiter(task);
    }
}
