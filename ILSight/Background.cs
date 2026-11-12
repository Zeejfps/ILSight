using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>Runs work on the thread pool for the UI thread to await.</summary>
public static class Background
{
    public static BackgroundWork<T> Run<T>(Func<T> work)
    {
        return new BackgroundWork<T>(Task.Run(work));
    }

    /// <summary>Waits for <paramref name="task"/> as <see cref="BackgroundWork{T}"/> does.</summary>
    public static BackgroundWork<T> Wait<T>(Task<T> task)
    {
        return new BackgroundWork<T>(task);
    }

    /// <summary>Waits for <paramref name="task"/>, which gives nothing back, as <see cref="BackgroundWait"/> does.</summary>
    public static BackgroundWait Wait(Task task)
    {
        return new BackgroundWait(task);
    }

    /// <summary>Lets <paramref name="workflow"/>, started on the UI thread, run to its end, counted in
    /// <paramref name="work"/> as <paramref name="name"/> until then, so a harness waiting for the UI to be ready
    /// waits for it. An exception it ends with is reported as <see cref="AppProblem.Unexpected"/> rather than
    /// ending the process, so the work counted always ends without one.</summary>
    public static void Start(BackgroundWork work, string name, Task workflow, Action<AppProblem> reportError)
    {
        work.Track(name, ReportAsync(workflow, reportError));
    }

    /// <summary>As <see cref="Start"/>, for a workflow that runs as long as the app does, such as watching a
    /// folder, which nothing waiting for the UI to be ready could wait out, so it is not counted.</summary>
    public static void StartForever(Task workflow, Action<AppProblem> reportError)
    {
        _ = ReportAsync(workflow, reportError);
    }

    private static async Task ReportAsync(Task workflow, Action<AppProblem> reportError)
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
