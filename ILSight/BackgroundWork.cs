using System.Runtime.CompilerServices;

namespace ILSight;

/// <summary>Work running on the thread pool. Awaited on the UI thread, it always continues on a
/// later turn of the UI thread, even when the work is already done, with its <see cref="Outcome{T}"/>.
/// Awaited where there is no UI thread, it continues on the thread pool as <c>await</c> does.</summary>
public readonly struct BackgroundWork<T>(Task<T> task)
{
    public Awaiter GetAwaiter()
    {
        return new Awaiter(task);
    }

    public readonly struct Awaiter(Task<T> task) : INotifyCompletion
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
}
