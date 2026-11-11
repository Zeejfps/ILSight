using ZGF.Gui.Immediate;
using static ILSight.NuGetBrowser;
using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Resources;

namespace ILSight;

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
}

/// <summary>What the "open from NuGet" dialog does with what its widgets bind: opening and closing it,
/// the feed and the query typed or picked, and what the settings keep of them.</summary>
public static class NuGetPicking
{
    /// <summary>Searches the feed and counts prereleases as the settings kept them.</summary>
    public static void RestoreSearch(NuGetPicker picker, SavedNuGetSearch? saved)
    {
        if (saved is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(saved.FeedUrl))
        {
            return;
        }

        picker.Browser.RestoreKeptSearch(saved);
        picker.FeedUrl = saved.FeedUrl;
        picker.IncludePrerelease = saved.IncludePrerelease;
    }

    /// <summary>Opens the dialog, its query taking focus. Until <see cref="ReadSetupAsync"/> has read the
    /// user's NuGet.Config, it says it is reading it.</summary>
    public static void OpenPicker(NuGetPicker picker)
    {
        picker.IsOpen = true;
        picker.Browser.FocusQuery();
    }

    /// <summary>Reads the user's NuGet.Config the first time the dialog opens; with no feed kept, its first
    /// feed is the one searched.</summary>
    public static async Task ReadSetupAsync(NuGetPicker picker, CancellationToken ct)
    {
        if (!picker.Browser.IsSetupUnread)
        {
            return;
        }

        await picker.Browser.ReadSetupAsync(ct);
        SearchFirstFeedIfNone(picker);
    }

    /// <summary>Searches and downloads with <paramref name="setup"/> from now on, in place of the one
    /// read from the user's NuGet.Config; with no feed kept, its first feed is the one searched.</summary>
    internal static void UseSetup(NuGetPicker picker, NuGetSetup setup)
    {
        picker.Browser.UseSetup(setup);
        SearchFirstFeedIfNone(picker);
    }

    private static void SearchFirstFeedIfNone(NuGetPicker picker)
    {
        if (picker.FeedUrl.Length > 0)
        {
            return;
        }

        var sources = picker.Browser.Sources;
        if (sources.Count == 0)
        {
            return;
        }

        picker.FeedUrl = sources[0].Url.Text;
    }

    /// <summary>Searches <paramref name="source"/> at once.</summary>
    public static void PickFeed(NuGetPicker picker, NuGetSource source)
    {
        picker.FeedUrl = source.Url.Text;
        picker.Browser.SettleFeed(source.Url.Text);
    }

    /// <summary>Closes the dialog, stopping the search, the download and the fetches it started.
    /// Returns the images of the icons shown, for the screen to free.</summary>
    public static List<string> ClosePicker(NuGetPicker picker)
    {
        picker.IsOpen = false;
        return picker.Browser.Close();
    }

    public static void ClearQuery(NuGetPicker picker)
    {
        picker.Query = "";
    }

    /// <summary>Takes in the query, the feed and whether prereleases count as they are at
    /// <paramref name="now"/>; returns the search to start now, or null. See <see cref="NuGetBrowser.Update"/>.</summary>
    public static FeedSearch? UpdatePicker(NuGetPicker picker, double now)
    {
        return picker.Browser.Update(picker.Query, picker.FeedUrl, picker.IncludePrerelease, now);
    }
}

/// <summary>
/// What the "open from NuGet" dialog found and fetched, and the rules it keeps: the user's NuGet.Config,
/// read the first time it opens; the feed searched a page at a time once what was typed settles, the
/// newest search the only one whose page is shown; the package selected and the version of it picked;
/// the icons and version details fetched the first time they are shown; and that version, fetched into
/// the package cache for the dialog to open. Its workflows return once their work is done, and reach the
/// feeds, the disk and the user's error reports through <see cref="IPorts"/>.
/// </summary>
public sealed class NuGetBrowser(NuGetBrowser.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user a search, a download or reading the config failed.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Reads the setup the user's NuGet.Config describes, without holding up the screen; a config
        /// that cannot be read at all comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<NuGetSetup>> ReadUserSetupAsync(CancellationToken ct);

        /// <summary>The setup to use when the user's config cannot be read: nuget.org and the default folders.</summary>
        NuGetSetup CreateDefaultSetup();

        /// <summary>The page <paramref name="search"/> asks of its feed; a search that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<FeedPage>> SearchAsync(FeedSearch search, CancellationToken ct);

        /// <summary>What <paramref name="feed"/>'s catalog says of <paramref name="version"/> of package
        /// <paramref name="id"/>; a read that failed comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<PackageVersionDetails>> ReadDetailsAsync(NuGetSource feed, PackageId id, FeedVersion version, CancellationToken ct);

        /// <summary>The icon at <paramref name="url"/>, <paramref name="side"/> pixels across; one that cannot be
        /// fetched comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<ResourceImage>> DownloadIconAsync(WebUrl url, int side, CancellationToken ct);

        /// <summary>The file of <paramref name="version"/> of package <paramref name="id"/>, fetched from
        /// <paramref name="feed"/> into <paramref name="cache"/> unless it is there; a download that failed
        /// comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<string>> DownloadPackageAsync(PackageCache cache, NuGetSource feed, PackageId id, PackageVersion version, CancellationToken ct);
    }

    /// <summary>How many pixels across a package's icon is fetched at, enough for its row on a screen
    /// of twice the usual density.</summary>
    public const int IconPixels = 64;

    private const double DebounceSeconds = 0.3;
    private const string IconImagePrefix = "nuget-icon-";

    private readonly Dictionary<WebUrl, PackageIcon> _icons = [];
    private readonly Dictionary<string, NuGetSource> _feeds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<DetailsKey, VersionDetails> _details = [];
    private readonly List<IconFetch> _iconsToFetch = [];
    private readonly List<DetailsFetch> _detailsToFetch = [];
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

    /// <summary>Whether icons or details were asked for that <see cref="DownloadAskedAsync"/> is still to fetch.</summary>
    public bool HasDownloadsToStart => _iconsToFetch.Count > 0 || _detailsToFetch.Count > 0;

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

    /// <summary>Reads the user's NuGet.Config, unless it is read or being read; one that cannot be read at
    /// all is reported, and nuget.org searched in its place.</summary>
    public async Task ReadSetupAsync(CancellationToken ct)
    {
        if (!IsSetupUnread)
        {
            return;
        }

        _setup = new SetupState.Reading();
        var read = await ports.ReadUserSetupAsync(ct);
        ct.ThrowIfCancellationRequested();
        UseSetup(read.Match(static ok => ok.Value, ReplaceUnreadableSetup));
    }

    private NuGetSetup ReplaceUnreadableSetup(Outcome<NuGetSetup>.Failed failed)
    {
        ports.ReportError(new AppProblem.NuGetConfigUnreadable(failed.Error));
        return ports.CreateDefaultSetup();
    }

    /// <summary>Searches and downloads with <paramref name="setup"/> from now on.</summary>
    internal void UseSetup(NuGetSetup setup)
    {
        _setup = new SetupState.Ready(setup);
        _feeds.Clear();
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
        var wanted = new FeedSearch(EnsureFeed(setup, source), _typedQuery.Text, includePrerelease, RequestedPage);
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

    /// <summary>The feed at <paramref name="source"/>'s URL as it was first asked for with this setup, named as the
    /// config names it when the config lists it, so the same URL typed in other letters is the same feed.</summary>
    private NuGetSource EnsureFeed(NuGetSetup setup, NuGetSource source)
    {
        if (_feeds.TryGetValue(source.Url.Text, out var feed))
        {
            return feed;
        }

        feed = NuGetFeeds.GetListedSource(setup, source);
        _feeds.Add(source.Url.Text, feed);
        return feed;
    }

    /// <summary>The seconds left at <paramref name="now"/> before both the query and the feed typed settled,
    /// or 0 once they have.</summary>
    public double CalculateSecondsToSettle(double now)
    {
        var query = _typedQuery.CalculateSecondsToSettle(now, DebounceSeconds);
        var feed = _typedUrl.CalculateSecondsToSettle(now, DebounceSeconds);
        return Math.Max(query, feed);
    }

    /// <summary>Searches as <paramref name="wanted"/> asks, in place of the search running, and shows the page
    /// found unless another search started or the dialog closed meanwhile. A failed search says why and is
    /// reported, keeping the page shown when it only asked for another page of it.</summary>
    public async Task SearchAsync(FeedSearch wanted, CancellationToken ct)
    {
        KeepSearched(wanted);
        _run = new SearchRun.Running(wanted);
        var ticket = _searches.Start(ct);
        var found = await ports.SearchAsync(wanted, ticket.Token);
        if (!_searches.TryFinish(ticket))
        {
            return;
        }

        ct.ThrowIfCancellationRequested();
        _run = found.Match<SearchRun>(ok => ShowFoundPage(wanted, ok.Value), failed => ReportSearchFailure(wanted, failed.Error));
    }

    /// <summary>Opens the selected package at <see cref="Version"/>, unless the query typed is still to be
    /// searched: then it is searched at once instead, so Enter never opens what an older query found.</summary>
    public Task SubmitAsync(CancellationToken ct)
    {
        if (IsSettling)
        {
            SearchNow();
            return Task.CompletedTask;
        }
        if (IsSearching)
        {
            return Task.CompletedTask;
        }

        return DownloadPickedAsync(ct);
    }

    /// <summary>Fetches <see cref="Version"/> of the selected package, from the feed it was found in, into the
    /// cache, for <see cref="TakeDownloaded"/> to hand over, unless it cannot be opened now. A failed download
    /// is reported; one the dialog dropped by closing is not.</summary>
    public async Task DownloadPickedAsync(CancellationToken ct)
    {
        var version = Version;
        var package = Selected;
        var setup = FindReadySetup();
        var shown = _shown;
        if (!CanOpen || version is null || package is null || setup is null || shown is null)
        {
            return;
        }

        var name = $"{package.Id} {version}";
        _download = new DownloadState.Running(name);
        var ticket = _downloads.Start(ct);
        var fetched = await ports.DownloadPackageAsync(setup.Cache, shown.Search.Feed, package.Id, version, ticket.Token);
        if (!_downloads.TryFinish(ticket))
        {
            return;
        }

        ct.ThrowIfCancellationRequested();
        _download = fetched.Match<DownloadState>(static ok => new DownloadState.Done(ok.Value), failed => ReportDownloadFailure(name, failed.Error));
    }

    private DownloadState.None ReportDownloadFailure(string name, Exception error)
    {
        ports.ReportError(new AppProblem.PackageNotDownloaded(name, error));
        return new DownloadState.None();
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

    /// <summary>Stops the search, the download and the fetches running, and forgets the details and icons
    /// fetched. Returns the images of the icons, for the screen to free.</summary>
    public List<string> Close()
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

    /// <summary>The icon of <paramref name="package"/>, for <see cref="DownloadAskedAsync"/> to fetch the first time it
    /// is asked for; null when the package gives none.</summary>
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
        _iconsToFetch.Add(new IconFetch { Url = url, Icon = icon });
        return icon;
    }

    /// <summary>What the feed says of <paramref name="version"/> of <paramref name="package"/>, for
    /// <see cref="DownloadAskedAsync"/> to read the first time it is asked for; null while no page of packages is shown
    /// or the package lists no such version.</summary>
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
        var key = new DetailsKey(feed.Url, package.Id, version);
        if (_details.TryGetValue(key, out var details))
        {
            return details;
        }

        var loading = new VersionDetails.Loading();
        _details.Add(key, loading);
        _detailsToFetch.Add(new DetailsFetch { Key = key, Feed = feed, Version = listed, Loading = loading });
        return loading;
    }

    /// <summary>Reads the details of <paramref name="version"/> of <paramref name="package"/> again, after they failed.</summary>
    public void RetryDetails(FeedPackage package, PackageVersion version)
    {
        if (_shown is null)
        {
            return;
        }

        _details.Remove(new DetailsKey(_shown.Search.Feed.Url, package.Id, version));
    }

    /// <summary>Fetches the icons and details asked for since it last ran, all at once, and returns once they
    /// are in. An icon gets its pixels unless it cannot be fetched, which leaves its row the stand-in; details
    /// take the place of their loading unless they were dropped or asked for again meanwhile. What the dialog
    /// closed on is dropped.</summary>
    public async Task DownloadAskedAsync(CancellationToken ct)
    {
        var closed = _fetches.Token;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(closed, ct);
        var fetches = new List<Task>(_iconsToFetch.Count + _detailsToFetch.Count);
        foreach (var icon in _iconsToFetch)
        {
            fetches.Add(DownloadIconAsync(icon, linked.Token));
        }

        foreach (var details in _detailsToFetch)
        {
            fetches.Add(ReadDetailsAsync(details, linked.Token));
        }

        _iconsToFetch.Clear();
        _detailsToFetch.Clear();
        await Task.WhenAll(fetches);
    }

    private async Task DownloadIconAsync(IconFetch fetch, CancellationToken ct)
    {
        var fetched = await ports.DownloadIconAsync(fetch.Url, IconPixels, ct);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        if (fetched is Outcome<ResourceImage>.Ok ok)
        {
            ShowIcon(fetch.Icon, ok.Value);
        }
    }

    private static void ShowIcon(PackageIcon icon, ResourceImage pixels)
    {
        icon.Shown = new ShownIcon(icon.ImageId, pixels, ResourceImages.IsDarkOnClear(pixels));
    }

    private async Task ReadDetailsAsync(DetailsFetch fetch, CancellationToken ct)
    {
        var read = await ports.ReadDetailsAsync(fetch.Feed, fetch.Key.Id, fetch.Version, ct);
        if (ct.IsCancellationRequested)
        {
            return;
        }

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

    /// <summary>Clears what was found once the feed typed is no URL, so nothing shows from another feed.</summary>
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

    /// <summary>The search running or last run; null before any, or once it was stopped or dropped.</summary>
    private FeedSearch? FindLastSearched()
    {
        return _run switch
            {
                SearchRun.None => null,
                SearchRun.Running running => running.Search,
                SearchRun.Found found => found.Search,
                SearchRun.Failed failed => failed.Search,
                _ => throw new UnreachableException(),
            };
    }

    private FeedSearch? FindShownSearch()
    {
        if (_shown is null)
        {
            return null;
        }

        return _shown.Search;
    }

    /// <summary>The search the next one is weighed against: the one running or last run, else the one shown.</summary>
    private FeedSearch? FindLastAsked()
    {
        var searched = FindLastSearched();
        if (searched is not null)
        {
            return searched;
        }

        return FindShownSearch();
    }

    /// <summary>Whether <paramref name="wanted"/> asks for other packages than the last search, not
    /// just another page of them.</summary>
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

    /// <summary>Has the settings keep the feed searched and whether prereleases count, once either changed.</summary>
    private void KeepSearched(FeedSearch searched)
    {
        var kept = new SavedNuGetSearch(searched.Feed.Url.Text, searched.IncludePrerelease);
        if (kept == KeptSearch)
        {
            return;
        }

        KeptSearch = kept;
        SearchChanges++;
    }

    /// <summary>Shows the page found in place of the one shown.</summary>
    private SearchRun.Found ShowFoundPage(FeedSearch searched, FeedPage page)
    {
        _shown = new ShownPage(searched, page);
        ItemSelections.Clear(Selection);
        if (page.Packages.Count > 0)
        {
            ItemSelections.Select(Selection, page.Packages[0], page.Packages);
        }

        return new SearchRun.Found(searched);
    }

    /// <summary>Reports the failed search and says why, keeping the page shown only when the search asked
    /// for another page of it.</summary>
    private SearchRun.Failed ReportSearchFailure(FeedSearch searched, Exception error)
    {
        var shown = FindShownSearch();
        if (!IsSamePackages(searched, shown))
        {
            ClearShown();
        }

        ports.ReportError(new AppProblem.NuGetSearchFailed(searched.Feed.Name, error));
        return new SearchRun.Failed(searched, error.Message);
    }

    /// <summary>What a search asked of which feed.</summary>
    public sealed record FeedSearch(NuGetSource Feed, string Query, bool IncludePrerelease, int Page);

    /// <summary>The page of packages shown, and the search that found it.</summary>
    private sealed record ShownPage(FeedSearch Search, FeedPage Found);

    /// <summary>Whether the user's NuGet.Config is read yet.</summary>
    private abstract record SetupState
    {
        private SetupState()
        {
        }

        public sealed record Unread : SetupState;

        public sealed record Reading : SetupState;

        public sealed record Ready(NuGetSetup Setup) : SetupState;
    }

    /// <summary>The last search: none since the dialog was made or the search was dropped, one
    /// running, or one that found a page or failed to.</summary>
    private abstract record SearchRun
    {
        private SearchRun()
        {
        }

        public sealed record None : SearchRun;

        public sealed record Running(FeedSearch Search) : SearchRun;

        public sealed record Found(FeedSearch Search) : SearchRun;

        public sealed record Failed(FeedSearch Search, string Error) : SearchRun;
    }

    /// <summary>A download: none, one running, or the package file one fetched, until it is taken.</summary>
    private abstract record DownloadState
    {
        private DownloadState()
        {
        }

        public sealed record None : DownloadState;

        public sealed record Running(string Name) : DownloadState;

        public sealed record Done(string Path) : DownloadState;
    }

    /// <summary>Which version of which package of which feed details are of.</summary>
    private readonly record struct DetailsKey(WebUrl Feed, PackageId Id, PackageVersion Version);

    /// <summary>An icon asked for, still to be fetched, and where from.</summary>
    private sealed record IconFetch
    {
        public required WebUrl Url { get; init; }

        public required PackageIcon Icon { get; init; }
    }

    /// <summary>Details asked for, still to be read, and the loading they take the place of.</summary>
    private sealed record DetailsFetch
    {
        public required DetailsKey Key { get; init; }

        public required NuGetSource Feed { get; init; }

        public required FeedVersion Version { get; init; }

        public required VersionDetails.Loading Loading { get; init; }
    }
}

/// <summary>Reaches the NuGet feeds through <paramref name="client"/>, which keeps the feeds and icons it speaks
/// to, the package cache and the user's NuGet.Config off the UI thread, and the app's error reports, for
/// <see cref="NuGetBrowser"/>.</summary>
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
        return await Background.Wait(Task.Run(NuGetFeeds.ReadUserSetup, ct));
    }

    public NuGetSetup CreateDefaultSetup()
    {
        return NuGetFeeds.CreateDefaultSetup();
    }

    /// <summary>Searches and comes back on the UI thread, with whatever the search threw as
    /// <see cref="Outcome{T}.Failed"/>.</summary>
    public async Task<Outcome<FeedPage>> SearchAsync(FeedSearch search, CancellationToken ct)
    {
        return await Background.Wait(NuGetFeeds.SearchAsync(client.EnsureFeed(search.Feed), search.Query, search.IncludePrerelease, search.Page, ct));
    }

    /// <inheritdoc cref="SearchAsync"/>
    public async Task<Outcome<PackageVersionDetails>> ReadDetailsAsync(NuGetSource feed, PackageId id, FeedVersion version, CancellationToken ct)
    {
        return await Background.Wait(NuGetFeeds.ReadDetailsAsync(client.EnsureFeed(feed), id, version, ct));
    }

    /// <inheritdoc cref="SearchAsync"/>
    public async Task<Outcome<ResourceImage>> DownloadIconAsync(WebUrl url, int side, CancellationToken ct)
    {
        return await Background.Wait(NuGetFeeds.GetIconAsync(client.Icons, url, side, ct));
    }

    /// <inheritdoc cref="SearchAsync"/>
    public async Task<Outcome<string>> DownloadPackageAsync(PackageCache cache, NuGetSource feed, PackageId id, PackageVersion version, CancellationToken ct)
    {
        return await Background.Wait(NuGetFeeds.GetPackageAsync(cache, client.EnsureFeed(feed), id, version, ct));
    }
}

/// <summary>The versions a package lists to pick from, newest first, how each is written, and the one
/// picked: the package's latest at first, then whichever of those listed the user picks.</summary>
public sealed class VersionChoice
{
    private readonly PackageVersion[] _versions;
    private readonly string[] _texts;

    private VersionChoice(FeedPackage package, PackageVersion[] versions, string[] texts, PackageVersion picked)
    {
        Package = package;
        _versions = versions;
        _texts = texts;
        Picked = picked;
    }

    /// <summary>The package the versions are of.</summary>
    public FeedPackage Package { get; }

    /// <summary>The version to open.</summary>
    public PackageVersion Picked { get; }

    /// <summary>The versions the package lists, newest first.</summary>
    public ReadOnlySpan<PackageVersion> Versions => _versions;

    /// <summary>How <see cref="Versions"/> are written, in their order.</summary>
    public ReadOnlySpan<string> Texts => _texts;

    /// <summary>The versions <paramref name="package"/> lists, its latest picked.</summary>
    public static VersionChoice FromPackage(FeedPackage package)
    {
        var versions = package.Versions
            .Select(static v => v.Version)
            .ToArray();
        var texts = versions
            .Select(static v => v.Text)
            .ToArray();
        return new VersionChoice(package, versions, texts, package.Version);
    }

    /// <summary>The same versions with <paramref name="version"/>, which must be one of them, picked.</summary>
    public VersionChoice WithPicked(PackageVersion version)
    {
        if (!_versions.Contains(version))
        {
            throw new ArgumentException($"Version '{version}' is not one {Package.Id} lists.", nameof(version));
        }

        return new VersionChoice(Package, _versions, _texts, version);
    }
}

/// <summary>A package's icon: the image the screen draws it as, and what it shows once its pixels are in.</summary>
public sealed class PackageIcon
{
    public required string ImageId { get; init; }

    /// <summary>The icon's pixels as drawn; null until they are in.</summary>
    public ShownIcon? Shown { get; set; }
}

/// <summary>An icon's pixels, drawn as the image <paramref name="ImageId"/>, with whether they are dark
/// marks on a clear background, which a dark theme would hide.</summary>
public sealed record ShownIcon(string ImageId, ResourceImage Pixels, bool IsDarkOnClear);

/// <summary>What a feed says of one version of a package: loading, read, or why it could not be.</summary>
public abstract record VersionDetails
{
    private VersionDetails()
    {
    }

    public sealed record Loading : VersionDetails;

    public sealed record Read(PackageVersionDetails Details) : VersionDetails;

    public sealed record Failed(string Error) : VersionDetails;
}

/// <summary>What the settings keep of the "open from NuGet" dialog: the feed last searched and
/// whether prereleases counted.</summary>
public sealed record SavedNuGetSearch(string FeedUrl, bool IncludePrerelease);
