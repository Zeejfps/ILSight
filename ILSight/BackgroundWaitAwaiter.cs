using System.Runtime.CompilerServices;

namespace ILSight;

/// <summary>The awaiter of <see cref="BackgroundWait"/>: it continues when the work ends, as that type says, and
/// throws what the work ended with when it did not run to its end, as <c>await</c> does.</summary>
public readonly struct BackgroundWaitAwaiter(Task task) : INotifyCompletion
{
    public bool IsCompleted => false;

    public void OnCompleted(Action continuation)
    {
        BackgroundAwaiting.ContinueAfter(task, continuation);
    }

    public void GetResult()
    {
        var ended = task;
        if (ended.IsCompletedSuccessfully)
        {
            return;
        }

        BackgroundAwaiting.Rethrow(ended);
    }
}
