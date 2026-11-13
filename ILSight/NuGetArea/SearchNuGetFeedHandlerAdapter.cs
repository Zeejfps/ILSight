using ILSight.Core;

namespace ILSight.NuGetArea;

/// <summary>Searches the NuGet feeds through <paramref name="client"/>, which keeps the feeds it speaks to, and
/// reaches the app's error reports, for <see cref="SearchNuGetFeedHandler"/>. What a search throws comes back as
/// <see cref="Outcome{T}.Failed"/>, except the cancellation of the token it was given, which it lets through; a
/// timeout nobody asked for is a failure.</summary>
public sealed class SearchNuGetFeedHandlerAdapter(Action<AppProblem> reportError, NuGetClient client) : SearchNuGetFeedHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        reportError(problem);
    }

    /// <summary>Searches and comes back on the UI thread, with a request that failed or timed out (<see cref="NetworkFailures"/>) as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public Task<Outcome<FeedPage>> SearchAsync(FeedSearch search, CancellationToken ct)
    {
        return NetworkFailures.CatchAsync(Background.Wait(NuGetFeeds.SearchAsync(client.EnsureFeed(search.Feed), search.Query, search.IncludePrerelease, search.Page, ct)), ct);
    }
}
