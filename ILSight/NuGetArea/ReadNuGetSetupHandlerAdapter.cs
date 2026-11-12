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

    /// <summary>Reads on the thread pool and comes back on the UI thread, with whatever the reading threw as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<NuGetSetup>> ReadUserSetupAsync(CancellationToken ct)
    {
        var read = await Background.Wait(Task.Run(NuGetFeeds.ReadUserSetup, ct));
        ct.ThrowIfCancellationRequested();
        return read;
    }

    public NuGetSetup CreateDefaultSetup()
    {
        return NuGetFeeds.CreateDefaultSetup();
    }
}
