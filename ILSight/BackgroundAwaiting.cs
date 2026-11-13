namespace ILSight;

/// <summary>What awaiting work on the thread pool does, whether it gives something back
/// (<see cref="BackgroundWork{T}"/>) or not (<see cref="BackgroundWait"/>).</summary>
internal static class BackgroundAwaiting
{
    /// <summary>Has <paramref name="continuation"/> run once <paramref name="task"/> ends: on a later turn of the UI
    /// thread when called on it, else on the thread pool. The task's own awaiter posts it to the context it finds,
    /// as <c>await</c> does; nothing else is started, so no task is left unobserved.</summary>
    public static void ContinueAfter(Task task, Action continuation)
    {
        task.GetAwaiter().OnCompleted(continuation);
    }

    /// <summary>What <paramref name="task"/>, which did not run to its end, failed with: the cancellation when it
    /// was cancelled.</summary>
    public static Exception GetError(Task task)
    {
        if (task.Exception is null)
        {
            return new OperationCanceledException();
        }

        return task.Exception.GetBaseException();
    }
}
