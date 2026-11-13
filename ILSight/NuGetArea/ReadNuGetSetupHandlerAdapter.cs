using ILSight.Core;

namespace ILSight.NuGetArea;

/// <summary>Reads the user's NuGet.Config off the UI thread, and reaches the app's error reports, for
/// <see cref="ReadNuGetSetupHandler"/>. What the read throws comes back as <see cref="Outcome{T}.Failed"/>, except the
/// cancellation of the token it was given, which it lets through.</summary>
public sealed class ReadNuGetSetupHandlerAdapter(Action<AppProblem> reportError) : ReadNuGetSetupHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        reportError(problem);
    }

    /// <summary>Reads on the thread pool and comes back on the UI thread, with a file it could not read (<see cref="FileReadFailures"/>) as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public Task<Outcome<NuGetConfigRead>> ReadUserConfigAsync(CancellationToken ct)
    {
        return FileReadFailures.CatchAsync(Background.Wait(Task.Run(NuGetFeeds.ReadUserConfig, ct)), ct);
    }

    public NuGetSetup CreateDefaultSetup()
    {
        return NuGetFeeds.CreateDefaultSetup();
    }

    public NuGetSetup CreateSetup(NuGetConfig config)
    {
        return NuGetFeeds.CreateSetup(config);
    }
}
