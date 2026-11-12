using ILSight.Core;

namespace ILSight.NuGetArea;

/// <summary>Searches a NuGet feed for the page a search the "open from NuGet" dialog started asks
/// (<see cref="NuGetBrowser.StartSearch"/>), which the dialog shows unless another search started or it closed
/// meanwhile; a failed search the dialog still waits for is reported. Reaches the feed and the user's error reports
/// through <see cref="IPorts"/>.</summary>
public sealed class SearchNuGetFeedHandler(SearchNuGetFeedHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user a search failed.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The page <paramref name="search"/> asks of its feed; a search that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<FeedPage>> SearchAsync(FeedSearch search, CancellationToken ct);
    }

    /// <summary><paramref name="ct"/> is the work's token, linked to what else may cancel it.</summary>
    public async Task HandleAsync(NuGetBrowser browser, FeedSearchTicket search, CancellationToken ct)
    {
        var found = await ports.SearchAsync(search.Search, ct);
        if (!browser.TryFinishSearch(search, found))
        {
            return;
        }

        if (found is Outcome<FeedPage>.Failed failed)
        {
            ports.ReportError(new AppProblem.NuGetSearchFailed(search.Search.Feed.Name, failed.Error));
        }
    }
}
