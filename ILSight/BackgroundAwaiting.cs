using System.Diagnostics;

namespace ILSight;

/// <summary>What awaiting work on the thread pool does, whether it gives something back
/// (<see cref="BackgroundWork{T}"/>) or not (<see cref="BackgroundWait"/>).</summary>
internal static class BackgroundAwaiting
{
    /// <summary>Has <paramref name="continuation"/> run once <paramref name="task"/> ends: on a later turn of the UI
    /// thread when called on it, else on the thread pool.</summary>
    public static void ContinueAfter(Task task, Action continuation)
    {
        var ui = SynchronizationContext.Current;
        if (ui is null)
        {
            task.ContinueWith(_ => continuation(), TaskScheduler.Default);
            return;
        }
        task.ContinueWith(_ => ui.Post(Continue, continuation), TaskScheduler.Default);
    }

    private static void Continue(object? continuation)
    {
        if (continuation is not Action action)
        {
            throw new UnreachableException("ContinueAfter posts Continue with the continuation it was given.");
        }

        action();
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
