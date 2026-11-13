using System.Runtime.ExceptionServices;

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

    /// <summary>Throws what <paramref name="ended"/>, which ended without running to its end, ended with, as
    /// <c>await</c> does: its cancellation, carrying the token that cancelled it, or the exception it failed with.</summary>
    public static void Rethrow(Task ended)
    {
        if (ended.IsCanceled)
        {
            throw new TaskCanceledException(ended);
        }

        var error = ended.Exception ?? throw new InvalidOperationException("The task ran to its end; it has nothing to throw.");
        ExceptionDispatchInfo.Throw(error.InnerExceptions[0]);
    }
}
