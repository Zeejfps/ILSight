using System.Runtime.CompilerServices;

namespace ILSight;

/// <summary>The awaiter of <see cref="BackgroundWork{T}"/>: it continues when the work ends, as that type says,
/// and hands back the work's <see cref="Outcome{T}"/>.</summary>
public readonly struct BackgroundWorkAwaiter<T>(Task<T> task) : INotifyCompletion
{
    public bool IsCompleted => false;

    public void OnCompleted(Action continuation)
    {
        BackgroundAwaiting.ContinueAfter(task, continuation);
    }

    public Outcome<T> GetResult()
    {
        if (task.IsCompletedSuccessfully)
        {
            return new Outcome<T>.Ok(task.Result);
        }

        return new Outcome<T>.Failed(BackgroundAwaiting.GetError(task));
    }
}
