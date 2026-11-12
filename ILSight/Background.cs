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
}
