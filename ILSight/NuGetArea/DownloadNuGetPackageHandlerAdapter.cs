using ILSight.Core;

namespace ILSight.NuGetArea;

/// <summary>Fetches packages from the NuGet feeds into the package cache through <paramref name="client"/>, which
/// keeps the feeds it speaks to, and reaches the app's error reports, for <see cref="DownloadNuGetPackageHandler"/>.
/// What a download throws comes back as <see cref="Outcome{T}.Failed"/>, except the cancellation of the token it was
/// given, which it lets through; a timeout nobody asked for is a failure.</summary>
public sealed class DownloadNuGetPackageHandlerAdapter(Action<AppProblem> reportError, NuGetClient client) : DownloadNuGetPackageHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        reportError(problem);
    }

    /// <summary>Downloads and comes back on the UI thread, with a request that failed or timed out (<see cref="NetworkFailures"/>) as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public Task<Outcome<string>> DownloadPackageAsync(PackageCache cache, NuGetSource feed, PackageId id, PackageVersion version, CancellationToken ct)
    {
        return NetworkFailures.CatchAsync(Background.Wait(NuGetFeeds.GetPackageAsync(cache, client.EnsureFeed(feed), id, version, ct)), ct);
    }
}
