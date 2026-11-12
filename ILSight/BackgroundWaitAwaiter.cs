using System.Runtime.CompilerServices;

namespace ILSight;

/// <summary>The awaiter of <see cref="BackgroundWait"/>: it continues when the work ends, as that type says,
/// and hands back the work's <see cref="Outcome"/>.</summary>
public readonly struct BackgroundWaitAwaiter(Task task) : INotifyCompletion
{
    public bool IsCompleted => false;

    public void OnCompleted(Action continuation)
    {
        BackgroundAwaiting.ContinueAfter(task, continuation);
    }

    public Outcome GetResult()
    {
        if (task.IsCompletedSuccessfully)
        {
            return new Outcome.Done();
        }

        return new Outcome.Failed(BackgroundAwaiting.GetError(task));
    }
}
