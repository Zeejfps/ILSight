using System.Runtime.CompilerServices;

namespace ILSight;

/// <summary>Work running on the thread pool that gives nothing back. Awaited, it continues as
/// <see cref="BackgroundWork{T}"/> does, with its <see cref="Outcome"/>.</summary>
public readonly struct BackgroundWait(Task task)
{
    public Awaiter GetAwaiter()
    {
        return new Awaiter(task);
    }

    public readonly struct Awaiter(Task task) : INotifyCompletion
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
}
