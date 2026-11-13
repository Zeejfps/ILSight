using ILSight.Core;

namespace ILSight.NuGetArea;

/// <summary>
/// What the "open from NuGet" dialog found and fetched, and the rules it keeps: the user's NuGet.Config,
/// read the first time it opens; the feed searched a page at a time once what was typed settles, the
/// newest search the only one whose page is shown; the package selected and the version of it picked;
/// the icons and version details fetched the first time they are shown; and that version, fetched into
/// the package cache for the dialog to open. The use cases that reach the feeds and the disk for it
/// (<see cref="ReadNuGetSetupHandler"/>, <see cref="SearchNuGetFeedHandler"/>, <see cref="DownloadNuGetPackageHandler"/>,
/// <see cref="FetchNuGetIconHandler"/>, <see cref="ReadNuGetDetailsHandler"/>) hand it what they found.
/// </summary>
public sealed class NuGetBrowser
{
    private const double DebounceSeconds = 0.3;
    private const string IconImagePrefix = "nuget-icon-";

    private readonly Dictionary<WebUrl, PackageIcon> _icons = [];
    private readonly Dictionary<VersionDetailsKey, VersionDetails> _details = [];
    private readonly List<PackageIconFetch> _iconsToFetch = [];
    private readonly List<VersionDetailsFetch> _detailsToFetch = [];
    private readonly LatestWork _searches = new();
    private readonly LatestWork _downloads = new();
    private readonly TypedText _typedQuery = new();
    private readonly TypedText _typedUrl = new();
    private CancellationTokenSource _fetches = new();
    private int _iconCount;
    private SetupState _setup = new SetupState.Unread();
    private SearchRun _run = new SearchRun.None();
    private ShownPage? _shown;
    private DownloadState _download = new DownloadState.None();
    private bool _focusQuery;
    private bool _stopped = true;

    /// <summary>The versions of the selected package and the one picked; null while none is selected.</summary>
    public VersionChoice? Choice { get; private set; }

    /// <summary>The version of the selected package to open; null while none is selected.</summary>
    public PackageVersion? Version => Choice?.Picked;

    public ItemSelection<FeedPackage> Selection { get; } = new();

    public FeedPackage? Selected => Selection.Item;

    /// <summary>The feed last searched and whether prereleases counted, for the settings to keep.</summary>
    public SavedNuGetSearch? KeptSearch { get; private set; }

    /// <summary>How many times the feed searched or whether prereleases count changed, for the
    /// settings to keep <see cref="KeptSearch"/> once it did.</summary>
    public int SearchChanges { get; private set; }

    /// <summary>The page asked for, from 0: the one shown, or the one loading in its place.</summary>
    public int RequestedPage { get; private set; }

    /// <summary>Whether what was typed changed and waits to settle before it is searched.</summary>
    public bool IsSettling { get; private set; }

    /// <summary>The packages on the page shown.</summary>
    public IReadOnlyList<FeedPackage> Results
    {
        get
        {
            if (_shown is null)
            {
                return [];
            }

            return _shown.Found.Packages;
        }
    }

    /// <summary>Where each part of what the feed sent for the page shown that couldn't be read was, left out of it.</summary>
    public IReadOnlyList<string> LeftOut
    {
        get
        {
            if (_shown is null)
            {
                return [];
            }

            return _shown.Found.LeftOut;
        }
    }

    /// <summary>The page shown, from 0.</summary>
    public int Page
    {
        get
        {
            if (_shown is null)
            {
                return 0;
            }

            return _shown.Search.Page;
        }
    }

    /// <summary>Whether another page of the search shown is loading in its place.</summary>
    public bool IsLoadingPage => IsSearching && RequestedPage != Page;

    /// <summary>How many pages the search shown has, at least one.</summary>
    public int PageCount => NuGetFeeds.CountPages(TotalHits);

    /// <summary>How many packages the search shown matched in all.</summary>
    public int TotalHits
    {
        get
        {
            if (_shown is null)
            {
                return 0;
            }

            return _shown.Found.TotalHits;
        }
    }

    /// <summary>How many of <see cref="TotalHits"/> the pages reach.</summary>
    public int BrowsableHits => Math.Min(TotalHits, NuGetFeed.MaxSkip + NuGetFeed.PageSize);

    public bool HasPreviousPage => Page > 0;

    public bool HasNextPage => Page + 1 < PageCount;

    /// <summary>Why the user's NuGet.Config couldn't be read, nuget.org standing in for its sources; null while it was
    /// read, there is none, or it isn't read yet.</summary>
    public string? ConfigUnreadable => FindReadySetup()?.ConfigUnreadable;

    /// <summary>Whether the user's NuGet.Config was read and lists no source: nothing is searched until a feed is
    /// typed.</summary>
    public bool HasNoSources => _setup is SetupState.Ready ready && ready.Setup.Sources.Count == 0;

    /// <summary>The feeds the user's NuGet.Config lists, once the setup is read.</summary>
    public IReadOnlyList<NuGetSource> Sources
    {
        get
        {
            var setup = FindReadySetup();
            if (setup is null)
            {
                return [];
            }

            return setup.Sources;
        }
    }

    public bool IsReadingSetup => _setup is not SetupState.Ready;

    /// <summary>Whether the user's NuGet.Config is still to be read: it is neither read nor being read.</summary>
    public bool IsSetupUnread => _setup is SetupState.Unread;

    /// <summary>The query the packages shown were found by.</summary>
    public string ShownQuery
    {
        get
        {
            if (_shown is null)
            {
                return "";
            }

            return _shown.Search.Query;
        }
    }

    public bool IsSearching => _run is SearchRun.Running;

    /// <summary>Why the last search failed, or null.</summary>
    public string? SearchError
    {
        get
        {
            if (_run is not SearchRun.Failed failed)
            {
                return null;
            }

            return failed.Error;
        }
    }

    public bool IsDownloading => _download is DownloadState.Running;

    /// <summary>The package and version being downloaded; empty while none is.</summary>
    public string DownloadingName
    {
        get
        {
            if (_download is not DownloadState.Running running)
            {
                return "";
            }

            return running.Name;
        }
    }

    /// <summary>Whether the packages shown are what the feed, query and prerelease choice now ask for:
    /// not while a changed query settles or another search runs. Another page loading over them, or
    /// failing to, leaves them so.</summary>
    public bool ShowsWhatWasAsked
    {
        get
        {
            if (IsSettling)
            {
                return false;
            }

            var searched = FindLastSearched();
            if (searched is null)
            {
                return true;
            }

            return IsSamePackages(searched, FindShownSearch());
        }
    }

    /// <summary>Whether the selected package can be opened: it is one of what was asked for, a version
    /// is picked, and no download runs.</summary>
    public bool CanOpen
    {
        get
        {
            if (Selected is null)
            {
                return false;
            }

            if (IsDownloading)
            {
                return false;
            }

            if (!ShowsWhatWasAsked)
            {
                return false;
            }

            return Version is not null;
        }
    }

    /// <summary>How many times <see cref="Version"/> of the selected package was downloaded; 0 when the feed does not say.</summary>
    public long VersionDownloads
    {
        get
        {
            var selected = Selected;
            var version = Version;
            if (selected is null || version is null)
            {
                return 0;
            }

            var listed = FindVersion(selected, version);
            if (listed is null)
            {
                return 0;
            }

            return listed.Downloads;
        }
    }

    /// <summary>Whether icons or details were asked for that are still to be fetched: an edge takes them
    /// (<see cref="TakeIconsToFetch"/>, <see cref="TakeDetailsToFetch"/>) and starts a fetch for each.</summary>
    public bool HasDownloadsToStart => _iconsToFetch.Count > 0 || _detailsToFetch.Count > 0;

    /// <summary>Fires when the dialog closes, which cancels the fetches started while it was open.</summary>
    public CancellationToken UntilClosed => _fetches.Token;

    /// <summary>Has the query take focus once, the next time <see cref="TryTakeQueryFocus"/> is asked.</summary>
    public void FocusQuery()
    {
        _focusQuery = true;
    }

    /// <summary>True once after <see cref="FocusQuery"/>: the query box should take focus.</summary>
    public bool TryTakeQueryFocus()
    {
        var focus = _focusQuery;
        _focusQuery = false;
        return focus;
    }

    /// <summary>Takes the feed and prerelease choice the settings kept as the ones last searched.</summary>
    public void RestoreKeptSearch(SavedNuGetSearch saved)
    {
        KeptSearch = saved;
    }

    /// <summary>Takes the user's NuGet.Config as being read, unless it is read or being read already: true when
    /// it was unread, for the caller to read it and hand over the setup (<see cref="UseSetup"/>).</summary>
    public bool TryBeginReadingSetup()
    {
        if (!IsSetupUnread)
        {
            return false;
        }

        _setup = new SetupState.Reading();
        return true;
    }

    /// <summary>Searches and downloads with <paramref name="setup"/> from now on.</summary>
    internal void UseSetup(NuGetSetup setup)
    {
        _setup = new SetupState.Ready(setup);
    }

    /// <summary>Takes in <paramref name="url"/>, picked rather than typed, and the query typed as settled, so
    /// they are searched at once.</summary>
    public void SettleFeed(string url)
    {
        _typedUrl.Settle(url);
        _typedQuery.Settle();
    }

    /// <summary>Searches what was typed with the next update, without waiting for it to settle.</summary>
    public void SearchNow()
    {
        _typedQuery.Settle();
        _typedUrl.Settle();
    }

    public void ShowPreviousPage()
    {
        if (!HasPreviousPage)
        {
            return;
        }

        RequestedPage = Page - 1;
    }

    public void ShowNextPage()
    {
        if (!HasNextPage)
        {
            return;
        }

        RequestedPage = Page + 1;
    }

    /// <summary>Searches again for what the last search asked, after it failed.</summary>
    public void RetrySearch()
    {
        StopSearch();
        _run = new SearchRun.None();
    }

    /// <summary>Takes in the query and the feed typed and whether prereleases count, as they are at
    /// <paramref name="now"/>, and returns the search they ask for, to start now: once the typed feed or query
    /// settled, or at once when another page is asked for or whether prereleases count changed; a new search
    /// starts on its first page. Null when nothing is to be searched yet, or what is asked is searched already;
    /// while what was typed settles, <see cref="IsSettling"/> says so and <see cref="CalculateSecondsToSettle"/>
    /// how long it has left.</summary>
    public FeedSearch? Update(string query, string feedUrl, bool includePrerelease, double now)
    {
        _typedQuery.Note(query, now);
        _typedUrl.Note(feedUrl, now);
        IsSettling = false;
        var setup = FindReadySetup();
        if (setup is null)
        {
            return null;
        }

        var source = NuGetSource.FromUrl(_typedUrl.Text);
        if (source is null)
        {
            DropResults();
            return null;
        }
        var wanted = new FeedSearch
        {
            Feed = NuGetFeeds.GetListedSource(setup, source),
            Query = _typedQuery.Text,
            IncludePrerelease = includePrerelease,
            Page = RequestedPage,
        };
        if (IsNewSearch(wanted))
        {
            RequestedPage = 0;
            wanted = wanted with { Page = 0 };
        }
        var lastSearched = FindLastSearched();
        if (wanted == lastSearched)
        {
            return null;
        }

        var remaining = CalculateSecondsToSettle(now);
        if (remaining > 0 && IsOnlyTypingChanged(wanted))
        {
            IsSettling = true;
            return null;
        }
        return wanted;
    }

    /// <summary>The seconds left at <paramref name="now"/> before both the query and the feed typed settled,
    /// or 0 once they have.</summary>
    public double CalculateSecondsToSettle(double now)
    {
        var query = _typedQuery.CalculateSecondsToSettle(now, DebounceSeconds);
        var feed = _typedUrl.CalculateSecondsToSettle(now, DebounceSeconds);
        return Math.Max(query, feed);
    }

    /// <summary>Starts searching as <paramref name="wanted"/> asks, in place of the search running, which is
    /// cancelled and its page dropped: from now on the dialog draws it searching. Returns what
    /// <see cref="SearchNuGetFeedHandler"/> needs to search, whose work a newer search or closing the dialog
    /// cancels.</summary>
    public FeedSearchTicket StartSearch(FeedSearch wanted)
    {
        KeepSearched(wanted);
        _run = new SearchRun.Running(wanted);
        _stopped = false;
        return new FeedSearchTicket { Search = wanted, Work = _searches.Start() };
    }

    /// <summary>Takes what <paramref name="search"/> <paramref name="found"/>, unless another search started or the
    /// dialog closed meanwhile: the page found is shown; a failed search says why, keeping the page shown when it
    /// only asked for another page of it. True when it was taken, for the caller to report a failure.</summary>
    public bool TryFinishSearch(FeedSearchTicket search, Outcome<FeedPage> found)
    {
        if (!_searches.TryFinish(search.Work))
        {
            return false;
        }

        var wanted = search.Search;
        _run = found.Match<SearchRun>(ok => ShowFoundPage(wanted, ok.Value), failed => ShowSearchFailure(wanted, failed.Error));
        return true;
    }

    /// <summary>What Enter does: starts downloading the selected package at <see cref="Version"/>, as
    /// <see cref="StartDownload"/> does, unless the query typed is still to be searched: then it is searched at
    /// once instead, so Enter never opens what an older query found. Null when no download started.</summary>
    public PackageDownloadTicket? Submit()
    {
        if (IsSettling)
        {
            SearchNow();
            return null;
        }
        if (IsSearching)
        {
            return null;
        }

        return StartDownload();
    }

    /// <summary>Starts downloading <see cref="Version"/> of the selected package, from the feed it was found in,
    /// unless it cannot be opened now: from now on the dialog draws it downloading. Returns what
    /// <see cref="DownloadNuGetPackageHandler"/> needs to download it, whose work closing the dialog cancels; null
    /// when nothing can be downloaded.</summary>
    public PackageDownloadTicket? StartDownload()
    {
        var version = Version;
        var package = Selected;
        var setup = FindReadySetup();
        var shown = _shown;
        if (!CanOpen || version is null || package is null || setup is null || shown is null)
        {
            return null;
        }

        var name = $"{package.Id} {version}";
        _download = new DownloadState.Running(name);
        _stopped = false;
        return new PackageDownloadTicket
        {
            Name = name,
            Cache = setup.Cache,
            Feed = shown.Search.Feed,
            Id = package.Id,
            Version = version,
            Work = _downloads.Start(),
        };
    }

    /// <summary>Takes the package file <paramref name="download"/> <paramref name="fetched"/> into the cache, for
    /// <see cref="TakeDownloaded"/> to hand over, unless the dialog dropped it by closing meanwhile; a failed
    /// download leaves none. True when it was taken, for the caller to report a failure.</summary>
    public bool TryFinishDownload(PackageDownloadTicket download, Outcome<string> fetched)
    {
        if (!_downloads.TryFinish(download.Work))
        {
            return false;
        }

        _download = fetched.Match<DownloadState>(static ok => new DownloadState.Done(ok.Value), static _ => new DownloadState.None());
        return true;
    }

    /// <summary>The package file the last download fetched, once; null until it is in.</summary>
    public string? TakeDownloaded()
    {
        if (_download is not DownloadState.Done done)
        {
            return null;
        }

        _download = new DownloadState.None();
        return done.Path;
    }

    /// <summary>Told each frame whether the dialog is open, however it was closed (the close box, Escape, Cancel,
    /// or handing a download over): once it is not, stops the search, the download and the fetches running, and
    /// forgets the details and icons fetched, returning the images of the icons for the screen to free; else
    /// returns none, allocating nothing.</summary>
    public IReadOnlyList<string> StopUnlessOpen(bool isOpen)
    {
        if (isOpen)
        {
            _stopped = false;
            return [];
        }

        if (_stopped)
        {
            return [];
        }

        _stopped = true;
        return Stop();
    }

    private List<string> Stop()
    {
        StopSearch();
        StopDownload();
        _fetches.Cancel();
        _fetches = new CancellationTokenSource();
        _iconsToFetch.Clear();
        _detailsToFetch.Clear();
        _details.Clear();
        return DropIcons();
    }

    /// <summary>Offers the versions of the package selected, picking its latest, once another is selected.</summary>
    public void FollowSelection()
    {
        var package = Selected;
        var offered = Choice?.Package;
        if (package == offered)
        {
            return;
        }

        if (package is null)
        {
            Choice = null;
            return;
        }
        Choice = VersionChoice.FromPackage(package);
    }

    /// <summary>Picks <paramref name="version"/>, one of the versions <see cref="Choice"/> offers, to open.</summary>
    public void PickVersion(PackageVersion version)
    {
        var choice = Choice;
        if (choice is null)
        {
            throw new InvalidOperationException($"Version '{version}' was picked while no package is selected.");
        }

        Choice = choice.WithPicked(version);
    }

    /// <summary>The icon of <paramref name="package"/>, recorded as to be fetched the first time it is asked for
    /// (see <see cref="HasDownloadsToStart"/>); null when the package gives none.</summary>
    public PackageIcon? EnsureIcon(FeedPackage package)
    {
        var url = package.IconUrl;
        if (url is null)
        {
            return null;
        }

        if (IsReadingSetup)
        {
            return null;
        }

        if (_icons.TryGetValue(url, out var icon))
        {
            return icon;
        }

        _iconCount++;
        icon = new PackageIcon { ImageId = IconImagePrefix + _iconCount };
        _icons.Add(url, icon);
        _iconsToFetch.Add(new PackageIconFetch { Url = url, Icon = icon });
        return icon;
    }

    /// <summary>What the feed says of <paramref name="version"/> of <paramref name="package"/>, recorded as to be
    /// read the first time it is asked for (see <see cref="HasDownloadsToStart"/>); null while no page of packages
    /// is shown or the package lists no such version.</summary>
    public VersionDetails? EnsureDetails(FeedPackage package, PackageVersion version)
    {
        if (_shown is null)
        {
            return null;
        }

        var listed = FindVersion(package, version);
        if (listed is null)
        {
            return null;
        }

        var feed = _shown.Search.Feed;
        var key = new VersionDetailsKey { Feed = feed.Url, Id = package.Id, Version = version };
        if (_details.TryGetValue(key, out var details))
        {
            return details;
        }

        var loading = new VersionDetails.Loading();
        _details.Add(key, loading);
        _detailsToFetch.Add(new VersionDetailsFetch { Key = key, Feed = feed, Version = listed, Loading = loading });
        return loading;
    }

    /// <summary>Reads the details of <paramref name="version"/> of <paramref name="package"/> again, after they failed.</summary>
    public void RetryDetails(FeedPackage package, PackageVersion version)
    {
        if (_shown is null)
        {
            return;
        }

        _details.Remove(new VersionDetailsKey { Feed = _shown.Search.Feed.Url, Id = package.Id, Version = version });
    }

    /// <summary>The icons asked for since this was last asked, each for a fetch of its own
    /// (<see cref="FetchNuGetIconHandler"/>); none are left to fetch afterwards.</summary>
    internal List<PackageIconFetch> TakeIconsToFetch()
    {
        var taken = _iconsToFetch.ToList();
        _iconsToFetch.Clear();
        return taken;
    }

    /// <summary>The details asked for since this was last asked, each for a read of its own
    /// (<see cref="ReadNuGetDetailsHandler"/>); none are left to read afterwards.</summary>
    internal List<VersionDetailsFetch> TakeDetailsToFetch()
    {
        var taken = _detailsToFetch.ToList();
        _detailsToFetch.Clear();
        return taken;
    }

    /// <summary>Takes the details <paramref name="fetch"/> asked for as <paramref name="read"/>, in place of their
    /// loading, unless they were dropped or asked for again meanwhile.</summary>
    internal void ShowDetails(VersionDetailsFetch fetch, Outcome<PackageVersionDetails> read)
    {
        if (!_details.TryGetValue(fetch.Key, out var current) || !ReferenceEquals(current, fetch.Loading))
        {
            return;
        }

        _details[fetch.Key] = read.Match<VersionDetails>(static ok => new VersionDetails.Read(ok.Value), static failed => new VersionDetails.Failed(failed.Error.Message));
    }

    internal static FeedVersion? FindVersion(FeedPackage package, PackageVersion version)
    {
        return package.Versions.FirstOrDefault(v => v.Version == version);
    }

    private NuGetSetup? FindReadySetup()
    {
        if (_setup is not SetupState.Ready ready)
        {
            return null;
        }

        return ready.Setup;
    }

    private void StopSearch()
    {
        if (_run is not SearchRun.Running)
        {
            return;
        }

        _searches.Cancel();
        _run = new SearchRun.None();
    }

    private void StopDownload()
    {
        if (_download is not DownloadState.Running)
        {
            return;
        }

        _downloads.Cancel();
        _download = new DownloadState.None();
    }

    private List<string> DropIcons()
    {
        var images = _icons.Values
            .Select(i => i.ImageId)
            .ToList();
        _icons.Clear();
        return images;
    }

    // For when the feed typed is no URL, so nothing shows from another feed.
    private void DropResults()
    {
        StopSearch();
        _run = new SearchRun.None();
        ClearShown();
        RequestedPage = 0;
    }

    private void ClearShown()
    {
        _shown = null;
        ItemSelections.Clear(Selection);
    }

    private FeedSearch? FindLastSearched()
    {
        return _run.Match<FeedSearch?>(
            static _ => null,
            static running => running.Search,
            static found => found.Search,
            static failed => failed.Search);
    }

    private FeedSearch? FindShownSearch()
    {
        if (_shown is null)
        {
            return null;
        }

        return _shown.Search;
    }

    // The search the next one is weighed against.
    private FeedSearch? FindLastAsked()
    {
        var searched = FindLastSearched();
        if (searched is not null)
        {
            return searched;
        }

        return FindShownSearch();
    }

    private bool IsNewSearch(FeedSearch wanted)
    {
        var last = FindLastAsked();
        if (last is null)
        {
            return false;
        }

        return !IsSamePackages(wanted, last);
    }

    private static bool IsSamePackages(FeedSearch a, FeedSearch? b)
    {
        if (b is null)
        {
            return false;
        }

        if (a.Feed != b.Feed)
        {
            return false;
        }

        if (a.Query != b.Query)
        {
            return false;
        }

        return a.IncludePrerelease == b.IncludePrerelease;
    }

    private bool IsOnlyTypingChanged(FeedSearch wanted)
    {
        var last = FindLastAsked();
        if (last is null)
        {
            return false;
        }

        if (wanted.IncludePrerelease != last.IncludePrerelease)
        {
            return false;
        }

        return wanted.Page == last.Page;
    }

    private void KeepSearched(FeedSearch searched)
    {
        var kept = new SavedNuGetSearch
        {
            FeedUrl = searched.Feed.Url.Text,
            IncludePrerelease = searched.IncludePrerelease,
        };
        if (kept == KeptSearch)
        {
            return;
        }

        KeptSearch = kept;
        SearchChanges++;
    }

    private SearchRun.Found ShowFoundPage(FeedSearch searched, FeedPage page)
    {
        _shown = new ShownPage { Search = searched, Found = page };
        ItemSelections.Clear(Selection);
        if (page.Packages.Count > 0)
        {
            ItemSelections.Select(Selection, page.Packages[0], page.Packages);
        }

        return new SearchRun.Found(searched);
    }

    private SearchRun.Failed ShowSearchFailure(FeedSearch searched, Exception error)
    {
        var shown = FindShownSearch();
        if (!IsSamePackages(searched, shown))
        {
            ClearShown();
        }

        return new SearchRun.Failed { Search = searched, Error = error.Message };
    }

    private sealed record ShownPage
    {
        public required FeedSearch Search { get; init; }

        public required FeedPage Found { get; init; }
    }

    private abstract record SetupState
    {
        private SetupState()
        {
        }

        public sealed record Unread : SetupState;

        public sealed record Reading : SetupState;

        public sealed record Ready(NuGetSetup Setup) : SetupState;
    }

    // Done holds the package file fetched until it is taken.
    private abstract record DownloadState
    {
        private DownloadState()
        {
        }

        public sealed record None : DownloadState;

        public sealed record Running(string Name) : DownloadState;

        public sealed record Done(string Path) : DownloadState;
    }
}
