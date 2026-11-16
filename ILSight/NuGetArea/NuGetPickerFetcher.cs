using ILSight.Core;
using ILSight.ILSightAppArea;

namespace ILSight.NuGetArea;

/// <summary>The work the "open from NuGet" dialog asks for, each piece a workflow of its own: the search of what was
/// typed, and the icons and versions' details it shows.</summary>
[Edge]
public sealed class NuGetPickerFetcher
{
    private readonly WorkflowRunner _workflows;
    private readonly NuGetPicker _picker;
    private readonly SearchNuGetFeedHandler _searchFeed;
    private readonly FetchNuGetIconHandler _fetchIcon;
    private readonly ReadNuGetDetailsHandler _readDetails;

    /// <param name="workflows">Runs the work.</param>
    /// <param name="picker">The dialog.</param>
    /// <param name="searchFeed">Searches the feed for the page the dialog asks.</param>
    /// <param name="fetchIcon">Fetches the icons the dialog shows.</param>
    /// <param name="readDetails">Reads the details of the package versions the dialog shows.</param>
    internal NuGetPickerFetcher(WorkflowRunner workflows, NuGetPicker picker, SearchNuGetFeedHandler searchFeed, FetchNuGetIconHandler fetchIcon,
        ReadNuGetDetailsHandler readDetails)
    {
        _workflows = workflows;
        _picker = picker;
        _searchFeed = searchFeed;
        _fetchIcon = fetchIcon;
        _readDetails = readDetails;
    }

    /// <summary>While the dialog is open, before it draws in <paramref name="frame"/>: starts the search what was typed
    /// asks for, so that the dialog draws it searching, or asks for a frame once what is being typed will have settled;
    /// and starts fetching the icons and details the last frame showed for the first time (<see cref="StartFetching"/>).</summary>
    public void StartAskedNuGetWork(IFrameHost frame)
    {
        if (!_picker.IsOpen)
        {
            return;
        }

        var now = frame.Now;
        var search = _picker.FindSearchToStart(now);
        if (search is not null)
        {
            StartSearching(search);
        }

        StartFetching();

        if (_picker.Browser.IsSettling)
        {
            frame.RequestFrameIn(_picker.Browser.CalculateTimeToSettle(now));
        }
    }

    // Its token is cancelled too by a newer search and by closing the dialog. A method of its own, so that the frames
    // that start nothing allocate no closure.
    private void StartSearching(FeedSearch wanted)
    {
        var browser = _picker.Browser;
        var searchFeed = _searchFeed;
        var search = browser.StartSearch(wanted);
        _workflows.StartSettling(nameof(SearchNuGetFeedHandler), ct => searchFeed.HandleAsync(browser, search, ct),
            error => browser.TryFinishSearch(search, new Outcome<FeedPage>.Failed(error)), search.Work.Token);
    }

    /// <summary>Starts a workflow of its own for each icon and each version's details the dialog asked for since
    /// the last frame, so that one that fails does not stop the others, each on a token that closing the dialog
    /// cancels too. Allocates nothing while nothing is asked for.</summary>
    public void StartFetching()
    {
        var browser = _picker.Browser;
        if (!browser.HasDownloadsToStart)
        {
            return;
        }

        var closed = browser.UntilClosed;
        foreach (var icon in browser.TakeIconsToFetch())
        {
            StartFetchingIcon(icon, closed);
        }

        foreach (var details in browser.TakeDetailsToFetch())
        {
            StartReadingDetails(details, closed);
        }
    }

    // Its own method, so that the closure is made only for an icon asked for.
    private void StartFetchingIcon(PackageIconFetch icon, CancellationToken closed)
    {
        var fetchIcon = _fetchIcon;
        _workflows.Start(nameof(FetchNuGetIconHandler), ct => fetchIcon.HandleAsync(icon, ct), closed);
    }

    // Its own method, so that the closure is made only for details asked for.
    private void StartReadingDetails(VersionDetailsFetch details, CancellationToken closed)
    {
        var browser = _picker.Browser;
        var readDetails = _readDetails;
        _workflows.StartSettling(nameof(ReadNuGetDetailsHandler), ct => readDetails.HandleAsync(browser, details, ct),
            error => browser.ShowDetails(details, new Outcome<PackageVersionDetails>.Failed(error)), closed);
    }
}
