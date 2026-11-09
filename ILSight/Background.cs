using System.Runtime.CompilerServices;
using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>What work run in the background gave: its value, or the error it failed with.</summary>
public abstract record Outcome<T>
{
    private Outcome()
    {
    }

    public sealed record Ok(T Value) : Outcome<T>;

    public sealed record Failed(Exception Error) : Outcome<T>;
}

/// <summary>Runs work on the thread pool for the UI thread to await.</summary>
public static class Background
{
    public static BackgroundWork<T> Run<T>(Func<T> work) => new(Task.Run(work));

    /// <summary>Waits for <paramref name="task"/> as <see cref="BackgroundWork{T}"/> does.</summary>
    public static BackgroundWork<T> Wait<T>(Task<T> task) => new(task);

    /// <summary>Lets <paramref name="workflow"/>, started on the UI thread, run to its end, counted in
    /// <paramref name="work"/> as <paramref name="name"/> until then, so a harness waiting for the UI to be ready
    /// waits for it. An exception it ends with is reported as <see cref="AppProblem.Unexpected"/> rather than
    /// ending the process, so the work counted always ends without one.</summary>
    public static void Start(BackgroundWork work, string name, Task workflow, Action<AppProblem> reportError)
    {
        work.Track(name, Report(workflow, reportError));
    }

    /// <summary>As <see cref="Start"/>, for a workflow that runs as long as the app does, such as watching a
    /// folder, which nothing waiting for the UI to be ready could wait out, so it is not counted.</summary>
    public static void StartForever(Task workflow, Action<AppProblem> reportError)
    {
        _ = Report(workflow, reportError);
    }

    private static async Task Report(Task workflow, Action<AppProblem> reportError)
    {
        try
        {
            await workflow;
        }
        catch (Exception e)
        {
            reportError(new AppProblem.Unexpected(e));
        }
    }
}

/// <summary>Work running on the thread pool. Awaited on the UI thread, it always continues on a
/// later turn of the UI thread, even when the work is already done, with its <see cref="Outcome{T}"/>.
/// Awaited where there is no UI thread, it continues on the thread pool as <c>await</c> does.</summary>
public readonly struct BackgroundWork<T>(Task<T> task)
{
    public Awaiter GetAwaiter() => new(task);

    public readonly struct Awaiter(Task<T> task) : INotifyCompletion
    {
        public bool IsCompleted => false;

        public void OnCompleted(Action continuation)
        {
            var ui = SynchronizationContext.Current;
            if (ui == null)
            {
                task.ContinueWith(_ => continuation(), TaskScheduler.Default);
                return;
            }
            task.ContinueWith(_ => ui.Post(Continue, continuation), TaskScheduler.Default);
        }

        private static void Continue(object? continuation)
        {
            var action = (Action)continuation!;
            action();
        }

        public Outcome<T> GetResult()
        {
            if (task.IsCompletedSuccessfully)
                return new Outcome<T>.Ok(task.Result);
            return new Outcome<T>.Failed(FindError());
        }

        private Exception FindError()
        {
            if (task.Exception == null)
                return new OperationCanceledException();
            return task.Exception.GetBaseException();
        }
    }
}
