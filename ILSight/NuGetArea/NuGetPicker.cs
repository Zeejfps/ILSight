using ILSight.Core;

namespace ILSight.NuGetArea;

/// <summary>
/// The "open from NuGet" dialog as its widgets bind it: whether it is open, where its window is, the
/// query, the feed typed and whether prereleases count; and <see cref="Browser"/>, what it found and
/// fetched, which keeps its own rules.
/// </summary>
public sealed class NuGetPicker
{
    public bool IsOpen;

    /// <summary>Where the dialog's window is on the screen.</summary>
    public DialogWindow Window { get; } = new();

    public string Query = "";

    /// <summary>The URL of the service index of the feed to search, typed or picked from <see cref="NuGetBrowser.Sources"/>.</summary>
    public string FeedUrl = "";

    public bool IncludePrerelease;

    /// <summary>The feed read and searched, the packages found, and what was fetched of them.</summary>
    public required NuGetBrowser Browser { get; init; }

    /// <summary>Whether <see cref="FeedUrl"/> is a URL a feed could be at.</summary>
    public bool HasFeedUrl => NuGetSource.FromUrl(FeedUrl) is not null;

    /// <summary>Searches the feed and counts prereleases as the settings kept them.</summary>
    public void RestoreSearch(SavedNuGetSearch? saved)
    {
        if (saved is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(saved.FeedUrl))
        {
            return;
        }

        Browser.RestoreKeptSearch(saved);
        FeedUrl = saved.FeedUrl;
        IncludePrerelease = saved.IncludePrerelease;
    }

    /// <summary>Opens the dialog, its query taking focus. Until <see cref="ReadSetupAsync"/> has read the
    /// user's NuGet.Config, it says it is reading it. Closing is only clearing <see cref="IsOpen"/>, as the
    /// window's close box does: <see cref="Browser"/> stops what the dialog started once it is told so.</summary>
    public void Open()
    {
        IsOpen = true;
        Browser.FocusQuery();
    }

    /// <summary>Reads the user's NuGet.Config the first time the dialog opens; with no feed kept, its first
    /// feed is the one searched.</summary>
    public async Task ReadSetupAsync(CancellationToken ct)
    {
        if (!Browser.IsSetupUnread)
        {
            return;
        }

        await Browser.ReadSetupAsync(ct);
        SearchFirstFeedIfNone();
    }

    /// <summary>Searches and downloads with <paramref name="setup"/> from now on, in place of the one
    /// read from the user's NuGet.Config; with no feed kept, its first feed is the one searched.</summary>
    internal void UseSetup(NuGetSetup setup)
    {
        Browser.UseSetup(setup);
        SearchFirstFeedIfNone();
    }

    private void SearchFirstFeedIfNone()
    {
        if (FeedUrl.Length > 0)
        {
            return;
        }

        var sources = Browser.Sources;
        if (sources.Count == 0)
        {
            return;
        }

        FeedUrl = sources[0].Url.Text;
    }

    /// <summary>Searches <paramref name="source"/> at once.</summary>
    public void PickFeed(NuGetSource source)
    {
        FeedUrl = source.Url.Text;
        Browser.SettleFeed(source.Url.Text);
    }

    public void ClearQuery()
    {
        Query = "";
    }

    /// <summary>Takes in the query, the feed and whether prereleases count as they are at
    /// <paramref name="now"/>; returns the search to start now, or null. See <see cref="NuGetBrowser.Update"/>.</summary>
    public FeedSearch? Update(double now)
    {
        return Browser.Update(Query, FeedUrl, IncludePrerelease, now);
    }
}
