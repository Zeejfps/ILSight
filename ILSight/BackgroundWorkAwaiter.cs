using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILSight;

/// <summary>The awaiter of <see cref="BackgroundWork{T}"/>: it continues when the work ends, as that type says, and
/// hands back what the work gave, or throws what it ended with, as <c>await</c> does.</summary>
public readonly struct BackgroundWorkAwaiter<T>(Task<T> task) : INotifyCompletion
{
    public bool IsCompleted => false;

    public void OnCompleted(Action continuation)
    {
        BackgroundAwaiting.ContinueAfter(task, continuation);
    }

    public T GetResult()
    {
        var ended = task;
        if (ended.IsCompletedSuccessfully)
        {
            return ended.Result;
        }

        BackgroundAwaiting.Rethrow(ended);
        throw new UnreachableException("A task that did not run to its end was cancelled or failed.");
    }
}
