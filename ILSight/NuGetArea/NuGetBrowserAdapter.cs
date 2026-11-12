using ILSight.Core;
using ILSight.Core.Resources;

namespace ILSight.NuGetArea;

/// <summary>Reaches the NuGet feeds through <paramref name="client"/>, which keeps the feeds and icons it speaks
/// to, the package cache and the user's NuGet.Config off the UI thread, and the app's error reports, for
/// <see cref="NuGetBrowser"/>. What a call throws comes back as <see cref="Outcome{T}.Failed"/>, except the
/// cancellation of the token the call was given, which it lets through; a timeout nobody asked for is a
/// failure.</summary>
public sealed class NuGetBrowserAdapter(Action<AppProblem> reportError, NuGetClient client) : NuGetBrowser.IPorts
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

    /// <summary>Searches and comes back on the UI thread, with whatever the search threw as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<FeedPage>> SearchAsync(FeedSearch search, CancellationToken ct)
    {
        var found = await Background.Wait(NuGetFeeds.SearchAsync(client.EnsureFeed(search.Feed), search.Query, search.IncludePrerelease, search.Page, ct));
        ct.ThrowIfCancellationRequested();
        return found;
    }

    /// <inheritdoc cref="SearchAsync"/>
    public async Task<Outcome<PackageVersionDetails>> ReadDetailsAsync(NuGetSource feed, PackageId id, FeedVersion version, CancellationToken ct)
    {
        var read = await Background.Wait(NuGetFeeds.ReadDetailsAsync(client.EnsureFeed(feed), id, version, ct));
        ct.ThrowIfCancellationRequested();
        return read;
    }

    /// <inheritdoc cref="SearchAsync"/>
    public async Task<Outcome<ResourceImage>> DownloadIconAsync(WebUrl url, int side, CancellationToken ct)
    {
        var icon = await Background.Wait(NuGetFeeds.GetIconAsync(client.Icons, url, side, ct));
        ct.ThrowIfCancellationRequested();
        return icon;
    }

    /// <inheritdoc cref="SearchAsync"/>
    public async Task<Outcome<string>> DownloadPackageAsync(PackageCache cache, NuGetSource feed, PackageId id, PackageVersion version, CancellationToken ct)
    {
        var fetched = await Background.Wait(NuGetFeeds.GetPackageAsync(cache, client.EnsureFeed(feed), id, version, ct));
        ct.ThrowIfCancellationRequested();
        return fetched;
    }
}
